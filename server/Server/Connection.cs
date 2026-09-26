using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Worms.Server
{
    /// <summary>
    /// One WebSocket. Sends go through a queue drained by a single writer, so
    /// rooms can send from any thread. A token bucket limits incoming messages.
    /// </summary>
    public sealed class Connection
    {
        const int MaxQueued = 512;
        const double RefillPerSecond = 40;
        const double Burst = 80;

        readonly WebSocket _socket;
        readonly Channel<byte[]> _outbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(MaxQueued) { FullMode = BoundedChannelFullMode.DropWrite });
        readonly Stopwatch _clock = Stopwatch.StartNew();
        double _tokens = Burst;
        double _lastRefill;

        public Identity User { get; }
        public Room Room { get; set; }
        public bool IsOpen => _socket.State == WebSocketState.Open;

        public Connection(WebSocket socket, Identity user)
        {
            _socket = socket;
            User = user;
        }

        public void Send(byte[] message)
        {
            if (!_outbox.Writer.TryWrite(message)) Close();
        }

        public void Close()
        {
            _outbox.Writer.TryComplete();
        }

        /// <summary>False when the client sends faster than allowed.</summary>
        public bool TakeToken()
        {
            double now = _clock.Elapsed.TotalSeconds;
            _tokens = Math.Min(Burst, _tokens + (now - _lastRefill) * RefillPerSecond);
            _lastRefill = now;
            if (_tokens < 1) return false;
            _tokens -= 1;
            return true;
        }

        public async Task RunWriterAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var message in _outbox.Reader.ReadAllAsync(ct))
                    await _socket.SendAsync(message, WebSocketMessageType.Binary, true, ct);
                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is ObjectDisposedException || e is System.IO.IOException)
            {
            }
        }
    }
}
