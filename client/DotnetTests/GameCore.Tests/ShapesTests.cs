using Worms.Game.Core;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class ShapesTests
    {
        static void AssertOutward(MeshBuffers m, bool allowDegenerate = true)
        {
            for (int t = 0; t < m.Triangles.Count; t += 3)
            {
                int a = m.Triangles[t] * 3, b = m.Triangles[t + 1] * 3, c = m.Triangles[t + 2] * 3;
                float abx = m.Positions[b] - m.Positions[a], aby = m.Positions[b + 1] - m.Positions[a + 1], abz = m.Positions[b + 2] - m.Positions[a + 2];
                float acx = m.Positions[c] - m.Positions[a], acy = m.Positions[c + 1] - m.Positions[a + 1], acz = m.Positions[c + 2] - m.Positions[a + 2];
                float nx = aby * acz - abz * acy, ny = abz * acx - abx * acz, nz = abx * acy - aby * acx;
                float len2 = nx * nx + ny * ny + nz * nz;
                if (len2 < 1e-12f && allowDegenerate) continue; // pole triangles
                float dot = nx * m.Normals[a] + ny * m.Normals[a + 1] + nz * m.Normals[a + 2]
                          + nx * m.Normals[c] + ny * m.Normals[c + 1] + nz * m.Normals[c + 2];
                Assert.True(dot > 0, "triangle " + t / 3 + " faces inward");
            }
        }

        [Fact] public void SphereFacesOutward() { AssertOutward(Shapes.Sphere(1f)); }
        [Fact] public void CapsuleFacesOutward() { AssertOutward(Shapes.Capsule(0.4f, 1.2f)); }

        [Fact]
        public void GridFacesUp()
        {
            var m = Shapes.Grid(0, 0, 10, 10, 4, 4);
            for (int t = 0; t < m.Triangles.Count; t += 3)
            {
                int a = m.Triangles[t] * 3, b = m.Triangles[t + 1] * 3, c = m.Triangles[t + 2] * 3;
                float abx = m.Positions[b] - m.Positions[a], abz = m.Positions[b + 2] - m.Positions[a + 2];
                float acx = m.Positions[c] - m.Positions[a], acz = m.Positions[c + 2] - m.Positions[a + 2];
                Assert.True(abz * acx - abx * acz > 0);
            }
        }
    }
}
