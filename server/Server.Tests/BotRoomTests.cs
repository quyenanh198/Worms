using System.Linq;
using System.Threading.Tasks;
using Worms.Game.Core;
using Worms.Protocol;
using Worms.Sim;
using Xunit;

namespace Worms.Server.Tests
{
    public class BotRoomTests : IClassFixture<ServerFactory>
    {
        readonly ServerFactory _factory;

        public BotRoomTests(ServerFactory factory)
        {
            _factory = factory;
        }

        async Task<TestClient> Host(int id)
        {
            var host = await TestClient.ConnectAsync(_factory, $"?name=Host{id}&guest={id}");
            Assert.True(await host.WaitFor(s => s.HasHello));
            host.Do(s => s.CreateRoom());
            Assert.True(await host.WaitFor(s => s.InRoom && s.IsHost));
            return host;
        }

        [Fact]
        public async Task HostCanPlayAloneAgainstABot()
        {
            await using var host = await Host(501);
            host.Do(s => s.AddBot());
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 2));
            var bot = host.Read(s => s.Lobby.Players[1]);
            Assert.True(bot.IsBot);
            Assert.True(bot.Ready);
            Assert.True(bot.Connected);
            Assert.True(bot.UserId < 0);
            Assert.Equal("Máy 1", bot.Name);

            host.Do(s => s.StartMatch());
            Assert.True(await host.WaitFor(s => s.Match != null));
            Assert.Equal(new[] { "Host501", "Máy 1" }, host.Read(s => s.Match.TeamNames.ToArray()));

            // The host goes first and fires into the ground at its feet; then it is the bot's turn.
            Assert.True(await host.WaitFor(s => s.Match.Buffer.Latest.Phase == Phase.Aiming && s.Match.Buffer.Latest.ActiveTeam == 0));
            host.Do(s => s.SendIntent(new Intent { Kind = InputKind.Fire, Angle = -1.5f, Power = 0.3f }));
            Assert.True(await host.WaitFor(s => s.Match.Buffer.Latest.ActiveTeam == 1, 20000));

            // The bot takes its turn by itself: a shot from one of team 1's worms.
            int wormsPerTeam = host.Read(s => s.Match.WormsPerTeam);
            Assert.True(await host.WaitFor(s => host.DueEvents.Any(e => e.Type == SimEventType.Fire && e.Worm >= wormsPerTeam), 20000),
                "the bot never fired");
            // ...and the turn comes back to the person.
            Assert.True(await host.WaitFor(s => s.Match.Buffer.Latest.Phase == Phase.Aiming && s.Match.Buffer.Latest.ActiveTeam == 0, 30000));
        }

        [Fact]
        public async Task OnlyTheHostAddsBotsAndRoomsCapAtFour()
        {
            await using var host = await Host(511);
            await using var guest = await TestClient.ConnectAsync(_factory, "?name=Guest&guest=512");
            Assert.True(await guest.WaitFor(s => s.HasHello));
            guest.Do(s => s.JoinRoom(host.Read(x => x.Lobby.Code)));
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 2));

            guest.Do(s => s.AddBot());
            Assert.True(await guest.WaitFor(s => s.LastError == ErrorCodes.NotHost));

            host.Do(s => s.AddBot());
            host.Do(s => s.AddBot());
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 4));
            Assert.Equal(new[] { "Máy 1", "Máy 2" }, host.Read(s => s.Lobby.Players.Where(p => p.IsBot).Select(p => p.Name).ToArray()));
            host.Do(s => s.AddBot());
            Assert.True(await host.WaitFor(s => s.LastError == ErrorCodes.RoomFull));
        }

        [Fact]
        public async Task RemovingABotFreesItsSeatAndItsName()
        {
            await using var host = await Host(521);
            host.Do(s => s.AddBot());
            host.Do(s => s.AddBot());
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 3));
            int first = host.Read(s => s.Lobby.Players.First(p => p.Name == "Máy 1").UserId);
            host.Do(s => s.RemoveBot(first));
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 2));
            Assert.Equal(new[] { 0, 1 }, host.Read(s => s.Lobby.Players.Select(p => p.Team).ToArray()));

            host.Do(s => s.AddBot());
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Any(p => p.Name == "Máy 1")));
        }

        [Fact]
        public async Task LeavingAMatchAgainstABotReturnsToTheMenu()
        {
            await using var host = await Host(531);
            host.Do(s => s.AddBot());
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 2));
            host.Do(s => s.StartMatch());
            Assert.True(await host.WaitFor(s => s.Match != null));

            // Leaving mid-match forfeits the person's team; the bot must not keep the player stuck in the room.
            host.Do(s => s.LeaveRoom());
            Assert.True(await host.WaitFor(s => !s.InRoom));
            host.Do(s => s.CreateRoom());
            Assert.True(await host.WaitFor(s => s.InRoom && s.IsHost && s.Lobby.Players.Count == 1));
        }

        [Fact]
        public async Task QuickMatchRoomsDoNotTakeBots()
        {
            await using var a = await TestClient.ConnectAsync(_factory, "?name=Q&guest=541");
            Assert.True(await a.WaitFor(s => s.HasHello));
            a.Do(s => s.QuickMatch());
            Assert.True(await a.WaitFor(s => s.InRoom));
            a.Do(s => s.AddBot());
            await Task.Delay(300);
            Assert.Equal(1, a.Read(s => s.Lobby.Players.Count));
        }
    }
}
