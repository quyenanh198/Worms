using System;
using System.Collections.Generic;

namespace Worms.Sim
{
    public enum Phase : byte
    {
        TurnStart = 0,
        Aiming = 1,
        Flying = 2,
        Settling = 3,
        Retreat = 4,
        EndOfTurn = 5,
        GameOver = 6,
    }

    public sealed class MatchSetup
    {
        public uint Seed;
        public int Teams = 2;
        public int WormsPerTeam = 4;
        /// <summary>Optional pre-built terrain (tests); generated from the seed when null.</summary>
        public Terrain Terrain;
        /// <summary>Optional fixed spawn points (tests), in team-major order.</summary>
        public Vec2[] Spawns;
    }

    /// <summary>
    /// The whole match state and its fixed-step update (60 Hz). Only the server's
    /// World is authoritative; clients render snapshots of it.
    /// </summary>
    public sealed partial class World
    {
        public readonly Rng Rng;
        public readonly Terrain Terrain;
        public readonly float WaterLevel;
        public readonly int TeamCount;
        public readonly List<Worm> Worms = new List<Worm>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<CarveOp> TerrainOps = new List<CarveOp>();
        public readonly uint Seed;

        public int Tick;
        public Phase Phase = Phase.TurnStart;
        public int PhaseTicks;
        public float Wind;
        public int ActiveTeam = -1;
        public int ActiveWorm = -1;
        /// <summary>Aiming time left; paused outside Aiming.</summary>
        public int TurnTicksLeft;
        public int RetreatTicksLeft;
        public int MoveDir;
        public WeaponId[] SelectedWeapon;
        /// <summary>Remaining uses per team and weapon; -1 = unlimited.</summary>
        public int[][] Ammo;
        public bool[] Forfeited;
        /// <summary>Winning team once GameOver: -1 = draw.</summary>
        public int Winner = -1;

        readonly int[] _nextWormOfTeam;
        readonly List<SimEvent> _events = new List<SimEvent>();
        int _stillTicks;
        int _activeDamageAtStart;
        int _nextEntityId = 1000;

        public World(MatchSetup setup)
        {
            Seed = setup.Seed;
            Rng = new Rng(setup.Seed);
            Terrain = setup.Terrain ?? MapGenerator.Generate(setup.Seed);
            WaterLevel = Terrain.Height - C.WaterDepth;
            TeamCount = setup.Teams;
            _nextWormOfTeam = new int[TeamCount];
            SelectedWeapon = new WeaponId[TeamCount];
            Forfeited = new bool[TeamCount];
            Ammo = new int[TeamCount][];
            for (int team = 0; team < TeamCount; team++)
            {
                Ammo[team] = new int[Weapons.Count];
                foreach (var def in Weapons.All) Ammo[team][(int)def.Id] = def.Ammo;
            }

            int id = 0;
            var spawnRng = new Rng(setup.Seed ^ 0x9E3779B9u);
            for (int team = 0; team < TeamCount; team++)
            {
                for (int i = 0; i < setup.WormsPerTeam; i++)
                {
                    Vec2 pos = setup.Spawns != null ? setup.Spawns[id] : FindSpawn(spawnRng);
                    Worms.Add(new Worm(id, team, pos) { Facing = pos.X < Terrain.Width / 2 ? 1 : -1 });
                    id++;
                }
            }
        }

        public Worm Active => ActiveWorm >= 0 ? Worms[ActiveWorm] : null;

        public void Emit(SimEvent e)
        {
            e.Tick = Tick;
            _events.Add(e);
        }

        public void ApplyCarve(CarveOp op)
        {
            Terrain.CarveCircle(op.X, op.Y, op.R);
            TerrainOps.Add(op);
        }

        public int NewEntityId()
        {
            return _nextEntityId++;
        }

        /// <summary>A team whose player left: its turns are skipped and it can no longer win.</summary>
        public void Forfeit(int team)
        {
            Forfeited[team] = true;
            if (ActiveTeam == team && (Phase == Phase.Aiming || Phase == Phase.Retreat)) BeginSettling();
        }

        /// <summary>Ends the active team's turn early (its player is disconnected).</summary>
        public void SkipTurn(int team)
        {
            if (ActiveTeam == team && (Phase == Phase.Aiming || Phase == Phase.Retreat)) BeginSettling();
        }

        /// <summary>Advances one tick and returns what happened during it.</summary>
        public List<SimEvent> Step(IReadOnlyList<SimInput> inputs)
        {
            _events.Clear();
            Tick++;
            PhaseTicks++;

            if (inputs != null)
                for (int i = 0; i < inputs.Count; i++) Apply(inputs[i]);

            var active = Active;
            if (active != null && active.Alive && (Phase == Phase.Aiming || Phase == Phase.Retreat))
            {
                if (MoveDir != 0) active.Walk(Terrain, MoveDir);
                else active.StopWalking();
            }

            foreach (var worm in Worms)
            {
                if (!worm.Alive) continue;
                int fall = worm.Update(this);
                if (fall > 0)
                {
                    worm.PendingDamage += fall;
                    Emit(new SimEvent { Type = SimEventType.Land, Worm = worm.Id, Amount = fall });
                }
                if (worm.Pos.Y > WaterLevel) Drown(worm);
            }

            StepProjectiles();
            StepPhase();
            return _events;
        }

