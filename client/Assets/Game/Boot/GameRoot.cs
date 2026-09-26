using System;
using UnityEngine;
using Worms.Game.Net;
using Worms.Game.Play;
using Worms.Game.UI;
using Worms.Protocol;

namespace Worms.Game.Boot
{
    /// <summary>
    /// Entry point. The Boot scene is empty; this object is created at startup,
    /// shows the menu and builds everything else from code.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        IWsTransport _socket;
        string _status = "Đang kết nối…";
        GUIStyle _title, _text, _button;
        Camera _menuCamera;
        SandboxMatch _sandbox;

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
            CreateMenuCamera();

            bool isWeb = Application.platform == RuntimePlatform.WebGLPlayer;
            string url = ServerUrl.Resolve(isWeb, Application.absoluteURL, CommandLineArg("-server"));
            _socket = WsTransport.Create();
            _socket.Connect(url, null);

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sandbox") >= 0) StartSandbox();
        }

        void CreateMenuCamera()
        {
            _menuCamera = new GameObject("Menu Camera").AddComponent<Camera>();
            _menuCamera.clearFlags = CameraClearFlags.SolidColor;
            _menuCamera.backgroundColor = new Color(0.08f, 0.11f, 0.16f);
        }

        void StartSandbox()
        {
            if (_menuCamera != null) Destroy(_menuCamera.gameObject);
            var go = new GameObject("Sandbox");
            _sandbox = go.AddComponent<SandboxMatch>();
            _sandbox.Leave = LeaveSandbox;
            _sandbox.Begin((uint)UnityEngine.Random.Range(1, int.MaxValue), 2, 4);
            go.AddComponent<Hud>().Source = _sandbox;
        }

        void LeaveSandbox()
        {
            if (_sandbox == null) return;
            Destroy(_sandbox.Presenter.gameObject);
            Destroy(_sandbox.gameObject);
            _sandbox = null;
            CreateMenuCamera();
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
            if (_sandbox != null) return;
            float u = Mathf.Max(12f, Screen.height / 36f);
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(u * 3), fontStyle = FontStyle.Bold };
                _title.normal.textColor = new Color(1f, 0.8f, 0.4f);
                _text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(u) };
                _text.normal.textColor = Color.white;
                _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u) };
            }
            float w = Screen.width, h = Screen.height;
            GUI.Label(new Rect(0, h * 0.18f, w, u * 4), "WORMS", _title);
            GUI.Label(new Rect(0, h * 0.18f + u * 4, w, u * 1.5f), _status, _text);
            if (GUI.Button(new Rect(w / 2 - u * 7, h * 0.55f, u * 14, u * 2.2f), "Chơi thử offline", _button)) StartSandbox();
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
