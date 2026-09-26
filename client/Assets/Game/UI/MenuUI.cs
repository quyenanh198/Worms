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
        string _toast;
        float _toastUntil;
        GUIStyle _title, _text, _small, _button, _smallButton, _field, _track, _thumb;
        float _sliderH;
        float _u;

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
            _text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(u), wordWrap = true, richText = true };
            _text.normal.textColor = Color.white;
            _small = new GUIStyle(_text) { fontSize = Mathf.RoundToInt(u * 0.8f) };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u) };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(u), alignment = TextAnchor.MiddleCenter };
            _smallButton = new GUIStyle(_button) { fontSize = Mathf.RoundToInt(u * 0.8f) };
            UiSkin.Button(_button);
            UiSkin.Button(_smallButton);
            UiSkin.Field(_field);
            (_track, _thumb, _sliderH) = UiSkin.Slider(u);
            UiFont.Apply(_title, _text, _small, _button, _smallButton, _field);
        }

        bool Button(ref float y, string label, bool enabled = true)
        {
            float w = _u * 14, h = _u * 2.2f;
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
            GUI.Label(new Rect(_u, y, Screen.width - _u * 2, _u * lines), text, style);
            y += _u * lines;
        }

        void OnGUI()
        {
            UiFont.UseForSkin();
            if (Hidden) return;
            Styles();
            float y = Screen.height * 0.08f;
            GUI.Label(new Rect(0, y, Screen.width, _u * 4), "WORMS", _title);
            y += _u * 4.5f;

            if (Net.Outdated) Outdated(ref y);
            else if (Net.Unauthorized) Login(ref y);
            else if (!Net.Connected || !Net.Session.HasHello) Offline(ref y);
            else if (Net.Session.InRoom) Room(ref y);
            else Main(ref y);

            AudioSettings();

            if (_toast != null && Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(0, Screen.height - _u * 3, Screen.width, _u * 2), "<color=#ff8866>" + _toast + "</color>", _text);
        }

        void AudioSettings()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            float w = _u * 9, x = Screen.width - w - _u, y = _u * 0.5f;
            GUI.Label(new Rect(x, y, w, _u * 1.2f), "Nhạc", _small);
            float music = GUI.HorizontalSlider(new Rect(x, y + _u * 1.2f, w, _sliderH), audio.MusicVolume, 0, 1, _track, _thumb);
            GUI.Label(new Rect(x, y + _u * 2.4f, w, _u * 1.2f), "Hiệu ứng", _small);
            float sfx = GUI.HorizontalSlider(new Rect(x, y + _u * 3.6f, w, _sliderH), audio.SfxVolume, 0, 1, _track, _thumb);
            if (!Mathf.Approximately(music, audio.MusicVolume) || !Mathf.Approximately(sfx, audio.SfxVolume)) audio.SetVolumes(sfx, music);

            int choice = Render.QualitySettingsManager.Choice;
            if (GUI.Button(new Rect(x, y + _u * 5.1f, w, _u * 1.7f), "Đồ họa: " + Render.QualitySettingsManager.Label(choice), _smallButton))
                Render.QualitySettingsManager.Choice = choice >= 2 ? -1 : choice + 1;
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
                float w = _u * 14, h = _u * 2;
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
            if (Button(ref y, "Chơi với máy"))
            {
                _wantBot = true;
                Net.Session.CreateRoom();
            }
            if (Button(ref y, "Tạo phòng")) Net.Session.CreateRoom();
            float w = _u * 14, h = _u * 2.2f;
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
                    if (GUI.Button(new Rect(Screen.width / 2f + _u * 7f - bw, rowY + (_u * 1.4f - bh) / 2f, bw, bh), "Bỏ", _small))
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
