namespace Worms.Sim
{
    /// <summary>
    /// Seeded PRNG (mulberry32). All randomness in the simulation goes through
    /// this so a seed plus a command log reproduces a match exactly.
    /// </summary>
    public sealed class Rng
    {
        uint _state;

        public Rng(uint seed)
        {
            _state = seed;
        }

        public uint State => _state;

        public uint NextUInt()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint t = _state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return t ^ (t >> 14);
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform int in [min, max).</summary>
        public int Range(int min, int max)
        {
            return min + (int)(NextUInt() % (uint)(max - min));
        }
    }
}
