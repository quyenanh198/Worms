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
        /// <summary>Fixed fuse in seconds (Placed); timer weapons otherwise use the player's choice.</summary>
        public int FixedFuse;
        public float WindFactor;
        public float Restitution;
        public float Friction;
        public float ProjectileRadius = 3f;
        /// <summary>Launch speed at full power (Ballistic), or speed of each missile (Airstrike).</summary>
        public float MaxSpeed;
        public float BlastRadius;
        public int MaxDamage;
        /// <summary>Uses per match per team; -1 = unlimited.</summary>
        public int Ammo = -1;

        /// <summary>Separate trigger pulls per use (shotgun: 2); the player may re-aim between them.</summary>
        public int Shots = 1;
        /// <summary>Bullets per trigger pull, fired automatically (uzi).</summary>
        public int Burst = 1;
        public int BurstIntervalTicks;
        /// <summary>Random spread per bullet, radians either side.</summary>
        public float Spread;
        public float Range = 1500f;
        /// <summary>Damage of a direct hit for hitscan without a blast, and for melee.</summary>
        public int HitDamage;
        /// <summary>Hitscan: whether a hit makes a small explosion (shotgun) or not (uzi).</summary>
        public bool Explodes;
        /// <summary>Melee: speed a struck worm is launched at.</summary>
        public float LaunchSpeed;
        public float MeleeReach;

        /// <summary>Cluster bomb: fragments released when it explodes.</summary>
        public int Fragments;
        public WeaponDef Fragment;

        /// <summary>Air strike: missiles and the distance between them.</summary>
        public int Missiles;
        public float MissileSpacing;

        /// <summary>Hold to charge power before firing (only thrown/launched weapons).</summary>
        public bool NeedsPower => Behavior == Behavior.Ballistic;
        /// <summary>Uses the aim angle (everything but dropped dynamite and the air strike).</summary>
        public bool Aims => Behavior != Behavior.Placed && Behavior != Behavior.Airstrike;
        /// <summary>Fires at a point picked on the map.</summary>
        public bool Targets => Behavior == Behavior.Airstrike;
        public bool UsesFuse => TimerFuse && FixedFuse == 0;
    }

    public static class Weapons
    {
        public const int Count = 8;

        static readonly WeaponDef ClusterFragment = new WeaponDef
        {
            Id = WeaponId.ClusterBomb, Behavior = Behavior.Ballistic, ProjectileRadius = 2f,
            BlastRadius = 20f, MaxDamage = 15,
        };

        static readonly WeaponDef[] Table =
        {
            new WeaponDef
            {
                Id = WeaponId.Bazooka, Behavior = Behavior.Ballistic,
                WindFactor = 1f, MaxSpeed = 1000f, BlastRadius = 50f, MaxDamage = 50,
            },
            new WeaponDef
            {
                Id = WeaponId.Grenade, Behavior = Behavior.Ballistic, TimerFuse = true,
                Restitution = 0.5f, Friction = 0.1f, MaxSpeed = 700f, BlastRadius = 50f, MaxDamage = 50,
            },
            new WeaponDef
            {
                Id = WeaponId.ClusterBomb, Behavior = Behavior.Ballistic, TimerFuse = true,
                Restitution = 0.5f, Friction = 0.1f, MaxSpeed = 700f, BlastRadius = 20f, MaxDamage = 20,
                Fragments = 5, Fragment = ClusterFragment, Ammo = 2,
            },
            new WeaponDef
            {
                Id = WeaponId.Shotgun, Behavior = Behavior.Hitscan, Shots = 2,
                Explodes = true, BlastRadius = 10f, MaxDamage = 25,
            },
            new WeaponDef
            {
                Id = WeaponId.Uzi, Behavior = Behavior.Hitscan, Burst = 10, BurstIntervalTicks = 4,
                Spread = 5f * (float)System.Math.PI / 180f, HitDamage = 5, Ammo = 3,
            },
            new WeaponDef
            {
                Id = WeaponId.Dynamite, Behavior = Behavior.Placed, TimerFuse = true, FixedFuse = 5,
                Restitution = 0.1f, Friction = 0.5f, ProjectileRadius = 4f, MaxSpeed = 40f,
                BlastRadius = 75f, MaxDamage = 75, Ammo = 1,
            },
            new WeaponDef
            {
                Id = WeaponId.BaseballBat, Behavior = Behavior.Melee,
                HitDamage = 30, LaunchSpeed = 700f, MeleeReach = 20f, Ammo = 2,
            },
            new WeaponDef
            {
                Id = WeaponId.AirStrike, Behavior = Behavior.Airstrike, MaxSpeed = 80f,
                Missiles = 5, MissileSpacing = 30f, BlastRadius = 30f, MaxDamage = 30, Ammo = 1,
            },
        };

        public static bool TryGet(WeaponId id, out WeaponDef def)
        {
            int i = (int)id;
            def = i >= 0 && i < Table.Length ? Table[i] : null;
            return def != null;
        }

        public static WeaponDef Get(WeaponId id)
        {
            return Table[(int)id];
        }

        public static IEnumerable<WeaponDef> All => Table;
    }
}
