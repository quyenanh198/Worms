using System;
using Worms.Game.Core;
using Worms.Sim;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class WormRigTests
    {
        static float MaxJump(WormRig rig, WormAnimInput from, WormAnimInput to)
        {
            float dt = 1f / 60;
            rig.Update(from, dt);
            var x = (float[])rig.X.Clone();
            var y = (float[])rig.Y.Clone();
            to.Time = from.Time + dt;
            rig.Update(to, dt);
            float max = 0;
            for (int i = 0; i < WormRig.Points; i++)
                max = Math.Max(max, (float)Math.Sqrt((rig.X[i] - x[i]) * (rig.X[i] - x[i]) + (rig.Y[i] - y[i]) * (rig.Y[i] - y[i])));
            return max;
        }

        [Fact]
        public void IdleWormStandsOnTheGroundWithHeadUp()
        {
            var rig = new WormRig();
            rig.Update(new WormAnimInput { State = WormState.Idle }, 1f / 60);
            float bottom = float.MaxValue;
            for (int i = 0; i < WormRig.Points; i++) bottom = Math.Min(bottom, rig.Y[i] - rig.R[i]);
            Assert.InRange(bottom, -C.WormRadius - 0.5f, -C.WormRadius + 0.5f);
            Assert.True(rig.HeadY > 4f, "head should be up");
            rig.HeadDirection(out _, out float dy);
            Assert.True(dy > 0.5f);
        }

        [Fact]
        public void StateChangesBlendWithoutJumps()
        {
            var rig = new WormRig();
            var idle = new WormAnimInput { State = WormState.Idle };
            for (int i = 0; i < 30; i++) { idle.Time = i / 60f; rig.Update(idle, 1f / 60); }
            float jump = MaxJump(rig, idle, new WormAnimInput { State = WormState.Tumbling });
            Assert.True(jump < 3f, "moved " + jump + " units in one frame");
        }

        [Fact]
        public void AimingLeansTheHeadTowardTheAim()
        {
            var up = new WormRig();
            var fwd = new WormRig();
            for (int i = 0; i < 120; i++)
            {
                up.Update(new WormAnimInput { State = WormState.Idle, Holding = true, Aim = 1.5f }, 1f / 60);
                fwd.Update(new WormAnimInput { State = WormState.Idle, Holding = true, Aim = 0f }, 1f / 60);
            }
            up.HeadDirection(out float ux, out _);
            fwd.HeadDirection(out float fx, out _);
            Assert.True(fx > ux + 0.3f, "aiming forward should tilt the head forward");
        }

        [Fact]
        public void TubeMeshFacesOutward()
        {
            var rig = new WormRig();
            rig.Update(new WormAnimInput { State = WormState.Walking, Time = 0.3f }, 1f / 60);
            var m = new MeshBuffers();
            rig.BuildMesh(m, 1f);
            Assert.InRange(m.VertexCount, 100, 250);
            int outward = 0, total = 0;
            for (int t = 0; t < m.Triangles.Count; t += 3)
            {
                int a = m.Triangles[t] * 3, b = m.Triangles[t + 1] * 3, c = m.Triangles[t + 2] * 3;
                float abx = m.Positions[b] - m.Positions[a], aby = m.Positions[b + 1] - m.Positions[a + 1], abz = m.Positions[b + 2] - m.Positions[a + 2];
                float acx = m.Positions[c] - m.Positions[a], acy = m.Positions[c + 1] - m.Positions[a + 1], acz = m.Positions[c + 2] - m.Positions[a + 2];
                float nx = aby * acz - abz * acy, ny = abz * acx - abx * acz, nz = abx * acy - aby * acx;
                if (nx * nx + ny * ny + nz * nz < 1e-10f) continue;
                total++;
                if (nx * m.Normals[a] + ny * m.Normals[a + 1] + nz * m.Normals[a + 2] > 0) outward++;
            }
            Assert.True(outward > total * 0.97f, outward + "/" + total);
        }

        [Fact]
        public void CylinderAndBoxFaceOutward()
        {
            foreach (var m in new[] { Shapes.Cylinder(0.5f, 2f), Shapes.Box(1f, 2f, 3f) })
            {
                for (int t = 0; t < m.Triangles.Count; t += 3)
                {
                    int a = m.Triangles[t] * 3, b = m.Triangles[t + 1] * 3, c = m.Triangles[t + 2] * 3;
                    float abx = m.Positions[b] - m.Positions[a], aby = m.Positions[b + 1] - m.Positions[a + 1], abz = m.Positions[b + 2] - m.Positions[a + 2];
                    float acx = m.Positions[c] - m.Positions[a], acy = m.Positions[c + 1] - m.Positions[a + 1], acz = m.Positions[c + 2] - m.Positions[a + 2];
                    float nx = aby * acz - abz * acy, ny = abz * acx - abx * acz, nz = abx * acy - aby * acx;
                    Assert.True(nx * m.Normals[a] + ny * m.Normals[a + 1] + nz * m.Normals[a + 2] > 0, "triangle " + t / 3);
                }
            }
        }
    }
}
