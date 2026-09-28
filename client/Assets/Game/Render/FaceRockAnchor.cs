using UnityEngine;

namespace Worms.Game.Render
{
    /// <summary>Hides painted rocks when their destructible dirt face is removed.</summary>
    public sealed class FaceRockAnchor : MonoBehaviour
    {
        public int CellX;
        public int CellY;
        public int HalfWidthCells;
        public int HalfHeightCells;

        public void Refresh(Worms.Sim.Terrain terrain)
        {
            if (!terrain.IsSolid(CellX, CellY) ||
                !terrain.IsSolid(CellX - HalfWidthCells, CellY) ||
                !terrain.IsSolid(CellX + HalfWidthCells, CellY) ||
                !terrain.IsSolid(CellX, CellY - HalfHeightCells) ||
                !terrain.IsSolid(CellX, CellY + HalfHeightCells))
                gameObject.SetActive(false);
        }
    }
}
