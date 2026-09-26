using System;
using System.Net;
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
        public async Task StatusNeedsTheToken()
        {
            var client = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/status")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/status?token=wrong")).StatusCode);
            var res = await client.GetAsync("/status?token=test-token");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("\"rooms\"", await res.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task DownloadPageLinksTheApk()
        {
            var res = await _factory.CreateClient().GetAsync("/download");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("Worms.apk", await res.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GuestGetsHelloAndEmptyLobby()
        {
            await using var c = await TestClient.ConnectAsync(_factory, "?name=Ánh");
            Assert.True(await c.WaitFor(s => s.HasHello));
            Assert.Equal("Ánh", c.Read(s => s.DisplayName));
            Assert.True(c.Read(s => s.UserId) < 0);
            Assert.False(c.Read(s => s.InRoom));
        }

        [Fact]
        public async Task AllowedOriginIsAccepted()
        {
            await using var c = await TestClient.ConnectAsync(_factory, origin: "https://chat.lazybutts.com");
            Assert.True(await c.WaitFor(s => s.HasHello));
        }

        [Fact]
        public async Task ForeignOriginIsRejected()
        {
            await Assert.ThrowsAnyAsync<Exception>(() => TestClient.ConnectAsync(_factory, origin: "https://evil.example"));
        }

        [Fact]
        public async Task WrongProtocolVersionIsRejected()
        {
            await using var c = await TestClient.ConnectAsync(_factory);
            Assert.True(await c.WaitFor(s => s.HasHello));
            c.SendRaw(new byte[] { 0x99, 0x00, (byte)MsgType.QuickMatch });
            Assert.True(await c.WaitFor(s => s.LastError == ErrorCodes.BadVersion));
        }

        [Fact]
        public async Task FloodingIsRateLimited()
        {
            await using var c = await TestClient.ConnectAsync(_factory);
            Assert.True(await c.WaitFor(s => s.HasHello));
            for (int i = 0; i < 200 && !c.Closed; i++) c.SendRaw(ClientMsg.SetReady(true));
            Assert.True(await c.WaitFor(s => s.LastError == ErrorCodes.RateLimited));
        }
    }
}
