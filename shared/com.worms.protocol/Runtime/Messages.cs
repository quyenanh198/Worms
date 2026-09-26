namespace Worms.Protocol
{
    /// <summary>Server -> Client, sent once right after the WebSocket opens.</summary>
    public struct HelloMsg
    {
        public string Server;
        public int UserId;
        public string DisplayName;

        public byte[] Encode()
        {
            return new MsgWriter(MsgType.Hello).Str(Server).I32(UserId).Str(DisplayName).ToArray();
        }

        public static HelloMsg Decode(MsgReader r)
        {
            return new HelloMsg { Server = r.Str(), UserId = r.I32(), DisplayName = r.Str() };
        }
    }

    /// <summary>Server -> Client, sent before the server closes a connection on purpose.</summary>
    public struct ErrorMsg
    {
        public string Code;

        public byte[] Encode()
        {
            return new MsgWriter(MsgType.Error).Str(Code).ToArray();
        }

        public static ErrorMsg Decode(MsgReader r)
        {
            return new ErrorMsg { Code = r.Str() };
        }
    }
}
