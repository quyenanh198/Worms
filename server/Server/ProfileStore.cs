using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Worms.Protocol;

namespace Worms.Server
{
    /// <summary>
    /// Gold, owned cosmetics and loadouts per Chat account, in one SQLite file (WORMS_DB;
    /// in memory when unset, for development and tests). The server is the only writer and
    /// traffic is tiny, so one connection behind a lock is enough. Every purchase and every
    /// reward is a single transaction: gold can never go negative or be spent twice.
    /// </summary>
    public sealed class ProfileStore : IDisposable
    {
        readonly SqliteConnection _db;
        readonly object _lock = new object();

        public ProfileStore(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            _db = new SqliteConnection(string.IsNullOrEmpty(path) ? "Data Source=:memory:" : "Data Source=" + path);
            _db.Open();
            Exec("PRAGMA journal_mode=WAL;");
            Exec(@"CREATE TABLE IF NOT EXISTS players(
                     user_id INTEGER PRIMARY KEY,
                     gold INTEGER NOT NULL,
                     hat INTEGER NOT NULL DEFAULT 0, armor INTEGER NOT NULL DEFAULT 0,
                     bazooka INTEGER NOT NULL DEFAULT 0, grenade INTEGER NOT NULL DEFAULT 0, bat INTEGER NOT NULL DEFAULT 0,
                     games INTEGER NOT NULL DEFAULT 0, wins INTEGER NOT NULL DEFAULT 0,
                     created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
                   CREATE TABLE IF NOT EXISTS owned(
                     user_id INTEGER NOT NULL, item INTEGER NOT NULL, bought_at TEXT NOT NULL,
                     PRIMARY KEY(user_id, item));");
        }

        public void Dispose() { lock (_lock) _db.Dispose(); }

        static string Now() { return DateTime.UtcNow.ToString("o"); }

        void Exec(string sql, SqliteTransaction tx = null, params (string, object)[] args)
        {
            using var cmd = Command(sql, tx, args);
            cmd.ExecuteNonQuery();
        }

        SqliteCommand Command(string sql, SqliteTransaction tx, (string, object)[] args)
        {
            var cmd = _db.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
            return cmd;
        }

        void Ensure(int userId, SqliteTransaction tx)
        {
            Exec("INSERT OR IGNORE INTO players(user_id, gold, created_at, updated_at) VALUES($u, $g, $t, $t)", tx,
                ("$u", userId), ("$g", Cosmetics.StartingGold), ("$t", Now()));
        }

        ProfileMsg Read(int userId, SqliteTransaction tx)
        {
            Ensure(userId, tx);
            var p = new ProfileMsg();
            using (var cmd = Command("SELECT gold, hat, armor, bazooka, grenade, bat FROM players WHERE user_id = $u", tx, new[] { ("$u", (object)userId) }))
            using (var r = cmd.ExecuteReader())
            {
                r.Read();
                p.Gold = r.GetInt32(0);
                p.Loadout = new Loadout
                {
                    Hat = (byte)r.GetInt32(1), Armor = (byte)r.GetInt32(2), Bazooka = (byte)r.GetInt32(3),
                    Grenade = (byte)r.GetInt32(4), Bat = (byte)r.GetInt32(5),
                };
            }
            using (var cmd = Command("SELECT item FROM owned WHERE user_id = $u ORDER BY item", tx, new[] { ("$u", (object)userId) }))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) p.Owned.Add((byte)r.GetInt32(0));
            return p;
        }

        public ProfileMsg Get(int userId)
        {
            lock (_lock)
            {
                using var tx = _db.BeginTransaction();
                var p = Read(userId, tx);
                tx.Commit();
                return p;
            }
        }

        public Loadout LoadoutOf(int userId) { return Get(userId).Loadout; }

        /// <summary>Buys an item; returns an error code (and changes nothing) if it cannot.</summary>
        public string Buy(int userId, byte itemId, out ProfileMsg profile)
        {
            lock (_lock)
            {
                using var tx = _db.BeginTransaction();
                profile = Read(userId, tx);
                var item = Cosmetics.Get(itemId);
                if (item == null) return ErrorCodes.BadMessage;
                if (profile.Owns(itemId)) return null; // already owned: nothing to do
                if (profile.Gold < item.Price) return ErrorCodes.NotEnoughGold;
                Exec("UPDATE players SET gold = gold - $p, updated_at = $t WHERE user_id = $u", tx, ("$p", item.Price), ("$t", Now()), ("$u", userId));
                Exec("INSERT INTO owned(user_id, item, bought_at) VALUES($u, $i, $t)", tx, ("$u", userId), ("$i", (int)itemId), ("$t", Now()));
                profile = Read(userId, tx);
                tx.Commit();
                return null;
            }
        }

        static readonly string[] SlotColumns = { "hat", "armor", "bazooka", "grenade", "bat" };

        /// <summary>Wears an owned item (or takes a slot off with 0).</summary>
        public string Equip(int userId, CosmeticSlot slot, byte itemId, out ProfileMsg profile)
        {
            lock (_lock)
            {
                using var tx = _db.BeginTransaction();
                profile = Read(userId, tx);
                if ((int)slot >= SlotColumns.Length) return ErrorCodes.BadMessage;
                if (itemId != 0)
                {
                    var item = Cosmetics.Get(itemId);
                    if (item == null || item.Slot != slot) return ErrorCodes.BadMessage;
                    if (!profile.Owns(itemId)) return ErrorCodes.NotOwned;
                }
                Exec($"UPDATE players SET {SlotColumns[(int)slot]} = $i, updated_at = $t WHERE user_id = $u", tx,
                    ("$i", (int)itemId), ("$t", Now()), ("$u", userId));
                profile = Read(userId, tx);
                tx.Commit();
                return null;
            }
        }

        /// <summary>Pays out a finished match.</summary>
        public ProfileMsg AddReward(int userId, int gold, bool won)
        {
            lock (_lock)
            {
                using var tx = _db.BeginTransaction();
                Ensure(userId, tx);
                Exec("UPDATE players SET gold = gold + $g, games = games + 1, wins = wins + $w, updated_at = $t WHERE user_id = $u", tx,
                    ("$g", Math.Max(0, gold)), ("$w", won ? 1 : 0), ("$t", Now()), ("$u", userId));
                var p = Read(userId, tx);
                tx.Commit();
                return p;
            }
        }
    }
}
