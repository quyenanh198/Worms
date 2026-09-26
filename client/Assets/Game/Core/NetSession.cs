using System;
using System.Collections.Generic;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.Core
{
    /// <summary>A match as seen by a client: terrain rebuilt from seed + carves, snapshots and scheduled events.</summary>
    public sealed class ClientMatch
    {
        public readonly uint Seed;
        public readonly int YourTeam;
        public readonly IReadOnlyList<string> TeamNames;
        public readonly int WormsPerTeam;
        public readonly Terrain Terrain;
        public readonly float WaterLevel;
        public readonly SnapshotBuffer Buffer = new SnapshotBuffer();

        readonly List<SimEvent> _pending = new List<SimEvent>();
        readonly int _startTick;

        public int Teams => TeamNames.Count;
        public bool IsSpectator => YourTeam < 0;

        public ClientMatch(MatchStartMsg m)
        {
            Seed = m.Seed;
            YourTeam = m.YourTeam;
            TeamNames = m.TeamNames.ToArray();
            WormsPerTeam = m.WormsPerTeam;
            Terrain = MapGenerator.Generate(m.Seed);
            foreach (var op in m.TerrainOps) Terrain.CarveCircle(op.X, op.Y, op.R);
            WaterLevel = Terrain.Height - C.WaterDepth;
            _startTick = (int)m.Snapshot.Tick;
            Buffer.Add(m.Snapshot);
        }

        public void AddEvents(List<SimEvent> events)
        {
            foreach (var e in events)
            {
                if (e.Tick <= _startTick) continue; // already contained in the start state
                int i = _pending.Count;
                while (i > 0 && _pending[i - 1].Tick > e.Tick) i--;
                _pending.Insert(i, e);
            }
        }

        /// <summary>
        /// Advances the render clock and hands out events that are now due.
        /// Explosions are carved into <see cref="Terrain"/> at that moment, so
        /// the crater appears when the rocket visibly lands.
        /// </summary>
        public void Advance(float dt, List<SimEvent> due, List<CellRect> dirty)
        {
            Buffer.Advance(dt);
            double now = Buffer.RenderTick;
            while (_pending.Count > 0 && _pending[0].Tick <= now)
            {
                var e = _pending[0];
                _pending.RemoveAt(0);
                if (e.Type == SimEventType.Explode)
                {
                    var op = CarveOp.FromExplosion(e.X, e.Y, e.Value);
                    dirty.Add(Terrain.CarveCircle(op.X, op.Y, op.R));
                }
                due.Add(e);
            }
        }

        public int PendingEvents => _pending.Count;
    }

    /// <summary>
    /// Client protocol state, independent of Unity and of the socket: feed it
    /// received messages, read its state, call its methods to send.
    /// </summary>
    public sealed class NetSession
    {
        public Action<byte[]> Send = _ => { };

        public bool HasHello { get; private set; }
        public int UserId { get; private set; }
        public string DisplayName { get; private set; } = string.Empty;
        /// <summary>Last error code from the server (ErrorCodes), cleared by <see cref="ClearError"/>.</summary>
        public string LastError { get; private set; }
        public LobbyMsg Lobby { get; private set; } = new LobbyMsg();
        public ClientMatch Match { get; private set; }

        public bool InRoom => !string.IsNullOrEmpty(Lobby.Code);
        public bool IsHost => InRoom && Lobby.HostUserId == UserId;

        readonly List<SimEvent> _events = new List<SimEvent>();

        public void ClearError() { LastError = null; }

        public void Receive(byte[] bytes)
        {
            var r = new MsgReader(bytes);
            if (r.Version != ProtocolInfo.Version)
            {
                LastError = ErrorCodes.BadVersion;
                return;
            }
            switch (r.Type)
            {
                case MsgType.Hello:
                    var hello = HelloMsg.Decode(r);
                    HasHello = true;
                    UserId = hello.UserId;
                    DisplayName = hello.DisplayName;
                    break;
                case MsgType.Error:
                    LastError = ErrorMsg.Decode(r).Code;
                    break;
                case MsgType.Lobby:
                    Lobby = LobbyMsg.Decode(r);
                    if (!InRoom || Lobby.State == RoomState.Lobby) Match = null;
                    break;
                case MsgType.MatchStart:
                    Match = new ClientMatch(MatchStartMsg.Decode(r));
                    break;
                case MsgType.Snapshot:
                    Match?.Buffer.Add(Snapshot.Decode(r));
                    break;
                case MsgType.Events:
                    _events.Clear();
                    EventsMsg.Decode(r, _events);
                    Match?.AddEvents(_events);
                    break;
            }
        }

        public void CreateRoom() { Send(ClientMsg.Simple(MsgType.CreateRoom)); }
        public void JoinRoom(string code) { Send(ClientMsg.JoinRoom(code)); }
        public void QuickMatch() { Send(ClientMsg.Simple(MsgType.QuickMatch)); }
        public void SetReady(bool ready) { Send(ClientMsg.SetReady(ready)); }
        public void StartMatch() { Send(ClientMsg.Simple(MsgType.StartMatch)); }
        public void LeaveRoom() { Send(ClientMsg.Simple(MsgType.LeaveRoom)); }
        public void Rematch() { Send(ClientMsg.Simple(MsgType.Rematch)); }
        /// <summary>Host only: adds a computer player to the private room.</summary>
        public void AddBot() { Send(ClientMsg.Simple(MsgType.AddBot)); }
        /// <summary>Host only: removes the computer player with this (negative) user id.</summary>
        public void RemoveBot(int userId) { Send(ClientMsg.RemoveBot(userId)); }

        public void SendIntent(Intent i)
        {
            Send(ClientMsg.Input(new SimInput
            {
                Kind = i.Kind, Dir = i.Dir, Angle = i.Angle, Power = i.Power, Fuse = i.Fuse, Weapon = i.Weapon,
                TargetX = i.TargetX, TargetY = i.TargetY,
            }));
        }
    }
}
