using System;
using System.Collections.Generic;
using Worms.Sim;

namespace Worms.Game.Core
{
    /// <summary>Raw player input for one frame, from keyboard or touch.</summary>
    public struct InputFrame
    {
        public float Dt;
        public bool Left, Right, Up, Down;
        public bool JumpPressed, BackflipPressed;
        public bool FireHeld;
        /// <summary>0 = none, otherwise WeaponId + 1.</summary>
        public int WeaponPressed;
        /// <summary>0 = none, 1..5 = fuse seconds.</summary>
        public int FusePressed;
        /// <summary>Touch aiming: absolute angle and power set by dragging (NaN = not used).</summary>
        public float DragAngle, DragPower;
        public bool DragReleased;
        /// <summary>A point on the map was picked (air strike), in simulation units.</summary>
        public bool TargetPicked;
        public float TargetX, TargetY;
    }

    /// <summary>What the local player asks for; sent as SimInput offline or Command online.</summary>
    public struct Intent
    {
        public InputKind Kind;
        public int Dir;
        public float Angle;
        public float Power;
        public int Fuse;
        public WeaponId Weapon;
        public float TargetX, TargetY;
    }

    /// <summary>
    /// Turns held keys into intents: aiming and power charging run locally for
    /// instant feedback (docs/PLAN.md §3.4.4); only the final shot is sent.
    /// </summary>
    public sealed class LocalControls
    {
        public const float AimSpeed = 1.3f;        // radians per second
        public const float ChargeSeconds = 1.0f;   // empty to full power
        public const float AimSendInterval = 0.1f; // aim is echoed to others at 10 Hz

        public float Aim { get; private set; }
        public float Power { get; private set; }
        public bool Charging { get; private set; }
        public int Fuse { get; private set; } = 3;
        public int MoveDir { get; private set; }

        /// <summary>Set from the selected weapon each frame: hold to charge (thrown weapons) or fire on press.</summary>
        public bool ChargeMode = true;
        /// <summary>Set from the selected weapon each frame: fire by picking a point (air strike).</summary>
        public bool TargetMode;
        bool _fireWasHeld;

        float _aimSentAt = float.NegativeInfinity;
        float _lastSentAim = float.NaN;
        float _time;

        /// <summary>Picks charge/target mode for a weapon.</summary>
        public void UseWeapon(WeaponId weapon)
        {
            var def = Weapons.Get(weapon);
            ChargeMode = def.NeedsPower;
            TargetMode = def.Targets;
        }

        public void Reset(float aim)
        {
            Aim = aim;
            Power = 0;
            Charging = false;
            MoveDir = 0;
            _lastSentAim = aim;
        }

        public void Update(InputFrame f, bool canAim, bool canMove, List<Intent> output)
        {
            _time += f.Dt;
            int dir = f.Right == f.Left ? 0 : (f.Right ? 1 : -1);
            if (Charging || !canMove) dir = 0;
            if (dir != MoveDir)
            {
                MoveDir = dir;
                output.Add(new Intent { Kind = InputKind.Move, Dir = dir });
            }
            if (canMove && !Charging)
            {
                if (f.JumpPressed) output.Add(new Intent { Kind = InputKind.Jump });
                if (f.BackflipPressed) output.Add(new Intent { Kind = InputKind.Backflip });
            }
            if (!canAim)
            {
                Charging = false;
                Power = 0;
                _fireWasHeld = f.FireHeld;
                return;
            }

            if (f.WeaponPressed > 0) output.Add(new Intent { Kind = InputKind.Select, Weapon = (WeaponId)(f.WeaponPressed - 1) });
            if (f.FusePressed >= 1 && f.FusePressed <= 5) Fuse = f.FusePressed;

            float half = (float)(Math.PI / 2);
            if (f.Up != f.Down) Aim = Math.Max(-half, Math.Min(half, Aim + (f.Up ? 1 : -1) * AimSpeed * f.Dt));
            if (!float.IsNaN(f.DragAngle)) Aim = Math.Max(-half, Math.Min(half, f.DragAngle));
            if (Aim != _lastSentAim && _time - _aimSentAt >= AimSendInterval)
            {
                output.Add(new Intent { Kind = InputKind.Aim, Angle = Aim });
                _aimSentAt = _time;
                _lastSentAim = Aim;
            }

            bool pressed = f.FireHeld && !_fireWasHeld;
            _fireWasHeld = f.FireHeld;
            if (TargetMode)
            {
                Charging = false;
                if (f.TargetPicked)
                    output.Add(new Intent { Kind = InputKind.Fire, Angle = Aim, TargetX = f.TargetX, TargetY = f.TargetY });
                return;
            }
            if (!ChargeMode)
            {
                Charging = false;
                if (pressed || f.DragReleased) Fire(output, 1f);
                return;
            }
            if (!float.IsNaN(f.DragPower))
            {
                Power = Math.Max(0, Math.Min(1, f.DragPower));
                if (f.DragReleased) Fire(output);
                return;
            }
            if (f.FireHeld)
            {
                if (!Charging)
                {
                    Charging = true;
                    Power = 0;
                }
                Power = Math.Min(1f, Power + f.Dt / ChargeSeconds);
                if (Power >= 1f) Fire(output);
            }
            else if (Charging)
            {
                Fire(output);
            }
        }

        void Fire(List<Intent> output, float power = -1)
        {
            output.Add(new Intent { Kind = InputKind.Fire, Angle = Aim, Power = power >= 0 ? power : Power, Fuse = Fuse });
            Charging = false;
            Power = 0;
        }
    }
}
