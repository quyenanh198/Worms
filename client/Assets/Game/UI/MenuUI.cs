using System;
using UnityEngine;
using Worms.Game.Audio;
using Worms.Game.Core;
using Worms.Game.Net;
using Worms.Game.Render;
using Worms.Protocol;

namespace Worms.Game.UI
{
    /// <summary>
    /// Menu and lobby (docs/PLAN.md §3.4.2, §3.15): login, quick match,
    /// private rooms with invite links, ready/start, offline sandbox.
    /// </summary>
    public sealed class MenuUI : MonoBehaviour
    {
        public NetClient Net;
        public Action StartSandbox;
        public bool Hidden;

        string _code = string.Empty;
        string _username = string.Empty;
        string _password = string.Empty;
        string _loginError;
        bool _loggingIn;
        /// <summary>"Chơi với máy" was pressed: add a bot as soon as our new room arrives.</summary>
        bool _wantBot;
        bool _showSettings;
        string _toast;
        float _toastUntil;
        GUIStyle _title, _text, _small, _button, _smallButton, _field, _track, _thumb, _buttonOn, _smallLeft, _smallCenterOneLine;
        float _sliderH;
        float _u;

        /// <summary>Center of the menu column: left of center on wide screens, so the squad shows on the right.</summary>
        float Cx => Screen.width >= Screen.height * 1.3f ? Screen.width * 0.3f : Screen.width / 2f;

        // Squad naming.
        bool _naming;
        readonly string[] _nameEdit = new string[WormNames.PerTeam];

        // Store state.
        bool _store;
        int _tab;
        byte _preview;
        static readonly string[] Tabs = { "Mũ", "Áo giáp", "Vũ khí" };

        void Update()
        {
            var session = Net.Session;
            if (_wantBot && session.InRoom && session.IsHost && session.Lobby.State == RoomState.Lobby)
            {
                _wantBot = false;
                if (session.Lobby.Players.Count == 1) session.AddBot();
            }

            var err = Net.Session.LastError;
            if (err != null && err != ErrorCodes.Unauthorized && err != ErrorCodes.BadVersion)
            {
                _toast = ErrorText(err);
                _toastUntil = Time.unscaledTime + 4f;
                Net.Session.ClearError();
            }
        }

        static string ErrorText(string code)
        {
            switch (code)
            {
                case ErrorCodes.RoomNotFound: return "Không tìm thấy phòng";
                case ErrorCodes.RoomFull: return "Phòng đã đủ người";
                case ErrorCodes.RoomBusy: return "Phòng đang chơi";
                case ErrorCodes.NotHost: return "Chỉ chủ phòng mới bắt đầu được";
                case ErrorCodes.NotReady: return "Cần ít nhất 2 đội (chơi một mình thì thêm máy) và mọi người sẵn sàng";
                case ErrorCodes.RateLimited: return "Thao tác quá nhanh";
                case ErrorCodes.ServerFull: return "Server đang đầy, thử lại sau ít phút";
                default: return "Lỗi: " + code;
            }
        }

