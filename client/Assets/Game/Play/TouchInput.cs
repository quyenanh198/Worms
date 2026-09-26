using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Core;
using Worms.Game.Render;

namespace Worms.Game.Play
{
    /// <summary>Unity side of the mobile controls: reads Input.touches and drives the camera.</summary>
    public sealed class TouchInput
    {
        readonly TouchInterpreter _interpreter = new TouchInterpreter();
        readonly List<TouchPoint> _points = new List<TouchPoint>();

        /// <summary>Show on-screen buttons: phones, tablets, touch laptops once touched.</summary>
        public static bool Visible => Application.isMobilePlatform || Input.touchCount > 0 || _touchedOnce;
        static bool _touchedOnce;

        public TouchLayout Layout { get; private set; } = TouchLayout.For(Screen.width, Screen.height);

        public void Apply(ref InputFrame f, TouchContext ctx, CameraRig rig)
        {
            Layout = TouchLayout.For(Screen.width, Screen.height);
            _points.Clear();
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                _points.Add(new TouchPoint
                {
                    Id = t.fingerId,
                    X = t.position.x,
                    Y = t.position.y,
                    Phase = t.phase == TouchPhase.Began ? TouchPhase2.Began
                        : t.phase == TouchPhase.Moved ? TouchPhase2.Moved
                        : t.phase == TouchPhase.Stationary ? TouchPhase2.Stationary
                        : TouchPhase2.Ended,
                });
            }
            if (_points.Count > 0) _touchedOnce = true;
            if (_points.Count == 0 && !_interpreter.AnyTouch) return;

            _interpreter.Update(_points, Layout, ctx, ref f, out float panX, out float panY, out float zoom);
            if (f.TargetPicked && KeyboardInput.TryPick(rig.Camera, new Vector2(f.TargetX, f.TargetY), out var sim))
            {
                f.TargetX = sim.x;
                f.TargetY = sim.y;
            }
            else
            {
                f.TargetPicked = false;
            }
            if (panX != 0 || panY != 0) rig.Pan(new Vector2(-panX, -panY) * rig.UnitsPerPixel);
            if (!Mathf.Approximately(zoom, 1f)) rig.Zoom(zoom);
        }

        /// <summary>Context for this frame from the match state.</summary>
        public static TouchContext Context(bool canAct, LocalControls controls, Vector3? wormWorld, int facing, Camera cam)
        {
            float wx = float.NaN, wy = float.NaN;
            if (canAct && wormWorld.HasValue)
            {
                var sp = cam.WorldToScreenPoint(wormWorld.Value);
                wx = sp.x;
                wy = sp.y;
            }
            return new TouchContext
            {
                CanAct = canAct,
                ChargeMode = controls.ChargeMode,
                TargetMode = controls.TargetMode,
                WormX = wx,
                WormY = wy,
                Facing = facing,
                FullPowerPixels = Screen.height * 0.35f,
            };
        }
    }
}
