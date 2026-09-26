using System.Collections.Generic;

namespace Worms.Protocol
{
    /// <summary>Where a cosmetic goes on the worm. Weapon slots re-skin that weapon.</summary>
    public enum CosmeticSlot : byte
    {
        Hat = 0,
        Armor = 1,
        Bazooka = 2,
        Grenade = 3,
        Bat = 4,
    }

    /// <summary>An item in the store. Purely cosmetic: it never changes how a worm plays.</summary>
    public sealed class CosmeticItem
    {
        /// <summary>Wire id; 0 means "nothing". Never reuse or renumber: players own items by id.</summary>
        public byte Id;
        public CosmeticSlot Slot;
        public string Name;
        public int Price;
        /// <summary>Short line for the store card.</summary>
        public string Blurb;
    }

    /// <summary>The store catalog and the gold rules, shared by the server (which enforces them) and the client (which shows them).</summary>
    public static class Cosmetics
    {
        public const int SlotCount = 5;
        public const int StartingGold = 150;

        public static readonly CosmeticItem[] All =
        {
            new CosmeticItem { Id = 1, Slot = CosmeticSlot.Hat, Name = "Nón lá", Price = 120, Blurb = "Che nắng, che mưa, che cả bazooka" },
            new CosmeticItem { Id = 2, Slot = CosmeticSlot.Hat, Name = "Mũ cối", Price = 150, Blurb = "Kiên cường như đất thép" },
            new CosmeticItem { Id = 3, Slot = CosmeticSlot.Hat, Name = "Băng đô Rambo", Price = 150, Blurb = "Một mình cân cả đội" },
            new CosmeticItem { Id = 4, Slot = CosmeticSlot.Hat, Name = "Mũ bảo hiểm", Price = 180, Blurb = "An toàn là trên hết" },
            new CosmeticItem { Id = 5, Slot = CosmeticSlot.Hat, Name = "Mũ cao bồi", Price = 220, Blurb = "Rút súng nhanh nhất miền Tây" },
            new CosmeticItem { Id = 6, Slot = CosmeticSlot.Hat, Name = "Mũ phù thủy", Price = 320, Blurb = "Lựu đạn biết bay theo ý muốn" },
            new CosmeticItem { Id = 7, Slot = CosmeticSlot.Hat, Name = "Vương miện", Price = 650, Blurb = "Vua của những con sâu" },
            new CosmeticItem { Id = 8, Slot = CosmeticSlot.Armor, Name = "Áo phao", Price = 150, Blurb = "Rơi xuống nước vẫn đẹp" },
            new CosmeticItem { Id = 9, Slot = CosmeticSlot.Armor, Name = "Đai ninja", Price = 250, Blurb = "Im lặng và nguy hiểm" },
            new CosmeticItem { Id = 10, Slot = CosmeticSlot.Armor, Name = "Áo choàng anh hùng", Price = 380, Blurb = "Bay trong gió, rất ngầu" },
            new CosmeticItem { Id = 11, Slot = CosmeticSlot.Armor, Name = "Giáp hiệp sĩ", Price = 450, Blurb = "Sáng loáng từ đầu đến đuôi" },
            new CosmeticItem { Id = 12, Slot = CosmeticSlot.Bazooka, Name = "Bazooka rằn ri", Price = 250, Blurb = "Hòa vào cỏ cây" },
            new CosmeticItem { Id = 13, Slot = CosmeticSlot.Bazooka, Name = "Bazooka vàng", Price = 550, Blurb = "Bắn trượt cũng sang" },
            new CosmeticItem { Id = 14, Slot = CosmeticSlot.Grenade, Name = "Lựu đạn dưa hấu", Price = 200, Blurb = "Mát lạnh mùa hè" },
            new CosmeticItem { Id = 15, Slot = CosmeticSlot.Grenade, Name = "Lựu đạn vàng", Price = 420, Blurb = "Nổ ra toàn tiền" },
            new CosmeticItem { Id = 16, Slot = CosmeticSlot.Bat, Name = "Gậy gai", Price = 300, Blurb = "Đập đâu đau đó" },
        };

