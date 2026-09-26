using System.IO;
using Microsoft.Data.Sqlite;
using Worms.Protocol;
using Xunit;

namespace Worms.Server.Tests
{
    public class ProfileStoreTests
    {
        [Fact]
        public void OpensADatabaseFromBeforeWormNamesAndKeepsItsData()
        {
            var path = Path.Combine(Path.GetTempPath(), "worms-old-" + System.Guid.NewGuid() + ".db");
            try
            {
                using (var db = new SqliteConnection("Data Source=" + path))
                {
                    db.Open();
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = @"CREATE TABLE players(user_id INTEGER PRIMARY KEY, gold INTEGER NOT NULL,
                        hat INTEGER NOT NULL DEFAULT 0, armor INTEGER NOT NULL DEFAULT 0, bazooka INTEGER NOT NULL DEFAULT 0,
                        grenade INTEGER NOT NULL DEFAULT 0, bat INTEGER NOT NULL DEFAULT 0, games INTEGER NOT NULL DEFAULT 0,
                        wins INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
                      INSERT INTO players(user_id, gold, hat, created_at, updated_at) VALUES(7, 999, 1, 'x', 'x');";
                    cmd.ExecuteNonQuery();
                }
                SqliteConnection.ClearAllPools();
                using var store = new ProfileStore(path);
                var p = store.Get(7);
                Assert.Equal(999, p.Gold);
                Assert.Equal(1, p.Loadout.Hat);
                Assert.Equal(ProfileStore.DefaultNames(7), p.WormNames);
                Assert.Equal("Bé", store.SetWormNames(7, new[] { "Bé", "", "", "" }).WormNames[0]);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                foreach (var f in new[] { path, path + "-wal", path + "-shm" }) if (File.Exists(f)) File.Delete(f);
            }
        }
    }
}
