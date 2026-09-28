using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Worms.Game.Render
{
    /// <summary>
    /// All transient effects (docs/PLAN.md §3.9-3.10): pooled particle
    /// systems fed with EmitParams, tracers, scorch marks for the terrain
    /// shader and floating damage numbers for the HUD. Particles use Shuriken
    /// only (no VFX Graph) so they run on WebGL2 too.
    /// </summary>
    public sealed class Vfx : MonoBehaviour
    {
        public struct Popup
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Age;
        }

        public const float PopupSeconds = 1.4f;
        const int MaxScorch = 32;

        public readonly List<Popup> Popups = new List<Popup>();
        /// <summary>Particle count multiplier for the quality tier.</summary>
        public float Density = 1f;

        ParticleSystem _fire, _smoke, _earth, _dirt, _rock, _sparks, _water, _trail;
        Material _alpha, _additive, _smokeMaterial, _earthMaterial, _clodMaterial, _debrisMaterial;
        Color _dirtColor = new Color(0.5f, 0.35f, 0.2f);
        readonly Vector4[] _scorch = new Vector4[MaxScorch];
        int _scorchNext, _scorchCount;
        readonly List<(LineRenderer line, float age)> _tracers = new List<(LineRenderer, float)>();
        readonly Stack<LineRenderer> _freeTracers = new Stack<LineRenderer>();
        sealed class BurstSprite
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
            public float Age;
            public float Radius;
        }
        static Mesh _burstQuad;
        Material _burstMaterial;
        readonly List<BurstSprite> _activeBursts = new List<BurstSprite>();
        readonly Stack<BurstSprite> _freeBursts = new Stack<BurstSprite>();

        static readonly int ScorchId = Shader.PropertyToID("_WormsScorch");
        static readonly int ScorchCountId = Shader.PropertyToID("_WormsScorchCount");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        public void Init(Color dirt, bool rockyTerrain = true, bool beachSoil = false)
        {
            _dirtColor = dirt;
            _alpha = Materials.Create("Worms/Particle", "Particles Alpha");
            _alpha.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _alpha.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            var smokeTexture = Resources.Load<Texture2D>("VFX/smoke-puff");
            if (smokeTexture != null)
            {
                _smokeMaterial = new Material(_alpha) { name = "Painted smoke puff" };
                _smokeMaterial.SetTexture("_MainTex", smokeTexture);
                // Crop the transparent canvas so a particle fills its billboard.
                _smokeMaterial.SetTextureScale("_MainTex", new Vector2(0.72f, 0.72f));
                _smokeMaterial.SetTextureOffset("_MainTex", new Vector2(0.14f, 0.14f));
            }
            var earthTexture = Resources.Load<Texture2D>("VFX/earth-dust");
            if (earthTexture != null)
            {
                _earthMaterial = new Material(_alpha) { name = "Painted earth dust" };
                _earthMaterial.SetTexture("_MainTex", earthTexture);
                _earthMaterial.SetFloat("_RadialFade", 0f);
                _earthMaterial.SetTextureScale("_MainTex", new Vector2(0.84f, 0.84f));
                _earthMaterial.SetTextureOffset("_MainTex", new Vector2(0.08f, 0.08f));
            }
            var debrisTexture = Resources.Load<Texture2D>("VFX/rock-debris");
            if (debrisTexture != null)
            {
                _debrisMaterial = new Material(_alpha) { name = "Painted rock debris" };
                _debrisMaterial.SetTexture("_MainTex", debrisTexture);
                _debrisMaterial.SetFloat("_RadialFade", 0f);
                _debrisMaterial.SetTextureScale("_MainTex", new Vector2(0.54f, 0.54f));
                _debrisMaterial.SetTextureOffset("_MainTex", new Vector2(0.23f, 0.23f));
            }
            var clodTexture = Resources.Load<Texture2D>(beachSoil ? "VFX/sand-clod" : "VFX/earth-clod");
            if (clodTexture == null) clodTexture = Resources.Load<Texture2D>("VFX/earth-clod");
            if (clodTexture != null)
            {
                _clodMaterial = new Material(_alpha) { name = "Painted earth clod" };
                _clodMaterial.SetTexture("_MainTex", clodTexture);
                _clodMaterial.SetFloat("_RadialFade", 0f);
                _clodMaterial.SetTextureScale("_MainTex", new Vector2(0.56f, 0.56f));
                _clodMaterial.SetTextureOffset("_MainTex", new Vector2(0.22f, 0.22f));
            }
            _additive = Materials.Create("Worms/Particle", "Particles Additive");
            _additive.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _additive.SetFloat("_DstBlend", (float)BlendMode.One);
            _additive.renderQueue = 3100;

            _smoke = System("Smoke", _smokeMaterial != null ? _smokeMaterial : _alpha, 0f, new[] { new Color(0.35f, 0.33f, 0.32f, 0.55f), new Color(0.6f, 0.6f, 0.6f, 0f) }, 0.6f, 1.8f);
            _earth = System("Earth dust", _earthMaterial != null ? _earthMaterial : _smokeMaterial != null ? _smokeMaterial : _alpha, 0.55f,
                new[] { Color.white, new Color(1f, 1f, 1f, 0f) }, 0.9f, 1.25f);
            _dirt = System("Dirt clods", _clodMaterial != null ? _clodMaterial : _alpha, 1.6f, new[] { Color.white, new Color(1, 1, 1, 0.9f) }, 1f, 0.8f);
            if (rockyTerrain && _debrisMaterial != null)
                _rock = System("Rock shards", _debrisMaterial, 1.6f, new[] { Color.white, new Color(1, 1, 1, 0.9f) }, 1f, 0.8f);
            _fire = System("Fire", _additive, -0.05f, new[] { new Color(1f, 0.76f, 0.26f, 0.85f), new Color(1f, 0.31f, 0.07f, 0.68f), new Color(0.4f, 0.1f, 0.05f, 0f) }, 0.75f, 1.35f);
            _sparks = System("Sparks", _additive, 1.2f, new[] { new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.4f, 0.1f, 0f) }, 1f, 0.3f);
            _water = System("Water", _alpha, 1.4f, new[] { new Color(0.85f, 0.95f, 1f, 0.9f), new Color(0.7f, 0.85f, 0.95f, 0f) }, 1f, 0.7f);
            _trail = System("Trail", _smokeMaterial != null ? _smokeMaterial : _alpha, -0.02f, new[] { new Color(0.97f, 0.94f, 0.87f, 0.88f), new Color(0.8f, 0.82f, 0.84f, 0f) }, 0.65f, 2.1f);
            var burstTexture = Resources.Load<Texture2D>("VFX/explosion-burst");
            if (burstTexture != null)
                _burstMaterial = Materials.BackdropSprite(burstTexture, "Painted explosion burst", 1f,
                    alphaThreshold: 0.04f);
            Shader.SetGlobalVectorArray(ScorchId, _scorch);
            Shader.SetGlobalFloat(ScorchCountId, 0);
        }

        ParticleSystem System(string name, Material mat, float gravity, Color[] colors, float sizeStart, float sizeEnd)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = 1500;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var gradient = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            var ak = new GradientAlphaKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float t = colors.Length == 1 ? 0 : (float)i / (colors.Length - 1);
                ck[i] = new GradientColorKey(colors[i], t);
                ak[i] = new GradientAlphaKey(colors[i].a, t);
            }
            gradient.SetKeys(ck, ak);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, sizeStart, 1, sizeEnd));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            if (name == "Smoke") r.sortingOrder = 1;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = color,
                applyShapeToPosition = false,
            };
            ps.Emit(p, 1);
        }

        int Count(int n) { return Mathf.Max(1, Mathf.RoundToInt(n * Density * (QualitySettingsManager.ReducedMotion ? 0.5f : 1f))); }

        /// <summary>Fireball, smoke, flying dirt and sparks; radius in world units.</summary>
        public void Explosion(Vector3 pos, float radius)
        {
            PaintedBurst(pos, radius);
            float visualRadius = radius * 1.35f;
            for (int i = 0; i < Count(8); i++)
                Emit(_fire, pos + Random.insideUnitSphere * visualRadius * 0.25f, Random.insideUnitSphere * visualRadius * 1.8f,
                    visualRadius * Random.Range(0.3f, 0.6f), Random.Range(0.3f, 0.55f), Color.white);
            for (int i = 0; i < Count(10); i++)
                Emit(_smoke, pos + Random.insideUnitSphere * visualRadius * 0.65f,
                    Random.insideUnitSphere * visualRadius + Vector3.up * visualRadius,
                    visualRadius * Random.Range(0.35f, 0.65f), Random.Range(1.2f, 2.2f),
                    new Color(0.85f, 0.78f, 0.72f, 0.85f));
            for (int i = 0; i < Count(10); i++)
            {
                var c = _earthMaterial != null ? Color.white : _dirtColor * Random.Range(0.72f, 1.12f);
                c.a = 0.75f;
                Emit(_earth, pos + Random.insideUnitSphere * visualRadius * 0.35f,
                    (Random.insideUnitSphere + Vector3.up * 0.5f) * radius * Random.Range(0.9f, 2.1f),
                    radius * Random.Range(0.2f, 0.4f), Random.Range(0.45f, 0.9f), c);
            }
            for (int i = 0; i < Count(_rock != null ? 14 : 18); i++)
            {
                var dir = Random.insideUnitSphere + Vector3.up * 0.8f;
                var c = (_clodMaterial != null ? Color.white : _dirtColor) * Random.Range(0.72f, 1.06f);
                c.a = 1;
                Emit(_dirt, pos, dir * radius * Random.Range(3f, 7f), radius * Random.Range(0.08f, 0.2f), Random.Range(0.6f, 1.3f), c);
            }
            if (_rock != null)
                for (int i = 0; i < Count(4); i++)
                    Emit(_rock, pos, (Random.insideUnitSphere + Vector3.up * 0.8f) * radius * Random.Range(3f, 7f),
                        radius * Random.Range(0.08f, 0.2f), Random.Range(0.6f, 1.3f), Color.white);
            for (int i = 0; i < Count(20); i++)
                Emit(_sparks, pos, (Random.insideUnitSphere + Vector3.up * 0.5f) * radius * Random.Range(4f, 9f), radius * 0.08f, Random.Range(0.3f, 0.7f), Color.white);
            AddScorch(pos, radius);
        }

        void PaintedBurst(Vector3 pos, float radius)
        {
            if (_burstMaterial == null || _activeBursts.Count >= 16) return;
            var burst = _freeBursts.Count > 0 ? _freeBursts.Pop() : NewBurstSprite();
            burst.Root.gameObject.SetActive(true);
            burst.Root.position = new Vector3(pos.x, pos.y - radius * 0.12f, -0.18f);
            burst.Age = 0f;
            burst.Radius = radius;
            burst.Block.SetFloat(OpacityId, 1f);
            burst.Renderer.SetPropertyBlock(burst.Block);
            _activeBursts.Add(burst);
        }

        BurstSprite NewBurstSprite()
        {
            if (_burstQuad == null)
            {
                _burstQuad = new Mesh
                {
                    name = "Explosion cutout quad",
                    vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(-0.5f, 1, 0),
                        new Vector3(0.5f, 1, 0), new Vector3(0.5f, 0, 0) },
                    uv = new[] { new Vector2(0, 0), new Vector2(0, 1),
                        new Vector2(1, 1), new Vector2(1, 0) },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                };
                _burstQuad.RecalculateBounds();
            }
            var go = new GameObject("Painted explosion");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _burstQuad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _burstMaterial;
            renderer.sortingOrder = 5;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new BurstSprite { Root = go.transform, Renderer = renderer };
        }

        public void Muzzle(Vector3 pos, Vector3 dir)
        {
            for (int i = 0; i < Count(6); i++)
                Emit(_fire, pos, dir * Random.Range(2f, 5f) + Random.insideUnitSphere, Random.Range(0.25f, 0.45f), 0.12f, Color.white);
            for (int i = 0; i < Count(5); i++)
                Emit(_smoke, pos, dir * Random.Range(0.5f, 1.5f) + Random.insideUnitSphere * 0.3f, Random.Range(0.3f, 0.5f), Random.Range(0.6f, 1f), Color.white);
        }

        /// <summary>Burning napalm: licking flames and a little black smoke (call every frame while it burns).</summary>
        public void Flame(Vector3 pos)
        {
            for (int i = 0; i < 2; i++)
                if (Random.value < 0.8f * Density)
                    Emit(_fire, pos + new Vector3(Random.Range(-0.12f, 0.12f), 0.05f, Random.Range(-0.06f, 0.06f)),
                        new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.9f, 1.8f), 0), Random.Range(0.22f, 0.38f), Random.Range(0.3f, 0.55f), Color.white);
            if (Random.value < 0.12f * Density)
                Emit(_smoke, pos + Vector3.up * 0.25f, new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0.4f, 0.8f), 0), Random.Range(0.18f, 0.3f), Random.Range(0.8f, 1.3f), new Color(0.25f, 0.22f, 0.2f, 1f));
        }

        public void Trail(Vector3 pos)
        {
            for (int i = 0; i < Count(2); i++)
                Emit(_trail, pos + Random.insideUnitSphere * 0.08f, Random.insideUnitSphere * 0.18f,
                    Random.Range(0.38f, 0.56f), Random.Range(1.0f, 1.7f), Color.white);
        }

        public void Splash(Vector3 pos)
        {
            for (int i = 0; i < Count(30); i++)
                Emit(_water, pos, new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 8f), Random.Range(-1f, 1f)), Random.Range(0.1f, 0.3f), Random.Range(0.6f, 1.1f), Color.white);
        }

        public void Dust(Vector3 pos, float amount)
        {
            for (int i = 0; i < Count(Mathf.RoundToInt(4 * amount)); i++)
            {
                var c = _earthMaterial != null ? Color.white : _dirtColor;
                c.a = 0.6f;
                Emit(_earth, pos, new Vector3(Random.Range(-1f, 1f), Random.Range(0.2f, 1f), 0), Random.Range(0.2f, 0.4f), Random.Range(0.4f, 0.8f), c);
            }
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            var line = _freeTracers.Count > 0 ? _freeTracers.Pop() : NewTracer();
            line.gameObject.SetActive(true);
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            _tracers.Add((line, 0f));
            for (int i = 0; i < Count(6); i++)
                Emit(_sparks, to, Random.insideUnitSphere * 4f, 0.08f, Random.Range(0.15f, 0.35f), Color.white);
        }

        LineRenderer NewTracer()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.sharedMaterial = _additive;
            line.startWidth = 0.06f;
            line.endWidth = 0.03f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            return line;
        }

        public void AddPopup(Vector3 world, string text, Color color)
        {
            Popups.Add(new Popup { World = world, Text = text, Color = color });
        }

        void AddScorch(Vector3 pos, float radius)
        {
            _scorch[_scorchNext] = new Vector4(pos.x, pos.y, radius, 0.85f);
            _scorchNext = (_scorchNext + 1) % MaxScorch;
            _scorchCount = Mathf.Min(MaxScorch, _scorchCount + 1);
            Shader.SetGlobalVectorArray(ScorchId, _scorch);
            Shader.SetGlobalFloat(ScorchCountId, _scorchCount);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _activeBursts.Count - 1; i >= 0; i--)
            {
                var burst = _activeBursts[i];
                burst.Age += dt;
                if (burst.Age >= 0.65f)
                {
                    burst.Root.gameObject.SetActive(false);
                    _freeBursts.Push(burst);
                    _activeBursts.RemoveAt(i);
                    continue;
                }
                float growth = Mathf.Lerp(0.85f, 1.12f, burst.Age / 0.65f);
                burst.Root.localScale = new Vector3(burst.Radius * 3.2f * growth,
                    burst.Radius * 2.8f * growth, 1f);
                burst.Block.SetFloat(OpacityId, Mathf.Clamp01((0.65f - burst.Age) / 0.45f));
                burst.Renderer.SetPropertyBlock(burst.Block);
            }
            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                var p = Popups[i];
                p.Age += dt;
                if (p.Age > PopupSeconds) Popups.RemoveAt(i);
                else Popups[i] = p;
            }
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                var (line, age) = _tracers[i];
                age += dt;
                if (age > 0.12f)
                {
                    line.gameObject.SetActive(false);
                    _freeTracers.Push(line);
                    _tracers.RemoveAt(i);
                    continue;
                }
                var c = new Color(1f, 0.9f, 0.6f, 1f - age / 0.12f);
                line.startColor = c;
                line.endColor = c;
                _tracers[i] = (line, age);
            }
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat(ScorchCountId, 0);
            if (_alpha != null) Destroy(_alpha);
            if (_additive != null) Destroy(_additive);
            if (_smokeMaterial != null) Destroy(_smokeMaterial);
            if (_earthMaterial != null) Destroy(_earthMaterial);
            if (_debrisMaterial != null) Destroy(_debrisMaterial);
            if (_burstMaterial != null) Destroy(_burstMaterial);
        }
    }
}
