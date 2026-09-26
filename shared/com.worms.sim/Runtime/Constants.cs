namespace Worms.Sim
{
    /// <summary>
    /// Every gameplay number lives here (docs/PLAN.md §3.6.1). Units: 1 u = one
    /// terrain cell; time in seconds unless the name says Ticks.
    /// </summary>
    public static class C
    {
        public const int TicksPerSecond = 60;
        public const float Dt = 1f / TicksPerSecond;

        public const int MapWidth = 2048;
        public const int MapHeight = 1024;
        /// <summary>Distance of the water surface above the bottom of the map.</summary>
        public const int WaterDepth = 40;

        public const float Gravity = 600f;
        public const float WindMax = 100f;

        public const float WormRadius = 8f;
        public const float WalkSpeed = 60f;
        public const int MaxClimb = 4;
        public const float JumpVx = 150f;
        public const float JumpVy = -200f;
        public const float BackflipVx = 40f;
        public const float BackflipVy = -330f;
        public const float SafeFallSpeed = 270f;
        public const float FallDamageDivisor = 8f;
        public const float WormRestitution = 0.3f;
        public const float WormFriction = 0.25f;
        /// <summary>Knockback speed per point of damage.</summary>
        public const float Knockback = 8f;
        /// <summary>Knockback direction always has at least this much upward component (before renormalizing).</summary>
        public const float KnockbackMinUp = 0.6f;
        /// <summary>A knocked-back worm stops tumbling on a floor below this speed.</summary>
        public const float LandSpeed = 40f;

        /// <summary>Ticks of slow contact on a non-floor surface after which a worm counts as landed.</summary>
        public const int StuckTicks = 45;

        public const int StartHp = 100;
        public const int TurnTicks = 45 * TicksPerSecond;
        public const int RetreatTicks = 3 * TicksPerSecond;
        /// <summary>Everything must be still this long before a turn can end.</summary>
        public const int SettleTicks = 30;
        /// <summary>Safety net: Settling never lasts longer than this.</summary>
        public const int MaxSettleTicks = 12 * TicksPerSecond;

        public const float DeathBlastRadius = 20f;
        public const int DeathBlastDamage = 10;

        public const float MuzzleOffset = WormRadius + 4f;
        /// <summary>Ticks during which a projectile ignores the worm that fired it.</summary>
        public const int OwnerImmunityTicks = 10;
    }
}
