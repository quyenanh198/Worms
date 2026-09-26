using Worms.Protocol;
using Xunit;

namespace Worms.Protocol.Tests
{
    public class CodecTests
    {
        [Fact]
        public void HelloRoundTrips()
        {
            var bytes = new HelloMsg { Server = "worms", UserId = 7, DisplayName = "Ánh" }.Encode();
            var r = new MsgReader(bytes);
            Assert.Equal(ProtocolInfo.Version, r.Version);
            Assert.Equal(MsgType.Hello, r.Type);
            var m = HelloMsg.Decode(r);
            Assert.Equal("worms", m.Server);
            Assert.Equal(7, m.UserId);
            Assert.Equal("Ánh", m.DisplayName);
        }

        [Fact]
        public void TruncatedMessageThrowsProtocolException()
        {
            var bytes = new HelloMsg { Server = "worms", UserId = 7, DisplayName = "x" }.Encode();
            var r = new MsgReader(bytes, bytes.Length - 3);
            Assert.Throws<ProtocolException>(() => HelloMsg.Decode(r));
        }

        [Fact]
        public void TooShortMessageThrows()
        {
            Assert.Throws<ProtocolException>(() => new MsgReader(new byte[] { 1, 0 }));
        }
    }
}
