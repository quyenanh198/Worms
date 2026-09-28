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
            new CosmeticItem { Id = 17, Slot = CosmeticSlot.Hat, Name = "Kính đen", Price = 180, Blurb = "Nhìn thật ngầu" },
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

    /// <summary>Server -> Client: the player's gold, what they own, what they wear and their worms' names.</summary>
    public sealed class ProfileMsg
    {
        public int Gold;
        public readonly List<byte> Owned = new List<byte>();
        public Loadout Loadout;
        public readonly List<string> WormNames = new List<string>();

        public bool Owns(byte id) { return Owned.Contains(id); }

        public byte[] Encode()
        {
            var w = new MsgWriter(MsgType.Profile).I32(Gold).U8((byte)Owned.Count);
            foreach (var id in Owned) w.U8(id);
            Loadout.WriteTo(w);
            WormNamesCodec.Write(w, WormNames);
            return w.ToArray();
        }

        public static ProfileMsg Decode(MsgReader r)
        {
            var m = new ProfileMsg { Gold = r.I32() };
            int n = r.U8();
            for (int i = 0; i < n; i++) m.Owned.Add(r.U8());
            m.Loadout = Loadout.Read(r);
            m.WormNames.AddRange(WormNamesCodec.Read(r));
            return m;
        }
    }

    /// <summary>
    /// Every worm has its own name. Players name their squad of <see cref="PerTeam"/> (kept
    /// with their profile); until they do, and for computer players, names come from a pool.
    /// </summary>
    public static class WormNames
    {
        public const int PerTeam = 4;
        public const int MaxLength = 12;

        public static readonly string[] Pool =
        {
            "Tèo", "Tí", "Bin", "Bo", "Sún", "Mập", "Còi", "Tũn", "Xoài", "Bơ", "Đậu", "Nấm",
            "Cốm", "Kem", "Mít", "Bắp", "Khoai", "Tôm", "Ốc", "Chuối", "Mèo", "Gấu", "Sóc", "Bống",
            "Rambo", "Ninja", "Pháo", "Sấm", "Bão", "Lửa", "Đen", "Vàng",
        };

        /// <summary>Four distinct names picked from the pool, the same for the same seed.</summary>
        public static List<string> Pick(uint seed)
        {
            var rng = new Worms.Sim.Rng(seed ^ 0x5A0E5u);
            var names = new List<string>();
            while (names.Count < PerTeam)
            {
                var n = Pool[rng.Range(0, Pool.Length)];
                if (!names.Contains(n)) names.Add(n);
            }
            return names;
        }

        /// <summary>A safe name: no control characters, trimmed, at most <see cref="MaxLength"/> characters; empty stays empty.</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var chars = new System.Text.StringBuilder();
            foreach (char c in name)
                if (!char.IsControl(c) && c != '\u2028' && c != '\u2029') chars.Append(c);
            var s = System.Text.RegularExpressions.Regex.Replace(chars.ToString(), "\\s+", " ").Trim();
            // Rich-text markup is not a name.
            s = s.Replace("<", "").Replace(">", "");
            return s.Length > MaxLength ? s.Substring(0, MaxLength).TrimEnd() : s;
        }

        /// <summary>Exactly <see cref="PerTeam"/> clean names; blanks take the default for that slot.</summary>
        public static List<string> Complete(IReadOnlyList<string> names, IReadOnlyList<string> defaults)
        {
            var result = new List<string>();
            for (int i = 0; i < PerTeam; i++)
            {
                string n = names != null && i < names.Count ? Clean(names[i]) : string.Empty;
                result.Add(n.Length > 0 ? n : defaults[i]);
            }
            return result;
        }
    }

    public static class WormNamesCodec
    {
        public static void Write(MsgWriter w, IReadOnlyList<string> names)
        {
            int n = names == null ? 0 : System.Math.Min(names.Count, 8);
            w.U8((byte)n);
            for (int i = 0; i < n; i++) w.Str(names[i]);
        }

        public static List<string> Read(MsgReader r)
        {
            int n = r.U8();
            if (n > 8) throw new ProtocolException("too many worm names");
            var names = new List<string>();
            for (int i = 0; i < n; i++) names.Add(r.Str());
            return names;
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
