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
        string _toast;
        float _toastUntil;
        bool _showSettings;
        GUIStyle _title, _text, _small, _button, _field, _optionButton;
        Texture2D _buttonBackground, _buttonHover, _fieldBackground;
        float _u;

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        void OnDestroy()
        {
            if (_buttonBackground != null) Destroy(_buttonBackground);
            if (_buttonHover != null) Destroy(_buttonHover);
            if (_fieldBackground != null) Destroy(_fieldBackground);
        }

        void Update()
        {
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
                case ErrorCodes.NotReady: return "Cần ít nhất 2 người và mọi người sẵn sàng";
                case ErrorCodes.RateLimited: return "Thao tác quá nhanh";
                case ErrorCodes.ServerFull: return "Server đang đầy, thử lại sau ít phút";
                default: return "Lỗi: " + code;
            }
        }

        void Styles()
        {
            float u = Mathf.Max(18f, Mathf.Min(Screen.height / 34f, Screen.width / 40f));
            if (_title != null && Mathf.Approximately(u, _u)) return;
            _u = u;
            _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(u * 3), fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.8f, 0.4f);
            _text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(u), wordWrap = true, richText = true };
            _text.normal.textColor = Color.white;
            _small = new GUIStyle(_text) { fontSize = Mathf.Max(12, Mathf.RoundToInt(u * 0.8f)) };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u) };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(u), alignment = TextAnchor.MiddleCenter };
            if (_buttonBackground == null)
            {
                _buttonBackground = Solid(new Color(0.12f, 0.24f, 0.35f));
                _buttonHover = Solid(new Color(0.2f, 0.37f, 0.48f));
                _fieldBackground = Solid(new Color(0.04f, 0.11f, 0.18f));
            }
            _button.normal.background = _buttonBackground;
            _button.hover.background = _buttonHover;
            _button.active.background = _buttonHover;
            _button.normal.textColor = Color.white;
            _button.hover.textColor = Color.white;
            _button.active.textColor = Color.white;
            _optionButton = new GUIStyle(_button) { fontSize = Mathf.Max(12, Mathf.RoundToInt(u * 0.72f)), wordWrap = true };
            _field.normal.background = _fieldBackground;
            _field.focused.background = _fieldBackground;
            _field.normal.textColor = Color.white;
            _field.focused.textColor = Color.white;
        }

        bool Button(ref float y, string label, bool enabled = true)
        {
            float w = _u * 14, h = Mathf.Max(44f, _u * 2.2f);
            var old = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(new Rect(Screen.width / 2f - w / 2, y, w, h), label, _button);
            GUI.enabled = old;
            y += h + _u * 0.5f;
            return clicked;
        }

        void Line(ref float y, string text, GUIStyle style = null, float lines = 1.5f)
        {
            style = style ?? _text;
            float width = Mathf.Min(Screen.width - _u * 4f, _u * 23f);
            float height = Mathf.Max(_u * lines, style.CalcHeight(new GUIContent(text), width));
            GUI.Label(new Rect((Screen.width - width) * 0.5f, y, width, height), text, style);
            y += height;
        }

        void OnGUI()
        {
            if (Hidden) return;
            Styles();
            if (Screen.width >= 700 || !_showSettings)
            {
            float panelWidth = Mathf.Min(Screen.width - _u * 2f, _u * 25f);
            bool roomCard = !Net.Outdated && !Net.Unauthorized && Net.Connected && Net.Session.HasHello && Net.Session.InRoom;
            float cardUnits = Net.Outdated ? 13f
                : Net.Unauthorized ? 24f
                : !Net.Connected || !Net.Session.HasHello ? 14f
                : 29f;
            var panel = new Rect((Screen.width - panelWidth) * 0.5f,
                roomCard ? _u * 0.4f : Screen.height * 0.05f, panelWidth,
                roomCard ? Screen.height - _u * 0.8f : Mathf.Min(_u * cardUnits, Screen.height * 0.9f));
            DrawPanel(panel);
            float y = Screen.height * 0.08f;
            GUI.Label(new Rect(0, y, Screen.width, _u * 4), "WORMS", _title);
            y += _u * 4.5f;

            if (Net.Outdated) Outdated(ref y);
            else if (Net.Unauthorized) Login(ref y);
            else if (!Net.Connected || !Net.Session.HasHello) Offline(ref y);
            else if (Net.Session.InRoom) Room(ref y);
            else Main(ref y);
            }

            AudioSettings();

            if (_toast != null && Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(0, Screen.height - _u * 3, Screen.width, _u * 2), "<color=#ff8866>" + _toast + "</color>", _text);
        }

        static void DrawPanel(Rect panel)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(0.35f, 0.56f, 0.68f, 0.85f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.07f, 0.14f, 0.22f, 0.91f);
            GUI.DrawTexture(new Rect(panel.x + 2, panel.y + 2, panel.width - 4, panel.height - 4), Texture2D.whiteTexture);
            GUI.color = oldColor;
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
                if (GUI.Button(toggle, _showSettings ? "Đóng" : "Cài đặt", _optionButton)) _showSettings = !_showSettings;
                if (!_showSettings) return;

                float panelW = Mathf.Min(safe.width - _u * 2f, _u * 26f);
                float panelH = Mathf.Min(safe.height - _u * 2f, Mathf.Max(300f, _u * 16f));
                var panel = new Rect(safe.center.x - panelW * 0.5f,
                    Screen.height - safe.center.y - panelH * 0.5f, panelW, panelH);
                DrawPanel(panel);
                float px = panel.x + _u, pw = panel.width - _u * 2f, row = panel.y + _u * 0.5f;
                GUI.Label(new Rect(px, row, pw, _u * 2f), "Cài đặt", _text);
                row += _u * 2.2f;
                GUI.Label(new Rect(px, row, pw, _u * 1.2f), "Nhạc", _small);
                row += _u * 1.2f;
                float musicValue = GUI.HorizontalSlider(new Rect(px, row, pw, 32f), audio.MusicVolume, 0, 1);
                row += 32f + _u * 0.2f;
                GUI.Label(new Rect(px, row, pw, _u * 1.2f), "Hiệu ứng", _small);
                row += _u * 1.2f;
                float sfxValue = GUI.HorizontalSlider(new Rect(px, row, pw, 32f), audio.SfxVolume, 0, 1);
                if (!Mathf.Approximately(musicValue, audio.MusicVolume) || !Mathf.Approximately(sfxValue, audio.SfxVolume))
                    audio.SetVolumes(sfxValue, musicValue);
                row += 32f + _u * 0.4f;
                int currentChoice = Render.QualitySettingsManager.Choice;
                if (GUI.Button(new Rect(px, row, pw, 44f), "Đồ họa: " + Render.QualitySettingsManager.Label(currentChoice), _optionButton))
                    Render.QualitySettingsManager.Choice = currentChoice >= 2 ? -1 : currentChoice + 1;
                row += 44f + _u * 0.4f;
                bool reduced = Render.QualitySettingsManager.ReducedMotion;
                if (GUI.Button(new Rect(px, row, pw, 44f), reduced ? "Chuyển động: giảm" : "Chuyển động: đầy đủ", _optionButton))
                    Render.QualitySettingsManager.ReducedMotion = !reduced;
                return;
            }
            float w = _u * 9, x = Screen.width - w - _u, y = _u * 0.5f;
            GUI.Label(new Rect(x, y, w, _u * 1.2f), "Nhạc", _small);
            float music = GUI.HorizontalSlider(new Rect(x, y + _u * 1.2f, w, _u), audio.MusicVolume, 0, 1);
            GUI.Label(new Rect(x, y + _u * 2.2f, w, _u * 1.2f), "Hiệu ứng", _small);
            float sfx = GUI.HorizontalSlider(new Rect(x, y + _u * 3.4f, w, _u), audio.SfxVolume, 0, 1);
            if (!Mathf.Approximately(music, audio.MusicVolume) || !Mathf.Approximately(sfx, audio.SfxVolume)) audio.SetVolumes(sfx, music);

            int choice = Render.QualitySettingsManager.Choice;
            float optionHeight = Mathf.Max(44f, _u * 1.8f);
            float qualityY = y + _u * 4.8f;
            if (GUI.Button(new Rect(x, qualityY, w, optionHeight), "Đồ họa: " + Render.QualitySettingsManager.Label(choice), _optionButton))
                Render.QualitySettingsManager.Choice = choice >= 2 ? -1 : choice + 1;
            bool reducedMotion = Render.QualitySettingsManager.ReducedMotion;
            if (GUI.Button(new Rect(x, qualityY + optionHeight + _u * 0.3f, w, optionHeight), reducedMotion ? "Chuyển động: giảm" : "Chuyển động: đầy đủ", _optionButton))
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
                float w = _u * 14, h = Mathf.Max(44f, _u * 2f);
                GUI.SetNextControlName("user");
                _username = GUI.TextField(new Rect(Screen.width / 2f - w / 2, y, w, h), _username, 32, _field);
                y += h + _u * 0.4f;
                _password = GUI.PasswordField(new Rect(Screen.width / 2f - w / 2, y, w, h), _password, '•', 128, _field);
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
            Line(ref y, Net.Status);
            y += _u * 0.5f;
            if (Button(ref y, "Ghép trận nhanh")) Net.Session.QuickMatch();
            if (Button(ref y, "Tạo phòng")) Net.Session.CreateRoom();
            float w = _u * 14, h = Mathf.Max(44f, _u * 2.2f);
            _code = GUI.TextField(new Rect(Screen.width / 2f - w / 2, y, w * 0.45f, h), _code.ToUpperInvariant(), 4, _field);
            var old = GUI.enabled;
            GUI.enabled = _code.Length == 4;
            if (GUI.Button(new Rect(Screen.width / 2f - w / 2 + w * 0.5f, y, w * 0.5f, h), "Vào phòng", _button)) Net.Session.JoinRoom(_code);
            GUI.enabled = old;
            y += h + _u * 0.5f;
            if (Button(ref y, "Chơi thử offline")) StartSandbox?.Invoke();
            if (!NetClient.IsWeb && Button(ref y, "Đăng xuất"))
            {
                ChatLogin.Logout();
                Net.Reconnect();
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
                GUI.Label(new Rect(0, y, Screen.width, _u * 3), lobby.Code, _title);
                y += _u * 3;
                string link = ServerUrl.InviteLink(Net.Url, lobby.Code);
                Line(ref y, link, _small, 1.4f);
                if (Button(ref y, "Sao chép link mời"))
                {
                    GUIUtility.systemCopyBuffer = link;
                    _toast = "Đã sao chép, dán vào Chat để mời";
                    _toastUntil = Time.unscaledTime + 3f;
                }
            }

            foreach (var p in lobby.Players)
            {
                string mark = p.UserId == lobby.HostUserId ? "chủ phòng" : p.Ready ? "sẵn sàng" : "chưa sẵn sàng";
                if (!p.Connected) mark = "mất kết nối";
                string color = ColorUtility.ToHtmlStringRGB(TeamColors.Of(p.Team));
                Line(ref y, "<color=#" + color + ">" + p.Name + "</color>  <size=" + Mathf.RoundToInt(_u * 0.8f) + ">" + mark + "</size>", _text, 1.4f);
            }
            y += _u * 0.5f;

            if (!lobby.IsQuick && lobby.State == RoomState.Lobby)
            {
                if (Net.Session.IsHost)
                {
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
