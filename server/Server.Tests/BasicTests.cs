using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Worms.Protocol;
using Xunit;

namespace Worms.Server.Tests
{
    public class BasicTests : IClassFixture<ServerFactory>
    {
        readonly ServerFactory _factory;

        public BasicTests(ServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task HealthzIsOk()
        {
            var res = await _factory.CreateClient().GetAsync("/healthz");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("ok", await res.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task IndexIsServedWithNoCache()
        {
            var res = await _factory.CreateClient().GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("Worms", await res.Content.ReadAsStringAsync());
            Assert.Equal("no-cache", res.Headers.CacheControl?.ToString());
        }

        [Fact]
        public async Task SocketSendsHelloFirst()
        {
            var ws = _factory.Server.CreateWebSocketClient();
            using var socket = await ws.ConnectAsync(new Uri(_factory.Server.BaseAddress, "ws"), CancellationToken.None);
            var buffer = new byte[1024];
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            var r = new MsgReader(buffer, result.Count);
            Assert.Equal(MsgType.Hello, r.Type);
            Assert.Equal("worms", HelloMsg.Decode(r).Server);
        }

        [Fact]
        public async Task AllowedOriginIsAccepted()
        {
            var ws = _factory.Server.CreateWebSocketClient();
            ws.ConfigureRequest = req => req.Headers.Origin = "https://chat.lazybutts.com";
            using var socket = await ws.ConnectAsync(new Uri(_factory.Server.BaseAddress, "ws"), CancellationToken.None);
            Assert.Equal(WebSocketState.Open, socket.State);
        }

        [Fact]
        public async Task ForeignOriginIsRejected()
        {
            var ws = _factory.Server.CreateWebSocketClient();
            ws.ConfigureRequest = req => req.Headers.Origin = "https://evil.example";
            await Assert.ThrowsAnyAsync<Exception>(() => ws.ConnectAsync(new Uri(_factory.Server.BaseAddress, "ws"), CancellationToken.None));
        }
    }
}
