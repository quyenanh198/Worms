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

        /// <summary>Closed cylinder along Y, centered at the origin.</summary>
        public static MeshBuffers Cylinder(float radius, float length, int segments = 14)
        {
            var m = new MeshBuffers();
            float h = length / 2;
            // Side.
            for (int s = 0; s <= segments; s++)
            {
                float a = 2f * (float)Math.PI * s / segments;
                float nx = (float)Math.Cos(a), nz = (float)Math.Sin(a);
                m.AddVertex(nx * radius, h, nz * radius, nx, 0, nz, (float)s / segments, 1);
                m.AddVertex(nx * radius, -h, nz * radius, nx, 0, nz, (float)s / segments, 0);
            }
            for (int s = 0; s < segments; s++)
            {
                int a = s * 2, b = a + 1, c = a + 2, d = a + 3;
                m.AddTriangle(a, c, b);
                m.AddTriangle(b, c, d);
            }
            // Caps.
            foreach (int side in new[] { 1, -1 })
            {
                int center = m.AddVertex(0, h * side, 0, 0, side, 0, 0.5f, 0.5f);
                int first = m.VertexCount;
                for (int s = 0; s <= segments; s++)
                {
                    float a = 2f * (float)Math.PI * s / segments;
                    m.AddVertex((float)Math.Cos(a) * radius, h * side, (float)Math.Sin(a) * radius, 0, side, 0, 0, 0);
                }
                for (int s = 0; s < segments; s++)
                {
                    if (side > 0) m.AddTriangle(center, first + s + 1, first + s);
                    else m.AddTriangle(center, first + s, first + s + 1);
                }
            }
            return m;
        }

        /// <summary>Axis-aligned box centered at the origin with flat faces.</summary>
        public static MeshBuffers Box(float sx, float sy, float sz)
        {
            var m = new MeshBuffers();
            float x = sx / 2, y = sy / 2, z = sz / 2;
            // Each face: normal and two tangents (u, v) with u x v = normal for a clockwise-from-outside quad.
            float[][] faces =
            {
                new float[] { 1, 0, 0, 0, 0, 1, 0, 1, 0 },
                new float[] { -1, 0, 0, 0, 0, -1, 0, 1, 0 },
                new float[] { 0, 1, 0, 1, 0, 0, 0, 0, 1 },
                new float[] { 0, -1, 0, -1, 0, 0, 0, 0, 1 },
                new float[] { 0, 0, 1, -1, 0, 0, 0, 1, 0 },
                new float[] { 0, 0, -1, 1, 0, 0, 0, 1, 0 },
            };
            foreach (var f in faces)
            {
                float nx = f[0], ny = f[1], nz = f[2];
                int start = m.VertexCount;
                foreach (var (su, sv) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    float px = nx * x + (f[3] * su + f[6] * sv) * x;
                    float py = ny * y + (f[4] * su + f[7] * sv) * y;
                    float pz = nz * z + (f[5] * su + f[8] * sv) * z;
                    m.AddVertex(px, py, pz, nx, ny, nz, (su + 1) / 2, (sv + 1) / 2);
                }
                m.AddTriangle(start, start + 2, start + 1);
                m.AddTriangle(start, start + 3, start + 2);
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
