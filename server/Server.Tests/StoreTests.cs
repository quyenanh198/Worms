using System.Linq;
using System.Threading.Tasks;
using Worms.Game.Core;
using Worms.Protocol;
using Xunit;

namespace Worms.Server.Tests
{
    public class StoreTests : IClassFixture<ServerFactory>
    {
        readonly ServerFactory _factory;

        public StoreTests(ServerFactory factory)
        {
            _factory = factory;
        }

        async Task<TestClient> Player(int id)
        {
            var c = await TestClient.ConnectAsync(_factory, $"?name=P{id}&guest={id}");
            Assert.True(await c.WaitFor(s => s.HasHello && s.Profile != null));
            return c;
        }

        [Fact]
        public async Task NewPlayersStartWithGoldAndNothingOwned()
        {
            await using var p = await Player(701);
            Assert.Equal(Cosmetics.StartingGold, p.Read(s => s.Profile.Gold));
            Assert.Empty(p.Read(s => s.Profile.Owned));
            Assert.Equal(default(Loadout), p.Read(s => s.Profile.Loadout));
        }

        [Fact]
        public async Task BuyingSpendsGoldOnceAndEquippingNeedsOwnership()
        {
            await using var p = await Player(711);
            var hat = Cosmetics.All.First(i => i.Slot == CosmeticSlot.Hat && i.Price <= Cosmetics.StartingGold);

            // Not owned yet: cannot wear it.
            p.Do(s => s.Equip(CosmeticSlot.Hat, hat.Id));
            Assert.True(await p.WaitFor(s => s.LastError == ErrorCodes.NotOwned));
            p.Do(s => s.ClearError());

            p.Do(s => s.Buy(hat.Id));
            Assert.True(await p.WaitFor(s => s.Profile.Owns(hat.Id)));
            Assert.Equal(Cosmetics.StartingGold - hat.Price, p.Read(s => s.Profile.Gold));

            // Buying again changes nothing.
            p.Do(s => s.Buy(hat.Id));
            await Task.Delay(200);
            Assert.Equal(Cosmetics.StartingGold - hat.Price, p.Read(s => s.Profile.Gold));

            // Too expensive now.
            var crown = Cosmetics.All.OrderByDescending(i => i.Price).First();
            p.Do(s => s.Buy(crown.Id));
            Assert.True(await p.WaitFor(s => s.LastError == ErrorCodes.NotEnoughGold));
            Assert.False(p.Read(s => s.Profile.Owns(crown.Id)));

            // A hat does not go in the armor slot.
            p.Do(s => s.ClearError());
            p.Do(s => s.Equip(CosmeticSlot.Armor, hat.Id));
            Assert.True(await p.WaitFor(s => s.LastError == ErrorCodes.BadMessage));

            p.Do(s => s.Equip(CosmeticSlot.Hat, hat.Id));
            Assert.True(await p.WaitFor(s => s.Profile.Loadout.Hat == hat.Id));
            p.Do(s => s.Equip(CosmeticSlot.Hat, 0));
            Assert.True(await p.WaitFor(s => s.Profile.Loadout.Hat == 0));
        }

        [Fact]
        public async Task EveryoneSeesWhatAPlayerWears()
        {
            await using var host = await Player(721);
            await using var guest = await Player(722);
            var hat = Cosmetics.All.First(i => i.Slot == CosmeticSlot.Hat && i.Price <= Cosmetics.StartingGold);
            host.Do(s => s.Buy(hat.Id));
            Assert.True(await host.WaitFor(s => s.Profile.Owns(hat.Id)));
            host.Do(s => s.Equip(CosmeticSlot.Hat, hat.Id));
            Assert.True(await host.WaitFor(s => s.Profile.Loadout.Hat == hat.Id));

            host.Do(s => s.CreateRoom());
            Assert.True(await host.WaitFor(s => s.InRoom));
            guest.Do(s => s.JoinRoom(host.Read(x => x.Lobby.Code)));
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 2));
            guest.Do(s => s.SetReady(true));
            Assert.True(await host.WaitFor(s => s.Lobby.Players.All(p => p.Ready || p.UserId == s.UserId)));
            host.Do(s => s.StartMatch());
            Assert.True(await guest.WaitFor(s => s.Match != null));
            Assert.Equal(hat.Id, guest.Read(s => s.Match.Loadouts[0].Hat));
            Assert.Equal(0, guest.Read(s => s.Match.Loadouts[1].Hat));
        }

        [Fact]
        public async Task TheWinnerOfARealMatchIsPaid()
        {
            await using var host = await Player(731);
            await using var guest = await Player(732);
            host.Do(s => s.CreateRoom());
            Assert.True(await host.WaitFor(s => s.InRoom));
            guest.Do(s => s.JoinRoom(host.Read(x => x.Lobby.Code)));
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 2));
            guest.Do(s => s.SetReady(true));
            Assert.True(await host.WaitFor(s => s.Lobby.Players.All(p => p.Ready || p.UserId == s.UserId)));
            host.Do(s => s.StartMatch());
            Assert.True(await host.WaitFor(s => s.Match != null));

            // Play long enough to count (the simulation runs fast in tests), then the guest walks out.
            Assert.True(await host.WaitFor(s => s.Match.Buffer.Latest.Tick > 31 * Worms.Sim.C.TicksPerSecond, 20000));
            guest.Do(s => s.LeaveRoom());

            Assert.True(await host.WaitFor(s => s.LastReward.HasValue, 15000), "no reward");
            var reward = host.Read(s => s.LastReward.Value);
            Assert.True(reward.Won);
            Assert.False(reward.OnlyBots);
            Assert.Equal(Cosmetics.Reward(true, reward.Damage, reward.Kills, false), reward.Gold);
            Assert.True(await host.WaitFor(s => s.Profile.Gold == Cosmetics.StartingGold + reward.Gold));
        }
    }
}
