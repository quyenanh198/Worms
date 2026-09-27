using System.Linq;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.Render
{
    /// <summary>Loadouts for worms nobody dressed: offline opponents and the menu squad.</summary>
    public static class CosmeticLooks
    {
        /// <summary>A random outfit (some slots left bare), the same for the same seed.</summary>
        public static Loadout Random(uint seed)
        {
            var rng = new Rng(seed ^ 0xC0575EEDu);
            var l = new Loadout();
            for (int slot = 0; slot < Cosmetics.SlotCount; slot++)
            {
                if (rng.NextFloat() < 0.4f) continue;
                var items = Cosmetics.InSlot((CosmeticSlot)slot).ToList();
                if (items.Count > 0) l[(CosmeticSlot)slot] = items[rng.Range(0, items.Count)].Id;
            }
            return l;
        }
    }
}
