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
        /// <summary>The worm's own name (null: fall back to its team's name).</summary>
        string WormName(int wormId);
    }

    /// <summary>Immediate-mode HUD (docs/PLAN.md §3.16): timer, wind, HP, weapon, power, labels.</summary>
    public sealed class Hud : MonoBehaviour
    {
        public IHudSource Source;
        GUIStyle _label, _big, _small, _panel, _button, _buttonOn, _tagName, _tagHp, _kamikaze, _oneLine, _weaponCaption;
        bool _weaponMenu;
        Texture2D _white;
        Texture2D _weaponIcons, _teamPortraits;
        int _styleWidth, _styleHeight;

        static readonly string[] WeaponNames = { "Bazooka", "Lựu đạn", "Bom chùm", "Shotgun", "Uzi", "Dynamite", "Gậy bóng chày", "Không kích", "Bom napalm" };

        public static string WeaponName(WeaponId id)
        {
            int i = (int)id;
            return i >= 0 && i < WeaponNames.Length ? WeaponNames[i] : id.ToString();
        }

        void Styles()
        {
            if (_label != null && _styleWidth == Screen.width && _styleHeight == Screen.height) return;
            _styleWidth = Screen.width;
            _styleHeight = Screen.height;
            float u = Mathf.Max(12f, Screen.height / 42f);
            _white = Texture2D.whiteTexture;
            _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u), alignment = TextAnchor.MiddleCenter, richText = true };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 1.8f), fontStyle = FontStyle.Bold };
            _small = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 0.8f) };
            _panel = new GUIStyle(GUI.skin.box);
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 0.85f), wordWrap = true, richText = true };
            UiSkin.Panel(_panel);
            UiSkin.Button(_button);
            // The selected weapon: gold outline and text.
            _buttonOn = new GUIStyle(_button) { normal = _button.onNormal, hover = _button.onHover };
            _tagName = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 0.72f), fontStyle = FontStyle.Bold };
            _tagHp = new GUIStyle(_label) { fontSize = Mathf.RoundToInt(u * 0.72f) };
            _kamikaze = new GUIStyle(_big) { fontStyle = FontStyle.Bold };
            _oneLine = new GUIStyle(_label) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            _weaponCaption = new GUIStyle(_small) { fontSize = Mathf.RoundToInt(u * 0.7f), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            UiFont.Apply(_label, _big, _small, _panel, _button, _buttonOn, _tagName, _tagHp, _kamikaze, _oneLine, _weaponCaption);
            _weaponIcons = Resources.Load<Texture2D>("UI/weapon-icons");
            _teamPortraits = Resources.Load<Texture2D>("UI/team-portraits");
        }

        void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        /// <summary>
        /// Under the timer: who plays next. Turns go to the team with the lowest delay
        /// (this turn's cost is added to the playing team first), ties rotating in seat order.
        /// </summary>
        void TurnOrder(Snapshot s, Rect timer, float u)
        {
            int teams = s.TeamDelay.Count;
            if (teams < 2 || s.ActiveTeam < 0 || s.Phase == Phase.GameOver) return;
            var delay = new int[teams];
            for (int t = 0; t < teams; t++) delay[t] = s.TeamDelay[t];
            delay[s.ActiveTeam] += s.TurnCost;
            var order = new System.Collections.Generic.List<int>();
            var alive = new bool[teams];
            foreach (var w in s.Worms) if (w.Alive && w.Team < teams) alive[w.Team] = true;
            // Simulate the next three turns, each costing a plain turn.
            int current = s.ActiveTeam;
            for (int k = 0; k < 3; k++)
            {
                int best = -1;
                for (int i = 1; i <= teams; i++)
                {
                    int t = (current + i) % teams;
                    if (!alive[t]) continue;
                    if (best < 0 || delay[t] < delay[best]) best = t;
                }
                if (best < 0) break;
                order.Add(best);
                delay[best] += C.TurnDelay;
                current = best;
            }
            if (order.Count == 0) return;
            float chip = u * 0.9f, x = timer.center.x - (order.Count * (chip + u * 0.3f)) / 2f + u * 1.6f;
            float y = timer.yMax + u * 0.35f;
            Shadowed(new Rect(x - u * 3.4f, y - u * 0.15f, u * 3.2f, chip + u * 0.3f), "Tiếp:", _small, Color.white);
            foreach (int t in order)
            {
                UiSkin.Pill(new Rect(x, y, chip, chip), TeamColors.Of(t));
                x += chip + u * 0.3f;
            }
            if (s.TurnCost != C.TurnDelay)
                Shadowed(new Rect(timer.x - u * 2f, y + chip + u * 0.1f, timer.width + u * 4f, u), "lượt này +" + s.TurnCost + " trễ", _small, new Color(1f, 0.8f, 0.4f));
        }

        /// <summary>A rounded tag at <paramref name="anchor"/> (screen, y down): name over HP, in the team's color.</summary>
        void NameTag(WormSnap worm, Vector2 anchor, float u, bool active)
        {
            string name = Source.WormName(worm.Id) ?? Source.TeamName(worm.Team);
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
            var safe = Screen.safeArea;
            float safeTop = h - safe.yMax, safeBottom = safe.yMin;
            bool compact = safe.width < 700f;

            // Name tags over every worm: the player's name, then HP (pending damage in red).
            var cam = Source.Presenter.Rig.Camera;
            foreach (var worm in s.Worms)
            {
                if (!worm.Alive || !Source.Presenter.Actors.TryGetLabelPoint(worm.Id, cam, out var p)) continue;
                if (p.x < safe.xMin || p.x > safe.xMax || p.y < safe.yMin || p.y > safe.yMax) continue;
                NameTag(worm, new Vector2(p.x, h - p.y), u, worm.Id == s.ActiveWorm && s.Phase != Phase.GameOver);
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

            // Top center: the turn timer in a badge ringed with the playing team's color.
            float seconds = s.Phase == Phase.Retreat ? s.RetreatTicksLeft / (float)C.TicksPerSecond : s.TurnTicksLeft / (float)C.TicksPerSecond;
            var timer = new Rect(safe.center.x - u * 2.2f, safeTop + u * 0.4f, u * 4.4f, u * 2.4f);
            if (s.ActiveTeam >= 0) UiSkin.Pill(new Rect(timer.x - 3, timer.y - 3, timer.width + 6, timer.height + 6), TeamColors.Of(s.ActiveTeam));
            UiSkin.Pill(timer, new Color(0.07f, 0.09f, 0.13f, 0.9f));
            Shadowed(timer, Mathf.CeilToInt(Mathf.Max(0, seconds)).ToString(), _big,
                seconds <= 5 && s.Phase == Phase.Aiming ? new Color(1f, 0.45f, 0.35f) : Color.white);

            TurnOrder(s, timer, u);

            // Top right: wind, a bar filling from the middle toward where it blows.
            float windW = u * 8;
            var windBadge = new Rect(safe.xMax - windW - u * 1.2f, safeTop + u * 0.4f, windW + u * 1.2f, u * 2.7f);
            UiSkin.Pill(windBadge, new Color(0.07f, 0.09f, 0.13f, 0.8f));
            var windRect = new Rect(windBadge.x + u * 0.6f, windBadge.y + u * 1.45f, windW, u * 0.5f);
            UiSkin.Pill(windRect, new Color(1f, 1f, 1f, 0.15f));
            float frac = Mathf.Clamp(s.Wind / C.WindMax, -1, 1);
            float mid = windRect.x + windRect.width / 2;
            if (Mathf.Abs(frac) > 0.02f)
                UiSkin.Pill(frac >= 0 ? new Rect(mid, windRect.y, windRect.width / 2 * frac, windRect.height)
                                      : new Rect(mid + windRect.width / 2 * frac, windRect.y, -windRect.width / 2 * frac, windRect.height),
                    new Color(0.55f, 0.85f, 1f));
            Box(new Rect(mid - 1f, windRect.y, 2f, windRect.height), Color.white);
            string windText = Mathf.Abs(frac) < 0.05f ? "Gió: lặng  0" : (frac > 0 ? "Gió  → " : "←  Gió ") + Mathf.RoundToInt(Mathf.Abs(s.Wind));
            Shadowed(new Rect(windBadge.x, windBadge.y + u * 0.15f, windBadge.width, u * 1.2f), windText, _small, Color.white);

            // Bottom: one rounded HP bar per team, the playing team's lit up.
            int teams = Source.Presenter.TeamCount;
            float barW = Mathf.Min(u * 10, (safe.width - u * 1.2f) / Mathf.Max(1, teams));
            for (int t = 0; t < teams; t++)
            {
                int hp = 0, members = 0;
                foreach (var worm in s.Worms)
                {
                    if (worm.Team != t) continue;
                    members++;
                    if (worm.Alive) hp += worm.Hp;
                }
                float healthLeft = compact ? safe.center.x - barW * teams / 2
                    : safe.xMax - barW * teams - u * 0.55f;
                var back = new Rect(healthLeft + t * barW + u * 0.08f,
                    compact ? safeTop + u * 5.2f : h - safeBottom - u * 2.95f, barW - u * 0.16f, u * 2.7f);
                UiSkin.Pill(back, t == s.ActiveTeam ? new Color(0.14f, 0.17f, 0.24f, 0.92f) : new Color(0.07f, 0.09f, 0.13f, 0.75f));
                float portraitW = _teamPortraits != null && barW >= u * 6f ? Mathf.Min(u * 1.6f, barW * 0.26f) : 0f;
                if (portraitW > 0)
                    GUI.DrawTextureWithTexCoords(new Rect(back.x + u * 0.12f, back.y + u * 0.16f, portraitW, portraitW * 4f / 3f),
                        _teamPortraits, new Rect((t % 4) * 0.25f, 0, 0.25f, 1f), true);
                var r = new Rect(back.x + portraitW + u * 0.28f, back.y + u * 1.9f,
                    back.width - portraitW - u * 0.48f, u * 0.5f);
                UiSkin.Pill(r, new Color(1f, 1f, 1f, 0.12f));
                float fill = Mathf.Clamp01(hp / (float)(C.StartHp * Mathf.Max(1, members)));
                if (fill > 0) UiSkin.Pill(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * fill), r.height), TeamColors.Of(t));
                string teamText = compact ? "#" + (t + 1) + " " + hp : Source.TeamName(t) + "  " + hp + "/" + (C.StartHp * members);
                Shadowed(new Rect(r.x, back.y + u * 0.25f, r.width, u * 1.1f), teamText, _small,
                    t == s.ActiveTeam ? TeamColors.Of(t) : Color.white);
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
                float weaponX = safe.xMin + u * 0.6f;
                float weaponY = compact ? safeTop + u * 8.2f : h - safeBottom - u * 6.8f;
                float weaponWidth = Mathf.Min(weaponSize.x + u * 1.4f, safe.width - u * 1.2f);
                UiSkin.Pill(new Rect(weaponX, weaponY, weaponWidth, u * 1.5f), new Color(0.07f, 0.09f, 0.13f, 0.8f));
                Shadowed(new Rect(weaponX + u * 0.4f, weaponY + u * 0.12f, weaponWidth - u * 0.8f, u * 1.2f), weapon, _oneLine, Color.white);
                if (def.Targets) Shadowed(new Rect(0, h - u * 5.5f, w, u * 1.2f), "Bấm chuột trái vào bản đồ để chọn mục tiêu", _label, Color.white);
                if (Source.Controls.Charging)
                {
                    var r = new Rect(safe.center.x - u * 6, h - safeBottom - u * 6.2f, u * 12, u * 0.8f);
                    UiSkin.Pill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0.07f, 0.09f, 0.13f, 0.85f));
                    UiSkin.Pill(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Source.Controls.Power), r.height),
                        Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.3f, 0.2f), Source.Controls.Power));
                }
                if (!compact) QuickWeaponBar(s, u, safe, h);
                WeaponMenu(s, w, h, u, compact, safe);
            }
            else
            {
                _weaponMenu = false;
            }

            if (Play.TouchInput.Visible && Source.IsLocalTurn && (s.Phase == Phase.Aiming || s.Phase == Phase.Retreat)) TouchButtons(s, h);

            var audio = Audio.AudioManager.Instance;
            if (audio != null && GUI.Button(new Rect(safe.xMin + u * 0.5f, safeTop + u * 0.5f, u * 5.5f, u * 1.9f), audio.Muted ? "Âm: tắt" : "Âm: bật", _button))
                audio.SetMuted(!audio.Muted);

            Kamikaze(w, h, u);
            if (s.Phase == Phase.GameOver) GameOverPanel(s, w, h, u);
        }

        /// <summary>Desktop quick picks follow the five visible icon slots in the approved concept.</summary>
        void QuickWeaponBar(Snapshot s, float u, Rect safe, float h)
        {
            const int slots = 5;
            float size = u * 3.55f, gap = u * 0.25f;
            float x = safe.xMin + u * 0.6f, y = h - safe.yMin - size - u * 0.55f;
            float width = slots * size + (slots - 1) * gap;
            UiSkin.Pill(new Rect(x - u * 0.32f, y - u * 0.32f,
                width + u * 0.64f, size + u * 0.64f), new Color(0.055f, 0.09f, 0.14f, 0.9f));
            bool oldEnabled = GUI.enabled;
            for (int i = 0; i < slots; i++)
            {
                var id = (WeaponId)i;
                var r = new Rect(x + i * (size + gap), y, size, size);
                bool selected = s.ActiveWeapon == id;
                if (selected) UiSkin.Pill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), UiSkin.Accent);
                int ammo = s.ActiveAmmo[i];
                GUI.enabled = oldEnabled && ammo != 0 && !s.AttackInProgress;
                if (GUI.Button(r, _weaponIcons == null ? WeaponName(id) : "", selected ? _buttonOn : _button))
                    Source.SelectWeapon(id);
                if (_weaponIcons != null)
                {
                    var oldColor = GUI.color;
                    if (!GUI.enabled) GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    GUI.DrawTextureWithTexCoords(new Rect(r.x + u * 0.35f, r.y + u * 0.2f,
                        size - u * 0.7f, size - u * 0.85f), _weaponIcons,
                        new Rect((i % 4) * 0.25f, (1 - i / 4) * 0.5f, 0.25f, 0.5f), true);
                    GUI.color = oldColor;
                }
                string count = ammo < 0 ? "∞" : ammo.ToString();
                Shadowed(new Rect(r.x, r.yMax - u * 0.8f, r.width, u * 0.7f), count, _weaponCaption, Color.white);
            }
            GUI.enabled = oldEnabled;
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
        void WeaponMenu(Snapshot s, float w, float h, float u, bool compact, Rect safe)
        {
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Tab)
            {
                _weaponMenu = !_weaponMenu;
                currentEvent.Use();
            }
            else if (_weaponMenu && currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            {
                _weaponMenu = false;
                currentEvent.Use();
            }

            float bottom = h - safe.yMin - (compact ? u * 1.2f : u * 3.7f);
            if (Play.TouchInput.Visible)
            {
                var fire = Source.Touch.Layout.Fire;
                bottom = Mathf.Min(bottom, h - (fire.Y + fire.H) - u * 0.6f);
            }
            float bh = compact ? Mathf.Max(44f, u * 1.9f) : u * 2.4f;
            float right = safe.xMax - u * 0.6f;
            var toggle = new Rect(right - u * 6.4f, bottom - bh, u * 6.4f, bh);
            var grenade = new Rect(toggle.x - u * 5.4f, toggle.y, u * 5f, bh);
            float top = toggle.y;

            if (GUI.Button(toggle, _weaponMenu ? "Đóng" : "Vũ khí (Tab)", _button)) _weaponMenu = !_weaponMenu;
            int grenadeAmmo = s.ActiveAmmo[(int)WeaponId.Grenade];
            var oldEnabled = GUI.enabled;
            GUI.enabled = grenadeAmmo != 0 && !s.AttackInProgress;
            if (GUI.Button(grenade, "Lựu đạn", s.ActiveWeapon == WeaponId.Grenade ? _buttonOn : _button))
            {
                Source.SelectWeapon(WeaponId.Grenade);
                _weaponMenu = false;
            }
            GUI.enabled = oldEnabled;

            if (Weapons.Get(s.ActiveWeapon).UsesFuse)
            {
                float chip = compact ? 44f : u * 1.9f;
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

            int cols = compact ? 2 : 3;
            int rows = (Weapons.Count + cols - 1) / cols;
            float cw = compact ? (safe.width - u * 1.6f) / cols : u * 7.4f;
            float ch = compact ? Mathf.Max(56f, u * 3.6f) : u * 3.2f;
            var panel = new Rect(Mathf.Max(safe.xMin + u * 0.3f, right - cw * cols - u),
                Mathf.Max(h - safe.yMax + u * 0.3f, top - ch * rows - u * 1.3f), cw * cols + u, ch * rows + u);
            bool dismissed = _weaponMenu && currentEvent.type == EventType.MouseDown &&
                !panel.Contains(currentEvent.mousePosition) && !toggle.Contains(currentEvent.mousePosition) &&
                !grenade.Contains(currentEvent.mousePosition);
            if (dismissed)
            {
                _weaponMenu = false;
                currentEvent.Use();
            }
            if (_weaponMenu)
            {
                GUI.Box(panel, GUIContent.none, _panel);
                for (int i = 0; i < Weapons.Count; i++)
                {
                    var id = (WeaponId)i;
                    int ammo = s.ActiveAmmo[i];
                    string label = WeaponName(id) + "\n" + (ammo < 0 ? "∞" : "còn " + ammo) + " · trễ " + Weapons.Get(id).Delay;
                    var r = new Rect(panel.x + u * 0.5f + (i % cols) * cw, panel.y + u * 0.5f + (i / cols) * ch,
                        cw - u * 0.3f, ch - u * 0.3f);
                    GUI.enabled = ammo != 0 && !s.AttackInProgress;
                    bool hasIcon = _weaponIcons != null && i < 8;
                    if (GUI.Button(r, hasIcon ? "" : label, id == s.ActiveWeapon ? _buttonOn : _button))
                    {
                        Source.SelectWeapon(id);
                        _weaponMenu = false;
                    }
                    if (hasIcon)
                    {
                        var oldColor = GUI.color;
                        if (!GUI.enabled) GUI.color = new Color(1, 1, 1, 0.38f);
                        float iconSize = Mathf.Min(r.width * 0.42f, ch * 0.48f);
                        var iconRect = new Rect(r.center.x - iconSize * 0.5f, r.y + u * 0.08f, iconSize, iconSize);
                        GUI.DrawTextureWithTexCoords(iconRect, _weaponIcons,
                            new Rect((i % 4) * 0.25f, (1 - i / 4) * 0.5f, 0.25f, 0.5f), true);
                        GUI.Label(new Rect(r.x + 2, iconRect.yMax, r.width - 4, r.yMax - iconRect.yMax - 2), label, _weaponCaption);
                        GUI.color = oldColor;
                    }
                    GUI.enabled = oldEnabled;
                }
            }
            var blocked = new Rect(grenade.x - u * 0.2f, top, right - grenade.x + u * 0.2f, toggle.yMax - top);
            if (!compact) blocked = new Rect(safe.xMin, Mathf.Min(top, h - safe.yMin - u * 4.5f),
                safe.width, h - safe.yMin - Mathf.Min(top, h - safe.yMin - u * 4.5f));
            if (_weaponMenu || dismissed) blocked = new Rect(0, 0, w, h);
            Play.KeyboardInput.BlockedArea = blocked;
            Play.TouchInput.BlockedArea = blocked;
        }

        /// <summary>White flash, then a "CẢM TỬ!" banner that pops in and fades.</summary>
        void Kamikaze(float w, float h, float u)
        {
            float age = Time.time - Source.Presenter.KamikazeAt;
            if (age < 0 || age > 2.4f) return;
            var old = GUI.color;
            float flash = QualitySettingsManager.ReducedMotion ? 0f : Mathf.Clamp01(1f - age / 0.35f);
            if (flash > 0)
            {
                GUI.color = new Color(1f, 0.97f, 0.9f, flash * 0.85f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }
            GUI.color = old;
            float pop = QualitySettingsManager.ReducedMotion ? 1f
                : age < 0.25f ? Mathf.Lerp(2.2f, 1f, age / 0.25f) : 1f + 0.04f * Mathf.Sin(age * 18f);
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
