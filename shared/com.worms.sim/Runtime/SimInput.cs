namespace Worms.Sim
{
    public enum InputKind : byte
    {
        Move = 1,
        Jump = 2,
        Backflip = 3,
        Aim = 4,
        Select = 5,
        Fire = 6,
    }

    /// <summary>
    /// A player's request, already decoded from the network. The world checks
    /// that <see cref="Team"/> is the active team and that the phase allows it.
    /// </summary>
    public struct SimInput
    {
        public int Team;
        public InputKind Kind;
        /// <summary>Move: -1, 0 or 1.</summary>
        public int Dir;
        /// <summary>Aim/Fire: elevation in radians, -PI/2 (down) .. PI/2 (up), relative to facing.</summary>
        public float Angle;
        /// <summary>Fire: 0..1.</summary>
        public float Power;
        /// <summary>Fire: fuse seconds for timer weapons, 1..5.</summary>
        public int Fuse;
        public WeaponId Weapon;
        public float TargetX;
        public float TargetY;
    }
}
