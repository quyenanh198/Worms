using UnityEngine;
using Worms.Game.Core;

namespace Worms.Game.Play
{
    /// <summary>Desktop controls (docs/PLAN.md §3.12) read through UnityEngine.Input.</summary>
    public static class KeyboardInput
    {
        public static InputFrame Read(float dt)
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
            return f;
        }

        /// <summary>Mouse wheel zoom and right/middle-drag pan.</summary>
        public static void CameraControls(Render.CameraRig rig)
        {
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
