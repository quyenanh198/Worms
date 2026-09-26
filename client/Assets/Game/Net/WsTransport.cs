using System;

namespace Worms.Game.Net
{
    public enum WsState
    {
        Connecting,
        Open,
        Closed,
    }

    /// <summary>
    /// Binary WebSocket used by the game. Messages are polled from the main
    /// thread (<see cref="TryReceive"/>) so no Unity API is ever touched off it.
    /// </summary>
    public interface IWsTransport : IDisposable
    {
        WsState State { get; }

        /// <summary>Why the socket closed, if it did; empty otherwise.</summary>
        string CloseReason { get; }

        /// <param name="cookie">Value for the Cookie header. Ignored on Web, where the browser sends cookies itself.</param>
        void Connect(string url, string cookie);

        void Send(byte[] data);

        bool TryReceive(out byte[] message);

        void Close();
    }

    public static class WsTransport
    {
        public static IWsTransport Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebGLWsTransport();
#else
            return new NativeWsTransport();
#endif
        }
    }
}
