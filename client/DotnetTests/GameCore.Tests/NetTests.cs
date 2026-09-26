using System.Collections.Generic;
using Worms.Game.Core;
using Worms.Protocol;
using Worms.Sim;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class NetTests
    {
        static Snapshot Snap(uint tick, float x)
        {
            var s = new Snapshot { Tick = tick };
            s.Worms.Add(new WormSnap { Id = 0, X = x });
            return s;
        }

        [Fact]
        public void BufferTrailsLatestByDelayAndInterpolates()
        {
            var b = new SnapshotBuffer();
            b.Add(Snap(100, 0));
            b.Add(Snap(103, 30));
            b.Add(Snap(106, 60));
            b.Add(Snap(109, 90));
            for (int i = 0; i < 300; i++) b.Advance(0); // no time passes: clock sits where it started
            b.Sample(out var from, out var to, out float alpha);
            Assert.True(from.Tick <= b.RenderTick && b.RenderTick <= to.Tick);

            // Keep feeding snapshots at 20 Hz for a while; the clock settles 6 ticks behind.
            uint tick = 109;
            for (int i = 0; i < 200; i++)
            {
                tick += 3;
                b.Add(Snap(tick, tick * 10));
                for (int f = 0; f < 3; f++) b.Advance(1f / 60);
            }
            Assert.InRange(b.RenderTick, tick - SnapshotBuffer.DelayTicks - 1.5, tick - SnapshotBuffer.DelayTicks + 1.5);
            b.Sample(out from, out to, out alpha);
            Assert.InRange(alpha, 0f, 1f);
            float x = from.Worms[0].X + (to.Worms[0].X - from.Worms[0].X) * alpha;
            Assert.Equal((float)(b.RenderTick * 10), x, 1);
        }

        [Fact]
        public void BufferJumpsAfterLongStall()
        {
            var b = new SnapshotBuffer();
            b.Add(Snap(10, 0));
            b.Add(Snap(500, 0));
            b.Advance(1f / 60);
            Assert.InRange(b.RenderTick, 490, 500);
        }

        [Fact]
        public void ExplosionsAreCarvedWhenTheyBecomeDue()
        {
            var w = new World(new MatchSetup { Seed = 8, Teams = 2, WormsPerTeam = 1 });
            w.Step(null);
            var start = MatchStartMsg.Decode(new MsgReader(MatchStartMsg.FromWorld(w, new[] { "A", "B" }, 0).Encode()));
            var m = new ClientMatch(start);
            int t0 = w.Tick;
            m.AddEvents(new List<SimEvent>
            {
                new SimEvent { Tick = t0 - 1, Type = SimEventType.Explode, X = 300, Y = 500, Value = 40 }, // old: ignored
                new SimEvent { Tick = t0 + 20, Type = SimEventType.Explode, X = 1000, Y = 500, Value = 40 },
            });
            var due = new List<SimEvent>();
            var dirty = new List<CellRect>();
            m.Buffer.Add(new Snapshot { Tick = (uint)(t0 + 3) });
            m.Advance(1f / 60, due, dirty);
            Assert.Empty(due);
            m.Buffer.Add(new Snapshot { Tick = (uint)(t0 + 40) });
            for (int i = 0; i < 60; i++) m.Advance(1f / 60, due, dirty);
            var e = Assert.Single(due);
            Assert.Equal(1000, e.X);
            Assert.Single(dirty);
            Assert.False(m.Terrain.IsSolid(1000, 500));
            Assert.Equal(0, m.PendingEvents);
        }

        [Fact]
        public void SessionTracksLobbyAndMatch()
        {
            var s = new NetSession();
            var sent = new List<byte[]>();
            s.Send = sent.Add;
            s.Receive(new HelloMsg { Server = "worms", UserId = 5, DisplayName = "Ánh" }.Encode());
            Assert.True(s.HasHello);
            Assert.Equal(5, s.UserId);

            var lobby = new LobbyMsg { Code = "WXYZ", HostUserId = 5, State = RoomState.Lobby };
            lobby.Players.Add(new PlayerInfo { UserId = 5, Name = "Ánh", Connected = true });
            s.Receive(lobby.Encode());
            Assert.True(s.InRoom);
            Assert.True(s.IsHost);

            var w = new World(new MatchSetup { Seed = 2, Teams = 2, WormsPerTeam = 1 });
            s.Receive(MatchStartMsg.FromWorld(w, new[] { "Ánh", "Bố" }, 0).Encode());
            Assert.NotNull(s.Match);
            Assert.Equal(0, s.Match.YourTeam);

            s.Receive(new ErrorMsg { Code = ErrorCodes.RoomFull }.Encode());
            Assert.Equal(ErrorCodes.RoomFull, s.LastError);

            s.JoinRoom("abcd");
            var r = new MsgReader(sent[0]);
            Assert.Equal(MsgType.JoinRoom, r.Type);
            Assert.Equal("abcd", r.Str());

            s.Receive(new LobbyMsg().Encode());
            Assert.False(s.InRoom);
            Assert.Null(s.Match);
        }
    }
}

namespace Worms.Game.Core.Tests
{
    public class ServerUrlTests
    {
        [Fact]
        public void ChatBaseAndInviteFollowTheSocketHost()
        {
            Assert.Equal("https://chat.lazybutts.com", ServerUrl.ChatBase("wss://chat.lazybutts.com/worms/ws"));
            Assert.Equal("http://localhost:8080", ServerUrl.ChatBase("ws://localhost:8080/ws"));
            Assert.Equal("https://chat.lazybutts.com/worms/?room=ABCD", ServerUrl.InviteLink("wss://chat.lazybutts.com/worms/ws", "ABCD"));
        }

        [Fact]
        public void QueryParamIsRead()
        {
            Assert.Equal("ABCD", ServerUrl.QueryParam("https://x/worms/?a=1&room=ABCD#top", "room"));
            Assert.Null(ServerUrl.QueryParam("https://x/worms/", "room"));
        }

        [Fact]
        public void SessionCookieIsExtracted()
        {
            Assert.Equal("lb_session=abc.def", ServerUrl.SessionCookieFromSetCookie("lb_session=abc.def; Max-Age=2592000; Path=/; HttpOnly; Secure; SameSite=Lax"));
            Assert.Equal("lb_session=x1", ServerUrl.SessionCookieFromSetCookie("other=1; Path=/, lb_session=x1; Path=/"));
            Assert.Null(ServerUrl.SessionCookieFromSetCookie("other=1"));
            Assert.Null(ServerUrl.SessionCookieFromSetCookie("lb_session=; Max-Age=0"));
        }
    }
}
