using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Server
{
    public sealed class Seat
    {
        public Identity User;
        public Connection Conn;
        public bool Ready;
        public int Team;
        public DateTime? DisconnectedAt;
        public bool Forfeited;
        /// <summary>Set for a computer player: it has no connection and is always ready.</summary>
        public BotDriver Bot;
        public bool IsBot => Bot != null;
    }

    public sealed class Room
    {
        public string Code;
        public bool IsQuick;
        public int HostUserId;
        public RoomState State;
        public readonly List<Seat> Seats = new List<Seat>();
        public World World;
        public List<string> TeamNames = new List<string>();
        public readonly ConcurrentQueue<SimInput> Inputs = new ConcurrentQueue<SimInput>();
        public CancellationTokenSource Loop;
        public DateTime EmptySince = DateTime.UtcNow;

        public int MaxPlayers => IsQuick ? 2 : 4;
        public Seat SeatOf(int userId) { return Seats.FirstOrDefault(s => s.User.UserId == userId); }
    }

    public sealed class MatchOptions
    {
        public int WormsPerTeam { get; init; } = 4;
        /// <summary>Seconds a player may be gone mid-match before their team forfeits.</summary>
        public double ReconnectGraceSeconds { get; init; } = 60;
        /// <summary>A room with nobody connected is removed after this long.</summary>
        public double EmptyRoomSeconds { get; init; } = 60;
        /// <summary>Simulation speed multiplier (tests only).</summary>
        public double Speed { get; init; } = 1;
        /// <summary>Rooms beyond this are refused with server_full.</summary>
        public int MaxRooms { get; init; } = 200;
    }

    /// <summary>
    /// Rooms, matchmaking and the per-room match loops (docs/PLAN.md §3.4).
    /// Lock order: the manager lock, then a room lock; the match loop only
    /// takes its room lock.
    /// </summary>
    public sealed class RoomManager : IDisposable
    {
        const int SnapshotEveryTicks = 3; // 20 Hz

        readonly object _lock = new object();
        readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();
        readonly Dictionary<int, Room> _byUser = new Dictionary<int, Room>();
        readonly MatchOptions _options;
        readonly ILogger<RoomManager> _log;
        readonly Timer _cleanup;

        public RoomManager(MatchOptions options, ILogger<RoomManager> log)
        {
            _options = options;
            _log = log;
            _cleanup = new Timer(_ => Cleanup(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public int RoomCount { get { lock (_lock) return _rooms.Count; } }
        public int PlayingCount { get { lock (_lock) return _rooms.Values.Count(r => r.State == RoomState.Playing); } }

        public void Dispose()
        {
            _cleanup.Dispose();
            lock (_lock) foreach (var r in _rooms.Values) r.Loop?.Cancel();
        }

        // ---- connection lifecycle -------------------------------------------------

        public void Connected(Connection conn)
        {
            lock (_lock)
            {
                if (_byUser.TryGetValue(conn.User.UserId, out var room))
                {
                    lock (room)
                    {
                        var seat = room.SeatOf(conn.User.UserId);
                        if (seat == null)
                        {
                            _byUser.Remove(conn.User.UserId);
                            conn.Send(new LobbyMsg().Encode());
                            return;
                        }
                        if (seat.Conn != null && seat.Conn != conn) seat.Conn.Close();
                        seat.Conn = conn;
                        seat.DisconnectedAt = null;
                        conn.Room = room;
                        BroadcastLobby(room);
                        if (room.State != RoomState.Lobby && room.World != null)
                            conn.Send(MatchStartMsg.FromWorld(room.World, room.TeamNames, seat.Team).Encode());
                    }
                    return;
                }
            }
            conn.Send(new LobbyMsg().Encode());
        }

        public void Disconnected(Connection conn)
        {
            lock (_lock)
            {
                var room = conn.Room;
                if (room == null) return;
                lock (room)
                {
                    var seat = room.SeatOf(conn.User.UserId);
                    if (seat == null || seat.Conn != conn) return; // already replaced by a reconnect
                    seat.Conn = null;
                    if (room.State == RoomState.Playing)
                    {
                        seat.DisconnectedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        RemoveSeat(room, seat);
                    }
                    if (room.Seats.All(s => s.Conn == null)) room.EmptySince = DateTime.UtcNow;
                    BroadcastLobby(room);
                }
            }
        }

        // ---- lobby ---------------------------------------------------------------

        public void CreateRoom(Connection conn)
        {
            lock (_lock)
            {
                if (_rooms.Count >= _options.MaxRooms) { conn.Send(Error(ErrorCodes.ServerFull)); return; }
                LeaveCurrent(conn);
                var room = NewRoom(false);
                lock (room) Seat(room, conn);
            }
        }

        public void JoinRoom(Connection conn, string code)
        {
            code = (code ?? string.Empty).Trim().ToUpperInvariant();
            lock (_lock)
            {
                if (!_rooms.TryGetValue(code, out var room)) { conn.Send(Error(ErrorCodes.RoomNotFound)); return; }
                if (conn.Room == room) { lock (room) BroadcastLobby(room); return; }
                lock (room)
                {
                    if (room.State != RoomState.Lobby) { conn.Send(Error(ErrorCodes.RoomBusy)); return; }
                    if (room.Seats.Count >= room.MaxPlayers) { conn.Send(Error(ErrorCodes.RoomFull)); return; }
                }
                LeaveCurrent(conn);
                lock (room) Seat(room, conn);
            }
        }

        public void QuickMatch(Connection conn)
        {
            lock (_lock)
            {
                LeaveCurrent(conn);
                var room = _rooms.Values.FirstOrDefault(r => r.IsQuick && r.State == RoomState.Lobby && r.Seats.Count < r.MaxPlayers
                                                             && r.Seats.All(s => s.Conn != null));
                if (room == null && _rooms.Count >= _options.MaxRooms) { conn.Send(Error(ErrorCodes.ServerFull)); return; }
                room ??= NewRoom(true);
                lock (room)
                {
                    var seat = Seat(room, conn);
                    seat.Ready = true;
                    if (room.Seats.Count == room.MaxPlayers) StartMatch(room);
                    else BroadcastLobby(room);
                }
            }
        }

        public void SetReady(Connection conn, bool ready)
        {
            var room = conn.Room;
            if (room == null) return;
            lock (room)
            {
                var seat = room.SeatOf(conn.User.UserId);
                if (seat == null || room.State != RoomState.Lobby) return;
                seat.Ready = ready;
                BroadcastLobby(room);
            }
        }

        public void Start(Connection conn)
        {
            var room = conn.Room;
            if (room == null) return;
            lock (room)
            {
                if (room.State != RoomState.Lobby) return;
                if (room.HostUserId != conn.User.UserId) { conn.Send(Error(ErrorCodes.NotHost)); return; }
                if (room.Seats.Count < 2 || room.Seats.Any(s => s.User.UserId != room.HostUserId && !s.Ready))
                {
                    conn.Send(Error(ErrorCodes.NotReady));
                    return;
                }
                StartMatch(room);
            }
        }

        /// <summary>Host adds a computer player to a private room that has a free seat.</summary>
        public void AddBot(Connection conn)
        {
            var room = conn.Room;
            if (room == null) return;
            lock (room)
            {
                if (room.State != RoomState.Lobby || room.IsQuick) return;
                if (room.HostUserId != conn.User.UserId) { conn.Send(Error(ErrorCodes.NotHost)); return; }
                if (room.Seats.Count >= room.MaxPlayers) { conn.Send(Error(ErrorCodes.RoomFull)); return; }
                // Bots get negative ids (people never do) and the lowest free number: "Máy 1", "Máy 2"...
                int n = 1;
                while (room.Seats.Exists(x => x.User.UserId == -n)) n++;
                room.Seats.Add(new Seat
                {
                    User = new Identity { UserId = -n, DisplayName = "Máy " + n },
                    Ready = true,
                    Team = room.Seats.Count,
                    Bot = new BotDriver((uint)RandomNumberGenerator.GetInt32(1, int.MaxValue)),
                });
                BroadcastLobby(room);
            }
        }

        public void RemoveBot(Connection conn, int botUserId)
        {
            lock (_lock)
            {
                var room = conn.Room;
                if (room == null) return;
                lock (room)
                {
                    if (room.State != RoomState.Lobby) return;
                    if (room.HostUserId != conn.User.UserId) { conn.Send(Error(ErrorCodes.NotHost)); return; }
                    var seat = room.SeatOf(botUserId);
                    if (seat == null || !seat.IsBot) return;
                    RemoveSeat(room, seat);
                    BroadcastLobby(room);
                }
            }
        }

        public void Leave(Connection conn)
        {
            lock (_lock) LeaveCurrent(conn);
            conn.Send(new LobbyMsg().Encode());
        }

        public void Rematch(Connection conn)
        {
            lock (_lock)
            {
                var room = conn.Room;
                if (room == null) return;
                lock (room)
                {
                    if (room.State != RoomState.Finished) return;
                    room.State = RoomState.Lobby;
                    room.World = null;
                    foreach (var gone in room.Seats.Where(s => s.Conn == null && !s.IsBot).ToList()) RemoveSeat(room, gone);
                    foreach (var s in room.Seats) { s.Ready = room.IsQuick || s.IsBot; s.Forfeited = false; s.DisconnectedAt = null; }
                    if (room.IsQuick && room.Seats.Count == room.MaxPlayers) StartMatch(room);
                    else BroadcastLobby(room);
                }
            }
        }

        public void Input(Connection conn, SimInput input)
        {
            var room = conn.Room;
            if (room == null) return;
            lock (room)
            {
                if (room.State != RoomState.Playing) return;
                var seat = room.SeatOf(conn.User.UserId);
                if (seat == null || seat.Forfeited) return;
                input.Team = seat.Team; // never trust a team from the client
                room.Inputs.Enqueue(input);
            }
        }

        // ---- internals (callers hold the needed locks) ------------------------------

        Room NewRoom(bool quick)
        {
            string code;
            do code = RandomCode(); while (_rooms.ContainsKey(code));
            var room = new Room { Code = code, IsQuick = quick, State = RoomState.Lobby };
            _rooms[code] = room;
            return room;
        }

        Seat Seat(Room room, Connection conn)
        {
            var seat = new Seat { User = conn.User, Conn = conn, Team = room.Seats.Count };
            room.Seats.Add(seat);
            if (room.Seats.Count == 1) room.HostUserId = conn.User.UserId;
            conn.Room = room;
            _byUser[conn.User.UserId] = room;
            BroadcastLobby(room);
            return seat;
        }

        void LeaveCurrent(Connection conn)
        {
            var room = conn.Room;
            if (room == null) return;
            lock (room)
            {
                var seat = room.SeatOf(conn.User.UserId);
                if (seat != null)
                {
                    if (room.State == RoomState.Playing)
                    {
                        seat.Conn = null;
                        Forfeit(room, seat);
                    }
                    else
                    {
                        RemoveSeat(room, seat);
                    }
                }
                _byUser.Remove(conn.User.UserId);
                conn.Room = null;
                BroadcastLobby(room);
            }
        }

        void RemoveSeat(Room room, Seat seat)
        {
            room.Seats.Remove(seat);
            if (_byUser.TryGetValue(seat.User.UserId, out var r) && r == room) _byUser.Remove(seat.User.UserId);
            var person = room.Seats.FirstOrDefault(x => !x.IsBot);
            if (room.HostUserId == seat.User.UserId && person != null) room.HostUserId = person.User.UserId;
            ReassignTeams(room);
            // Bots alone do not keep a room alive: cleanup removes it once no person is left.
            if (person == null) room.EmptySince = DateTime.UtcNow;
        }

        static void ReassignTeams(Room room)
        {
            for (int i = 0; i < room.Seats.Count; i++) room.Seats[i].Team = i;
        }

        void Forfeit(Room room, Seat seat)
        {
            if (seat.Forfeited || room.World == null) return;
            seat.Forfeited = true;
            room.World.Forfeit(seat.Team);
        }

        void StartMatch(Room room)
        {
            uint seed = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);
            room.World = new World(new MatchSetup { Seed = seed, Teams = room.Seats.Count, WormsPerTeam = _options.WormsPerTeam });
            room.TeamNames = room.Seats.Select(s => s.User.DisplayName).ToList();
            room.State = RoomState.Playing;
            while (room.Inputs.TryDequeue(out _)) { }
            BroadcastLobby(room);
            foreach (var s in room.Seats)
                s.Conn?.Send(MatchStartMsg.FromWorld(room.World, room.TeamNames, s.Team).Encode());
            room.Loop?.Cancel();
            room.Loop = new CancellationTokenSource();
            var token = room.Loop.Token;
            _log.LogInformation("Room {Code}: match started, seed {Seed}, {Teams} teams", room.Code, seed, room.Seats.Count);
            Task.Run(() => RunMatch(room, token));
        }

        async Task RunMatch(Room room, CancellationToken token)
        {
            var clock = Stopwatch.StartNew();
            double tickSeconds = C.Dt / _options.Speed;
            long done = 0;
            var inputs = new List<SimInput>();
            var events = new List<SimEvent>();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    long due = (long)(clock.Elapsed.TotalSeconds / tickSeconds);
                    if (due - done > 30) done = due - 1; // stalled (e.g. GC or suspend): do not fast-forward seconds of play
                    int steps = 0;
                    while (done < due && steps++ < 5)
                    {
                        done++;
                        lock (room)
                        {
                            if (room.World == null || room.State != RoomState.Playing) return;
                            StepRoom(room, inputs, events);
                            if (room.State != RoomState.Playing) return;
                        }
                    }
                    double wait = (done + 1) * tickSeconds - clock.Elapsed.TotalSeconds;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(0.001, wait)), token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.LogError(e, "Room {Code}: match loop crashed", room.Code);
            }
        }

        void StepRoom(Room room, List<SimInput> inputs, List<SimEvent> events)
        {
            var world = room.World;
            inputs.Clear();
            while (room.Inputs.TryDequeue(out var i)) inputs.Add(i);

            var now = DateTime.UtcNow;
            foreach (var seat in room.Seats)
            {
                if (seat.Forfeited) continue;
                if (seat.IsBot)
                {
                    seat.Bot.Tick(world, seat.Team, inputs);
                    continue;
                }
                if (seat.Conn != null) continue;
                if (seat.DisconnectedAt.HasValue && (now - seat.DisconnectedAt.Value).TotalSeconds >= _options.ReconnectGraceSeconds)
                    Forfeit(room, seat);
                else
                    world.SkipTurn(seat.Team); // an absent player's turn passes
            }

            events.Clear();
            events.AddRange(world.Step(inputs));
            bool important = false;
            foreach (var e in events) important |= e.Type == SimEventType.Turn || e.Type == SimEventType.GameOver;
            if (events.Count > 0) Broadcast(room, EventsMsg.Encode(events));
            if (important || world.Tick % SnapshotEveryTicks == 0) Broadcast(room, Snapshot.FromWorld(world).Encode());

            if (world.Phase == Phase.GameOver)
            {
                room.State = RoomState.Finished;
                foreach (var s in room.Seats) s.Ready = false;
                BroadcastLobby(room);
                _log.LogInformation("Room {Code}: match over, winner team {Winner}", room.Code, world.Winner);
            }
        }

        void Cleanup()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                foreach (var room in _rooms.Values.ToList())
                {
                    lock (room)
                    {
                        if (room.Seats.Any(s => s.Conn != null)) continue;
                        if ((now - room.EmptySince).TotalSeconds < _options.EmptyRoomSeconds) continue;
                        room.Loop?.Cancel();
                        foreach (var s in room.Seats)
                            if (_byUser.TryGetValue(s.User.UserId, out var r) && r == room) _byUser.Remove(s.User.UserId);
                        _rooms.Remove(room.Code);
                        _log.LogInformation("Room {Code}: removed (empty)", room.Code);
                    }
                }
            }
        }

        static void Broadcast(Room room, byte[] message)
        {
            foreach (var s in room.Seats) s.Conn?.Send(message);
        }

        static void BroadcastLobby(Room room)
        {
            var msg = new LobbyMsg { Code = room.Code, IsQuick = room.IsQuick, HostUserId = room.HostUserId, State = room.State };
            foreach (var s in room.Seats)
                msg.Players.Add(new PlayerInfo { UserId = s.User.UserId, Name = s.User.DisplayName, Team = s.Team, Ready = s.Ready, Connected = s.Conn != null || s.IsBot, IsBot = s.IsBot });
            Broadcast(room, msg.Encode());
        }

        static byte[] Error(string code) { return new ErrorMsg { Code = code }.Encode(); }

        static string RandomCode()
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            var chars = new char[4];
            for (int i = 0; i < chars.Length; i++) chars[i] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
            return new string(chars);
        }
    }
}
