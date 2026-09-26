using System;

namespace Worms.Game.Net
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
    }
}
