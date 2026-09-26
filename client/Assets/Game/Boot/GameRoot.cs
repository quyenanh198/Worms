using System;
using UnityEngine;
using Worms.Game.Net;
using Worms.Protocol;

namespace Worms.Game.Boot
{
    /// <summary>
    /// Entry point. The Boot scene is empty; this object is created at startup
    /// and builds everything else from code.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        IWsTransport _socket;
        string _status = "Đang kết nối…";
        GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameRoot>() != null) return;
            var go = new GameObject("GameRoot");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.08f, 0.11f);
            }

            bool isWeb = Application.platform == RuntimePlatform.WebGLPlayer;
            string url = ServerUrl.Resolve(isWeb, Application.absoluteURL, CommandLineArg("-server"));
            _socket = WsTransport.Create();
            _socket.Connect(url, null);
        }

        void Update()
        {
            if (_socket == null) return;
            while (_socket.TryReceive(out var bytes)) Handle(bytes);
            if (_socket.State == WsState.Closed && !_status.StartsWith("Mất kết nối", StringComparison.Ordinal))
                _status = "Mất kết nối " + _socket.CloseReason;
        }

        void Handle(byte[] bytes)
        {
            try
            {
                var r = new MsgReader(bytes);
                if (r.Version != ProtocolInfo.Version)
                {
                    _status = "Phiên bản không khớp, hãy cập nhật game";
                    return;
                }
                if (r.Type == MsgType.Hello) _status = "Đã kết nối tới " + HelloMsg.Decode(r).Server;
            }
            catch (ProtocolException e)
            {
                Debug.LogWarning("Bad message: " + e.Message);
            }
        }

        void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.RoundToInt(Screen.height * 0.04f),
                };
                _style.normal.textColor = Color.white;
            }
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "WORMS\n" + _status, _style);
        }

        void OnDestroy()
        {
            _socket?.Dispose();
        }

        static string CommandLineArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
