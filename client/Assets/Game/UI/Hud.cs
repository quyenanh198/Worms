using System;
using UnityEngine;
using Worms.Game.Core;
using Worms.Game.Play;
using Worms.Game.Render;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.UI
{
    /// <summary>What the HUD needs from a match, local or online.</summary>
    public interface IHudSource
    {
        Snapshot Current { get; }
        MatchPresenter Presenter { get; }
        LocalControls Controls { get; }
        Play.TouchInput Touch { get; }
        /// <summary>True when this device controls the active worm.</summary>
        bool IsLocalTurn { get; }
        string TeamName(int team);
        /// <summary>Shown on the game-over panel; null hides the button.</summary>
        Action PlayAgain { get; }
        Action Leave { get; }
        void SelectWeapon(WeaponId weapon);
    }

    /// <summary>Immediate-mode HUD (docs/PLAN.md §3.16): timer, wind, HP, weapon, power, labels.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public IHudSource Source;
        GUIStyle _label, _big, _small, _panel, _button;
        bool _weaponMenu;
        Texture2D _white;

        static readonly string[] WeaponNames = { "Bazooka", "Lựu đạn", "Bom chùm", "Shotgun", "Uzi", "Dynamite", "Gậy bóng chày", "Không kích" };

        public static string WeaponName(WeaponId id)
        {
            int i = (int)id;
            return i >= 0 && i < WeaponNames.Length ? WeaponNames[i] : id.ToString();
        }

        void Styles()
        {
            if (_label != null) return;
            float u = Mathf.Max(12f, Screen.height / 42f);
            _white = Texture2D.whiteTexture;
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u), alignment = TextAnchor.MiddleCenter, richText = true };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 1.8f), fontStyle = FontStyle.Bold };
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 0.8f) };
            _panel = new GUIStyle(GUI.skin.box);
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 0.85f), wordWrap = true };
            UiFont.Apply(_label, _big, _small, _panel, _button);
        }

        void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.6f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        void OnGUI()
        {
            var s = Source?.Current;
            if (s == null) return;
            Styles();
            float w = Screen.width, h = Screen.height, u = _label.fontSize;

            // Worm labels: HP above each worm, pending damage in red.
            var cam = Source.Presenter.Rig.Camera;
            foreach (var worm in s.Worms)
            {
                if (!worm.Alive || !Source.Presenter.Actors.TryGetLabelPoint(worm.Id, cam, out var p)) continue;
                var r = new Rect(p.x - u * 3, h - p.y - u * 1.6f, u * 6, u * 1.2f);
                string text = worm.Hp.ToString();
                if (worm.PendingDamage > 0) text += " <color=#ff6655>-" + worm.PendingDamage + "</color>";
                Shadowed(r, text, _small, TeamColors.Of(worm.Team));
            }

            // Damage numbers rising from worms.
            foreach (var popup in Source.Presenter.Vfx.Popups)
            {
                var sp = cam.WorldToScreenPoint(popup.World + Vector3.up * popup.Age * 0.8f);
                if (sp.z <= 0) continue;
                var c = popup.Color;
                c.a = 1f - Mathf.Clamp01((popup.Age - Render.Vfx.PopupSeconds * 0.6f) / (Render.Vfx.PopupSeconds * 0.4f));
                Shadowed(new Rect(sp.x - u * 3, h - sp.y - u, u * 6, u * 2), popup.Text, _big, c);
            }

            // Top center: whose turn and the timer.
            string who = s.ActiveTeam >= 0 ? Source.TeamName(s.ActiveTeam) : "";
            float seconds = s.Phase == Phase.Retreat ? s.RetreatTicksLeft / (float)C.TicksPerSecond : s.TurnTicksLeft / (float)C.TicksPerSecond;
            Shadowed(new Rect(0, u * 0.4f, w, u * 2), Mathf.CeilToInt(Mathf.Max(0, seconds)).ToString(), _big,
                seconds <= 5 && s.Phase == Phase.Aiming ? new Color(1f, 0.4f, 0.3f) : Color.white);
            if (s.ActiveTeam >= 0)
                Shadowed(new Rect(0, u * 2.3f, w, u * 1.2f), who, _label, TeamColors.Of(s.ActiveTeam));

            // Top right: wind.
            float windW = u * 8;
            var windRect = new Rect(w - windW - u, u, windW, u * 0.7f);
            Box(windRect, new Color(0, 0, 0, 0.4f));
            float frac = Mathf.Clamp(s.Wind / C.WindMax, -1, 1);
            float mid = windRect.x + windRect.width / 2;
            Box(frac >= 0 ? new Rect(mid, windRect.y, windRect.width / 2 * frac, windRect.height)
                          : new Rect(mid + windRect.width / 2 * frac, windRect.y, -windRect.width / 2 * frac, windRect.height),
                new Color(0.55f, 0.85f, 1f));
            Shadowed(new Rect(windRect.x, windRect.yMax, windRect.width, u), "Gió", _small, Color.white);

            // Bottom: team HP bars.
            int teams = Source.Presenter.TeamCount;
            float barW = Mathf.Min(u * 10, (w - u * 2) / Mathf.Max(1, teams));
            for (int t = 0; t < teams; t++)
            {
                int hp = 0;
                foreach (var worm in s.Worms) if (worm.Team == t && worm.Alive) hp += worm.Hp;
                var r = new Rect(w / 2 - barW * teams / 2 + t * barW + u * 0.2f, h - u * 1.6f, barW - u * 0.4f, u * 0.8f);
                Box(r, new Color(0, 0, 0, 0.45f));
                Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(hp / 400f), r.height), TeamColors.Of(t));
                Shadowed(new Rect(r.x, r.y - u, r.width, u), Source.TeamName(t), _small, Color.white);
            }

            // Bottom left: weapon, fuse and power while it is our turn.
            Play.KeyboardInput.BlockedArea = Rect.zero;
            if (Source.IsLocalTurn && s.Phase == Phase.Aiming)
            {
                var def = Weapons.Get(s.ActiveWeapon);
                string weapon = WeaponName(s.ActiveWeapon);
                if (def.UsesFuse) weapon += "  •  ngòi " + Source.Controls.Fuse + "s (F1–F5)";
                Shadowed(new Rect(u, h - u * 3.4f, u * 16, u * 1.2f), weapon, _label, Color.white);
                if (def.Targets) Shadowed(new Rect(0, h - u * 5.5f, w, u * 1.2f), "Bấm chuột trái vào bản đồ để chọn mục tiêu", _label, Color.white);
                if (Source.Controls.Charging)
                {
                    var r = new Rect(w / 2 - u * 6, h - u * 4, u * 12, u * 0.8f);
                    Box(r, new Color(0, 0, 0, 0.5f));
                    Box(new Rect(r.x, r.y, r.width * Source.Controls.Power, r.height), Color.Lerp(Color.yellow, Color.red, Source.Controls.Power));
                }
                WeaponMenu(s, w, h, u);
            }
            else
            {
                _weaponMenu = false;
            }

            if (Play.TouchInput.Visible && Source.IsLocalTurn && (s.Phase == Phase.Aiming || s.Phase == Phase.Retreat)) TouchButtons(s, h);

            var audio = Audio.AudioManager.Instance;
            if (audio != null && GUI.Button(new Rect(u * 0.5f, u * 0.5f, u * 6.5f, u * 1.6f), audio.Muted ? "Âm thanh: tắt" : "Âm thanh: bật", _button))
                audio.SetMuted(!audio.Muted);

            if (s.Phase == Phase.GameOver) GameOverPanel(s, w, h, u);
        }

        /// <summary>Draws the on-screen buttons (input itself is read from touches in TouchInput).</summary>
        void TouchButtons(Snapshot s, float h)
        {
            var l = Source.Touch.Layout;
            void Pad(RectF r, string text)
            {
                var gui = new Rect(r.X, h - r.Y - r.H, r.W, r.H);
                Box(gui, new Color(0, 0, 0, 0.35f));
                Shadowed(gui, text, _label, Color.white);
            }
            Pad(l.Left, "<");
            Pad(l.Right, ">");
            Pad(l.Jump, "Nhảy");
            Pad(l.Backflip, "Lộn");
            var def = Weapons.Get(s.ActiveWeapon);
            if (s.Phase == Phase.Aiming && !def.NeedsPower && !def.Targets) Pad(l.Fire, "Bắn");
            if (s.Phase == Phase.Aiming && def.NeedsPower)
                Shadowed(new Rect(0, h * 0.18f, Screen.width, _label.fontSize * 1.4f), "Kéo từ con sâu để ngắm, thả tay để bắn", _small, Color.white);
        }

        void WeaponMenu(Snapshot s, float w, float h, float u)
        {
            var toggle = new Rect(w - u * 7, h - u * 3.4f, u * 6, u * 1.8f);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
            {
                _weaponMenu = !_weaponMenu;
                Event.current.Use();
            }
            if (GUI.Button(toggle, _weaponMenu ? "Đóng" : "Vũ khí (Tab)", _button)) _weaponMenu = !_weaponMenu;
            Play.KeyboardInput.BlockedArea = toggle;
            if (!_weaponMenu) return;

            const int cols = 4;
            float cw = u * 6.2f, ch = u * 3f;
            var panel = new Rect(w - cw * cols - u * 1.5f, h - u * 4.2f - ch * 2 - u, cw * cols + u, ch * 2 + u);
            Box(panel, new Color(0, 0, 0, 0.55f));
            Play.KeyboardInput.BlockedArea = new Rect(panel.x, panel.y, panel.width, toggle.yMax - panel.y);
            for (int i = 0; i < Weapons.Count; i++)
            {
                var id = (WeaponId)i;
                int ammo = s.ActiveAmmo[i];
                string label = (i + 1) + ". " + WeaponName(id) + "\n" + (ammo < 0 ? "∞" : "còn " + ammo);
                var r = new Rect(panel.x + u * 0.5f + (i % cols) * cw, panel.y + u * 0.5f + (i / cols) * ch, cw - u * 0.3f, ch - u * 0.3f);
                var old = GUI.enabled;
                GUI.enabled = ammo != 0 && !s.AttackInProgress;
                var oldColor = GUI.backgroundColor;
                if (id == s.ActiveWeapon) GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                if (GUI.Button(r, label, _button))
                {
                    Source.SelectWeapon(id);
                    _weaponMenu = false;
                }
                GUI.backgroundColor = oldColor;
                GUI.enabled = old;
            }
        }

        void GameOverPanel(Snapshot s, float w, float h, float u)
        {
            var r = new Rect(w / 2 - u * 10, h / 2 - u * 4, u * 20, u * 8);
            Box(r, new Color(0, 0, 0, 0.65f));
            string text = s.Winner >= 0 ? Source.TeamName(s.Winner) + " thắng!" : "Hòa!";
            Shadowed(new Rect(r.x, r.y + u, r.width, u * 2), text, _big, s.Winner >= 0 ? TeamColors.Of(s.Winner) : Color.white);
            float bw = u * 8;
            if (Source.PlayAgain != null && GUI.Button(new Rect(r.center.x - bw - u * 0.5f, r.yMax - u * 2.6f, bw, u * 1.8f), "Chơi lại")) Source.PlayAgain();
            if (Source.Leave != null && GUI.Button(new Rect(r.center.x + u * 0.5f, r.yMax - u * 2.6f, bw, u * 1.8f), "Về menu")) Source.Leave();
        }
    }
}
