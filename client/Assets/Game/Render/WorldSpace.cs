using UnityEngine;

namespace Worms.Game.Render
{
    /// <summary>
    /// Simulation units (1 u = 1 terrain cell, Y down) to Unity world space
    /// (meters, Y up). The playing field is the XY plane; the terrain slab runs
    /// from <see cref="TerrainFrontZ"/> to <see cref="TerrainBackZ"/> and actors
    /// stand just in front of it.
    /// </summary>
    public static class WorldSpace
    {
        public const float Scale = 0.05f;
        public const float TerrainFrontZ = 0f;
        public const float TerrainBackZ = 2.2f;
        public const float ActorZ = -0.35f;

        public static Vector3 ToWorld(float x, float y, float z = ActorZ)
        {
            return new Vector3(x * Scale, -y * Scale, z);
        }

        public static Vector2 ToSim(Vector3 world)
        {
            return new Vector2(world.x / Scale, -world.y / Scale);
        }
    }
}
