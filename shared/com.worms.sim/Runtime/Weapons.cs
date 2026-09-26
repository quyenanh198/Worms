using System.Collections.Generic;

namespace Worms.Sim
{
    /// <summary>Wire-stable ids: never renumber, only append.</summary>
    public enum WeaponId : byte
    {
        Bazooka = 0,
        Grenade = 1,
        ClusterBomb = 2,
        Shotgun = 3,
        Uzi = 4,
        Dynamite = 5,
        BaseballBat = 6,
        AirStrike = 7,
    }

    public enum Behavior : byte
    {
        Ballistic,
        Hitscan,
        Melee,
        Placed,
        Airstrike,
    }

    /// <summary>One weapon = one row of data plus a shared behavior (docs/PLAN.md §3.8).</summary>
    public sealed class WeaponDef
    {
        public WeaponId Id;
        public Behavior Behavior;
        /// <summary>Explodes when its fuse runs out instead of on impact.</summary>
        public bool TimerFuse;
        public float WindFactor;
        public float Restitution;
        public float Friction;
        public float ProjectileRadius;
        /// <summary>Launch speed at full power.</summary>
        public float MaxSpeed;
        public float BlastRadius;
        public int MaxDamage;
        /// <summary>Uses per match per team; -1 = unlimited.</summary>
        public int Ammo;
    }

    public static class Weapons
    {
        static readonly Dictionary<WeaponId, WeaponDef> Table = new Dictionary<WeaponId, WeaponDef>
        {
            [WeaponId.Bazooka] = new WeaponDef
            {
                Id = WeaponId.Bazooka, Behavior = Behavior.Ballistic, TimerFuse = false,
                WindFactor = 1f, Restitution = 0f, Friction = 0f, ProjectileRadius = 3f,
                MaxSpeed = 1000f, BlastRadius = 50f, MaxDamage = 50, Ammo = -1,
            },
            [WeaponId.Grenade] = new WeaponDef
            {
                Id = WeaponId.Grenade, Behavior = Behavior.Ballistic, TimerFuse = true,
                WindFactor = 0f, Restitution = 0.5f, Friction = 0.1f, ProjectileRadius = 3f,
                MaxSpeed = 700f, BlastRadius = 50f, MaxDamage = 50, Ammo = -1,
            },
        };

        public static bool TryGet(WeaponId id, out WeaponDef def)
        {
            return Table.TryGetValue(id, out def);
        }

        public static WeaponDef Get(WeaponId id)
        {
            return Table[id];
        }

        public static IEnumerable<WeaponDef> All => Table.Values;
    }
}
