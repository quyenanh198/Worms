using UnityEngine;
using Worms.Game.Core;

namespace Worms.Game.Play
{
    /// <summary>Desktop controls (docs/PLAN.md §3.12) read through UnityEngine.Input.</summary>
    public static class KeyboardInput
    {
        /// <summary>Screen area covered by HUD panels this frame; clicks there are not map picks.</summary>
        public static Rect BlockedArea;

        public static InputFrame Read(float dt, Camera camera)
        {
            var f = new InputFrame
            {
                Dt = dt,
                Left = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A),
                Right = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D),
                Up = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W),
                Down = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S),
                JumpPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter),
                BackflipPressed = Input.GetKeyDown(KeyCode.Backspace),
                FireHeld = Input.GetKey(KeyCode.Space),
                DragAngle = float.NaN,
                DragPower = float.NaN,
            };
            for (int i = 0; i < 8; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) f.WeaponPressed = i + 1;
            for (int i = 0; i < 5; i++)
                if (Input.GetKeyDown(KeyCode.F1 + i)) f.FusePressed = i + 1;

            var mouse = (Vector2)Input.mousePosition;
            var guiPoint = new Vector2(mouse.x, Screen.height - mouse.y);
            // On touch screens the touch path picks targets (Unity also fakes mouse clicks from touches).
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0) && camera != null && !BlockedArea.Contains(guiPoint) && TryPick(camera, mouse, out var sim))
            {
                f.TargetPicked = true;
                f.TargetX = sim.x;
                f.TargetY = sim.y;
            }
            return f;
        }

        /// <summary>Screen point to simulation coordinates on the playing plane.</summary>
        public static bool TryPick(Camera camera, Vector2 screen, out Vector2 sim)
        {
            sim = default;
            var ray = camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.z) < 1e-4f) return false;
            float t = (Render.WorldSpace.ActorZ - ray.origin.z) / ray.direction.z;
            if (t <= 0) return false;
            sim = Render.WorldSpace.ToSim(ray.origin + ray.direction * t);
            return true;
        }

        /// <summary>Mouse wheel zoom and right/middle-drag pan.</summary>
        public static void CameraControls(Render.CameraRig rig)
        {
            if (Input.touchCount > 0) return;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) rig.Zoom(Mathf.Pow(0.9f, wheel));
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                var d = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
                if (d.sqrMagnitude > 0) rig.Pan(-d * rig.UnitsPerPixel * 12f);
            }
        }
    }
}
