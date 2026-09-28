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
        static Mesh _paintedQuad;
        static Material _paintedMaterial;
        static Material _blinkMaterial;
        static Material _aimMaterial;
        static Material _hurtMaterial;
        static Material _airborneMaterial;
        static Material[] _walkMaterials;
        static Material _white, _black, _mouthMat, _cheekMat;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int TeamColorId = Shader.PropertyToID("_TeamColor");

        public readonly Transform Root;
        readonly Transform _body, _eyeL, _eyeR, _pupilL, _pupilR, _handL, _handR, _weaponPivot, _mouth, _cheek;
        readonly WormRig _rig = new WormRig();
        readonly MeshBuffers _buffers = new MeshBuffers();
        readonly MeshUtil _util = new MeshUtil();
        readonly Mesh _mesh;
        readonly MeshRenderer _renderer;
        readonly MeshRenderer _paintedRenderer;
        readonly Transform _paintedSprite;
        readonly Color _teamColor;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        Transform _prop;
        WeaponId _propId;
        byte _propSkin;
        Loadout _loadout;
        Transform _hat, _armor, _cloth;
        int _armorPoint;
        float _spin, _flash, _hurtPoseTime, _blinkAt, _sinkT = -1;
        Vector3 _sinkFrom;
        readonly float _paintedScale;

        public bool Sinking => _sinkT >= 0;
        public bool UsesPaintedArt => _paintedSprite != null;

        public WormView(Transform parent, int id, int team, Color teamColor, float paintedScale = 1f)
        {
            _paintedScale = paintedScale;
            _teamColor = teamColor;
            if (_eyeMesh == null)
            {
                _eyeMesh = MeshUtil.Create(Shapes.Sphere(0.5f, 10, 14), "Eye");
                _handMesh = _eyeMesh;
                _white = Materials.Toon(new Color(0.97f, 0.97f, 0.97f));
                _black = Materials.Toon(new Color(0.05f, 0.05f, 0.07f));
                _mouthMat = Materials.Toon(new Color(0.32f, 0.07f, 0.09f));
                _cheekMat = Materials.Toon(new Color(1f, 0.55f, 0.6f));
            }
            Root = new GameObject("Worm " + id + " team " + team).transform;
            Root.SetParent(parent, false);
            _body = new GameObject("Body").transform;
            _body.SetParent(Root, false);
            _mesh = new Mesh { name = "Worm " + id };
            _mesh.MarkDynamic();
            _body.gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _body.gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = Materials.Toon(teamColor, segments: true);

            if (_walkMaterials == null)
            {
                _walkMaterials = new Material[2];
                // A single source-space crop keeps idle and aim poses at the same scale.
                // Source dimensions are used because Unity can downsample on WebGL/mobile.
                var crop = new Rect(50, 60, 1200, 1140);
                var idle = Resources.Load<Texture2D>("Characters/worm-red");
                var blink = Resources.Load<Texture2D>("Characters/worm-red-blink");
                var aim = Resources.Load<Texture2D>("Characters/worm-red-aim");
                var hurt = Resources.Load<Texture2D>("Characters/worm-red-hurt");
                var airborne = Resources.Load<Texture2D>("Characters/worm-red-airborne");
                var walkA = Resources.Load<Texture2D>("Characters/worm-red-walk-a");
                var walkB = Resources.Load<Texture2D>("Characters/worm-red-walk-b");
                if (idle != null) _paintedMaterial = PaintedMaterial(idle, "Worm idle", crop, 1263, 1246);
                if (blink != null) _blinkMaterial = PaintedMaterial(blink, "Worm blink", crop, 1263, 1246);
                if (aim != null) _aimMaterial = PaintedMaterial(aim, "Worm aim", crop, 1263, 1246);
                if (hurt != null) _hurtMaterial = PaintedMaterial(hurt, "Worm hurt", crop, 1263, 1246);
                if (airborne != null) _airborneMaterial = PaintedMaterial(airborne, "Worm airborne", crop, 1263, 1246);
                if (walkA != null) _walkMaterials[0] = PaintedMaterial(walkA, "Worm walk A", crop, 1263, 1246);
                if (walkB != null) _walkMaterials[1] = PaintedMaterial(walkB, "Worm walk B", crop, 1263, 1246);
            }
            if (_paintedMaterial != null)
            {
                if (_paintedQuad == null)
                {
                    _paintedQuad = new Mesh
                    {
                        name = "Painted worm quad",
                        vertices = new[]
                        {
                            new Vector3(-0.72f, -0.45f, -0.03f),
                            new Vector3(-0.72f, 1.20f, -0.03f),
                            new Vector3(0.72f, 1.20f, -0.03f),
                            new Vector3(0.72f, -0.45f, -0.03f),
                        },
                        uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
                        triangles = new[] { 0, 1, 2, 0, 2, 3 },
                    };
                    _paintedQuad.RecalculateBounds();
                }
                _paintedSprite = new GameObject("Painted worm").transform;
                _paintedSprite.SetParent(_body, false);
                _paintedSprite.gameObject.AddComponent<MeshFilter>().sharedMesh = _paintedQuad;
                _paintedRenderer = _paintedSprite.gameObject.AddComponent<MeshRenderer>();
                _paintedRenderer.sharedMaterial = _paintedMaterial;
                _block.SetColor(TeamColorId, _teamColor);
                _paintedRenderer.SetPropertyBlock(_block);
                _paintedRenderer.sortingOrder = 10;
                _paintedRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _paintedRenderer.receiveShadows = false;
                _renderer.enabled = false;
            }

            _eyeL = Ball(_body, "EyeL", _white, 0.16f);
            _eyeR = Ball(_body, "EyeR", _white, 0.16f);
            _pupilL = Ball(_eyeL, "Pupil", _black, 0.5f);
            _pupilR = Ball(_eyeR, "Pupil", _black, 0.5f);
            // A glint in each eye makes the face read as alive.
            foreach (var pupil in new[] { _pupilL, _pupilR })
            {
                var glint = Ball(pupil, "Glint", _white, 0.38f);
                glint.localPosition = new Vector3(-0.22f, 0.26f, -0.42f);
            }
            _mouth = Ball(_body, "Mouth", _mouthMat, 0.1f);
            _cheek = Ball(_body, "Cheek", _cheekMat, 0.1f);
            if (_paintedSprite != null)
            {
                _eyeL.gameObject.SetActive(false);
                _eyeR.gameObject.SetActive(false);
                _mouth.gameObject.SetActive(false);
                _cheek.gameObject.SetActive(false);
            }
            var skin = _renderer.sharedMaterial;
            _handL = Ball(_body, "HandL", skin, 0.1f);
            _handR = Ball(_body, "HandR", skin, 0.1f);
            _weaponPivot = new GameObject("Weapon").transform;
            _weaponPivot.SetParent(_body, false);
            _blinkAt = Random.Range(1f, 4f);
        }

        static Material PaintedMaterial(Texture2D texture, string name, Rect crop, int width, int height)
        {
            var material = Materials.BackdropSprite(texture, name, 1f);
            material.SetFloat("_RecolorStrength", 1f);
            material.SetTextureScale("_MainTex", new Vector2(crop.width / width, crop.height / height));
            material.SetTextureOffset("_MainTex", new Vector2(crop.x / width,
                (height - crop.yMax) / height));
            return material;
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

        public void Flash() { _flash = 1f; _hurtPoseTime = 0.45f; }

        /// <summary>Puts on a set of store cosmetics (replacing what the worm wore).</summary>
        public void SetLoadout(Loadout loadout)
        {
            bool hatChanged = _hat == null ? loadout.Hat != 0 : loadout.Hat != _loadout.Hat;
            bool armorChanged = _armor == null ? loadout.Armor != 0 : loadout.Armor != _loadout.Armor;
            _loadout = loadout;
            if (hatChanged)
            {
                if (_hat != null) Object.Destroy(_hat.gameObject);
                _hat = CosmeticProps.BuildHat(loadout.Hat, _body);
            }
            if (armorChanged)
            {
                if (_armor != null) Object.Destroy(_armor.gameObject);
                _armor = CosmeticProps.BuildArmor(loadout.Armor, _body);
                _armorPoint = CosmeticProps.ArmorSpinePoint(loadout.Armor);
                _cloth = _armor != null ? _armor.Find("Cloth") : null;
            }
            _propSkin = 255; // rebuild the held weapon with the new skin
        }

        static byte SkinFor(Loadout l, WeaponId weapon)
        {
            switch (weapon)
            {
                case WeaponId.Bazooka: return l.Bazooka;
                case WeaponId.Grenade: return l.Grenade;
                case WeaponId.BaseballBat: return l.Bat;
                default: return 0;
            }
        }

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
            if (_paintedSprite == null)
            {
                _rig.BuildMesh(_buffers, WorldSpace.Scale);
                _util.Apply(_buffers, _mesh);
            }

            // Mirror to face left or right; spin while tumbling.
            if (w.State == WormState.Tumbling)
                _spin -= Mathf.Sign(w.Vx == 0 ? 1 : w.Vx) * w.Facing * new Vector2(w.Vx, w.Vy).magnitude * dt * 0.12f * Mathf.Rad2Deg;
            else
                _spin = Mathf.MoveTowardsAngle(_spin, 0, 900 * dt);
            float visualScale = _paintedSprite != null ? _paintedScale : 1f;
            _body.localScale = new Vector3((w.Facing >= 0 ? 1 : -1) * visualScale, visualScale, 1);
            // The sprite grows upward from its original foot line; simulation stays put.
            _body.localPosition = Vector3.up * ((visualScale - 1f) * 0.45f);
            _body.localRotation = Quaternion.Euler(0, 0, _spin);
            if (_paintedSprite != null)
            {
                var hurt = (w.State == WormState.Tumbling || _hurtPoseTime > 0f) && _hurtMaterial != null;
                bool walking = w.State == WormState.Walking && _walkMaterials[0] != null
                    && _walkMaterials[1] != null;
                int walkFrame = Mathf.FloorToInt(Time.time * 7f) & 1;
                bool activeWalk = walking && !hurt;
                var pose = hurt ? _hurtMaterial
                    : activeWalk ? _walkMaterials[walkFrame]
                    : w.State == WormState.Airborne && _airborneMaterial != null
                        ? _airborneMaterial
                    : holding && w.State != WormState.Tumbling && _aimMaterial != null
                        ? _aimMaterial
                    : _blinkAt < 0.12f && _blinkMaterial != null
                        ? _blinkMaterial : _paintedMaterial;
                if (_paintedRenderer.sharedMaterial != pose) _paintedRenderer.sharedMaterial = pose;
                float footOffset = activeWalk ? (walkFrame == 0 ? -0.11f : -0.038f) : 0f;
                float bob = activeWalk ? 0.012f * Mathf.Sin(Time.time * 14f) : 0f;
                _paintedSprite.localPosition = Vector3.up * (footOffset + bob);
                _paintedSprite.localScale = Vector3.one;
            }

            // Eyes on the front of the head, looking along the aim; blink now and then.
            _rig.HeadDirection(out float hx, out float hy);
            float s = WorldSpace.Scale;
            var head = new Vector3(_rig.HeadX * s, _rig.HeadY * s, 0);
            // The face follows the head only partly, so it stays upright and readable.
            var up = Vector3.Slerp(Vector3.up, new Vector3(hx, hy, 0), 0.35f).normalized;
            var side = new Vector3(up.y, -up.x, 0);
            float r = _rig.HeadR * s;
            if (_paintedSprite != null)
            {
                // The cutout's oversized face sits right of the old spine head.
                head += new Vector3(0.24f, 0.04f, 0);
                r = 0.46f;
            }
            _blinkAt -= dt;
            float blink = _blinkAt < 0.12f ? 0.15f : 1f;
            if (_blinkAt < 0) _blinkAt = Random.Range(2f, 5f);
            // Big eyes on the side of the head that faces the camera, turned a little toward
            // the way the worm looks, so the face reads from the playing view.
            _eyeL.localPosition = head + up * r * 0.28f + side * r * 0.12f + Vector3.back * r * 0.86f;
            _eyeR.localPosition = head + up * r * 0.28f + side * r * 0.66f + Vector3.back * r * 0.66f;
            _eyeL.localScale = _eyeR.localScale = new Vector3(0.2f, 0.22f * blink, 0.2f);
            var look = new Vector3(Mathf.Cos(aim), Mathf.Sin(aim), -0.6f).normalized * 0.28f;
            _pupilL.localPosition = _pupilR.localPosition = look;

            // Mouth: a small smile, a round "O" when hit or tumbling. A rosy cheek on the near side.
            bool shocked = _flash > 0.05f || w.State == WormState.Tumbling;
            _mouth.localPosition = head - up * r * 0.36f + side * r * 0.45f + Vector3.back * r * 0.86f;
            _mouth.localRotation = Quaternion.LookRotation(Vector3.forward, up);
            _mouth.localScale = shocked ? new Vector3(0.07f, 0.08f, 0.04f) : new Vector3(0.12f, 0.035f, 0.04f);
            _cheek.localPosition = head - up * r * 0.12f + side * r * 0.86f + Vector3.back * r * 0.52f;
            _cheek.localRotation = _mouth.localRotation;
            _cheek.localScale = new Vector3(0.08f, 0.05f, 0.03f);

            // Cosmetics ride on the head and the spine, scaled by their radii.
            if (_hat != null)
            {
                _hat.localPosition = head;
                _hat.localRotation = Quaternion.FromToRotation(Vector3.up, up);
                _hat.localScale = Vector3.one * r;
            }
            if (_armor != null)
            {
                int k = Mathf.Clamp(_armorPoint, 1, WormRig.Points - 2);
                var tangent = new Vector3(_rig.X[k + 1] - _rig.X[k - 1], _rig.Y[k + 1] - _rig.Y[k - 1], 0);
                if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.up;
                _armor.localPosition = new Vector3(_rig.X[k] * s, _rig.Y[k] * s, 0);
                _armor.localRotation = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
                _armor.localScale = Vector3.one * (_rig.R[k] * s);
                if (_cloth != null)
                {
                    // The cape streams back, more when moving.
                    float speed = Mathf.Clamp(new Vector2(w.Vx, w.Vy).magnitude / 200f, 0f, 1f);
                    _cloth.localRotation = Quaternion.Euler(0, 0, 12f + 25f * speed + 6f * Mathf.Sin(Time.time * 5f + _blinkAt));
                }
            }

            // Weapon in hand (grenade or dynamite: only the item, no gun).
            bool show = holding && w.State != WormState.Tumbling;
            byte skin = SkinFor(_loadout, weapon);
            if (show && (_prop == null || _propId != weapon || _propSkin != skin))
            {
                if (_prop != null) Object.Destroy(_prop.gameObject);
                _prop = WeaponProps.Build(weapon, _weaponPivot, skin);
                _propId = weapon;
                _propSkin = skin;
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
            if (_hurtPoseTime > 0) _hurtPoseTime = Mathf.Max(0, _hurtPoseTime - dt);
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(FlashId, _flash);
            _block.SetColor(TeamColorId, _teamColor);
            _renderer.SetPropertyBlock(_block);
            if (_paintedRenderer != null) _paintedRenderer.SetPropertyBlock(_block);
        }

        public void Destroy()
        {
            Object.Destroy(Root.gameObject);
            Object.Destroy(_mesh);
        }
    }
}
