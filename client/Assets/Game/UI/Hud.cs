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
        GUIStyle _label, _big, _small, _panel, _button, _selectedButton, _weaponLabel, _weaponLabelDark;
        bool _weaponMenu;
        Texture2D _white;
        Texture2D _buttonTexture, _buttonHoverTexture, _selectedTexture;
        Texture2D _weaponIcons, _teamPortraits;
        static readonly Color PanelColor = new Color(0.075f, 0.14f, 0.21f, 0.88f);
        static readonly Color PanelEdge = new Color(0.37f, 0.54f, 0.66f, 0.8f);
        static readonly Color Accent = new Color(1f, 0.78f, 0.27f);

        static readonly string[] WeaponNames = { "Bazooka", "Lựu đạn", "Bom chùm", "Shotgun", "Uzi", "Dynamite", "Gậy bóng chày", "Không kích" };

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        public static string WeaponName(WeaponId id)
        {
            int i = (int)id;
            return i >= 0 && i < WeaponNames.Length ? WeaponNames[i] : id.ToString();
        }

        void Styles()
        {
            float u = Mathf.Max(12f, Screen.height / 42f);
            if (_label != null && _label.fontSize == Mathf.RoundToInt(u)) return;
            _white = Texture2D.whiteTexture;
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u), alignment = TextAnchor.MiddleCenter, richText = true };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 1.8f), fontStyle = FontStyle.Bold };
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 0.8f) };
            _panel = new GUIStyle(GUI.skin.box);
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 0.85f), wordWrap = true };
            _button.normal.textColor = Color.white;
            _button.hover.textColor = Color.white;
            _button.active.textColor = Color.white;
            if (_buttonTexture == null)
            {
                _buttonTexture = Solid(new Color(0.11f, 0.21f, 0.31f));
                _buttonHoverTexture = Solid(new Color(0.18f, 0.34f, 0.45f));
                _selectedTexture = Solid(Accent);
            }
            _button.normal.background = _buttonTexture;
            _button.hover.background = _buttonHoverTexture;
            _button.active.background = _buttonHoverTexture;
            _selectedButton = new GUIStyle(_button);
            _selectedButton.normal.background = _selectedTexture;
            _selectedButton.normal.textColor = new Color(0.09f, 0.13f, 0.18f);
            _weaponLabel = new GUIStyle(_small) { fontSize = Mathf.RoundToInt(u * 0.68f), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _weaponLabelDark = new GUIStyle(_weaponLabel);
            _weaponLabelDark.normal.textColor = new Color(0.09f, 0.13f, 0.18f);
            _weaponIcons = Resources.Load<Texture2D>("UI/weapon-icons");
            _teamPortraits = Resources.Load<Texture2D>("UI/team-portraits");
        }

        void OnDestroy()
        {
            if (_buttonTexture != null) Destroy(_buttonTexture);
            if (_buttonHoverTexture != null) Destroy(_buttonHoverTexture);
            if (_selectedTexture != null) Destroy(_selectedTexture);
        }

        void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        void Panel(Rect r)
        {
            Box(r, PanelEdge);
            Box(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), PanelColor);
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
            var safe = Screen.safeArea;
            float safeTop = h - safe.yMax, safeBottom = safe.yMin;
            bool compact = safe.width < u * 34f;

            // Worm labels: HP above each worm, pending damage in red.
            var cam = Source.Presenter.Rig.Camera;
            foreach (var worm in s.Worms)
            {
                if (!worm.Alive || !Source.Presenter.Actors.TryGetLabelPoint(worm.Id, cam, out var p)) continue;
                if (p.x < u * 3f || p.x > w - u * 3f || p.y < u * 2f || p.y > h - u * 4f) continue;
                var r = new Rect(p.x - u * 3, h - p.y - u * 1.6f, u * 6, u * 1.2f);
                string text = worm.Hp.ToString();
                if (worm.PendingDamage > 0) text += " <color=#ff6655>-" + worm.PendingDamage + "</color>";
                Shadowed(r, text, _small, TeamColors.Of(worm.Team));
            }

            // Damage numbers rising from worms.
            foreach (var popup in Source.Presenter.Vfx.Popups)
            {
                var sp = cam.WorldToScreenPoint(popup.World + Vector3.up * (QualitySettingsManager.ReducedMotion ? 0f : popup.Age * 0.8f));
                if (sp.z <= 0) continue;
                var c = popup.Color;
                c.a = 1f - Mathf.Clamp01((popup.Age - Render.Vfx.PopupSeconds * 0.6f) / (Render.Vfx.PopupSeconds * 0.4f));
                Shadowed(new Rect(sp.x - u * 3, h - sp.y - u, u * 6, u * 2), popup.Text, _big, c);
            }

            // Top center: whose turn and the timer.
            string who = s.ActiveTeam >= 0 ? Source.TeamName(s.ActiveTeam) : "";
            float seconds = s.Phase == Phase.Retreat ? s.RetreatTicksLeft / (float)C.TicksPerSecond : s.TurnTicksLeft / (float)C.TicksPerSecond;
            var timerPanel = new Rect(safe.center.x - u * (compact ? 2.5f : 3.5f), safeTop,
                u * (compact ? 5f : 7f), u * 3.8f);
            Panel(timerPanel);
            Shadowed(new Rect(timerPanel.x, safeTop + u * 0.2f, timerPanel.width, u * 2), Mathf.CeilToInt(Mathf.Max(0, seconds)).ToString(), _big,
                seconds <= 5 && s.Phase == Phase.Aiming ? new Color(1f, 0.4f, 0.3f) : Color.white);
            if (s.ActiveTeam >= 0)
                Shadowed(new Rect(timerPanel.x, safeTop + u * 2.15f, timerPanel.width, u * 1.2f), who, _small, TeamColors.Of(s.ActiveTeam));

            // Top right: wind.
            float windW = u * (compact ? 4f : 8f);
            var windRect = new Rect(safe.xMax - windW - u * (compact ? 0.5f : 1f), safeTop + u, windW, u * 0.7f);
            Panel(new Rect(windRect.x - u * 0.5f, safeTop + u * 0.25f, windRect.width + u, u * 2.7f));
            Box(windRect, new Color(0.02f, 0.08f, 0.14f, 0.9f));
            float frac = Mathf.Clamp(s.Wind / C.WindMax, -1, 1);
            float mid = windRect.x + windRect.width / 2;
            Box(frac >= 0 ? new Rect(mid, windRect.y, windRect.width / 2 * frac, windRect.height)
                          : new Rect(mid + windRect.width / 2 * frac, windRect.y, -windRect.width / 2 * frac, windRect.height),
                new Color(0.34f, 0.74f, 1f));
            Box(new Rect(mid - 1f, windRect.y, 2f, windRect.height), Color.white);
            string windLabel = (compact ? "" : "Gió ") + (s.Wind >= 0 ? "→ " : "← ") + Mathf.RoundToInt(Mathf.Abs(s.Wind));
            Shadowed(new Rect(windRect.x, windRect.yMax, windRect.width, u), windLabel, _small, Color.white);

            // Bottom: team HP bars.
            int teams = Source.Presenter.TeamCount;
            float barW = Mathf.Min(u * 10, (safe.width - u * 2) / Mathf.Max(1, teams));
            var teamPanel = new Rect(safe.center.x - barW * teams / 2 - u * 0.2f, compact ? safeTop + u * 4.1f : h - safeBottom - u * 2.9f,
                barW * teams + u * 0.4f, u * 2.7f);
            Panel(teamPanel);
            for (int t = 0; t < teams; t++)
            {
                int hp = 0, maxHp = 0;
                foreach (var worm in s.Worms)
                    if (worm.Team == t)
                    {
                        maxHp += C.StartHp;
                        if (worm.Alive) hp += worm.Hp;
                    }
                float left = safe.center.x - barW * teams / 2 + t * barW + u * 0.2f;
                float portraitW = _teamPortraits != null && barW >= u * 6f ? Mathf.Min(u * 1.8f, barW * 0.32f) : 0f;
                if (portraitW > 0)
                    GUI.DrawTextureWithTexCoords(new Rect(left, teamPanel.y + u * 0.15f, portraitW, portraitW * 4f / 3f),
                        _teamPortraits, new Rect((t % 4) * 0.25f, 0, 0.25f, 1f), true);
                float labelX = left + portraitW + (portraitW > 0 ? u * 0.1f : 0f);
                var r = new Rect(labelX, compact ? teamPanel.y + u * 1.9f : h - safeBottom - u * 1.15f,
                    barW - u * 0.4f - (labelX - left), u * 0.5f);
                Box(r, new Color(0.01f, 0.04f, 0.08f, 0.9f));
                Box(new Rect(r.x, r.y, r.width * (maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f), r.height), TeamColors.Of(t));
                Shadowed(new Rect(r.x, r.y - u * 1.3f, r.width, u), Source.TeamName(t) + "  " + hp + "/" + maxHp, _small, Color.white);
            }

            // Bottom left: weapon, fuse and power while it is our turn.
            Play.KeyboardInput.BlockedArea = Rect.zero;
            if (Source.IsLocalTurn && s.Phase == Phase.Aiming)
            {
                var def = Weapons.Get(s.ActiveWeapon);
                string weapon = WeaponName(s.ActiveWeapon);
                if (def.UsesFuse) weapon += "  •  ngòi " + Source.Controls.Fuse + "s (F1–F5)";
                var weaponPanel = new Rect(safe.xMin + u * 0.5f, compact ? safeTop + u * 7.2f : h - safeBottom - u * 3.7f,
                    compact ? safe.width - u * 8.5f : u * 15.8f, u * 2.4f);
                Panel(weaponPanel);
                Shadowed(new Rect(weaponPanel.x + u * 0.5f, weaponPanel.y + u * 0.5f,
                    weaponPanel.width - u, u * 1.2f), weapon, compact ? _small : _label, Accent);
                if (def.Targets) Shadowed(new Rect(0, h - u * 5.5f, w, u * 1.2f), "Bấm chuột trái vào bản đồ để chọn mục tiêu", _label, Color.white);
                if (Source.Controls.Charging)
                {
                    var r = new Rect(safe.center.x - u * 6, h - safeBottom - u * 4, u * 12, u * 0.8f);
                    Box(r, new Color(0, 0, 0, 0.5f));
                    Box(new Rect(r.x, r.y, r.width * Source.Controls.Power, r.height), Color.Lerp(Color.yellow, Color.red, Source.Controls.Power));
                }
                WeaponMenu(s, w, h, u, compact);
            }
            else
            {
                _weaponMenu = false;
            }

            if (Play.TouchInput.Visible && Source.IsLocalTurn && (s.Phase == Phase.Aiming || s.Phase == Phase.Retreat)) TouchButtons(s, h);

            var audio = Audio.AudioManager.Instance;
            if (audio != null && GUI.Button(new Rect(safe.xMin + u * 0.5f, safeTop + u * 0.5f, u * 6.5f, u * 1.6f), audio.Muted ? "Âm thanh: tắt" : "Âm thanh: bật", _button))
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

        void WeaponMenu(Snapshot s, float w, float h, float u, bool compact)
        {
            var safe = Screen.safeArea;
            float safeTop = h - safe.yMax, safeBottom = safe.yMin;
            var toggle = new Rect(safe.xMax - u * 7, compact ? safeTop + u * 7.5f : h - safeBottom - u * 3.4f, u * 6, u * 1.8f);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
            {
                _weaponMenu = !_weaponMenu;
                Event.current.Use();
            }
            else if (_weaponMenu && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _weaponMenu = false;
                Event.current.Use();
            }
            if (GUI.Button(toggle, _weaponMenu ? "Đóng" : "Vũ khí (Tab)", _button)) _weaponMenu = !_weaponMenu;
            Play.KeyboardInput.BlockedArea = toggle;
            if (!_weaponMenu) return;

            int cols = compact ? 2 : 4;
            float cw = compact ? (safe.width - u * 2f) / 2f : u * 6.2f;
            float ch = compact ? u * 4.7f : u * 5.6f;
            var panel = compact
                ? new Rect(safe.xMin + u * 0.5f, toggle.yMax + u * 0.3f, safe.width - u, ch * 4 + u)
                : new Rect(safe.xMax - cw * cols - u * 1.5f, h - safeBottom - u * 4.2f - ch * 2 - u, cw * cols + u, ch * 2 + u);
            Panel(panel);
            Play.KeyboardInput.BlockedArea = new Rect(0, 0, w, h);
            if (Event.current.type == EventType.MouseDown &&
                !panel.Contains(Event.current.mousePosition) && !toggle.Contains(Event.current.mousePosition))
            {
                _weaponMenu = false;
                Event.current.Use();
                return;
            }
            for (int i = 0; i < Weapons.Count; i++)
            {
                var id = (WeaponId)i;
                int ammo = s.ActiveAmmo[i];
                string label = (i + 1) + ". " + WeaponName(id) + "\n" + (ammo < 0 ? "∞" : "còn " + ammo);
                var r = new Rect(panel.x + u * 0.5f + (i % cols) * cw, panel.y + u * 0.5f + (i / cols) * ch, cw - u * 0.3f, ch - u * 0.3f);
                var old = GUI.enabled;
                GUI.enabled = ammo != 0 && !s.AttackInProgress;
                if (GUI.Button(r, _weaponIcons == null ? label : "", id == s.ActiveWeapon ? _selectedButton : _button))
                {
                    Source.SelectWeapon(id);
                    _weaponMenu = false;
                }
                if (_weaponIcons != null)
                {
                    var oldColor = GUI.color;
                    if (ammo == 0 || s.AttackInProgress) GUI.color = new Color(1, 1, 1, 0.38f);
                    float iconSize = Mathf.Min(r.width * 0.46f, u * 2.5f);
                    var iconRect = new Rect(r.center.x - iconSize * 0.5f, r.y + u * 0.12f, iconSize, iconSize);
                    int row = i / 4, col = i % 4;
                    GUI.DrawTextureWithTexCoords(iconRect, _weaponIcons,
                        new Rect(col * 0.25f, (1 - row) * 0.5f, 0.25f, 0.5f), true);
                    var textRect = new Rect(r.x + 2, iconRect.yMax, r.width - 4, r.yMax - iconRect.yMax - 2);
                    GUI.Label(textRect, label, id == s.ActiveWeapon ? _weaponLabelDark : _weaponLabel);
                    GUI.color = oldColor;
                }
                GUI.enabled = old;
            }
        }

        void GameOverPanel(Snapshot s, float w, float h, float u)
        {
            var r = new Rect(w / 2 - u * 10, h / 2 - u * 4, u * 20, u * 8);
            Panel(r);
            string text = s.Winner >= 0 ? Source.TeamName(s.Winner) + " thắng!" : "Hòa!";
            Shadowed(new Rect(r.x, r.y + u, r.width, u * 2), text, _big, s.Winner >= 0 ? TeamColors.Of(s.Winner) : Color.white);
            float bw = u * 8;
            if (Source.PlayAgain != null && GUI.Button(new Rect(r.center.x - bw - u * 0.5f, r.yMax - u * 2.6f, bw, u * 1.8f), "Chơi lại", _button)) Source.PlayAgain();
            if (Source.Leave != null && GUI.Button(new Rect(r.center.x + u * 0.5f, r.yMax - u * 2.6f, bw, u * 1.8f), "Về menu", _button)) Source.Leave();
        }
    }
}
