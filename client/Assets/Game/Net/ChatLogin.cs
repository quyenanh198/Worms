using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Worms.Game.Core;

namespace Worms.Game.Net
{
    /// <summary>
    /// Native builds log in to the Chat app directly (docs/PLAN.md §3.15) and
    /// keep its session cookie; the Web build is already logged in via the browser.
    /// </summary>
    public static class ChatLogin
    {
        public static IEnumerator Login(string chatBase, string username, string password, Action<string> onError, Action onSuccess)
        {
            string body = "{\"username\":" + Json(username) + ",\"password\":" + Json(password) + "}";
            using (var req = new UnityWebRequest(chatBase + "/api/auth/login", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;
                yield return req.SendWebRequest();

                if (req.responseCode == 200)
                {
                    string cookie = ServerUrl.SessionCookieFromSetCookie(req.GetResponseHeader("Set-Cookie"));
                    if (cookie == null)
                    {
                        onError("Chat không trả về phiên đăng nhập");
                        yield break;
                    }
                    NetClient.StoredCookie = cookie;
                    onSuccess();
                }
                else if (req.responseCode == 401)
                {
                    onError("Sai tên đăng nhập hoặc mật khẩu");
                }
                else if (req.responseCode == 429)
                {
                    onError("Thử quá nhiều lần, đợi một phút rồi thử lại");
                }
                else
                {
                    onError("Không kết nối được Chat (" + (req.responseCode > 0 ? req.responseCode.ToString() : req.error) + ")");
                }
            }
        }

        public static void Logout()
        {
            NetClient.StoredCookie = null;
        }

        static string Json(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
