namespace Worms.Sim
{
    public sealed class Projectile
    {
        public int Id;
        public WeaponDef Weapon;
        public Body Body;
        public int OwnerWorm;
        /// <summary>Fuse length in ticks for timer weapons; -1 for impact fuses.</summary>
        public int FuseTicks;
        /// <summary>Ticks since the tick it was fired on (0 on the first tick after firing).</summary>
        public int Age;

        public int FuseLeft => FuseTicks < 0 ? -1 : FuseTicks - Age;
        public bool Alive = true;

        public Vec2 Pos => Body.Pos;
    }
}
