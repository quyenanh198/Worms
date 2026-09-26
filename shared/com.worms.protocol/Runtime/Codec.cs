using System;
using System.IO;
using System.Text;

namespace Worms.Protocol
{
    /// <summary>
    /// Little-endian binary writer used for every message:
    /// [ProtocolInfo.Version:u16][MsgType:u8][payload].
    /// </summary>
    public sealed class MsgWriter
    {
        readonly MemoryStream _stream = new MemoryStream(256);
        readonly BinaryWriter _w;

        public MsgWriter(MsgType type)
        {
            _w = new BinaryWriter(_stream, Encoding.UTF8, true);
            _w.Write(ProtocolInfo.Version);
            _w.Write((byte)type);
        }

        public MsgWriter U8(byte v) { _w.Write(v); return this; }
        public MsgWriter I8(sbyte v) { _w.Write(v); return this; }
        public MsgWriter U16(ushort v) { _w.Write(v); return this; }
        public MsgWriter I16(short v) { _w.Write(v); return this; }
        public MsgWriter U32(uint v) { _w.Write(v); return this; }
        public MsgWriter I32(int v) { _w.Write(v); return this; }
        public MsgWriter F32(float v) { _w.Write(v); return this; }
        public MsgWriter Bool(bool v) { _w.Write(v); return this; }
        public MsgWriter Str(string v) { _w.Write(v ?? string.Empty); return this; }

        public byte[] ToArray()
        {
            _w.Flush();
            return _stream.ToArray();
        }
    }

    /// <summary>Reader matching <see cref="MsgWriter"/>. Throws <see cref="ProtocolException"/> on malformed input.</summary>
    public sealed class MsgReader
    {
        readonly BinaryReader _r;

        public ushort Version { get; }
        public MsgType Type { get; }

        public MsgReader(byte[] data) : this(data, data?.Length ?? 0) { }

        public MsgReader(byte[] data, int count)
        {
            if (data == null || count < 3) throw new ProtocolException("message too short");
            _r = new BinaryReader(new MemoryStream(data, 0, count, false), Encoding.UTF8);
            Version = _r.ReadUInt16();
            Type = (MsgType)_r.ReadByte();
        }

        public byte U8() { return Guard(() => _r.ReadByte()); }
        public sbyte I8() { return Guard(() => _r.ReadSByte()); }
        public ushort U16() { return Guard(() => _r.ReadUInt16()); }
        public short I16() { return Guard(() => _r.ReadInt16()); }
        public uint U32() { return Guard(() => _r.ReadUInt32()); }
        public int I32() { return Guard(() => _r.ReadInt32()); }
        public float F32() { return Guard(() => _r.ReadSingle()); }
        public bool Bool() { return Guard(() => _r.ReadBoolean()); }
        public string Str() { return Guard(() => _r.ReadString()); }

        static T Guard<T>(Func<T> read)
        {
            try { return read(); }
            catch (EndOfStreamException) { throw new ProtocolException("unexpected end of message"); }
            catch (FormatException) { throw new ProtocolException("malformed field"); }
        }
    }

    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message) { }
    }
}
