#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Worms.Game.Net
{
    /// <summary>WebSocket for desktop and Android, on top of System.Net.WebSockets.</summary>
    sealed class NativeWsTransport : IWsTransport
    {
        readonly ConcurrentQueue<byte[]> _inbox = new ConcurrentQueue<byte[]>();
        readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        ClientWebSocket _socket;
        volatile WsState _state = WsState.Closed;
        volatile string _closeReason = string.Empty;

        public WsState State => _state;
        public string CloseReason => _closeReason;

        public void Connect(string url, string cookie)
        {
            _socket = new ClientWebSocket();
            if (!string.IsNullOrEmpty(cookie)) _socket.Options.SetRequestHeader("Cookie", cookie);
            _state = WsState.Connecting;
            Task.Run(() => Run(new Uri(url)));
        }

        async Task Run(Uri uri)
        {
            try
            {
                await _socket.ConnectAsync(uri, _cts.Token).ConfigureAwait(false);
                _state = WsState.Open;
                var buffer = new byte[16 * 1024];
                var message = new MemoryStream();
                while (_socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _closeReason = result.CloseStatusDescription ?? string.Empty;
                        break;
                    }
                    message.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        _inbox.Enqueue(message.ToArray());
                        message.SetLength(0);
                    }
                }
            }
            catch (Exception e)
            {
                if (!_cts.IsCancellationRequested) _closeReason = e.Message;
            }
            _state = WsState.Closed;
        }

        public void Send(byte[] data)
        {
            if (_state != WsState.Open) return;
            Task.Run(async () =>
            {
                await _sendLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _closeReason = e.Message;
                }
                finally
                {
                    _sendLock.Release();
                }
            });
        }

        public bool TryReceive(out byte[] message)
        {
            return _inbox.TryDequeue(out message);
        }

        public void Close()
        {
            if (_socket == null) return;
            _cts.Cancel();
            try { _socket.Abort(); } catch (Exception) { }
            _state = WsState.Closed;
        }

        public void Dispose()
        {
            Close();
            _socket?.Dispose();
            _cts.Dispose();
        }
    }
}
#endif
