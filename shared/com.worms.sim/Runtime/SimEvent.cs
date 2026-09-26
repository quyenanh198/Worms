namespace Worms.Sim
{
    public enum SimEventType : byte
    {
        Turn = 1,
        Fire = 2,
        Explode = 3,
        Hit = 4,
        Bounce = 5,
        Land = 6,
        Splash = 7,
        Death = 8,
        GameOver = 9,
        Shot = 10,
    }

    public enum DeathCause : byte
    {
        Hp = 0,
        Water = 1,
    }

    /// <summary>
    /// Something that happened during a tick. Clients use events for sound
    /// and effects only; state always comes from snapshots.
    /// Field use per type:
    /// Turn: Worm, Team, Value = wind.
    /// Fire: Worm, Weapon.
    /// Explode: X, Y, Value = radius.
    /// Hit: Worm, Amount = damage, X/Y = knockback direction.
    /// Bounce: Entity, X, Y, Value = impact speed.
    /// Land: Worm, Amount = fall damage, Value = impact speed.
    /// Splash: Entity, X, Y.
    /// Death: Worm, Cause.
    /// GameOver: Team = winning team or -1 for a draw.
    /// Shot: Worm (shooter), Weapon, X/Y = where the bullet stopped.
    /// </summary>
    public struct SimEvent
    {
        public int Tick;
        public SimEventType Type;
        public int Worm;
        public int Team;
        public int Entity;
        public WeaponId Weapon;
        public DeathCause Cause;
        public int Amount;
        public float X;
        public float Y;
        public float Value;
    }
}
