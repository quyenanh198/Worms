using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Worms.Server.Tests
{
    /// <summary>Guest mode (no CHAT_API_URL), simulation sped up so tests see whole turns quickly.</summary>
    public sealed class ServerFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ALLOWED_ORIGINS", "https://chat.lazybutts.com");
            builder.UseSetting("SIM_SPEED", "6");
            builder.UseSetting("STATUS_TOKEN", "test-token");
        }
    }
}