        void Apply(SimInput input)
        {
            if (input.Team != ActiveTeam) return;
            bool aiming = Phase == Phase.Aiming;
            bool canMove = aiming || Phase == Phase.Retreat;
            var worm = Active;
            if (worm == null || !worm.Alive) return;

            switch (input.Kind)
            {
                case InputKind.Move:
                    if (canMove && _burstLeft == 0) MoveDir = Math.Sign(input.Dir);
                    break;
                case InputKind.Jump:
                case InputKind.Backflip:
                    if (canMove && _burstLeft == 0) worm.Jump(input.Kind == InputKind.Backflip);
                    break;
                case InputKind.Aim:
                    if (aiming) worm.Aim = ClampAngle(input.Angle);
                    break;
                case InputKind.Select:
                    if (aiming && !AttackInProgress && Weapons.TryGet(input.Weapon, out _) && Ammo[ActiveTeam][(int)input.Weapon] != 0)
                        SelectedWeapon[ActiveTeam] = input.Weapon;
                    break;
                case InputKind.Fire:
                    if (aiming && worm.IsGrounded && _burstLeft == 0) Fire(worm, input);
                    break;
            }
        }

        static float ClampAngle(float a)
        {
            const float half = (float)(Math.PI / 2);
            if (float.IsNaN(a)) return 0;
            return Math.Max(-half, Math.Min(half, a));
        }

        void StepProjectiles()
        {
            for (int i = 0; i < Projectiles.Count; i++)
            {
                var p = Projectiles[i];
                if (!p.Alive) continue;
                p.Age++;
                var hit = Physics.Step(p.Body, Terrain, Wind, C.ProjectileDt);

                if (p.FuseTicks >= 0)
                {
                    if (hit.Hit && hit.ImpactSpeed > 20f)
                        Emit(new SimEvent { Type = SimEventType.Bounce, Entity = p.Id, X = p.Pos.X, Y = p.Pos.Y, Value = hit.ImpactSpeed });
                    if (p.Age >= p.FuseTicks) ExplodeProjectile(p);
                }
                else if (hit.Hit || HitsWorm(p))
                {
                    ExplodeProjectile(p);
                }

                if (p.Alive && (p.Pos.Y > WaterLevel || p.Pos.X < -500 || p.Pos.X > Terrain.Width + 500))
                {
                    if (p.Pos.Y > WaterLevel) Emit(new SimEvent { Type = SimEventType.Splash, Entity = p.Id, X = p.Pos.X, Y = WaterLevel });
                    p.Alive = false;
                }
            }
            Projectiles.RemoveAll(p => !p.Alive);
        }

        bool HitsWorm(Projectile p)
        {
            foreach (var worm in Worms)
            {
                if (!worm.Alive) continue;
                if (worm.Id == p.OwnerWorm && p.Age < C.OwnerImmunityTicks) continue;
                float r = worm.Body.Radius + p.Body.Radius;
                if ((worm.Pos - p.Pos).LengthSq <= r * r) return true;
            }
            return false;
        }

        void ExplodeProjectile(Projectile p)
        {
            p.Alive = false;
            Explosion.Detonate(this, p.Pos, p.Weapon.BlastRadius, p.Weapon.MaxDamage);
            if (p.Weapon.Fragments > 0) SpawnFragments(p);
        }

        void Drown(Worm worm)
        {
            Emit(new SimEvent { Type = SimEventType.Splash, Entity = worm.Id, Worm = worm.Id, X = worm.Pos.X, Y = WaterLevel });
            Kill(worm, DeathCause.Water);
        }

        void Kill(Worm worm, DeathCause cause)
        {
            worm.Alive = false;
            worm.State = WormState.Dead;
            worm.Hp = 0;
            worm.PendingDamage = 0;
            worm.Body.Vel = Vec2.Zero;
            Emit(new SimEvent { Type = SimEventType.Death, Worm = worm.Id, Cause = cause });
            if (cause == DeathCause.Hp)
                Explosion.Detonate(this, worm.Pos, C.DeathBlastRadius, C.DeathBlastDamage, worm.Id);
        }

        bool EverythingStill()
        {
            if (Projectiles.Count > 0) return false;
            foreach (var worm in Worms)
                if (worm.Alive && !worm.IsGrounded) return false;
            return true;
        }

        void SetPhase(Phase phase)
        {
            Phase = phase;
            PhaseTicks = 0;
        }

        void BeginSettling()
        {
            _stillTicks = 0;
            _shotsLeft = 0;
            _burstLeft = 0;
            MoveDir = 0;
            Active?.StopWalking();
            SetPhase(Phase.Settling);
        }

