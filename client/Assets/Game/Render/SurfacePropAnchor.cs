using UnityEngine;

namespace Worms.Game.Render
{
    /// <summary>Keeps a non-colliding scenery cutout attached to destructible ground.</summary>
    public sealed class SurfacePropAnchor : MonoBehaviour
    {
        public int CellX;
        public int OriginalTop;
        public int MaxDropCells;

        public void Refresh(Worms.Sim.Terrain terrain)
        {
            int top = 0;
            while (top < terrain.Height && !terrain.IsSolid(CellX, top)) top++;
            if (top >= terrain.Height - 8 || top - OriginalTop > MaxDropCells)
            {
                gameObject.SetActive(false);
                return;
            }
            transform.localPosition = new Vector3(0, (OriginalTop - top) * WorldSpace.Scale, 0);
        }
    }
}
