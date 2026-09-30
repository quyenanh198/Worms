using UnityEngine;

namespace Worms.Game.Render
{
    /// <summary>Keeps a non-colliding scenery cutout attached to destructible ground.</summary>
    public sealed class SurfacePropAnchor : MonoBehaviour
    {
        public int CellX;
        public int OriginalTop;
        public int MaxDropCells;
        public int HalfWidthCells;
        public bool RequireContinuousGround;

        public void Refresh(Worms.Sim.Terrain terrain)
        {
            int top = SurfaceTop(terrain, CellX);
            if (top >= terrain.Height - 8 || top - OriginalTop > MaxDropCells)
            {
                gameObject.SetActive(false);
                return;
            }
            if (HalfWidthCells > 0 &&
                (Mathf.Abs(SurfaceTop(terrain, CellX - HalfWidthCells) - top) > 8 ||
                 Mathf.Abs(SurfaceTop(terrain, CellX + HalfWidthCells) - top) > 8))
            {
                gameObject.SetActive(false);
                return;
            }
            if (RequireContinuousGround)
                for (int dx = -HalfWidthCells; dx <= HalfWidthCells; dx += 8)
                    if (Mathf.Abs(SurfaceTop(terrain, CellX + dx) - top) > 12)
                    {
                        gameObject.SetActive(false);
                        return;
                    }
            transform.localPosition = new Vector3(0, (OriginalTop - top) * WorldSpace.Scale, 0);
        }

        static int SurfaceTop(Worms.Sim.Terrain terrain, int x)
        {
            int top = 0;
            while (top < terrain.Height && !terrain.IsSolid(x, top)) top++;
            return top;
        }
    }
}