        static readonly Dictionary<byte, CosmeticItem> ById = new Dictionary<byte, CosmeticItem>();

        static Cosmetics()
        {
            foreach (var item in All) ById[item.Id] = item;
        }

        public static CosmeticItem Get(byte id)
        {
            return ById.TryGetValue(id, out var item) ? item : null;
        }

        public static IEnumerable<CosmeticItem> InSlot(CosmeticSlot slot)
        {
            foreach (var item in All)
                if (item.Slot == slot) yield return item;
        }

        // ---- gold for a finished match -----------------------------------------------------

        public const int GoldForPlaying = 20;
        public const int GoldForWinning = 60;
        public const int GoldPerKill = 15;
        /// <summary>One gold per this much damage dealt to enemies.</summary>
        public const int DamagePerGold = 2;

        /// <summary>
        /// Gold a player earns for a match they finished. Beating only computer players pays half,
        /// so farming bots is slower than playing people.
        /// </summary>
        public static int Reward(bool won, int damageDealt, int kills, bool onlyBots)
        {
            int gold = GoldForPlaying + (won ? GoldForWinning : 0) + damageDealt / DamagePerGold + kills * GoldPerKill;
            return onlyBots ? gold / 2 : gold;
        }
    }

    /// <summary>What a team wears: one item id per slot (0 = default look).</summary>
    public struct Loadout
    {
        public byte Hat, Armor, Bazooka, Grenade, Bat;

        public byte this[CosmeticSlot slot]
        {
            get
            {
                switch (slot)
                {
                    case CosmeticSlot.Hat: return Hat;
                    case CosmeticSlot.Armor: return Armor;
                    case CosmeticSlot.Bazooka: return Bazooka;
                    case CosmeticSlot.Grenade: return Grenade;
                    default: return Bat;
                }
            }
            set
            {
                switch (slot)
                {
                    case CosmeticSlot.Hat: Hat = value; break;
                    case CosmeticSlot.Armor: Armor = value; break;
                    case CosmeticSlot.Bazooka: Bazooka = value; break;
                    case CosmeticSlot.Grenade: Grenade = value; break;
                    default: Bat = value; break;
                }
            }
        }

        public void WriteTo(MsgWriter w) { w.U8(Hat).U8(Armor).U8(Bazooka).U8(Grenade).U8(Bat); }

        public static Loadout Read(MsgReader r)
        {
            return new Loadout { Hat = r.U8(), Armor = r.U8(), Bazooka = r.U8(), Grenade = r.U8(), Bat = r.U8() };
        }
    }

    /// <summary>Server -> Client: the player's gold, what they own and what they wear.</summary>
    public sealed class ProfileMsg
    {
        public int Gold;
        public readonly List<byte> Owned = new List<byte>();
        public Loadout Loadout;

        public bool Owns(byte id) { return Owned.Contains(id); }

        public byte[] Encode()
        {
            var w = new MsgWriter(MsgType.Profile).I32(Gold).U8((byte)Owned.Count);
            foreach (var id in Owned) w.U8(id);
            Loadout.WriteTo(w);
            return w.ToArray();
        }

        public static ProfileMsg Decode(MsgReader r)
        {
            var m = new ProfileMsg { Gold = r.I32() };
            int n = r.U8();
            for (int i = 0; i < n; i++) m.Owned.Add(r.U8());
            m.Loadout = Loadout.Read(r);
            return m;
        }
    }

    /// <summary>Server -> Client after a match: the gold this player earned and why.</summary>
    public struct RewardMsg
    {
        public int Gold, Damage, Kills;
        public bool Won, OnlyBots;

        public byte[] Encode()
        {
            return new MsgWriter(MsgType.Reward).I32(Gold).I32(Damage).U8((byte)Kills).Bool(Won).Bool(OnlyBots).ToArray();
        }

        public static RewardMsg Decode(MsgReader r)
        {
            return new RewardMsg { Gold = r.I32(), Damage = r.I32(), Kills = r.U8(), Won = r.Bool(), OnlyBots = r.Bool() };
        }
    }
}
