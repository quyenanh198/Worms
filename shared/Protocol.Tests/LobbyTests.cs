using System.Collections.Generic;
using Worms.Protocol;
using Worms.Sim;
using Xunit;

namespace Worms.Protocol.Tests
{
    public class LobbyTests
    {
        [Fact]
        public void LobbyRoundTrips()
        {
            var m = new LobbyMsg { Code = "ABCD", IsQuick = true, HostUserId = 7, State = RoomState.Playing };
            m.Players.Add(new PlayerInfo { UserId = 7, Name = "Ánh", Team = 0, Ready = true, Connected = true });
            m.Players.Add(new PlayerInfo { UserId = 9, Name = "Bố", Team = 1, Ready = false, Connected = false });
            m.Players.Add(new PlayerInfo { UserId = -1, Name = "Máy 1", Team = 2, Ready = true, Connected = true, IsBot = true });
            var r = new MsgReader(m.Encode());
            Assert.Equal(MsgType.Lobby, r.Type);
            var d = LobbyMsg.Decode(r);
            Assert.Equal("ABCD", d.Code);
            Assert.True(d.IsQuick);
            Assert.Equal(RoomState.Playing, d.State);
            Assert.Equal(m.Players, d.Players);
        }

        [Fact]
        public void MatchStartRebuildsSameTerrain()
        {
            var w = new World(new MatchSetup { Seed = 5, Teams = 2, WormsPerTeam = 2 });
            w.Step(null);
            Explosion.Detonate(w, new Vec2(500, 400), 50, 10);
            var m = MatchStartMsg.FromWorld(w, new[] { "A", "B" }, 1);
            var d = MatchStartMsg.Decode(new MsgReader(m.Encode()));
            Assert.Equal(5u, d.Seed);
            Assert.Equal(2, d.WormsPerTeam);
            Assert.Equal(1, d.YourTeam);
            Assert.Equal(new[] { "A", "B" }, d.TeamNames);
            var t = MapGenerator.Generate(d.Seed);
            foreach (var op in d.TerrainOps) t.CarveCircle(op.X, op.Y, op.R);
            Assert.Equal(w.Terrain.CopyCells(), t.CopyCells());
            Assert.Equal(w.Tick, (int)d.Snapshot.Tick);
        }

        [Fact]
        public void EventsRoundTrip()
        {
            var events = new List<SimEvent>
            {
                new SimEvent { Tick = 10, Type = SimEventType.Explode, X = 1.5f, Y = 2.5f, Value = 50 },
                new SimEvent { Tick = 11, Type = SimEventType.Death, Worm = 3, Cause = DeathCause.Water },
                new SimEvent { Tick = 12, Type = SimEventType.GameOver, Team = -1 },
            };
            var r = new MsgReader(EventsMsg.Encode(events));
            var d = new List<SimEvent>();
            EventsMsg.Decode(r, d);
            Assert.Equal(events, d);
        }

        [Fact]
        public void InputRoundTripsWithoutTeam()
        {
            var i = new SimInput { Team = 3, Kind = InputKind.Fire, Dir = -1, Angle = 0.7f, Power = 0.4f, Fuse = 2, Weapon = WeaponId.Grenade };
            var r = new MsgReader(ClientMsg.Input(i));
            var d = ClientMsg.ReadInput(r);
            Assert.Equal(0, d.Team);
            Assert.Equal(InputKind.Fire, d.Kind);
            Assert.Equal(0.7f, d.Angle);
            Assert.Equal(2, d.Fuse);
            Assert.Equal(WeaponId.Grenade, d.Weapon);
        }

        [Fact]
        public void RemoveBotCarriesTheBotId()
        {
            var r = new MsgReader(ClientMsg.RemoveBot(-2));
            Assert.Equal(MsgType.RemoveBot, r.Type);
            Assert.Equal(-2, r.I32());
        }
    }
}
