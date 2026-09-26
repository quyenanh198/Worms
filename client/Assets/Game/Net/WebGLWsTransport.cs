#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Worms.Game.Net
{
    /// <summary>
    /// WebSocket for the Web build, backed by the browser's WebSocket through
    /// Plugins/WebGL/WormsSocket.jslib. Messages are queued on the JS side and
    /// pulled here once per frame, so there are no JS-to-C# callbacks.
    /// </summary>
    sealed class WebGLWsTransport : IWsTransport
    {
        [DllImport("__Internal")] static extern int WormsWs_Open(string url);
        [DllImport("__Internal")] static extern int WormsWs_State(int id);
        [DllImport("__Internal")] static extern int WormsWs_PeekSize(int id);
        [DllImport("__Internal")] static extern void WormsWs_Pop(int id, byte[] buffer, int length);
        [DllImport("__Internal")] static extern void WormsWs_Send(int id, byte[] data, int length);
        [DllImport("__Internal")] static extern void WormsWs_Close(int id);

        int _id;

        public WsState State
        {
            get
            {
                if (_id == 0) return WsState.Closed;
                switch (WormsWs_State(_id))
                {
                    case 0: return WsState.Connecting;
                    case 1: return WsState.Open;
                    default: return WsState.Closed;
                }
            }
        }

        public string CloseReason => State == WsState.Closed && _id != 0 ? "closed" : string.Empty;

        public void Connect(string url, string cookie)
        {
            _id = WormsWs_Open(url);
        }

        public void Send(byte[] data)
        {
            if (State == WsState.Open) WormsWs_Send(_id, data, data.Length);
        }

        public bool TryReceive(out byte[] message)
        {
            message = null;
            if (_id == 0) return false;
            int size = WormsWs_PeekSize(_id);
            if (size < 0) return false;
            message = new byte[size];
            WormsWs_Pop(_id, message, size);
            return true;
        }

        public void Close()
        {
            if (_id != 0) WormsWs_Close(_id);
        }

        public void Dispose()
        {
            Close();
            _id = 0;
        }
    }
}
#endif
