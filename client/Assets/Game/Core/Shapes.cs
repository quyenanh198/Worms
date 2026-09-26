using System;

namespace Worms.Game.Core
{
    /// <summary>Procedural meshes, so the game needs no model files for simple shapes.</summary>
    public static class Shapes
    {
        /// <summary>UV sphere centered at the origin.</summary>
        public static MeshBuffers Sphere(float radius, int rings = 12, int segments = 18)
        {
            var m = new MeshBuffers();
            for (int r = 0; r <= rings; r++)
            {
                float v = (float)r / rings;
                float phi = (float)Math.PI * v;
                for (int s = 0; s <= segments; s++)
                {
                    float u = (float)s / segments;
                    float theta = 2f * (float)Math.PI * u;
                    float nx = (float)(Math.Sin(phi) * Math.Cos(theta));
                    float ny = (float)Math.Cos(phi);
                    float nz = (float)(Math.Sin(phi) * Math.Sin(theta));
                    m.AddVertex(nx * radius, ny * radius, nz * radius, nx, ny, nz, u, v);
                }
            }
            int row = segments + 1;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * row + s, b = a + 1, c = a + row, d = c + 1;
                    // Clockwise seen from outside (Unity front face).
                    m.AddTriangle(a, b, c);
                    m.AddTriangle(b, d, c);
                }
            return m;
        }

        /// <summary>Capsule along Y: total height <paramref name="height"/>, centered at the origin.</summary>
        public static MeshBuffers Capsule(float radius, float height, int rings = 8, int segments = 18)
        {
            var m = new MeshBuffers();
            float half = Math.Max(0, height / 2 - radius);
            int totalRings = rings * 2 + 1;
            for (int r = 0; r <= totalRings; r++)
            {
                // Top hemisphere rings 0..rings, bottom rings+1..2*rings+1.
                bool top = r <= rings;
                float v = top ? (float)r / rings * 0.5f : 0.5f + (float)(r - rings - 1) / rings * 0.5f;
                float phi = (float)Math.PI * v;
                float offset = top ? half : -half;
                for (int s = 0; s <= segments; s++)
                {
                    float u = (float)s / segments;
                    float theta = 2f * (float)Math.PI * u;
                    float nx = (float)(Math.Sin(phi) * Math.Cos(theta));
                    float ny = (float)Math.Cos(phi);
                    float nz = (float)(Math.Sin(phi) * Math.Sin(theta));
                    m.AddVertex(nx * radius, ny * radius + offset, nz * radius, nx, ny, nz, u, v);
                }
            }
            int row = segments + 1;
            for (int r = 0; r < totalRings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * row + s, b = a + 1, c = a + row, d = c + 1;
                    m.AddTriangle(a, b, c);
                    m.AddTriangle(b, d, c);
                }
            return m;
        }

        /// <summary>Flat grid in XZ at y = 0, facing up.</summary>
        public static MeshBuffers Grid(float x0, float z0, float x1, float z1, int nx, int nz)
        {
            var m = new MeshBuffers();
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float u = (float)i / nx, v = (float)j / nz;
                    m.AddVertex(x0 + (x1 - x0) * u, 0, z0 + (z1 - z0) * v, 0, 1, 0, u, v);
                }
            int row = nx + 1;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                    m.AddTriangle(a, c, b);
                    m.AddTriangle(b, c, d);
                }
            return m;
        }
    }
}
