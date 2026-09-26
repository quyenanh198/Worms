using System;
using System.Collections.Generic;
using Worms.Sim;

namespace Worms.Protocol
{
    public struct WormSnap
    {
        public int Id;
        public int Team;
        public float X, Y;
        public float Vx, Vy;
        public int Hp;
        public int PendingDamage;
        public WormState State;
        public int Facing;
        public float Aim;

        public bool Alive => State != WormState.Dead;
    }

    public struct ProjectileSnap
    {
        public int Id;
        public WeaponId Weapon;
        public float X, Y;
        /// <summary>Ticks until a timer fuse explodes; -1 for impact fuses.</summary>
        public int FuseLeft;
    }

    /// <summary>
    /// Everything a client renders except the terrain, which it rebuilds from
    /// the seed and carve ops. Sent 20 times a second; the offline sandbox
    /// makes one per tick from its local World.
    /// </summary>
    public sealed class Snapshot
    {
        public uint Tick;
        public Phase Phase;
        public int ActiveTeam = -1;
        public int ActiveWorm = -1;
        public float Wind;
        public int TurnTicksLeft;
        public int RetreatTicksLeft;
        public int Winner = -1;
        public WeaponId ActiveWeapon;
        /// <summary>Uses left of each weapon for the active team (-1 = unlimited), indexed by WeaponId.</summary>
        public readonly int[] ActiveAmmo = new int[Weapons.Count];
        /// <summary>A multi-shot weapon (shotgun, uzi) is mid-attack: no switching.</summary>
        public bool AttackInProgress;
        public readonly List<WormSnap> Worms = new List<WormSnap>();
        public readonly List<ProjectileSnap> Projectiles = new List<ProjectileSnap>();

        public static Snapshot FromWorld(World w, Snapshot reuse = null)
        {
            var s = reuse ?? new Snapshot();
            s.Tick = (uint)w.Tick;
            s.Phase = w.Phase;
            s.ActiveTeam = w.ActiveTeam;
            s.ActiveWorm = w.ActiveWorm;
            s.Wind = w.Wind;
            s.TurnTicksLeft = w.TurnTicksLeft;
            s.RetreatTicksLeft = w.RetreatTicksLeft;
            s.Winner = w.Winner;
            s.ActiveWeapon = w.ActiveTeam >= 0 ? w.SelectedWeapon[w.ActiveTeam] : WeaponId.Bazooka;
            for (int i = 0; i < Weapons.Count; i++) s.ActiveAmmo[i] = w.ActiveTeam >= 0 ? w.Ammo[w.ActiveTeam][i] : -1;
            s.AttackInProgress = w.AttackInProgress;
            s.Worms.Clear();
            foreach (var m in w.Worms)
            {
                s.Worms.Add(new WormSnap
                {
                    Id = m.Id, Team = m.Team,
                    X = m.Pos.X, Y = m.Pos.Y, Vx = m.Body.Vel.X, Vy = m.Body.Vel.Y,
                    Hp = m.Hp, PendingDamage = m.PendingDamage, State = m.State,
                    Facing = m.Facing, Aim = m.Aim,
                });
            }
            s.Projectiles.Clear();
            foreach (var p in w.Projectiles)
                s.Projectiles.Add(new ProjectileSnap { Id = p.Id, Weapon = p.Weapon.Id, X = p.Pos.X, Y = p.Pos.Y, FuseLeft = p.FuseLeft });
            return s;
        }

        public byte[] Encode()
        {
            var w = new MsgWriter(MsgType.Snapshot);
            WriteTo(w);
            return w.ToArray();
        }

        public void WriteTo(MsgWriter w)
        {
            w.U32(Tick).U8((byte)Phase).I8((sbyte)ActiveTeam).I16((short)ActiveWorm)
                .F32(Wind).U16((ushort)TurnTicksLeft).U16((ushort)RetreatTicksLeft)
                .I8((sbyte)Winner).U8((byte)ActiveWeapon).Bool(AttackInProgress);
            foreach (var a in ActiveAmmo) w.I8((sbyte)Math.Max(-1, Math.Min(100, a)));
            w.U8((byte)Worms.Count);
            foreach (var m in Worms)
            {
                w.I16((short)m.Id).U8((byte)m.Team).F32(m.X).F32(m.Y).F32(m.Vx).F32(m.Vy)
                 .I16((short)m.Hp).I16((short)m.PendingDamage).U8((byte)m.State).I8((sbyte)m.Facing).F32(m.Aim);
            }
            w.U8((byte)Projectiles.Count);
            foreach (var p in Projectiles)
                w.I32(p.Id).U8((byte)p.Weapon).F32(p.X).F32(p.Y).I16((short)p.FuseLeft);
        }

        public static Snapshot Decode(MsgReader r, Snapshot reuse = null)
        {
            var s = reuse ?? new Snapshot();
            s.Tick = r.U32();
            s.Phase = (Phase)r.U8();
            s.ActiveTeam = r.I8();
            s.ActiveWorm = r.I16();
            s.Wind = r.F32();
            s.TurnTicksLeft = r.U16();
            s.RetreatTicksLeft = r.U16();
            s.Winner = r.I8();
            s.ActiveWeapon = (WeaponId)r.U8();
            s.AttackInProgress = r.Bool();
            for (int i = 0; i < Weapons.Count; i++) s.ActiveAmmo[i] = r.I8();
            s.Worms.Clear();
            int wormCount = r.U8();
            for (int i = 0; i < wormCount; i++)
            {
                s.Worms.Add(new WormSnap
                {
                    Id = r.I16(), Team = r.U8(), X = r.F32(), Y = r.F32(), Vx = r.F32(), Vy = r.F32(),
                    Hp = r.I16(), PendingDamage = r.I16(), State = (WormState)r.U8(), Facing = r.I8(), Aim = r.F32(),
                });
            }
            s.Projectiles.Clear();
            int projCount = r.U8();
            for (int i = 0; i < projCount; i++)
                s.Projectiles.Add(new ProjectileSnap { Id = r.I32(), Weapon = (WeaponId)r.U8(), X = r.F32(), Y = r.F32(), FuseLeft = r.I16() });
            return s;
        }

        public WormSnap? FindWorm(int id)
        {
            foreach (var m in Worms) if (m.Id == id) return m;
            return null;
        }
    }
}
