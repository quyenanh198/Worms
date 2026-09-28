using UnityEngine;
using Worms.Game.Audio;
using Worms.Game.Core;
using Worms.Protocol;
using Worms.Sim;
using SimTerrain = Worms.Sim.Terrain;

namespace Worms.Game.Render
{
    /// <summary>
    /// The living backdrop of the menu: the Worms squad on a little island. On open, the
    /// camera swoops in and the four worms drop from the sky one by one and land with a thud;
    /// then each shows off (bazooka, grenade, bat, happy hops) in store outfits while clouds
    /// drift. The leader wears the player's outfit, and the store dresses it up to preview items.
    /// </summary>
    public sealed class MenuScene : MonoBehaviour
    {
        public static MenuScene Instance { get; private set; }

        /// <summary>Seconds since the scene opened (the menu times its title to it).</summary>
        public float Age => Time.time - _start;
        /// <summary>When the last worm has landed.</summary>
        public const float SquadReady = 2.3f;

        /// <summary>What the leader wears; the menu sets it every frame (profile, or a store preview).</summary>
        public Loadout LeaderLoadout;
        /// <summary>Weapon the leader shows off (the store sets it to preview a weapon skin).</summary>
        public WeaponId? LeaderWeapon;

        const int Squad = 4;

        /// <summary>The squad shows off the store (the leader's slot is replaced by the player's own look).</summary>
        static readonly Loadout[] Showcase =
        {
            default,
            new Loadout { Hat = 5, Armor = 10, Grenade = 14 },      // cowboy, hero cape, watermelon grenade
            new Loadout { Hat = 6, Armor = 11, Bat = 16 },          // wizard, knight plate, spiked bat
            new Loadout { Hat = 3, Armor = 9, Bazooka = 12 },       // Rambo band, ninja belt, camo bazooka
        };
        const float DropHeight = 7f, Gravity = 22f;

        SimTerrain _terrain;
        Camera _cam;
        Vfx _vfx;
        readonly WormView[] _worms = new WormView[Squad];
        readonly Vector3[] _spots = new Vector3[Squad];
        readonly bool[] _landed = new bool[Squad];
        readonly Loadout[] _looks = new Loadout[Squad];
        Vector3 _focus;
        float _start;

        public static MenuScene Create()
        {
            var go = new GameObject("Menu Scene");
            var scene = go.AddComponent<MenuScene>();
            scene.Build();
            return scene;
        }

        void Build()
        {
            Instance = this;
            _start = Time.time;
            var theme = Theme.Meadow;
            var sun = SceneBuilder.CreateSun(transform, theme);
            SceneBuilder.ConfigureEnvironment(theme, sun);

            // A small grassy island: a soft mound with a gentle dip for the squad to stand in.
            _terrain = new SimTerrain(460, 220);
            for (int x = 0; x < _terrain.Width; x++)
            {
                float u = (x - 230f) / 150f;
                int top = Mathf.RoundToInt(150f - 55f * Mathf.Exp(-u * u) + 4f * Mathf.Sin(x * 0.05f));
                _terrain.FillRect(x, top, x + 1, _terrain.Height, true);
            }
            float waterY = -(_terrain.Height - 30) * WorldSpace.Scale;
            var terrainView = new GameObject("Island").AddComponent<TerrainView>();
            terrainView.transform.SetParent(transform, false);
            terrainView.Init(_terrain, Materials.Terrain(theme));
            SceneBuilder.CreateGroundFoliage(transform, _terrain, menuComposition: true);
            float width = _terrain.Width * WorldSpace.Scale;
            // The battle's near procedural hills cover the painted cliffs at this tighter menu camera.
            SceneBuilder.CreateBackdrop(transform, theme, 7u, width, waterY, menuComposition: true);
            SceneBuilder.CreateWater(transform, theme, width, waterY);

            _vfx = new GameObject("Vfx").AddComponent<Vfx>();
            _vfx.transform.SetParent(transform, false);
            _vfx.Init(theme.Dirt);

            // The squad stands on the crest, in a row.
            for (int i = 0; i < Squad; i++)
            {
                int x = 175 + i * 36;
                int top = 0;
                while (top < _terrain.Height && !_terrain.IsSolid(x, top)) top++;
                _spots[i] = WorldSpace.ToWorld(x, top - C.WormRadius);
                _worms[i] = new WormView(transform, 900 + i, i, TeamColors.Of(i));
                _looks[i] = Showcase[i];
                _worms[i].SetLoadout(_looks[i]);
            }
            _focus = (_spots[0] + _spots[Squad - 1]) / 2f + Vector3.up * 0.4f;

            _cam = new GameObject("Menu Camera").AddComponent<Camera>();
            _cam.transform.SetParent(transform, false);
            _cam.fieldOfView = 35f;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 600f;
            _cam.clearFlags = CameraClearFlags.Skybox;
            UrpSetup.ConfigureCamera(_cam);
            UrpSetup.CreateVolume(transform);
        }