        void StepPhase()
        {
            var active = Active;
            switch (Phase)
            {
                case Phase.TurnStart:
                    StartTurn();
                    break;

                case Phase.Aiming:
                    if (_burstLeft > 0)
                    {
                        StepBurst(active);
                        break;
                    }
                    TurnTicksLeft--;
                    if (TurnTicksLeft <= 0 || active == null || !active.Alive || active.PendingDamage > _activeDamageAtStart
                        || active.State == WormState.Tumbling || Forfeited[ActiveTeam])
                        BeginSettling();
                    break;

                case Phase.Retreat:
                    // Shots may still be in flight while the worm runs for cover.
                    if (--RetreatTicksLeft <= 0 || active == null || !active.Alive || active.PendingDamage > _activeDamageAtStart
                        || active.State == WormState.Tumbling || Forfeited[ActiveTeam])
                    {
                        MoveDir = 0;
                        active?.StopWalking();
                        if (Projectiles.Count > 0) SetPhase(Phase.Flying);
                        else BeginSettling();
                    }
                    break;

                case Phase.Flying:
                    if (Projectiles.Count == 0) BeginSettling();
                    break;

                case Phase.Settling:
                    _stillTicks = EverythingStill() ? _stillTicks + 1 : 0;
                    if (_stillTicks >= C.SettleTicks || PhaseTicks >= C.MaxSettleTicks) SetPhase(Phase.EndOfTurn);
                    break;

                case Phase.EndOfTurn:
                    EndOfTurn();
                    break;
            }
        }

        void EndOfTurn()
        {
            foreach (var worm in Worms)
            {
                if (!worm.Alive || worm.PendingDamage == 0) continue;
                worm.Hp = Math.Max(0, worm.Hp - worm.PendingDamage);
                worm.PendingDamage = 0;
            }
            // Dying worms blow up one at a time; each blast may kill more, so
            // settle and come back here until nobody is left at 0 HP.
            foreach (var worm in Worms)
            {
                if (worm.Alive && worm.Hp <= 0)
                {
                    Kill(worm, DeathCause.Hp);
                    BeginSettling();
                    return;
                }
            }

            int aliveTeams = 0, lastTeam = -1;
            for (int team = 0; team < TeamCount; team++)
            {
                if (Forfeited[team]) continue;
                if (Worms.Exists(x => x.Alive && x.Team == team))
                {
                    aliveTeams++;
                    lastTeam = team;
                }
            }
            if (aliveTeams <= 1)
            {
                Winner = aliveTeams == 1 ? lastTeam : -1;
                SetPhase(Phase.GameOver);
                Emit(new SimEvent { Type = SimEventType.GameOver, Team = Winner });
                return;
            }
            SetPhase(Phase.TurnStart);
        }

        void StartTurn()
        {
            int team = ActiveTeam;
            for (int i = 0; i < TeamCount; i++)
            {
                team = (team + 1) % TeamCount;
                if (!Forfeited[team] && Worms.Exists(x => x.Alive && x.Team == team)) break;
            }
            ActiveTeam = team;

            var members = Worms.FindAll(x => x.Team == team);
            for (int i = 0; i < members.Count; i++)
            {
                var candidate = members[(_nextWormOfTeam[team] + i) % members.Count];
                if (!candidate.Alive) continue;
                ActiveWorm = candidate.Id;
                _nextWormOfTeam[team] = (members.IndexOf(candidate) + 1) % members.Count;
                break;
            }

            Wind = (float)Math.Round(Rng.Range(-C.WindMax, C.WindMax));
            MoveDir = 0;
            _shotsLeft = 0;
            _burstLeft = 0;
            if (Ammo[ActiveTeam][(int)SelectedWeapon[ActiveTeam]] == 0) SelectedWeapon[ActiveTeam] = WeaponId.Bazooka;
            TurnTicksLeft = C.TurnTicks;
            _activeDamageAtStart = Active.PendingDamage;
            SetPhase(Phase.Aiming);
            Emit(new SimEvent { Type = SimEventType.Turn, Worm = ActiveWorm, Team = ActiveTeam, Value = Wind });
        }

        Vec2 FindSpawn(Rng rng)
        {
            for (int attempt = 0; attempt < 500; attempt++)
            {
                int x = rng.Range(80, Terrain.Width - 80);
                float? y = SurfaceY(x);
                if (y == null || y.Value > WaterLevel - 3 * C.WormRadius) continue;
                var p = new Vec2(x + 0.5f, y.Value);
                bool crowded = Worms.Exists(wm => Math.Abs(wm.Pos.X - p.X) < 40f && Math.Abs(wm.Pos.Y - p.Y) < 40f);
                if (!crowded || attempt > 400) return p;
            }
            return new Vec2(Terrain.Width / 2f, 0);
        }

        /// <summary>Highest standing position at column x, or null if the column is empty.</summary>
        public float? SurfaceY(int x)
        {
            var p = new Vec2(x + 0.5f, C.WormRadius + 1);
            if (Terrain.OverlapsCircle(p, C.WormRadius)) return null;
            for (int y = (int)p.Y; y < Terrain.Height; y++)
            {
                p = new Vec2(x + 0.5f, y);
                if (Terrain.OverlapsCircle(p + new Vec2(0, 1), C.WormRadius)) return y;
            }
            return null;
        }
    }
}
