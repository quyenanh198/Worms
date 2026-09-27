using System.Linq;
using System.Threading.Tasks;
using Worms.Game.Core;
using Worms.Protocol;
using Xunit;

namespace Worms.Server.Tests
{
    public class WormNameTests : IClassFixture<ServerFactory>
    {
        readonly ServerFactory _factory;

        public WormNameTests(ServerFactory factory)
        {
            _factory = factory;
        }

        async Task<TestClient> Player(int id)
        {
            var c = await TestClient.ConnectAsync(_factory, $"?name=N{id}&guest={id}");
            Assert.True(await c.WaitFor(s => s.HasHello && s.Profile != null));
            return c;
        }

        [Fact]
        public async Task EveryPlayerStartsWithFourDistinctNames()
        {
            await using var p = await Player(801);
            var names = p.Read(s => s.Profile.WormNames.ToList());
            Assert.Equal(WormNames.PerTeam, names.Count);
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.All(names, n => Assert.Contains(n, WormNames.Pool));
        }

        [Fact]
        public async Task NamesAreCleanedAndBlanksKeepTheirDefault()
        {
            await using var p = await Player(811);
            var defaults = p.Read(s => s.Profile.WormNames.ToList());
            p.Do(s => s.SetWormNames(new[] { "  Tèo   Em  ", "<b>Hack</b>", "", "Một cái tên dài quá mức cho phép" }));
            Assert.True(await p.WaitFor(s => s.Profile.WormNames[0] == "Tèo Em"));
            var names = p.Read(s => s.Profile.WormNames.ToList());
            Assert.Equal("bHack/b", names[1]);
            Assert.Equal(defaults[2], names[2]);
            Assert.True(names[3].Length <= WormNames.MaxLength);
        }

        [Fact]
        public async Task EveryoneSeesEachWormsName()
        {
            await using var host = await Player(821);
            host.Do(s => s.SetWormNames(new[] { "Một", "Hai", "Ba", "Bốn" }));
            Assert.True(await host.WaitFor(s => s.Profile.WormNames[3] == "Bốn"));
            host.Do(s => s.CreateRoom());
            Assert.True(await host.WaitFor(s => s.InRoom));
            host.Do(s => s.AddBot());
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 2));
            host.Do(s => s.StartMatch());
            Assert.True(await host.WaitFor(s => s.Match != null));
            Assert.Equal("Ba", host.Read(s => s.Match.WormName(2)));
            int perTeam = host.Read(s => s.Match.WormsPerTeam);
            var botName = host.Read(s => s.Match.WormName(perTeam));
            Assert.Contains(botName, WormNames.Pool);
        }
    }
}
