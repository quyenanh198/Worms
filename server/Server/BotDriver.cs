using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Worms.Sim;

namespace Worms.Server
{
    /// <summary>
    /// Plays one team for a computer player. The match loop ticks it under the room lock:
    /// when its turn starts it snapshots the world, lets <see cref="BotAi"/> think on the
    /// thread pool (so the match never stutters while it searches), then feeds the chosen
    /// move back in as ordinary <see cref="SimInput"/>s at a human pace, so people can see
    /// whose turn it is and what it is aiming at. Delays count world ticks, so tests that
    /// speed the simulation up get faster bots too.
    /// </summary>
    public sealed class BotDriver
    {
        /// <summary>Pause before acting: the turn banner and camera move first.</summary>
        const int ThinkTicks = 90;
        /// <summary>The aim is shown this long before the shot.</summary>
        const int AimTicks = 40;
        /// <summary>The shotgun's second barrel.</summary>
        const int SecondShotTicks = 50;
        /// <summary>If planning is somehow still running this long, fire the fallback instead of wasting the turn.</summary>
        const int GiveUpTicks = 15 * C.TicksPerSecond;

        enum Stage { Idle, Thinking, Turned, Aimed, SecondShot, Done }

        readonly Rng _rng;
        int _turnKey = int.MinValue;
        Stage _stage;
        int _stageTick;
        Task<BotPlan> _planning;
        BotPlan _plan;

        public BotDriver(uint seed)
        {
            _rng = new Rng(seed);
        }

        /// <summary>Adds this tick's inputs for <paramref name="team"/>, if it is the bot's turn.</summary>
        public void Tick(World w, int team, List<SimInput> into)
        {
            var worm = w.Active;
            if (w.Phase != Phase.Aiming || w.ActiveTeam != team || worm == null || !worm.Alive) return;

            // Constant for the whole Aiming phase of one turn (the shotgun's two shots included).
            int key = w.Tick - w.PhaseTicks;
            if (key != _turnKey)
            {
                _turnKey = key;
                _plan = null;
                _stage = Stage.Thinking;
                _stageTick = w.Tick;
                var view = BotAi.Capture(w);
                uint seed = _rng.NextUInt(); // drawn here so the planner never shares the Rng across threads
                _planning = Task.Run(() => BotAi.Plan(view, new Rng(seed)));
                return;
            }

            int waited = w.Tick - _stageTick;
            switch (_stage)
            {
                case Stage.Thinking:
                    if (waited < ThinkTicks) return;
                    if (_planning.IsCompletedSuccessfully) _plan = _planning.Result;
                    else if (!_planning.IsCompleted && waited < GiveUpTicks) return;
                    else _plan = Fallback(w, team);
                    // One tick of walking turns the worm around; it is stopped again next tick.
                    if (worm.Facing != _plan.Facing) into.Add(new SimInput { Team = team, Kind = InputKind.Move, Dir = _plan.Facing });
                    into.Add(new SimInput { Team = team, Kind = InputKind.Select, Weapon = _plan.Weapon });
                    Next(Stage.Turned, w);
                    break;

                case Stage.Turned:
                    into.Add(new SimInput { Team = team, Kind = InputKind.Move, Dir = 0 });
                    into.Add(new SimInput { Team = team, Kind = InputKind.Aim, Angle = _plan.Angle });
                    Next(Stage.Aimed, w);
                    break;

                case Stage.Aimed:
                    if (waited < AimTicks || !worm.IsGrounded) return; // firing needs solid ground
                    into.Add(FireInput(team));
                    Next(_plan.Weapon == WeaponId.Shotgun ? Stage.SecondShot : Stage.Done, w);
                    break;

                case Stage.SecondShot:
                    if (waited < SecondShotTicks || !worm.IsGrounded) return;
                    if (w.AttackInProgress) into.Add(FireInput(team));
                    Next(Stage.Done, w);
                    break;
            }
        }

        void Next(Stage stage, World w)
        {
            _stage = stage;
            _stageTick = w.Tick;
        }

        SimInput FireInput(int team)
        {
            return new SimInput
            {
                Team = team, Kind = InputKind.Fire, Weapon = _plan.Weapon,
                Angle = _plan.Angle, Power = _plan.Power, Fuse = _plan.Fuse,
            };
        }

        /// <summary>A plain rocket lobbed toward the nearest enemy, for when planning failed.</summary>
        static BotPlan Fallback(World w, int team)
        {
            var me = w.Active;
            Worm nearest = null;
            foreach (var other in w.Worms)
            {
                if (!other.Alive || other.Team == team) continue;
                if (nearest == null || Vec2.Distance(other.Pos, me.Pos) < Vec2.Distance(nearest.Pos, me.Pos)) nearest = other;
            }
            int facing = nearest == null ? me.Facing : (nearest.Pos.X >= me.Pos.X ? 1 : -1);
            return new BotPlan { Weapon = WeaponId.Bazooka, Facing = facing, Angle = (float)(Math.PI / 4), Power = 0.6f };
        }
    }
}
