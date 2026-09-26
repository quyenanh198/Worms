using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Worms.Game.Core;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Server.Tests
{
    /// <summary>A real client session (the engine-free NetSession the game uses) over the test server's WebSocket.</summary>
    public sealed class TestClient : IAsyncDisposable
    {
        public readonly NetSession Session = new NetSession();
        public readonly List<SimEvent> DueEvents = new List<SimEvent>();
        public bool Closed { get; private set; }

        readonly object _gate = new object();
        WebSocket _ws;
        Task _loop;

        public static async Task<TestClient> ConnectAsync<T>(WebApplicationFactory<T> factory, string query = "", string cookie = null, string origin = null)
            where T : class
        {
            var client = factory.Server.CreateWebSocketClient();
            client.ConfigureRequest = req =>
            {
                if (cookie != null) req.Headers.Cookie = cookie;
                if (origin != null) req.Headers.Origin = origin;
            };
            var c = new TestClient();
            c._ws = await client.ConnectAsync(new Uri(factory.Server.BaseAddress, "ws" + query), CancellationToken.None);
            c.Session.Send = bytes => c.SendRaw(bytes);
            c._loop = Task.Run(c.ReceiveLoop);
            return c;
        }

        public void SendRaw(byte[] bytes)
        {
            lock (_gate) _ws.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None).GetAwaiter().GetResult();
        }

        async Task ReceiveLoop()
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (_ws.State == WebSocketState.Open)
                {
                    int count = 0;
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), CancellationToken.None);
                        count += r.Count;
                    } while (!r.EndOfMessage && r.MessageType != WebSocketMessageType.Close);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    var msg = new byte[count];
                    Array.Copy(buffer, msg, count);
                    lock (_gate) Session.Receive(msg);
                }
            }
            catch (Exception)
            {
            }
            Closed = true;
        }

        /// <summary>Runs <paramref name="action"/> with the session locked (it is updated from the receive loop).</summary>
        public T Read<T>(Func<NetSession, T> action)
        {
            lock (_gate) return action(Session);
        }

        public void Do(Action<NetSession> action)
        {
            // Sends take the same lock, so run outside it.
            action(Session);
        }

        public async Task<bool> WaitFor(Func<NetSession, bool> condition, int timeoutMs = 8000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                lock (_gate)
                {
                    if (condition(Session)) return true;
                    // Keep the client's render clock moving like a real frame loop.
                    Session.Match?.Advance(0.02f, DueEvents, new List<Worms.Sim.CellRect>());
                }
                if (Closed) lock (_gate) return condition(Session);
                await Task.Delay(20);
            }
            return false;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                {
                    using var cts = new CancellationTokenSource(2000);
                    await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
                }
            }
            catch (Exception)
            {
            }
            _ws.Dispose();
            await Task.WhenAny(_loop, Task.Delay(1000));
        }
    }
}
