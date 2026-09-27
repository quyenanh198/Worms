using System;
using Worms.Game.Core;
using Worms.Sim;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class TerrainFieldTests
    {
        static Terrain Ground()
        {
            var t = new Terrain(300, 200);
            t.FillRect(0, 100, 300, 200, true);
            return t;
        }

        [Fact]
        public void MeasuresDistanceToAirAndLandAbove()
        {
            var f = new TerrainField(Ground());
            Assert.InRange(f.EdgeCells(150, 100), 0.9f, 1.1f);   // top row touches air
            Assert.InRange(f.EdgeCells(150, 103), 3.8f, 4.2f);
            Assert.InRange(f.EdgeCells(150, 150), TerrainField.MaxEdge - 0.1f, TerrainField.MaxEdge);
            Assert.Equal(0f, f.EdgeCells(150, 50));               // air
            Assert.InRange(f.CellsAbove(150, 100), 0f, 0.1f);    // the surface: nothing above
            Assert.InRange(f.CellsAbove(150, 105), 4.9f, 5.1f);
            Assert.InRange(f.CellsAbove(150, 180), TerrainField.MaxDepth - 0.1f, TerrainField.MaxDepth);
        }

        [Fact]
        public void CarvingUpdatesTheSameAsRebuilding()
        {
            var t = Ground();
            var f = new TerrainField(t);
            var dirty = t.CarveCircle(150, 110, 20);
            f.Update(dirty);
            var fresh = new TerrainField(t);
            Assert.Equal(fresh.Data, f.Data);
            // The crater rim is an edge now.
            Assert.InRange(f.EdgeCells(150, 131), 0.9f, 1.2f);
        }

        [Fact]
        public void BuriedCavityFloorDoesNotBecomeGrass()
        {
            var t = Ground();
            var f = new TerrainField(t);
            var dirty = t.CarveCircle(150, 130, 12);
            f.Update(dirty);
            Assert.InRange(f.CellsAbove(150, 100), 0f, 0.1f);
            Assert.InRange(f.CellsAbove(150, 143), TerrainField.MaxDepth - 0.1f, TerrainField.MaxDepth);
        }
    }
}
