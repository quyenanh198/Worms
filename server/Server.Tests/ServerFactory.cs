using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Worms.Server.Tests
{
    public sealed class ServerFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ALLOWED_ORIGINS", "https://chat.lazybutts.com");
        }
    }
}
