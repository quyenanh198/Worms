using UnityEngine;

namespace Worms.Game.Render
{
    /// <summary>
    /// Perspective camera looking at the XY plane from -Z (docs/PLAN.md §3.9):
    /// follows a target, can be panned and zoomed, tilts slightly to show depth.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public const float MinDistance = 9f, MaxDistance = 60f;

        public Camera Camera { get; private set; }
        public float Distance = 24f;
        public float Tilt = 7f;

        Vector3 _focus;
        Vector3 _velocity;
        Vector3? _follow;
        float _manualUntil;
        Rect _bounds;

        public void Init(Rect bounds)
        {
            _bounds = bounds;
            Camera = TryGetComponent<Camera>(out var existing) ? existing : gameObject.AddComponent<Camera>();
            gameObject.tag = "MainCamera";
            Camera.fieldOfView = 35f;
            Camera.nearClipPlane = 0.3f;
            Camera.farClipPlane = 600f;
            Camera.clearFlags = CameraClearFlags.Skybox;
            _focus = new Vector3(bounds.center.x, bounds.center.y, 0);
        }

        /// <summary>Point to follow this frame; ignored for a few seconds after the player pans.</summary>
        public void Follow(Vector3? worldPoint)
        {
            _follow = worldPoint;
        }

        public void Pan(Vector2 worldDelta)
        {
            _focus += (Vector3)worldDelta;
            _manualUntil = Time.time + 3f;
        }

        public void Zoom(float factor)
        {
            Distance = Mathf.Clamp(Distance * factor, MinDistance, MaxDistance);
        }

        /// <summary>World units per screen pixel at the playing plane (for drag panning).</summary>
        public float UnitsPerPixel => 2f * Distance * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;

        void LateUpdate()
        {
            if (Camera == null) return;
            if (_follow.HasValue && Time.time >= _manualUntil)
                _focus = Vector3.SmoothDamp(_focus, _follow.Value, ref _velocity, 0.35f);
            _focus.x = Mathf.Clamp(_focus.x, _bounds.xMin, _bounds.xMax);
            _focus.y = Mathf.Clamp(_focus.y, _bounds.yMin, _bounds.yMax);
            _focus.z = 0;

            var rotation = Quaternion.Euler(Tilt, 0, 0);
            transform.position = _focus + rotation * new Vector3(0, 0, -Distance) + Vector3.up * (Distance * 0.06f);
            transform.rotation = rotation;
        }
    }
}
