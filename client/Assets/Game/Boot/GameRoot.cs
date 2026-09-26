using System;
using UnityEngine;
using Worms.Game.Audio;
using Worms.Game.Core;
using Worms.Game.Net;
using Worms.Game.Play;
using Worms.Game.UI;

namespace Worms.Game.Boot
{
    /// <summary>
    /// Entry point. The Boot scene is empty; this object is created at startup
    /// and switches between the menu, an online match and the offline sandbox.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        NetClient _net;
        MenuUI _menu;
        Camera _menuCamera;
        SandboxMatch _sandbox;
        NetMatch _netMatch;
        string _roomFromUrl;

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

            gameObject.AddComponent<AudioManager>();
            _net = gameObject.AddComponent<NetClient>();
            _net.Connect(ServerUrl.Resolve(NetClient.IsWeb, Application.absoluteURL, CommandLineArg("-server")));
            _roomFromUrl = ServerUrl.QueryParam(Application.absoluteURL, "room");

            _menu = gameObject.AddComponent<MenuUI>();
            _menu.Net = _net;
            _menu.StartSandbox = StartSandbox;

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sandbox") >= 0) StartSandbox();
        }

        void Update()
        {
            var session = _net.Session;
            // Invite link: join the room once we are connected.
            if (_roomFromUrl != null && session.HasHello && !session.InRoom)
            {
                session.JoinRoom(_roomFromUrl);
                _roomFromUrl = null;
            }

            if (_sandbox == null)
            {
                if (session.Match != null && _netMatch == null) StartNetMatch();
                else if (session.Match == null && _netMatch != null) EndNetMatch();
            }
            _menu.Hidden = _sandbox != null || _netMatch != null;
        }

        void CreateMenuCamera()
        {
            if (_menuCamera != null) return;
            _menuCamera = new GameObject("Menu Camera").AddComponent<Camera>();
            _menuCamera.clearFlags = CameraClearFlags.SolidColor;
            _menuCamera.backgroundColor = new Color(0.08f, 0.11f, 0.16f);
        }

        void DestroyMenuCamera()
        {
            if (_menuCamera != null) Destroy(_menuCamera.gameObject);
            _menuCamera = null;
        }

        void StartNetMatch()
        {
            DestroyMenuCamera();
            var go = new GameObject("Online Match");
            _netMatch = go.AddComponent<NetMatch>();
            _netMatch.Net = _net;
            go.AddComponent<Hud>().Source = _netMatch;
        }

        void EndNetMatch()
        {
            Destroy(_netMatch.gameObject);
            _netMatch = null;
            CreateMenuCamera();
        }

        void StartSandbox()
        {
            if (_netMatch != null) EndNetMatch();
            DestroyMenuCamera();
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

        static string CommandLineArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
