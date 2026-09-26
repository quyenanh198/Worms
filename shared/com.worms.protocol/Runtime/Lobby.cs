using System.Collections.Generic;
using Worms.Sim;

namespace Worms.Protocol
{
    public enum RoomState : byte
    {
        Lobby = 0,
        Playing = 1,
        Finished = 2,
    }

    public struct PlayerInfo
    {
        public int UserId;
        public string Name;
        public int Team;
        public bool Ready;
        public bool Connected;
    }

    /// <summary>Server -> Client: the room the player is in. An empty <see cref="Code"/> means no room.</summary>
    public sealed class LobbyMsg
    {
        public string Code = string.Empty;
        public bool IsQuick;
        public int HostUserId;
        public RoomState State;
        public readonly List<PlayerInfo> Players = new List<PlayerInfo>();

        public byte[] Encode()
        {
            var w = new MsgWriter(MsgType.Lobby).Str(Code).Bool(IsQuick).I32(HostUserId).U8((byte)State).U8((byte)Players.Count);
            foreach (var p in Players) w.I32(p.UserId).Str(p.Name).U8((byte)p.Team).Bool(p.Ready).Bool(p.Connected);
            return w.ToArray();
        }

        public static LobbyMsg Decode(MsgReader r)
        {
            var m = new LobbyMsg { Code = r.Str(), IsQuick = r.Bool(), HostUserId = r.I32(), State = (RoomState)r.U8() };
            int n = r.U8();
            for (int i = 0; i < n; i++)
                m.Players.Add(new PlayerInfo { UserId = r.I32(), Name = r.Str(), Team = r.U8(), Ready = r.Bool(), Connected = r.Bool() });
            return m;
        }
    }

    /// <summary>
    /// Server -> Client when a match starts or a player reconnects: enough to
    /// rebuild the terrain (seed + carves) and render (snapshot).
    /// </summary>
    public sealed class MatchStartMsg
    {
        public uint Seed;
        public int WormsPerTeam;
        /// <summary>This client's team, or -1 when spectating.</summary>
        public int YourTeam = -1;
        public readonly List<string> TeamNames = new List<string>();
        public readonly List<CarveOp> TerrainOps = new List<CarveOp>();
        public Snapshot Snapshot = new Snapshot();

        public int Teams => TeamNames.Count;

        public static MatchStartMsg FromWorld(World w, IReadOnlyList<string> teamNames, int yourTeam)
        {
            var m = new MatchStartMsg { Seed = w.Seed, WormsPerTeam = w.Worms.Count / w.TeamCount, YourTeam = yourTeam };
            m.TeamNames.AddRange(teamNames);
            m.TerrainOps.AddRange(w.TerrainOps);
            Snapshot.FromWorld(w, m.Snapshot);
            return m;
        }

        public byte[] Encode()
        {
            var w = new MsgWriter(MsgType.MatchStart).U32(Seed).U8((byte)WormsPerTeam).I8((sbyte)YourTeam).U8((byte)TeamNames.Count);
            foreach (var n in TeamNames) w.Str(n);
            w.I32(TerrainOps.Count);
            foreach (var op in TerrainOps) w.I16(op.X).I16(op.Y).I16(op.R);
            Snapshot.WriteTo(w);
            return w.ToArray();
        }

        public static MatchStartMsg Decode(MsgReader r)
        {
            var m = new MatchStartMsg { Seed = r.U32(), WormsPerTeam = r.U8(), YourTeam = r.I8() };
            int teams = r.U8();
            for (int i = 0; i < teams; i++) m.TeamNames.Add(r.Str());
            int ops = r.I32();
            if (ops < 0 || ops > 100000) throw new ProtocolException("too many terrain ops");
            for (int i = 0; i < ops; i++) m.TerrainOps.Add(new CarveOp { X = r.I16(), Y = r.I16(), R = r.I16() });
            Snapshot.Decode(r, m.Snapshot);
            return m;
        }
    }

    /// <summary>Server -> Client: everything that happened during one or more ticks.</summary>
    public static class EventsMsg
    {
        public static byte[] Encode(IReadOnlyList<SimEvent> events)
        {
            var w = new MsgWriter(MsgType.Events).U16((ushort)events.Count);
            foreach (var e in events)
            {
                w.U32((uint)e.Tick).U8((byte)e.Type).I16((short)e.Worm).I8((sbyte)e.Team).I32(e.Entity)
                 .U8((byte)e.Weapon).U8((byte)e.Cause).I16((short)e.Amount).F32(e.X).F32(e.Y).F32(e.Value);
            }
            return w.ToArray();
        }

        public static void Decode(MsgReader r, List<SimEvent> into)
        {
            int n = r.U16();
            for (int i = 0; i < n; i++)
            {
                into.Add(new SimEvent
                {
                    Tick = (int)r.U32(), Type = (SimEventType)r.U8(), Worm = r.I16(), Team = r.I8(), Entity = r.I32(),
                    Weapon = (WeaponId)r.U8(), Cause = (DeathCause)r.U8(), Amount = r.I16(), X = r.F32(), Y = r.F32(), Value = r.F32(),
                });
            }
        }
    }

    /// <summary>Client -> Server messages.</summary>
    public static class ClientMsg
    {
        public static byte[] Simple(MsgType type) { return new MsgWriter(type).ToArray(); }
        public static byte[] JoinRoom(string code) { return new MsgWriter(MsgType.JoinRoom).Str(code ?? string.Empty).ToArray(); }
        public static byte[] SetReady(bool ready) { return new MsgWriter(MsgType.SetReady).Bool(ready).ToArray(); }

        public static byte[] Input(SimInput i)
        {
            return new MsgWriter(MsgType.Input).U8((byte)i.Kind).I8((sbyte)i.Dir).F32(i.Angle).F32(i.Power)
                .U8((byte)i.Fuse).U8((byte)i.Weapon).F32(i.TargetX).F32(i.TargetY).ToArray();
        }

        /// <summary>Decodes an input; the team is set by the server from the sender, never trusted from the wire.</summary>
        public static SimInput ReadInput(MsgReader r)
        {
            return new SimInput
            {
                Kind = (InputKind)r.U8(), Dir = r.I8(), Angle = r.F32(), Power = r.F32(),
                Fuse = r.U8(), Weapon = (WeaponId)r.U8(), TargetX = r.F32(), TargetY = r.F32(),
            };
        }
    }
}
