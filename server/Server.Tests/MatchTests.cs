using System.Linq;
using System.Threading.Tasks;
using Worms.Game.Core;
using Worms.Protocol;
using Worms.Sim;
using Xunit;

namespace Worms.Server.Tests
{
    public class MatchTests : IClassFixture<ServerFactory>
    {
        readonly ServerFactory _factory;

        public MatchTests(ServerFactory factory)
        {
            _factory = factory;
        }

        async Task<(TestClient a, TestClient b)> QuickPair(int idA, int idB)
        {
            var a = await TestClient.ConnectAsync(_factory, $"?name=A{idA}&guest={idA}");
            var b = await TestClient.ConnectAsync(_factory, $"?name=B{idB}&guest={idB}");
            Assert.True(await a.WaitFor(s => s.HasHello));
            Assert.True(await b.WaitFor(s => s.HasHello));
            a.Do(s => s.QuickMatch());
            Assert.True(await a.WaitFor(s => s.InRoom));
            b.Do(s => s.QuickMatch());
            Assert.True(await a.WaitFor(s => s.Match != null));
            Assert.True(await b.WaitFor(s => s.Match != null));
            return (a, b);
        }

        [Fact]
        public async Task QuickMatchStartsAndOnlyTheActivePlayerCanFire()
        {
            var (a, b) = await QuickPair(101, 102);
            await using var _a = a;
            await using var _b = b;
            Assert.Equal(0, a.Read(s => s.Match.YourTeam));
            Assert.Equal(1, b.Read(s => s.Match.YourTeam));
            Assert.Equal(a.Read(s => s.Match.Seed), b.Read(s => s.Match.Seed));
            Assert.Equal(new[] { "A101", "B102" }, a.Read(s => s.Match.TeamNames.ToArray()));

            Assert.True(await a.WaitFor(s => s.Match.Buffer.Latest.Phase == Phase.Aiming));
            Assert.Equal(0, a.Read(s => s.Match.Buffer.Latest.ActiveTeam));

            // Team 1 tries to fire out of turn: nothing happens.
            b.Do(s => s.SendIntent(new Intent { Kind = InputKind.Fire, Angle = 0.8f, Power = 0.5f }));
            await Task.Delay(150);
            Assert.Equal(Phase.Aiming, a.Read(s => s.Match.Buffer.Latest.Phase));

            // Team 0 fires: both clients see the shot and the explosion.
            a.Do(s => s.SendIntent(new Intent { Kind = InputKind.Fire, Angle = 1.0f, Power = 0.7f }));
            Assert.True(await b.WaitFor(s => b.DueEvents.Any(e => e.Type == SimEventType.Fire)));
            Assert.True(await b.WaitFor(s => b.DueEvents.Any(e => e.Type == SimEventType.Explode), 15000));
            Assert.True(await a.WaitFor(s => s.Match.Buffer.Latest.Phase == Phase.Aiming && s.Match.Buffer.Latest.ActiveTeam == 1, 20000));
        }

        [Fact]
        public async Task PrivateRoomNeedsReadyPlayersAndHost()
        {
            await using var host = await TestClient.ConnectAsync(_factory, "?name=Host&guest=201");
            await using var guest = await TestClient.ConnectAsync(_factory, "?name=Guest&guest=202");
            await using var third = await TestClient.ConnectAsync(_factory, "?name=Third&guest=203");
            Assert.True(await host.WaitFor(s => s.HasHello));
            host.Do(s => s.CreateRoom());
            Assert.True(await host.WaitFor(s => s.InRoom && s.IsHost));
            string code = host.Read(s => s.Lobby.Code);

            guest.Do(s => s.JoinRoom("zzzz"));
            Assert.True(await guest.WaitFor(s => s.LastError == ErrorCodes.RoomNotFound));
            guest.Do(s => s.JoinRoom(code.ToLowerInvariant()));
            Assert.True(await host.WaitFor(s => s.Lobby.Players.Count == 2));

            guest.Do(s => s.StartMatch());
            Assert.True(await guest.WaitFor(s => s.LastError == ErrorCodes.NotHost));
            host.Do(s => s.StartMatch());
            Assert.True(await host.WaitFor(s => s.LastError == ErrorCodes.NotReady));

            guest.Do(s => s.SetReady(true));
            Assert.True(await host.WaitFor(s => s.Lobby.Players.All(p => p.UserId == s.UserId || p.Ready)));
            host.Do(s => s.StartMatch());
            Assert.True(await guest.WaitFor(s => s.Match != null));

            third.Do(s => s.JoinRoom(code));
            Assert.True(await third.WaitFor(s => s.LastError == ErrorCodes.RoomBusy));
        }

        [Fact]
        public async Task ReconnectResumesWithTheSameTerrain()
        {
            var (a, b) = await QuickPair(301, 302);
            await using var _b = b;
            Assert.True(await a.WaitFor(s => s.Match.Buffer.Latest.Phase == Phase.Aiming));
            a.Do(s => s.SendIntent(new Intent { Kind = InputKind.Fire, Angle = 1.0f, Power = 0.6f }));
            Assert.True(await b.WaitFor(s => b.DueEvents.Any(e => e.Type == SimEventType.Explode), 15000));
            await a.DisposeAsync();

            await using var again = await TestClient.ConnectAsync(_factory, "?name=A301&guest=301");
            Assert.True(await again.WaitFor(s => s.Match != null));
            Assert.Equal(0, again.Read(s => s.Match.YourTeam));
            // b has applied every explosion that happened so far by now.
            Assert.True(await b.WaitFor(s => s.Match.PendingEvents == 0));
            var fresh = again.Read(s => s.Match.Terrain.CopyCells());
            var live = b.Read(s => s.Match.Terrain.CopyCells());
            Assert.Equal(live, fresh);
            Assert.True(await b.WaitFor(s => s.Lobby.Players.All(p => p.Connected)));
        }

        [Fact]
        public async Task AbsentPlayersTurnIsSkipped()
        {
            var (a, b) = await QuickPair(401, 402);
            await using var _b = b;
            Assert.True(await b.WaitFor(s => s.Match.Buffer.Latest.Phase == Phase.Aiming && s.Match.Buffer.Latest.ActiveTeam == 0));
            await a.DisposeAsync();
            // Team 0's turn passes to team 1 without waiting 45 seconds.
            Assert.True(await b.WaitFor(s => s.Match.Buffer.Latest.ActiveTeam == 1 && s.Match.Buffer.Latest.Phase == Phase.Aiming, 8000));
            Assert.True(await b.WaitFor(s => s.Lobby.Players.Any(p => !p.Connected)));
        }

        [Fact]
        public async Task LeavingMidMatchForfeits()
        {
            var (a, b) = await QuickPair(501, 502);
            await using var _a = a;
            await using var _b = b;
            Assert.True(await a.WaitFor(s => s.Match.Buffer.Latest.Phase == Phase.Aiming));
            b.Do(s => s.LeaveRoom());
            Assert.True(await a.WaitFor(s => s.Match.Buffer.Latest.Phase == Phase.GameOver, 20000));
            Assert.Equal(0, a.Read(s => s.Match.Buffer.Latest.Winner));
            Assert.True(await a.WaitFor(s => s.Lobby.State == RoomState.Finished));
            Assert.False(b.Read(s => s.InRoom));
        }
    }
}
