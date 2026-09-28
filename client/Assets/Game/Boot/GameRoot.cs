using System;
#if DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
#endif
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
        Render.MenuScene _menuScene;
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
#if DEVELOPMENT_BUILD
            var capturePath = CommandLineArg("-capture-path");
            if (!string.IsNullOrEmpty(capturePath)) StartCoroutine(CapturePreview(capturePath));
#endif
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

        /// <summary>The menu's 3D backdrop: the Worms squad on its island (it brings its own camera).</summary>
        void CreateMenuCamera()
        {
            if (_menuScene != null) return;
            _menuScene = Render.MenuScene.Create();
        }

        void DestroyMenuCamera()
        {
            if (_menuScene != null) Destroy(_menuScene.gameObject);
            _menuScene = null;
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
            var profile = _net.Session.Profile;
            if (profile != null)
            {
                _sandbox.PlayerLoadout = profile.Loadout;
                _sandbox.PlayerWormNames = profile.WormNames;
            }
            uint seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            int teams = 2, wormsPerTeam = 4;
#if DEVELOPMENT_BUILD
            if (uint.TryParse(CommandLineArg("-capture-seed"), out uint captureSeed)) seed = captureSeed;
            if (int.TryParse(CommandLineArg("-capture-teams"), out int captureTeams)) teams = Mathf.Clamp(captureTeams, 2, 4);
            if (int.TryParse(CommandLineArg("-capture-worms"), out int captureWorms)) wormsPerTeam = Mathf.Clamp(captureWorms, 1, 4);
#endif
            _sandbox.Begin(seed, teams, wormsPerTeam);
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

#if DEVELOPMENT_BUILD
        // The preview player runs on a separate Windows desktop. This records its real
        // camera and IMGUI output without bringing a window to the user's desktop.
        static IEnumerator CapturePreview(string path)
        {
            // Wait through the menu squad drop so a menu capture shows the final layout.
            for (int i = 0; i < 240; i++) yield return new WaitForEndOfFrame();
            var shotStage = CommandLineArg("-capture-shot");
            if (!string.IsNullOrEmpty(shotStage) &&
                FindAnyObjectByType<SandboxMatch>() is SandboxMatch shotMatch)
            {
                int oldCarves = shotMatch.World.TerrainOps.Count;
                shotMatch.FireCaptureShot();
                if (shotStage == "flight")
                {
                    for (int i = 0; i < 70; i++) yield return new WaitForEndOfFrame();
                }
                else
                {
                    // These are actual world ticks, projectile collisions and terrain
                    // updates. Stop shortly after the first carve to catch the blast.
                    for (int i = 0; i < 600 && shotMatch.World.TerrainOps.Count == oldCarves; i++)
                        yield return new WaitForEndOfFrame();
                    if (shotStage == "aftermath")
                        for (int i = 0; i < 50; i++) yield return new WaitForEndOfFrame();
                    else
                        for (int i = 0; i < 5; i++) yield return new WaitForEndOfFrame();
                }
            }
            if (CommandLineArg("-capture-vfx") == "explosion" &&
                FindAnyObjectByType<SandboxMatch>() is SandboxMatch match)
            {
                int x = match.World.Terrain.Width * 63 / 100;
                float y = match.World.SurfaceY(x) ?? match.World.Terrain.Height * 0.55f;
                var at = Render.WorldSpace.ToWorld(x, y) + Vector3.up * 0.8f;
                match.Presenter.Vfx.Explosion(at, 2.6f);
                for (int i = 0; i < 9; i++) yield return new WaitForEndOfFrame();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var capture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            capture.Apply();
            File.WriteAllBytes(path, capture.EncodeToPNG());
            Destroy(capture);
            Application.Quit();
        }
#endif
    }
}
