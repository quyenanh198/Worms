using System;

namespace Worms.Game.Core
{
    public static class ServerUrl
    {
        /// <summary>Production endpoint used by native builds.</summary>
        public const string Default = "wss://chat.lazybutts.com/worms/ws";

        /// <summary>
        /// On Web the socket lives next to the page that loaded the game, e.g.
        /// https://chat.lazybutts.com/worms/?room=AB  ->  wss://chat.lazybutts.com/worms/ws.
        /// Native builds use <paramref name="overrideUrl"/> when given, else <see cref="Default"/>.
        /// </summary>
        public static string Resolve(bool isWeb, string pageUrl, string overrideUrl)
        {
            if (!string.IsNullOrEmpty(overrideUrl)) return overrideUrl;
            if (!isWeb || string.IsNullOrEmpty(pageUrl)) return Default;

            var page = new Uri(pageUrl);
            string scheme = page.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            string path = page.AbsolutePath;
            path = path.Substring(0, path.LastIndexOf('/') + 1);
            return scheme + "://" + page.Authority + path + "ws";
        }

        /// <summary>Base URL of the Chat app that owns the accounts: same host as the game socket.</summary>
        public static string ChatBase(string socketUrl)
        {
            var u = new Uri(socketUrl);
            string scheme = u.Scheme == "wss" ? "https" : "http";
            return scheme + "://" + u.Authority;
        }

        /// <summary>Invite link for a room code (web page next to the socket).</summary>
        public static string InviteLink(string socketUrl, string roomCode)
        {
            var u = new Uri(socketUrl);
            string scheme = u.Scheme == "wss" ? "https" : "http";
            string path = u.AbsolutePath;
            path = path.Substring(0, path.LastIndexOf('/') + 1);
            return scheme + "://" + u.Authority + path + "?room=" + Uri.EscapeDataString(roomCode);
        }

        /// <summary>Value of a query parameter in a page URL, or null.</summary>
        public static string QueryParam(string pageUrl, string name)
        {
            if (string.IsNullOrEmpty(pageUrl)) return null;
            int q = pageUrl.IndexOf('?');
            if (q < 0) return null;
            string query = pageUrl.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (var part in query.Split('&'))
            {
                int eq = part.IndexOf('=');
                string key = eq < 0 ? part : part.Substring(0, eq);
                if (key == name) return eq < 0 ? string.Empty : Uri.UnescapeDataString(part.Substring(eq + 1));
            }
            return null;
        }

        /// <summary>
        /// Picks the Chat session cookie out of a Set-Cookie header (possibly
        /// several cookies merged) and returns it as "lb_session=value", or null.
        /// </summary>
        public static string SessionCookieFromSetCookie(string setCookie)
        {
            const string name = "lb_session=";
            if (string.IsNullOrEmpty(setCookie)) return null;
            int i = setCookie.IndexOf(name, StringComparison.Ordinal);
            if (i < 0) return null;
            int end = setCookie.IndexOf(';', i);
            string item = end < 0 ? setCookie.Substring(i) : setCookie.Substring(i, end - i);
            int comma = item.IndexOf(',');
            if (comma >= 0) item = item.Substring(0, comma);
            item = item.Trim();
            return item.Length > name.Length ? item : null;
        }
    }
}