        void Styles()
        {
            float u = Mathf.Max(12f, Mathf.Min(Screen.height / 34f, Screen.width / 40f));
            if (_title != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(u * 3), fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.8f, 0.4f);
            _text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.width < 700 ? Mathf.Max(14f, u) : u), wordWrap = true, richText = true };
            _text.normal.textColor = Color.white;
            _small = new GUIStyle(_text) { fontSize = Mathf.RoundToInt(Mathf.Max(12f, u * 0.8f)) };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.width < 700 ? Mathf.Max(16f, u) : u) };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(u), alignment = TextAnchor.MiddleCenter };
            _smallButton = new GUIStyle(_button) { fontSize = Mathf.RoundToInt(Mathf.Max(14f, u * 0.8f)) };
            _buttonOn = new GUIStyle(_smallButton);
            _smallLeft = new GUIStyle(_small) { alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = true };
            _smallCenterOneLine = new GUIStyle(_small) { wordWrap = false, richText = true, clipping = TextClipping.Clip };
            UiSkin.Button(_button);
            UiSkin.Button(_smallButton);
            UiSkin.Button(_buttonOn);
            _buttonOn.normal = _buttonOn.onNormal;
            _buttonOn.hover = _buttonOn.onHover;
            UiSkin.Field(_field);
            (_track, _thumb, _sliderH) = UiSkin.Slider(u);
            UiFont.Apply(_title, _text, _small, _button, _smallButton, _field, _buttonOn, _smallLeft, _smallCenterOneLine);
        }

        bool Button(ref float y, string label, bool enabled = true)
        {
            float w = _u * 14, h = Screen.width < 700 ? Mathf.Max(44f, _u * 2.2f) : _u * 2.2f;
            var old = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(new Rect(Cx - w / 2, y, w, h), label, _button);
            GUI.enabled = old;
            y += h + _u * 0.5f;
            return clicked;
        }

        void Line(ref float y, string text, GUIStyle style = null, float lines = 1.5f)
        {
            style = style ?? _text;
            GUI.Label(new Rect(Cx - _u * 9.5f, y, _u * 19f, _u * lines), text, style);
            y += _u * lines;
        }

        void LateUpdate()
        {
            // The squad leader wears the player's outfit, or the item being tried on in the store.
            var scene = MenuScene.Instance;
            if (scene == null) return;
            var profile = Net.Session.Profile;
            var look = profile != null ? profile.Loadout : default;
            scene.LeaderWeapon = null;
            if (_store && _preview != 0)
            {
                var item = Cosmetics.Get(_preview);
                if (item != null)
                {
                    look[item.Slot] = item.Id;
                    scene.LeaderWeapon = item.Slot == CosmeticSlot.Grenade ? Worms.Sim.WeaponId.Grenade
                        : item.Slot == CosmeticSlot.Bat ? Worms.Sim.WeaponId.BaseballBat
                        : item.Slot == CosmeticSlot.Bazooka ? Worms.Sim.WeaponId.Bazooka : (Worms.Sim.WeaponId?)null;
                }
            }
            scene.LeaderLoadout = look;
        }

        void OnGUI()
        {
            UiFont.UseForSkin();
            if (Hidden) return;
            Styles();
            if (Screen.width < 700 && _showSettings)
            {
                AudioSettings();
                return;
            }

            // A soft panel behind the menu column keeps it readable over the 3D scene.
            UiSkin.Pill(new Rect(Cx - _u * 10.6f, Screen.height * 0.04f, _u * 21.2f, Screen.height * 0.92f), new Color(0.05f, 0.07f, 0.1f, 0.55f));

            // The title drops in with a bounce; the squad's name follows once they have landed.
            float age = MenuScene.Instance != null ? MenuScene.Instance.Age : 10f;
            float drop = Mathf.Clamp01(age / 0.7f);
            float bounce = 1f - Mathf.Pow(1f - drop, 3f) + Mathf.Sin(drop * Mathf.PI) * 0.12f;
            float y = Screen.height * 0.07f - (1f - bounce) * _u * 6f;
            GUI.Label(new Rect(Cx - _u * 10f, y, _u * 20f, _u * 4), "WORMS", _title);
            y += _u * 3.6f;
            float squadAlpha = Mathf.Clamp01((age - MenuScene.SquadReady) / 0.6f);
            if (squadAlpha > 0)
            {
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, squadAlpha);
                GUI.Label(new Rect(Cx - _u * 10f, y, _u * 20f, _u * 1.2f), "BIỆT ĐỘI SÂU", _small);
                GUI.color = old;
            }
            y += _u * 1.6f;

            if (Net.Outdated) Outdated(ref y);
            else if (Net.Unauthorized) Login(ref y);
            else if (!Net.Connected || !Net.Session.HasHello) Offline(ref y);
            else if (Net.Session.InRoom) Room(ref y);
            else if (_store) Store(ref y);
            else if (_naming) Naming(ref y);
            else Main(ref y);

            GoldBadge();
            SquadNames();

            AudioSettings();

            if (_toast != null && Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(0, Screen.height - _u * 3, Screen.width, _u * 2), "<color=#ff8866>" + _toast + "</color>", _text);
        }

        void AudioSettings()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            if (Screen.width < 700)
            {
                var safe = Screen.safeArea;
                float toggleW = Mathf.Max(100f, _u * 7f), toggleH = Mathf.Max(44f, _u * 2.5f);
                var toggle = new Rect(safe.xMax - toggleW - _u * 0.5f,
                    Screen.height - safe.yMin - toggleH - _u * 0.5f, toggleW, toggleH);
                if (!_showSettings)
                {
                    if (GUI.Button(toggle, "Cài đặt", _smallButton)) _showSettings = true;
                    return;
                }

                var old = GUI.color;
                GUI.color = new Color(0.02f, 0.05f, 0.08f, 0.75f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = old;
                float panelW = Mathf.Min(safe.width - _u * 2f, _u * 26f);
                float panelH = Mathf.Min(safe.height - _u * 2f, Mathf.Max(340f, _u * 25f));
                var panel = new Rect(safe.center.x - panelW * 0.5f,
                    Screen.height - safe.center.y - panelH * 0.5f, panelW, panelH);
                UiSkin.Pill(panel, new Color(0.06f, 0.11f, 0.17f, 0.97f));
                float px = panel.x + _u, pw = panel.width - _u * 2f, row = panel.y + _u;
                GUI.Label(new Rect(px, row, pw, _u * 2f), "Cài đặt", _text);
                row += _u * 2.6f;
                GUI.Label(new Rect(px, row, pw, _u * 1.2f), "Nhạc", _small);
                row += _u * 1.2f;
                float musicValue = GUI.HorizontalSlider(new Rect(px, row, pw, 32f), audio.MusicVolume, 0, 1, _track, _thumb);
                row += 46f;
                GUI.Label(new Rect(px, row, pw, _u * 1.2f), "Hiệu ứng", _small);
                row += _u * 1.2f;
                float sfxValue = GUI.HorizontalSlider(new Rect(px, row, pw, 32f), audio.SfxVolume, 0, 1, _track, _thumb);
                row += 46f;
                if (!Mathf.Approximately(musicValue, audio.MusicVolume) || !Mathf.Approximately(sfxValue, audio.SfxVolume))
                    audio.SetVolumes(sfxValue, musicValue);
                int currentChoice = Render.QualitySettingsManager.Choice;
                if (GUI.Button(new Rect(px, row, pw, 44f), "Đồ họa: " + Render.QualitySettingsManager.Label(currentChoice), _smallButton))
                    Render.QualitySettingsManager.Choice = currentChoice >= 2 ? -1 : currentChoice + 1;
                row += 44f + _u * 0.5f;
                bool reduced = Render.QualitySettingsManager.ReducedMotion;
                if (GUI.Button(new Rect(px, row, pw, 44f), reduced ? "Chuyển động: giảm" : "Chuyển động: đầy đủ", _smallButton))
                    Render.QualitySettingsManager.ReducedMotion = !reduced;
                if (GUI.Button(toggle, "Đóng", _smallButton)) _showSettings = false;
                return;
            }
            float w = _u * 9, x = Screen.width - w - _u, y = _u * 0.5f;
            GUI.Label(new Rect(x, y, w, _u * 1.2f), "Nhạc", _small);
            float music = GUI.HorizontalSlider(new Rect(x, y + _u * 1.2f, w, _sliderH), audio.MusicVolume, 0, 1, _track, _thumb);
            GUI.Label(new Rect(x, y + _u * 2.4f, w, _u * 1.2f), "Hiệu ứng", _small);
            float sfx = GUI.HorizontalSlider(new Rect(x, y + _u * 3.6f, w, _sliderH), audio.SfxVolume, 0, 1, _track, _thumb);
            if (!Mathf.Approximately(music, audio.MusicVolume) || !Mathf.Approximately(sfx, audio.SfxVolume)) audio.SetVolumes(sfx, music);

            int choice = Render.QualitySettingsManager.Choice;
            float optionH = Mathf.Max(44f, _u * 1.7f);
            if (GUI.Button(new Rect(x - _u * 4f, y + _u * 5.1f, w + _u * 4f, optionH), "Đồ họa: " + Render.QualitySettingsManager.Label(choice), _smallButton))
                Render.QualitySettingsManager.Choice = choice >= 2 ? -1 : choice + 1;
            bool reducedMotion = Render.QualitySettingsManager.ReducedMotion;
            if (GUI.Button(new Rect(x - _u * 4f, y + _u * 5.1f + optionH + _u * 0.3f, w + _u * 4f, optionH),
                reducedMotion ? "Chuyển động: giảm" : "Chuyển động: đầy đủ", _smallButton))
                Render.QualitySettingsManager.ReducedMotion = !reducedMotion;
        }

        void Outdated(ref float y)
        {
            Line(ref y, "Game vừa được cập nhật. Hãy tải lại trang hoặc cài bản mới.", _text, 3);
            if (NetClient.IsWeb && Button(ref y, "Tải lại")) Application.OpenURL(Application.absoluteURL);
        }

        void Offline(ref float y)
        {
            Line(ref y, Net.Status);
            y += _u;
            if (Button(ref y, "Chơi thử offline")) StartSandbox?.Invoke();
        }

        void Login(ref float y)
        {
            string chat = ServerUrl.ChatBase(Net.Url);
            if (NetClient.IsWeb)
            {
                Line(ref y, "Hãy đăng nhập tài khoản Chat trong trình duyệt này rồi thử lại.", _text, 3);
                if (Button(ref y, "Mở Chat")) Application.OpenURL(chat);
                if (Button(ref y, "Thử lại")) Net.Reconnect();
            }
            else
            {
                Line(ref y, "Đăng nhập bằng tài khoản Chat (" + new Uri(chat).Host + ")");
                float w = _u * 14, h = Screen.width < 700 ? Mathf.Max(44f, _u * 2f) : _u * 2f;
                GUI.SetNextControlName("user");
                _username = GUI.TextField(new Rect(Cx - w / 2, y, w, h), _username, 32, _field);
                y += h + _u * 0.4f;
                _password = GUI.PasswordField(new Rect(Cx - w / 2, y, w, h), _password, '•', 128, _field);
                y += h + _u * 0.6f;
                if (Button(ref y, _loggingIn ? "Đang đăng nhập…" : "Đăng nhập", !_loggingIn && _username.Length > 0 && _password.Length > 0))
                {
                    _loggingIn = true;
                    _loginError = null;
                    StartCoroutine(ChatLogin.Login(chat, _username, _password,
                        e => { _loggingIn = false; _loginError = e; },
                        () => { _loggingIn = false; _password = string.Empty; Net.Reconnect(); }));
                }
                if (_loginError != null) Line(ref y, "<color=#ff8866>" + _loginError + "</color>");
                if (Button(ref y, "Chưa có tài khoản? Mở Chat")) Application.OpenURL(chat);
            }
            if (Button(ref y, "Chơi thử offline")) StartSandbox?.Invoke();
        }

        void Main(ref float y)
        {
            _store = false;
            _naming = false;
            Line(ref y, Net.Status);
            y += _u * 0.5f;
            if (Button(ref y, "Ghép trận nhanh")) Net.Session.QuickMatch();
            if (Button(ref y, "Chơi với máy"))
            {
                _wantBot = true;
                Net.Session.CreateRoom();
            }
            if (Button(ref y, "Tạo phòng")) Net.Session.CreateRoom();
            float w = _u * 14, h = Screen.width < 700 ? Mathf.Max(44f, _u * 2.2f) : _u * 2.2f;
            _code = GUI.TextField(new Rect(Cx - w / 2, y, w * 0.45f, h), _code.ToUpperInvariant(), 4, _field);
            var old = GUI.enabled;
            GUI.enabled = _code.Length == 4;
            if (GUI.Button(new Rect(Cx - w / 2 + w * 0.5f, y, w * 0.5f, h), "Vào phòng", _button)) Net.Session.JoinRoom(_code);
            GUI.enabled = old;
            y += h + _u * 0.5f;
            if (Button(ref y, "Cửa hàng", Net.Session.Profile != null))
            {
                _store = true;
                _preview = 0;
            }
            if (Button(ref y, "Đặt tên biệt đội", Net.Session.Profile != null))
            {
                _naming = true;
                var names = Net.Session.Profile.WormNames;
                for (int i = 0; i < _nameEdit.Length; i++) _nameEdit[i] = i < names.Count ? names[i] : string.Empty;
            }
            if (Button(ref y, "Chơi thử offline")) StartSandbox?.Invoke();
            if (!NetClient.IsWeb && Button(ref y, "Đăng xuất"))
            {
                ChatLogin.Logout();
                Net.Reconnect();
            }
        }

        /// <summary>Four text fields, one per worm; saved on the server with the profile.</summary>
        void Naming(ref float y)
        {
            var profile = Net.Session.Profile;
            if (profile == null) { _naming = false; return; }
            Line(ref y, "Đặt tên cho 4 con sâu của bạn (tối đa " + WormNames.MaxLength + " ký tự)", _small, 1.4f);
            float w = _u * 14, h = Screen.width < 700 ? Mathf.Max(44f, _u * 2f) : _u * 2f;
            for (int i = 0; i < _nameEdit.Length; i++)
            {
                GUI.Label(new Rect(Cx - w / 2 - _u * 2.2f, y, _u * 2f, h), (i + 1) + ".", _small);
                _nameEdit[i] = GUI.TextField(new Rect(Cx - w / 2, y, w, h), _nameEdit[i] ?? string.Empty, WormNames.MaxLength, _field);
                y += h + _u * 0.35f;
            }
            y += _u * 0.3f;
            if (Button(ref y, "Tên ngẫu nhiên"))
            {
                var pick = WormNames.Pick((uint)UnityEngine.Random.Range(1, int.MaxValue));
                for (int i = 0; i < _nameEdit.Length; i++) _nameEdit[i] = pick[i];
            }
            if (Button(ref y, "Lưu"))
            {
                Net.Session.SetWormNames(_nameEdit);
                _naming = false;
                _toast = "Đã lưu tên biệt đội";
                _toastUntil = Time.unscaledTime + 2.5f;
            }
            if (Button(ref y, "Quay lại")) _naming = false;
        }

        /// <summary>The squad's names over their heads in the menu scene (the player's own names).</summary>
        void SquadNames()
        {
            var scene = MenuScene.Instance;
            var profile = Net.Session.Profile;
            if (scene == null || profile == null) return;
            for (int i = 0; i < WormNames.PerTeam && i < profile.WormNames.Count; i++)
            {
                if (!scene.TryGetLabel(i, out var at)) continue;
                string name = _naming && !string.IsNullOrEmpty(_nameEdit[i]) ? WormNames.Clean(_nameEdit[i]) : profile.WormNames[i];
                var size = _smallLeft.CalcSize(new GUIContent(name));
                var r = new Rect(at.x - size.x / 2 - _u * 0.5f, at.y - _u * 1.3f, size.x + _u, _u * 1.3f);
                UiSkin.Pill(r, new Color(0.05f, 0.07f, 0.1f, 0.8f));
                UiSkin.Pill(new Rect(r.x + _u * 0.3f, r.yMax - 3, r.width - _u * 0.6f, 3), TeamColors.Of(i));
                var old = _smallLeft.normal.textColor;
                _smallLeft.normal.textColor = TeamColors.Of(i);
                GUI.Label(new Rect(r.x + _u * 0.5f, r.y, size.x + 2, r.height), name, _smallLeft);
                _smallLeft.normal.textColor = old;
            }
        }

        /// <summary>Top-left: the player's gold.</summary>
        void GoldBadge()
        {
            var profile = Net.Session.Profile;
            if (profile == null) return;
            string text = profile.Gold.ToString("N0").Replace(",", ".") + " vàng";
            float w = _small.CalcSize(new GUIContent(text)).x + _u * 2.6f;
            var r = new Rect(_u * 0.6f, _u * 0.6f, w, _u * 1.8f);
            UiSkin.Pill(r, new Color(0.05f, 0.07f, 0.1f, 0.85f));
            UiSkin.Pill(new Rect(r.x + _u * 0.5f, r.y + _u * 0.45f, _u * 0.9f, _u * 0.9f), CosmeticProps.Gold);
            GUI.Label(new Rect(r.x + _u * 1.6f, r.y, w - _u * 1.8f, r.height), text, _smallLeft);
        }

        /// <summary>
        /// The store: tabs, a row per item with its price or state, buy / wear / take off.
        /// Tapping a row tries it on the squad leader behind the menu.
        /// </summary>
        void Store(ref float y)
        {
            var profile = Net.Session.Profile;
            if (profile == null) { _store = false; return; }
            Line(ref y, "Cửa hàng · chỉ để đẹp, không đổi sức mạnh", _small, 1.3f);

            float w = _u * 19.6f, x0 = Cx - w / 2;
            float tw = w / Tabs.Length;
            for (int t = 0; t < Tabs.Length; t++)
                if (GUI.Button(new Rect(x0 + t * tw + 2, y, tw - 4, _u * 1.8f), Tabs[t], t == _tab ? _buttonOn : _smallButton))
                {
                    _tab = t;
                    _preview = 0;
                }
            y += _u * 2.3f;

            foreach (var item in Cosmetics.All)
            {
                int tab = item.Slot == CosmeticSlot.Hat ? 0 : item.Slot == CosmeticSlot.Armor ? 1 : 2;
                if (tab != _tab) continue;
                bool owned = profile.Owns(item.Id);
                bool worn = profile.Loadout[item.Slot] == item.Id;
                var row = new Rect(x0, y, w, _u * 2.5f);
                UiSkin.Pill(row, _preview == item.Id ? new Color(0.2f, 0.25f, 0.34f, 0.95f) : new Color(0.1f, 0.12f, 0.17f, 0.85f));
                // The whole row (except its button) previews the item.
                if (GUI.Button(new Rect(row.x, row.y, row.width - _u * 6.4f, row.height), GUIContent.none, GUIStyle.none)) _preview = item.Id;
                string slot = _tab == 2 ? SlotName(item.Slot) + " · " : "";
                GUI.Label(new Rect(row.x + _u * 0.7f, row.y + _u * 0.2f, row.width - _u * 7f, _u * 1.2f), "<b>" + item.Name + "</b>", _smallLeft);
                GUI.Label(new Rect(row.x + _u * 0.7f, row.y + _u * 1.2f, row.width - _u * 7f, _u * 1.1f), "<size=" + Mathf.RoundToInt(_u * 0.66f) + ">" + slot + item.Blurb + "</size>", _smallLeft);

                var buy = new Rect(row.xMax - _u * 6.1f, row.y + _u * 0.35f, _u * 5.7f, _u * 1.8f);
                var old = GUI.enabled;
                if (worn)
                {
                    if (GUI.Button(buy, "Bỏ ra", _buttonOn)) Net.Session.Equip(item.Slot, 0);
                }
                else if (owned)
                {
                    if (GUI.Button(buy, "Mặc", _smallButton)) Net.Session.Equip(item.Slot, item.Id);
                }
                else
                {
                    GUI.enabled = profile.Gold >= item.Price;
                    if (GUI.Button(buy, item.Price + " vàng", _smallButton))
                    {
                        Net.Session.Buy(item.Id);
                        _preview = item.Id;
                    }
                }
                GUI.enabled = old;
                y += _u * 2.8f;
            }
            y += _u * 0.3f;
            if (Button(ref y, "Quay lại"))
            {
                _store = false;
                _preview = 0;
            }
        }

        static string SlotName(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Bazooka: return "Bazooka";
                case CosmeticSlot.Grenade: return "Lựu đạn";
                case CosmeticSlot.Bat: return "Gậy";
                default: return "";
            }
        }

        void Room(ref float y)
        {
            var lobby = Net.Session.Lobby;
            if (lobby.IsQuick)
            {
                Line(ref y, lobby.State == RoomState.Lobby ? "Đang tìm đối thủ…" : "Đang vào trận…");
            }
            else
            {
                Line(ref y, "Mã phòng", _small, 1.2f);
                GUI.Label(new Rect(Cx - _u * 10f, y, _u * 20f, _u * 3), lobby.Code, _title);
                y += _u * 3;
                string link = ServerUrl.InviteLink(Net.Url, lobby.Code);
                GUI.Label(new Rect(Cx - _u * 10.4f, y, _u * 20.8f, _u * 1.4f), "<size=" + Mathf.RoundToInt(_u * 0.66f) + ">" + link + "</size>", _smallCenterOneLine);
                y += _u * 1.4f;
                if (Button(ref y, "Sao chép link mời"))
                {
                    GUIUtility.systemCopyBuffer = link;
                    _toast = "Đã sao chép, dán vào Chat để mời";
                    _toastUntil = Time.unscaledTime + 3f;
                }
            }

            bool hostInLobby = Net.Session.IsHost && lobby.State == RoomState.Lobby;
            foreach (var p in lobby.Players)
            {
                string mark = p.IsBot ? "máy" : p.UserId == lobby.HostUserId ? "chủ phòng" : p.Ready ? "sẵn sàng" : "chưa sẵn sàng";
                if (!p.Connected) mark = "mất kết nối";
                string color = ColorUtility.ToHtmlStringRGB(TeamColors.Of(p.Team));
                float rowY = y;
                Line(ref y, "<color=#" + color + ">" + p.Name + "</color>  <size=" + Mathf.RoundToInt(_u * 0.8f) + ">" + mark + "</size>", _text, 1.4f);
                if (p.IsBot && hostInLobby)
                {
                    // Right edge of the button column (buttons are 14u wide, centered).
                    float bw = _u * 3f, bh = _u * 1.2f;
                    if (GUI.Button(new Rect(Cx + _u * 7f - bw, rowY + (_u * 1.4f - bh) / 2f, bw, bh), "Bỏ", _small))
                        Net.Session.RemoveBot(p.UserId);
                }
            }
            y += _u * 0.5f;

            if (!lobby.IsQuick && lobby.State == RoomState.Lobby)
            {
                if (Net.Session.IsHost)
                {
                    // Private rooms hold 4 teams (server: Room.MaxPlayers).
                    if (lobby.Players.Count < 4 && Button(ref y, "+ Thêm máy")) Net.Session.AddBot();
                    bool canStart = lobby.Players.Count >= 2 && lobby.Players.TrueForAll(p => p.UserId == lobby.HostUserId || p.Ready);
                    if (Button(ref y, "Bắt đầu", canStart)) Net.Session.StartMatch();
                }
                else
                {
                    bool ready = lobby.Players.Exists(p => p.UserId == Net.Session.UserId && p.Ready);
                    if (Button(ref y, ready ? "Bỏ sẵn sàng" : "Sẵn sàng")) Net.Session.SetReady(!ready);
                }
            }
            if (lobby.State == RoomState.Finished && Button(ref y, "Chơi lại")) Net.Session.Rematch();
            if (Button(ref y, "Rời phòng")) Net.Session.LeaveRoom();
        }
    }
}
