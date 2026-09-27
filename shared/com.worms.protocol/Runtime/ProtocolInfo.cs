namespace Worms.Protocol
{
    public static class ProtocolInfo
    {
        /// <summary>Bumped when wire format or seed-derived world data changes; the server rejects other versions.</summary>
        public const ushort Version = 8;
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
        /// <summary>Gold, owned cosmetics and loadout (after Hello, and after every change).</summary>
        Profile = 7,
        /// <summary>Gold earned in the match that just ended.</summary>
        Reward = 8,

        // Client -> Server
        CreateRoom = 20,
        JoinRoom = 21,
        QuickMatch = 22,
        SetReady = 23,
        StartMatch = 24,
        LeaveRoom = 25,
        Input = 26,
        Rematch = 27,
        /// <summary>Host adds a computer player to a private room.</summary>
        AddBot = 28,
        /// <summary>Host removes a computer player (payload: its user id).</summary>
        RemoveBot = 29,
        /// <summary>Buy a cosmetic (payload: item id).</summary>
        Buy = 30,
        /// <summary>Wear a cosmetic (payload: slot, item id; 0 takes the slot off).</summary>
        Equip = 31,
        /// <summary>Name the squad (payload: count, names).</summary>
        SetWormNames = 32,
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
        public const string NotEnoughGold = "not_enough_gold";
        public const string NotOwned = "not_owned";
    }
}
