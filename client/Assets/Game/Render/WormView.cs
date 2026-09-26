using UnityEngine;
using Worms.Game.Core;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.Render
{
    /// <summary>
    /// One worm on screen: the procedural body (WormRig), eyes that follow
    /// the aim and blink, little hands and the weapon it holds, hit flash,
    /// tumbling spin and the sinking animation when it drowns.
    /// </summary>
    public sealed class WormView
    {
        static Mesh _eyeMesh, _handMesh;
        static Material _white, _black;
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        public readonly Transform Root;
        readonly Transform _body, _eyeL, _eyeR, _pupilL, _pupilR, _handL, _handR, _weaponPivot;
        readonly WormRig _rig = new WormRig();
        readonly MeshBuffers _buffers = new MeshBuffers();
        readonly MeshUtil _util = new MeshUtil();
        readonly Mesh _mesh;
        readonly MeshRenderer _renderer;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        Transform _prop;
        WeaponId _propId;
        float _spin, _flash, _blinkAt, _sinkT = -1;
        Vector3 _sinkFrom;

        public bool Sinking => _sinkT >= 0;

        public WormView(Transform parent, int id, Color teamColor)
        {
            if (_eyeMesh == null)
            {
                _eyeMesh = MeshUtil.Create(Shapes.Sphere(0.5f, 10, 14), "Eye");
                _handMesh = _eyeMesh;
                _white = Materials.Toon(new Color(0.97f, 0.97f, 0.97f));
                _black = Materials.Toon(new Color(0.05f, 0.05f, 0.07f));
            }
            Root = new GameObject("Worm " + id).transform;
            Root.SetParent(parent, false);
            _body = new GameObject("Body").transform;
            _body.SetParent(Root, false);
            _mesh = new Mesh { name = "Worm " + id };
            _mesh.MarkDynamic();
            _body.gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _body.gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = Materials.Toon(teamColor);

            _eyeL = Ball(_body, "EyeL", _white, 0.16f);
            _eyeR = Ball(_body, "EyeR", _white, 0.16f);
            _pupilL = Ball(_eyeL, "Pupil", _black, 0.5f);
            _pupilR = Ball(_eyeR, "Pupil", _black, 0.5f);
            var skin = _renderer.sharedMaterial;
            _handL = Ball(_body, "HandL", skin, 0.1f);
            _handR = Ball(_body, "HandR", skin, 0.1f);
            _weaponPivot = new GameObject("Weapon").transform;
            _weaponPivot.SetParent(_body, false);
            _blinkAt = Random.Range(1f, 4f);
        }

        static Transform Ball(Transform parent, string name, Material mat, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = _eyeMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        public void Flash() { _flash = 1f; }

        public void StartSinking(Vector3 at)
        {
            _sinkT = 0;
            _sinkFrom = at;
        }

        /// <param name="holding">This worm has the turn and aims: show the weapon.</param>
        public void Update(WormSnap w, Vector3 position, bool holding, WeaponId weapon, float aim, float dt)
        {
            if (Sinking)
            {
                _sinkT += dt;
                Root.gameObject.SetActive(_sinkT < 1.6f);
                Root.position = _sinkFrom + Vector3.down * (_sinkT * _sinkT * 0.8f);
                _body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_sinkT * 14f) * 12f);
                return;
            }

            Root.position = position;
            _rig.Update(new WormAnimInput { State = w.State, Aim = aim, Holding = holding, Vx = w.Vx, Vy = w.Vy, Time = Time.time }, dt);
            _rig.BuildMesh(_buffers, WorldSpace.Scale);
            _util.Apply(_buffers, _mesh);

            // Mirror to face left or right; spin while tumbling.
            if (w.State == WormState.Tumbling)
                _spin -= Mathf.Sign(w.Vx == 0 ? 1 : w.Vx) * w.Facing * new Vector2(w.Vx, w.Vy).magnitude * dt * 0.12f * Mathf.Rad2Deg;
            else
                _spin = Mathf.MoveTowardsAngle(_spin, 0, 900 * dt);
            _body.localScale = new Vector3(w.Facing >= 0 ? 1 : -1, 1, 1);
            _body.localRotation = Quaternion.Euler(0, 0, _spin);

            // Eyes on the front of the head, looking along the aim; blink now and then.
            _rig.HeadDirection(out float hx, out float hy);
            float s = WorldSpace.Scale;
            var head = new Vector3(_rig.HeadX * s, _rig.HeadY * s, 0);
            var up = new Vector3(hx, hy, 0);
            var side = new Vector3(hy, -hx, 0);
            float r = _rig.HeadR * s;
            _blinkAt -= dt;
            float blink = _blinkAt < 0.12f ? 0.15f : 1f;
            if (_blinkAt < 0) _blinkAt = Random.Range(2f, 5f);
            _eyeL.localPosition = head + up * r * 0.35f + side * r * 0.45f + Vector3.back * r * 0.75f;
            _eyeR.localPosition = head + up * r * 0.35f + side * r * 0.95f + Vector3.back * r * 0.35f;
            _eyeL.localScale = _eyeR.localScale = new Vector3(0.16f, 0.16f * blink, 0.16f);
            var look = new Vector3(Mathf.Cos(aim), Mathf.Sin(aim), -0.6f).normalized * 0.28f;
            _pupilL.localPosition = _pupilR.localPosition = look;

            // Weapon in hand (grenade or dynamite: only the item, no gun).
            bool show = holding && w.State != WormState.Tumbling;
            if (show && (_prop == null || _propId != weapon))
            {
                if (_prop != null) Object.Destroy(_prop.gameObject);
                _prop = WeaponProps.Build(weapon, _weaponPivot);
                _propId = weapon;
            }
            _weaponPivot.gameObject.SetActive(show);
            _handL.gameObject.SetActive(show);
            _handR.gameObject.SetActive(show);
            if (show)
            {
                var grip = head + side * r * 1.15f + up * (-r * 0.9f) + Vector3.back * r * 0.9f;
                _weaponPivot.localPosition = grip;
                _weaponPivot.localRotation = Quaternion.Euler(0, 0, aim * Mathf.Rad2Deg);
                _handL.localPosition = grip + _weaponPivot.localRotation * new Vector3(0.02f, -0.06f, -0.04f);
                _handR.localPosition = grip + _weaponPivot.localRotation * new Vector3(0.22f, -0.05f, -0.04f);
            }

            if (_flash > 0) _flash = Mathf.Max(0, _flash - dt * 4f);
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(FlashId, _flash);
            _renderer.SetPropertyBlock(_block);
        }

        public void Destroy()
        {
            Object.Destroy(Root.gameObject);
            Object.Destroy(_mesh);
        }
    }
}
