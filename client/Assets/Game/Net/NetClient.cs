using System;
using UnityEngine;
using Worms.Game.Core;
using Worms.Protocol;

namespace Worms.Game.Net
{
    /// <summary>
    /// Keeps the WebSocket to the game server open, feeds received messages to
    /// the <see cref="NetSession"/> and reconnects with backoff when the
    /// connection drops (docs/PLAN.md §3.4.2).
    /// </summary>
    public sealed class NetClient : MonoBehaviour
    {
        public const string CookiePref = "worms.chat_session";

        public NetSession Session { get; private set; } = new NetSession();
        public string Url { get; private set; }
        public bool Connected => _socket != null && _socket.State == WsState.Open;
        public bool Unauthorized { get; private set; }
        public bool Outdated { get; private set; }
        public string Status { get; private set; } = "Đang kết nối…";

        IWsTransport _socket;
        float _retryAt = -1;
        int _failures;

        public static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>Stored Chat session cookie for native builds; the browser handles it on Web.</summary>
        public static string StoredCookie
        {
            get { return PlayerPrefs.GetString(CookiePref, string.Empty); }
            set
            {
                if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(CookiePref);
                else PlayerPrefs.SetString(CookiePref, value);
                PlayerPrefs.Save();
            }
        }

        public void Connect(string url)
        {
            Url = url;
            Open();
        }

        void Open()
        {
            _socket?.Dispose();
            Session = new NetSession();
            Unauthorized = false;
            Status = "Đang kết nối…";
            _socket = WsTransport.Create();
            Session.Send = bytes => _socket?.Send(bytes);
            _socket.Connect(Url, IsWeb ? null : StoredCookie);
        }

        /// <summary>Reconnect now (after logging in, or from a Retry button).</summary>
        public void Reconnect()
        {
            _failures = 0;
            Open();
        }

        void Update()
        {
            if (_socket == null) return;
            while (_socket.TryReceive(out var bytes))
            {
                try
                {
                    Session.Receive(bytes);
                }
                catch (ProtocolException e)
                {
                    Debug.LogWarning("Bad message: " + e.Message);
                }
            }

            if (Session.LastError == ErrorCodes.Unauthorized) Unauthorized = true;
            if (Session.LastError == ErrorCodes.BadVersion) Outdated = true;

            if (_socket.State == WsState.Open)
            {
                _failures = 0;
                Status = Session.HasHello ? "Xin chào " + Session.DisplayName : "Đang kết nối…";
            }
            else if (_socket.State == WsState.Closed)
            {
                if (Unauthorized) Status = "Cần đăng nhập tài khoản Chat";
                else if (Outdated) Status = "Game đã có bản mới, hãy tải lại";
                else if (_retryAt < 0)
                {
                    _failures++;
                    float delay = Mathf.Min(10f, Mathf.Pow(2, Mathf.Min(_failures, 4)) * 0.5f);
                    _retryAt = Time.unscaledTime + delay;
                    Status = "Mất kết nối, thử lại sau " + Mathf.CeilToInt(delay) + " giây…";
                }
                else if (Time.unscaledTime >= _retryAt)
                {
                    _retryAt = -1;
                    Open();
                }
            }
        }

        void OnDestroy()
        {
            _socket?.Dispose();
        }
    }
}
