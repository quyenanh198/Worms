using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Worms.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Worms.Server.Tests
{
    /// <summary>Real-time speed, many rooms at once: every client keeps getting ~20 snapshots per second.</summary>
    public sealed class LoadFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("MAX_ROOMS", "40");
        }
    }

    public class LoadTests : IClassFixture<LoadFactory>
    {
        readonly LoadFactory _factory;
        readonly ITestOutputHelper _out;

        public LoadTests(LoadFactory factory, ITestOutputHelper output)
        {
            _factory = factory;
            _out = output;
        }

        [Fact]
        public async Task ThirtyMatchesAtOnceKeepTheirSnapshotRate()
        {
            const int rooms = 30;
            var clients = new List<TestClient>();
            try
            {
                for (int i = 0; i < rooms * 2; i++)
                {
                    var c = await TestClient.ConnectAsync(_factory, $"?name=L{i}&guest={1000 + i}");
                    clients.Add(c);
                }
                foreach (var c in clients) Assert.True(await c.WaitFor(s => s.HasHello));
                // Pair them up two by two through quick match.
                for (int i = 0; i < clients.Count; i += 2)
                {
                    clients[i].Do(s => s.QuickMatch());
                    Assert.True(await clients[i].WaitFor(s => s.InRoom));
                    clients[i + 1].Do(s => s.QuickMatch());
                    Assert.True(await clients[i + 1].WaitFor(s => s.Match != null));
                }
                foreach (var c in clients) Assert.True(await c.WaitFor(s => s.Match != null));

                var start = clients.Select(c => c.Read(s => s.Match.Buffer.Latest.Tick)).ToArray();
                await Task.Delay(3000);
                var ticks = clients.Select((c, i) => (int)(c.Read(s => s.Match.Buffer.Latest.Tick) - start[i])).ToArray();
                _out.WriteLine($"ticks advanced in 3 s: min {ticks.Min()}, avg {ticks.Average():0}, max {ticks.Max()} (ideal 180)");
                Assert.True(ticks.Min() >= 120, "a match fell behind real time: " + ticks.Min());

                // One more room than allowed is refused.
                await using var extra = await TestClient.ConnectAsync(_factory, "?name=Extra&guest=9999");
                Assert.True(await extra.WaitFor(s => s.HasHello));
                for (int i = 0; i < 20; i++)
                {
                    await using var filler = await TestClient.ConnectAsync(_factory, $"?name=F{i}&guest={5000 + i}");
                    Assert.True(await filler.WaitFor(s => s.HasHello));
                    filler.Do(s => s.CreateRoom());
                    await filler.WaitFor(s => s.InRoom || s.LastError != null);
                }
                extra.Do(s => s.CreateRoom());
                Assert.True(await extra.WaitFor(s => s.LastError == ErrorCodes.ServerFull));
            }
            finally
            {
                foreach (var c in clients) await c.DisposeAsync();
            }
        }
    }
}
