using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Worms.Protocol;
using Xunit;

namespace Worms.Server.Tests
{
    public class IdentityTests : IClassFixture<ServerFactory>
    {
        readonly ServerFactory _factory;

        public IdentityTests(ServerFactory factory)
        {
            _factory = factory;
        }

        sealed class FakeChat : HttpMessageHandler
        {
            public int Calls;
            public string LastCookie;
            public HttpStatusCode Status = HttpStatusCode.OK;
            public string Body = "{\"id\":42,\"username\":\"anh\",\"display_name\":\"Ánh\",\"is_admin\":true}";

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Calls++;
                LastCookie = string.Join(";", request.Headers.GetValues("Cookie"));
                Assert.Equal("http://chat:8082/api/me", request.RequestUri.ToString());
                return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") });
            }
        }

        static HttpContext Ctx(string cookie)
        {
            var ctx = new DefaultHttpContext();
            if (cookie != null) ctx.Request.Headers.Cookie = cookie;
            return ctx;
        }

        [Fact]
        public async Task ChatIdentityForwardsOnlyTheSessionCookieAndCaches()
        {
            var chat = new FakeChat();
            var ids = new ChatIdentityProvider(new HttpClient(chat), "http://chat:8082/");
            var who = await ids.ResolveAsync(Ctx("theme=dark; lb_session=abc.def; other=1"));
            Assert.Equal(42, who.UserId);
            Assert.Equal("Ánh", who.DisplayName);
            Assert.Equal("lb_session=abc.def", chat.LastCookie);
            await ids.ResolveAsync(Ctx("lb_session=abc.def"));
            Assert.Equal(1, chat.Calls);
        }

        [Fact]
        public async Task NoCookieOrRejectedCookieIsAnonymous()
        {
            var chat = new FakeChat { Status = HttpStatusCode.Unauthorized, Body = "{\"error\":\"unauthorized\"}" };
            var ids = new ChatIdentityProvider(new HttpClient(chat), "http://chat:8082");
            Assert.Null(await ids.ResolveAsync(Ctx(null)));
            Assert.Null(await ids.ResolveAsync(Ctx("lb_session=bad")));
            Assert.Equal(1, chat.Calls);
        }

        [Fact]
        public async Task UnauthenticatedSocketGetsErrorAndIsClosed()
        {
            var chat = new FakeChat { Status = HttpStatusCode.Unauthorized, Body = "{}" };
            var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
                s.AddSingleton<IIdentityProvider>(new ChatIdentityProvider(new HttpClient(chat), "http://chat:8082"))));
            await using var c = await TestClient.ConnectAsync(factory, cookie: "lb_session=nope");
            Assert.True(await c.WaitFor(s => s.LastError == ErrorCodes.Unauthorized));
            Assert.False(c.Read(s => s.HasHello));
        }

        [Fact]
        public async Task ChatUserConnects()
        {
            var chat = new FakeChat();
            var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
                s.AddSingleton<IIdentityProvider>(new ChatIdentityProvider(new HttpClient(chat), "http://chat:8082"))));
            await using var c = await TestClient.ConnectAsync(factory, cookie: "lb_session=good");
            Assert.True(await c.WaitFor(s => s.HasHello));
            Assert.Equal(42, c.Read(s => s.UserId));
        }
    }
}