        /// <summary>Where to draw squad member <paramref name="i"/>'s name (GUI space, y down); false while it has not landed.</summary>
        public bool TryGetLabel(int i, out Vector2 gui)
        {
            gui = default;
            if (_cam == null || i < 0 || i >= Squad || !_landed[i]) return false;
            var sp = _cam.WorldToScreenPoint(_worms[i].Root.position + Vector3.up * 1.35f);
            if (sp.z <= 0) return false;
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float t = Age, dt = Time.deltaTime;
            _worms[0].SetLoadout(LeaderLoadout);

            for (int i = 0; i < Squad; i++) Animate(i, t, dt);
            MoveCamera(t);

        }

        void Animate(int i, float t, float dt)
        {
            float dropAt = 0.5f + 0.4f * i;
            var snap = new WormSnap { Id = 900 + i, Team = i, Hp = 100, Facing = i < 2 ? 1 : -1, State = WormState.Idle };
            var pos = _spots[i];
            bool holding = false;
            var weapon = WeaponId.Bazooka;
            float aim = 0.2f;

            if (t < dropAt)
            {
                _worms[i].Root.gameObject.SetActive(false);
                return;
            }
            _worms[i].Root.gameObject.SetActive(true);
            float fall = t - dropAt;
            float height = DropHeight - 0.5f * Gravity * fall * fall;
            if (height > 0)
            {
                // Falling: stretched, tail flapping.
                pos += Vector3.up * height;
                snap.State = WormState.Airborne;
                snap.Vy = Gravity * fall / WorldSpace.Scale;
            }
            else
            {
                if (!_landed[i])
                {
                    _landed[i] = true;
                    _vfx.Dust(_spots[i] + Vector3.down * 0.35f, 3f);
                    AudioManager.Instance?.Play(Sfx.Land, (_spots[i].x - _cam.transform.position.x) / 6f, 0.8f);
                }
                if (t > SquadReady + 0.3f)
                {
                    switch (i)
                    {
                        case 0: // the leader shoulders a bazooka and scans the sky
                            holding = true;
                            weapon = LeaderWeapon ?? WeaponId.Bazooka;
                            aim = 0.35f + 0.28f * Mathf.Sin(t * 0.9f);
                            break;
                        case 1: // tosses a grenade from hand to hand
                            holding = true;
                            weapon = WeaponId.Grenade;
                            aim = 0.8f + 0.35f * Mathf.Sin(t * 2.6f);
                            break;
                        case 2: // practice swings
                            holding = true;
                            weapon = WeaponId.BaseballBat;
                            aim = 0.5f + 0.6f * Mathf.Sin(t * 1.8f);
                            break;
                        default: // happy hops
                            float phase = Mathf.Repeat(t, 2.4f);
                            if (phase < 0.5f)
                            {
                                float hop = Mathf.Sin(phase / 0.5f * Mathf.PI);
                                pos += Vector3.up * hop * 0.45f;
                                snap.State = WormState.Airborne;
                                snap.Vy = Mathf.Cos(phase / 0.5f * Mathf.PI) * -260f;
                            }
                            break;
                    }
                }
            }
            snap.X = pos.x / WorldSpace.Scale;
            snap.Y = -pos.y / WorldSpace.Scale;
            _worms[i].Update(snap, pos, holding, weapon, aim, dt);
        }

        void MoveCamera(float t)
        {
            // Swoop in from high and wide, then drift gently. The squad sits right of center,
            // clear of the menu buttons.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 2.4f));
            float distance = Mathf.Lerp(26f, 14f, k);
            float aspect = Mathf.Max(1f, _cam.aspect);
            float halfWidth = distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;
            var look = _focus + Vector3.left * halfWidth * 0.5f + Vector3.up * Mathf.Lerp(3f, 0.9f, k);
            var sway = new Vector3(Mathf.Sin(t * 0.21f) * 0.6f, Mathf.Sin(t * 0.33f) * 0.25f, 0);
            _cam.transform.position = look + sway + new Vector3(0, distance * 0.12f, -distance);
            _cam.transform.rotation = Quaternion.Euler(6f + 8f * (1f - k), Mathf.Sin(t * 0.17f) * 2f, 0);
        }
    }
}
