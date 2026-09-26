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
        /// <summary>Gold earned in the match that just ended, for the game-over panel; null if none.</summary>
        string RewardText { get; }
    }

    /// <summary>Immediate-mode HUD (docs/PLAN.md §3.16): timer, wind, HP, weapon, power, labels.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public IHudSource Source;
        GUIStyle _label, _big, _small, _panel, _button, _buttonOn, _tagName, _tagHp, _kamikaze, _oneLine;
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
            UiSkin.Panel(_panel);
            UiSkin.Button(_button);
            // The selected weapon: gold outline and text.
            _buttonOn = new GUIStyle(_button) { normal = _button.onNormal, hover = _button.onHover };
            _tagName = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 0.72f), fontStyle = FontStyle.Bold };
            _tagHp = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 0.72f) };
            _kamikaze = new GUIStyle(_big) { fontStyle = FontStyle.Bold };
            _oneLine = new GUIStyle(_label) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            UiFont.Apply(_label, _big, _small, _panel, _button, _buttonOn, _tagName, _tagHp, _kamikaze, _oneLine);
        }

        void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        /// <summary>A rounded tag at <paramref name="anchor"/> (screen, y down): name over HP, in the team's color.</summary>
        void NameTag(WormSnap worm, Vector2 anchor, float u, bool active)
        {
            string name = Source.TeamName(worm.Team);
            string hp = worm.Hp.ToString();
            if (worm.PendingDamage > 0) hp += " <color=#ff7766>-" + worm.PendingDamage + "</color>";
            var nameSize = _tagName.CalcSize(new GUIContent(name));
            float width = Mathf.Max(nameSize.x, u * 2.6f) + u * 0.9f, height = u * 2.05f;
            var r = new Rect(anchor.x - width / 2, anchor.y - height, width, height);
            var team = TeamColors.Of(worm.Team);
            UiSkin.Pill(r, active ? new Color(0.1f, 0.12f, 0.17f, 0.92f) : new Color(0.07f, 0.09f, 0.13f, 0.72f));
            UiSkin.Pill(new Rect(r.x + u * 0.3f, r.yMax - 3, r.width - u * 0.6f, 3), team);
            Shadowed(new Rect(r.x, r.y + u * 0.08f, r.width, u * 0.95f), name, _tagName, team);
            Shadowed(new Rect(r.x, r.y + u * 0.95f, r.width, u * 0.95f), hp, _tagHp, Color.white);
            if (active)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 4f)) * u * 0.45f;
                var old = GUI.color;
                GUI.color = new Color(1f, 0.8f, 0.3f);
                GUI.DrawTexture(new Rect(anchor.x - u * 0.55f, r.y - u * 1.25f - bob, u * 1.1f, u * 1.1f), UiSkin.Arrow);
                GUI.color = old;
            }
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
            UiFont.UseForSkin();
            var s = Source?.Current;
            if (s == null) return;
            Styles();
            float w = Screen.width, h = Screen.height, u = _label.fontSize;

            // Name tags over every worm: the player's name, then HP (pending damage in red).
            // The worm whose turn it is gets a bobbing arrow above its tag.
            var cam = Source.Presenter.Rig.Camera;
            foreach (var worm in s.Worms)
            {
                if (!worm.Alive || !Source.Presenter.Actors.TryGetLabelPoint(worm.Id, cam, out var p)) continue;
                NameTag(worm, new Vector2(p.x, h - p.y), u, worm.Id == s.ActiveWorm && s.Phase != Phase.GameOver);
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

            // Top center: the turn timer in a badge ringed with the playing team's color.
            float seconds = s.Phase == Phase.Retreat ? s.RetreatTicksLeft / (float)C.TicksPerSecond : s.TurnTicksLeft / (float)C.TicksPerSecond;
            var timer = new Rect(w / 2 - u * 2.2f, u * 0.4f, u * 4.4f, u * 2.4f);
            if (s.ActiveTeam >= 0) UiSkin.Pill(new Rect(timer.x - 3, timer.y - 3, timer.width + 6, timer.height + 6), TeamColors.Of(s.ActiveTeam));
            UiSkin.Pill(timer, new Color(0.07f, 0.09f, 0.13f, 0.9f));
            Shadowed(timer, Mathf.CeilToInt(Mathf.Max(0, seconds)).ToString(), _big,
                seconds <= 5 && s.Phase == Phase.Aiming ? new Color(1f, 0.45f, 0.35f) : Color.white);

            // Top right: wind, a bar filling from the middle toward where it blows.
            float windW = u * 8;
            var windBadge = new Rect(w - windW - u * 1.6f, u * 0.4f, windW + u * 1.2f, u * 2.4f);
            UiSkin.Pill(windBadge, new Color(0.07f, 0.09f, 0.13f, 0.8f));
            var windRect = new Rect(windBadge.x + u * 0.6f, windBadge.y + u * 1.45f, windW, u * 0.5f);
            UiSkin.Pill(windRect, new Color(1f, 1f, 1f, 0.15f));
            float frac = Mathf.Clamp(s.Wind / C.WindMax, -1, 1);
            float mid = windRect.x + windRect.width / 2;
            if (Mathf.Abs(frac) > 0.02f)
                UiSkin.Pill(frac >= 0 ? new Rect(mid, windRect.y, windRect.width / 2 * frac, windRect.height)
                                      : new Rect(mid + windRect.width / 2 * frac, windRect.y, -windRect.width / 2 * frac, windRect.height),
                    new Color(0.55f, 0.85f, 1f));
            string windText = Mathf.Abs(frac) < 0.05f ? "Gió: lặng" : frac > 0 ? "Gió  »" : "«  Gió";
            Shadowed(new Rect(windBadge.x, windBadge.y + u * 0.15f, windBadge.width, u * 1.2f), windText, _small, Color.white);

            // Bottom: one rounded HP bar per team, the playing team's lit up.
            int teams = Source.Presenter.TeamCount;
            float barW = Mathf.Min(u * 10, (w - u * 2) / Mathf.Max(1, teams));
            for (int t = 0; t < teams; t++)
            {
                int hp = 0, members = 0;
                foreach (var worm in s.Worms)
                {
                    if (worm.Team != t) continue;
                    members++;
                    if (worm.Alive) hp += worm.Hp;
                }
                var r = new Rect(w / 2 - barW * teams / 2 + t * barW + u * 0.3f, h - u * 1.5f, barW - u * 0.6f, u * 0.7f);
                var back = new Rect(r.x - u * 0.35f, r.y - u * 1.25f, r.width + u * 0.7f, r.height + u * 1.6f);
                UiSkin.Pill(back, t == s.ActiveTeam ? new Color(0.14f, 0.17f, 0.24f, 0.92f) : new Color(0.07f, 0.09f, 0.13f, 0.75f));
                UiSkin.Pill(r, new Color(1f, 1f, 1f, 0.12f));
                float fill = Mathf.Clamp01(hp / (float)(C.StartHp * Mathf.Max(1, members)));
                if (fill > 0) UiSkin.Pill(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * fill), r.height), TeamColors.Of(t));
                Shadowed(new Rect(r.x, r.y - u * 1.15f, r.width, u), Source.TeamName(t), _small, t == s.ActiveTeam ? TeamColors.Of(t) : Color.white);
            }

            // Bottom left: weapon, fuse and power while it is our turn.
            Play.KeyboardInput.BlockedArea = Rect.zero;
            Play.TouchInput.BlockedArea = Rect.zero;
            if (Source.IsLocalTurn && s.Phase == Phase.Aiming)
            {
                var def = Weapons.Get(s.ActiveWeapon);
                string weapon = WeaponName(s.ActiveWeapon);
                if (def.UsesFuse) weapon += "  ·  ngòi " + Source.Controls.Fuse + "s";
                var weaponSize = _oneLine.CalcSize(new GUIContent(weapon));
                UiSkin.Pill(new Rect(u * 0.6f, h - u * 3.55f, weaponSize.x + u * 1.4f, u * 1.5f), new Color(0.07f, 0.09f, 0.13f, 0.8f));
                Shadowed(new Rect(u * 1.3f, h - u * 3.4f, weaponSize.x + 4, u * 1.2f), weapon, _oneLine, Color.white);
                if (def.Targets) Shadowed(new Rect(0, h - u * 5.5f, w, u * 1.2f), "Bấm chuột trái vào bản đồ để chọn mục tiêu", _label, Color.white);
                if (Source.Controls.Charging)
                {
                    var r = new Rect(w / 2 - u * 6, h - u * 4.2f, u * 12, u * 0.8f);
                    UiSkin.Pill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0.07f, 0.09f, 0.13f, 0.85f));
                    UiSkin.Pill(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Source.Controls.Power), r.height),
                        Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.2f), Source.Controls.Power));
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

            Kamikaze(w, h, u);
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

        /// <summary>
        /// Bottom-right weapon controls: a quick grenade button, the full weapon grid behind
        /// "Vũ khí" (or Tab), and fuse buttons for weapons with a timer, so phones can do
        /// everything the keyboard can. On touch screens it sits above the fire button.
        /// </summary>
        void WeaponMenu(Snapshot s, float w, float h, float u)
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
            {
                _weaponMenu = !_weaponMenu;
                Event.current.Use();
            }
            float bottom = h - u * 1.2f;
            if (Play.TouchInput.Visible)
            {
                var fire = Source.Touch.Layout.Fire;
                bottom = h - (fire.Y + fire.H) - u * 0.6f;
            }
            float bh = u * 1.9f, right = w - u * 0.8f;
            var toggle = new Rect(right - u * 6.4f, bottom - bh, u * 6.4f, bh);
            var grenade = new Rect(toggle.x - u * 5.4f, toggle.y, u * 5f, bh);
            float top = toggle.y;

            if (GUI.Button(toggle, _weaponMenu ? "Đóng" : "Vũ khí", _button)) _weaponMenu = !_weaponMenu;
            int grenadeAmmo = s.ActiveAmmo[(int)WeaponId.Grenade];
            var old = GUI.enabled;
            GUI.enabled = grenadeAmmo != 0 && !s.AttackInProgress;
            if (GUI.Button(grenade, "Lựu đạn", s.ActiveWeapon == WeaponId.Grenade ? _buttonOn : _button))
            {
                Source.SelectWeapon(WeaponId.Grenade);
                _weaponMenu = false;
            }
            GUI.enabled = old;

            // Fuse: 1..5 seconds.
            if (Weapons.Get(s.ActiveWeapon).UsesFuse)
            {
                float chip = u * 1.9f;
                float y = top - chip - u * 0.3f;
                float x = right - chip * 5 - u * 0.2f * 4;
                Shadowed(new Rect(x - u * 3.2f, y, u * 3f, chip), "Ngòi", _small, Color.white);
                for (int f = 1; f <= 5; f++)
                {
                    var r = new Rect(x + (f - 1) * (chip + u * 0.2f), y, chip, chip);
                    if (GUI.Button(r, f + "s", Source.Controls.Fuse == f ? _buttonOn : _button)) Source.Controls.SetFuse(f);
                }
                top = y;
            }

            var blocked = new Rect(grenade.x - u * 3.4f, top, right - grenade.x + u * 3.4f, toggle.yMax - top);
            if (_weaponMenu)
            {
                const int cols = 4;
                float cw = u * 6.2f, ch = u * 3f;
                var panel = new Rect(right - cw * cols - u, top - u * 0.4f - ch * 2 - u, cw * cols + u, ch * 2 + u);
                GUI.Box(panel, GUIContent.none, _panel);
                for (int i = 0; i < Weapons.Count; i++)
                {
                    var id = (WeaponId)i;
                    int ammo = s.ActiveAmmo[i];
                    string label = WeaponName(id) + "\n" + (ammo < 0 ? "∞" : "còn " + ammo);
                    var r = new Rect(panel.x + u * 0.5f + (i % cols) * cw, panel.y + u * 0.5f + (i / cols) * ch, cw - u * 0.3f, ch - u * 0.3f);
                    GUI.enabled = ammo != 0 && !s.AttackInProgress;
                    if (GUI.Button(r, label, id == s.ActiveWeapon ? _buttonOn : _button))
                    {
                        Source.SelectWeapon(id);
                        _weaponMenu = false;
                    }
                    GUI.enabled = old;
                }
                blocked = new Rect(panel.x, panel.y, right - panel.x, toggle.yMax - panel.y);
            }
            Play.KeyboardInput.BlockedArea = blocked;
            Play.TouchInput.BlockedArea = blocked;
        }

        /// <summary>White flash, then a "CẢM TỬ!" banner that pops in and fades.</summary>
        void Kamikaze(float w, float h, float u)
        {
            float age = Time.time - Source.Presenter.KamikazeAt;
            if (age < 0 || age > 2.4f) return;
            var old = GUI.color;
            float flash = Mathf.Clamp01(1f - age / 0.35f);
            if (flash > 0)
            {
                GUI.color = new Color(1f, 0.97f, 0.9f, flash * 0.85f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }
            GUI.color = old;
            float pop = age < 0.25f ? Mathf.Lerp(2.2f, 1f, age / 0.25f) : 1f + 0.04f * Mathf.Sin(age * 18f);
            float alpha = Mathf.Clamp01((2.4f - age) / 0.5f);
            var m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(w / 2, h * 0.3f));
            var banner = new Rect(w / 2 - u * 9, h * 0.3f - u * 1.8f, u * 18, u * 3.6f);
            UiSkin.Pill(banner, new Color(0.55f, 0.05f, 0.05f, 0.85f * alpha));
            _kamikaze.fontSize = Mathf.RoundToInt(u * 2.3f);
            Shadowed(banner, "CẢM TỬ!", _kamikaze, new Color(1f, 0.85f, 0.3f, alpha));
            GUI.matrix = m;
        }

        void GameOverPanel(Snapshot s, float w, float h, float u)
        {
            var r = new Rect(w / 2 - u * 10, h / 2 - u * 4, u * 20, u * 8);
            GUI.Box(r, GUIContent.none, _panel);
            string text = s.Winner >= 0 ? Source.TeamName(s.Winner) + " thắng!" : "Hòa!";
            Shadowed(new Rect(r.x, r.y + u, r.width, u * 2), text, _big, s.Winner >= 0 ? TeamColors.Of(s.Winner) : Color.white);
            string reward = Source.RewardText;
            if (reward != null) Shadowed(new Rect(r.x, r.y + u * 3f, r.width, u * 1.2f), reward, _small, new Color(1f, 0.82f, 0.35f));
            float bw = u * 8;
            if (Source.PlayAgain != null && GUI.Button(new Rect(r.center.x - bw - u * 0.5f, r.yMax - u * 2.6f, bw, u * 1.8f), "Chơi lại", _button)) Source.PlayAgain();
            if (Source.Leave != null && GUI.Button(new Rect(r.center.x + u * 0.5f, r.yMax - u * 2.6f, bw, u * 1.8f), "Về menu", _button)) Source.Leave();
        }
    }
}
