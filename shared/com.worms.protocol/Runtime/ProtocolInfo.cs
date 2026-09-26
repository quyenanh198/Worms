namespace Worms.Protocol
{
    public static class ProtocolInfo
    {
        /// <summary>Bumped on every wire-format change; the server rejects other versions.</summary>
        public const ushort Version = 3;
    }

    /// <summary>First byte after the version header of every message.</summary>
    public enum MsgType : byte
    {
        // Server -> Client
        Hello = 1,
        Error = 2,
        Snapshot = 3,
        Events = 4,
        Lobby = 5,
        MatchStart = 6,

        // Client -> Server
        CreateRoom = 20,
        JoinRoom = 21,
        QuickMatch = 22,
        SetReady = 23,
        StartMatch = 24,
        LeaveRoom = 25,
        Input = 26,
        Rematch = 27,
    }

    /// <summary>Codes sent in <see cref="ErrorMsg"/>.</summary>
    public static class ErrorCodes
    {
        public const string Unauthorized = "unauthorized";
        public const string BadVersion = "bad_version";
        public const string RateLimited = "rate_limited";
        public const string RoomNotFound = "room_not_found";
        public const string RoomFull = "room_full";
        public const string RoomBusy = "room_busy";
        public const string NotHost = "not_host";
        public const string NotReady = "not_ready";
        public const string BadMessage = "bad_message";
        public const string ServerFull = "server_full";
    }
}
