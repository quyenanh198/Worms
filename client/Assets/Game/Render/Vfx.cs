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

        ParticleSystem _fire, _smoke, _dirt, _sparks, _water, _trail;
        Material _alpha, _additive;
        Color _dirtColor = new Color(0.5f, 0.35f, 0.2f);
        readonly Vector4[] _scorch = new Vector4[MaxScorch];
        int _scorchNext, _scorchCount;
        readonly List<(LineRenderer line, float age)> _tracers = new List<(LineRenderer, float)>();
        readonly Stack<LineRenderer> _freeTracers = new Stack<LineRenderer>();

        static readonly int ScorchId = Shader.PropertyToID("_WormsScorch");
        static readonly int ScorchCountId = Shader.PropertyToID("_WormsScorchCount");

        public void Init(Color dirt)
        {
            _dirtColor = dirt;
            _alpha = Materials.Create("Worms/Particle", "Particles Alpha");
            _alpha.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _alpha.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _additive = Materials.Create("Worms/Particle", "Particles Additive");
            _additive.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _additive.SetFloat("_DstBlend", (float)BlendMode.One);
            _additive.renderQueue = 3100;

            _smoke = System("Smoke", _alpha, 0f, new[] { new Color(0.35f, 0.33f, 0.32f, 0.7f), new Color(0.6f, 0.6f, 0.6f, 0f) }, 0.6f, 1.8f);
            _dirt = System("Dirt", _alpha, 1.6f, new[] { Color.white, new Color(1, 1, 1, 0.9f) }, 1f, 0.8f);
            _fire = System("Fire", _additive, -0.05f, new[] { new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.45f, 0.1f, 0.8f), new Color(0.4f, 0.1f, 0.05f, 0f) }, 0.8f, 1.6f);
            _sparks = System("Sparks", _additive, 1.2f, new[] { new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.4f, 0.1f, 0f) }, 1f, 0.3f);
            _water = System("Water", _alpha, 1.4f, new[] { new Color(0.85f, 0.95f, 1f, 0.9f), new Color(0.7f, 0.85f, 0.95f, 0f) }, 1f, 0.7f);
            _trail = System("Trail", _alpha, -0.02f, new[] { new Color(0.9f, 0.9f, 0.9f, 0.55f), new Color(0.8f, 0.8f, 0.8f, 0f) }, 0.4f, 2.6f);
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

        int Count(int n) { return Mathf.Max(1, Mathf.RoundToInt(n * Density)); }

        /// <summary>Fireball, smoke, flying dirt and sparks; radius in world units.</summary>
        public void Explosion(Vector3 pos, float radius)
        {
            for (int i = 0; i < Count(18); i++)
                Emit(_fire, pos + Random.insideUnitSphere * radius * 0.3f, Random.insideUnitSphere * radius * 2.2f, radius * Random.Range(0.6f, 1.1f), Random.Range(0.25f, 0.5f), Color.white);
            for (int i = 0; i < Count(14); i++)
                Emit(_smoke, pos + Random.insideUnitSphere * radius * 0.5f, Random.insideUnitSphere * radius + Vector3.up * radius, radius * Random.Range(0.7f, 1.2f), Random.Range(1.2f, 2.2f), Color.white);
            for (int i = 0; i < Count(26); i++)
            {
                var dir = Random.insideUnitSphere + Vector3.up * 0.8f;
                var c = _dirtColor * Random.Range(0.6f, 1.1f);
                c.a = 1;
                Emit(_dirt, pos, dir * radius * Random.Range(3f, 7f), radius * Random.Range(0.08f, 0.2f), Random.Range(0.6f, 1.3f), c);
            }
            for (int i = 0; i < Count(20); i++)
                Emit(_sparks, pos, (Random.insideUnitSphere + Vector3.up * 0.5f) * radius * Random.Range(4f, 9f), radius * 0.08f, Random.Range(0.3f, 0.7f), Color.white);
            AddScorch(pos, radius);
        }

        public void Muzzle(Vector3 pos, Vector3 dir)
        {
            for (int i = 0; i < Count(6); i++)
                Emit(_fire, pos, dir * Random.Range(2f, 5f) + Random.insideUnitSphere, Random.Range(0.25f, 0.45f), 0.12f, Color.white);
            for (int i = 0; i < Count(5); i++)
                Emit(_smoke, pos, dir * Random.Range(0.5f, 1.5f) + Random.insideUnitSphere * 0.3f, Random.Range(0.3f, 0.5f), Random.Range(0.6f, 1f), Color.white);
        }

        public void Trail(Vector3 pos)
        {
            Emit(_trail, pos + Random.insideUnitSphere * 0.05f, Random.insideUnitSphere * 0.15f, Random.Range(0.18f, 0.3f), Random.Range(0.8f, 1.4f), Color.white);
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
                var c = _dirtColor;
                c.a = 0.6f;
                Emit(_smoke, pos, new Vector3(Random.Range(-1f, 1f), Random.Range(0.2f, 1f), 0), Random.Range(0.2f, 0.4f), Random.Range(0.4f, 0.8f), c);
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
        }
    }
}
