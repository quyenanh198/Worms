using NUnit.Framework;
using Worms.Game.Net;

namespace Worms.Tests
{
    public class ServerUrlTests
    {
        [Test]
        public void WebUsesSocketNextToPage()
        {
            Assert.AreEqual("wss://chat.lazybutts.com/worms/ws",
                ServerUrl.Resolve(true, "https://chat.lazybutts.com/worms/?room=AB", null));
            Assert.AreEqual("wss://chat.lazybutts.com/worms/ws",
                ServerUrl.Resolve(true, "https://chat.lazybutts.com/worms/index.html", null));
        }

        [Test]
        public void PlainHttpPageUsesPlainSocket()
        {
            Assert.AreEqual("ws://localhost:8080/ws", ServerUrl.Resolve(true, "http://localhost:8080/", null));
        }

        [Test]
        public void NativeUsesDefaultOrOverride()
        {
            Assert.AreEqual(ServerUrl.Default, ServerUrl.Resolve(false, "", null));
            Assert.AreEqual("ws://10.0.0.5:8080/ws", ServerUrl.Resolve(false, "", "ws://10.0.0.5:8080/ws"));
        }
    }
}
