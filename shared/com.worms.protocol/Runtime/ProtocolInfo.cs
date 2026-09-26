namespace Worms.Protocol
{
    public static class ProtocolInfo
    {
        /// <summary>Bumped on every wire-format change; the server rejects other versions.</summary>
        public const ushort Version = 1;
    }

    /// <summary>First byte after the version header of every message.</summary>
    public enum MsgType : byte
    {
        // Server -> Client
        Hello = 1,
        Error = 2,
    }
}
