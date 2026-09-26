using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Worms.Server
{
    public sealed class Identity
    {
        public int UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
    }

    public interface IIdentityProvider
    {
        /// <summary>Who is behind this request, or null if unknown.</summary>
        Task<Identity> ResolveAsync(HttpContext ctx);
    }

    /// <summary>
    /// Accounts live in the Chat app (docs/PLAN.md §3.15): forward the Chat
    /// session cookie to its /api/me and trust the answer. Answers are cached
    /// for a few seconds so reconnect storms do not hammer Chat.
    /// </summary>
    public sealed class ChatIdentityProvider : IIdentityProvider
    {
        const string CookieName = "lb_session";
        static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

        readonly HttpClient _http;
        readonly string _root;
        readonly ConcurrentDictionary<string, (Identity who, DateTime until)> _cache = new ConcurrentDictionary<string, (Identity, DateTime)>();

        public ChatIdentityProvider(HttpClient http, string chatApiUrl)
        {
            _http = http;
            _root = chatApiUrl.TrimEnd('/');
        }

        /// <summary>Only the Chat session cookie is forwarded, never the others.</summary>
        public static string SessionCookie(string cookieHeader)
        {
            if (string.IsNullOrEmpty(cookieHeader)) return null;
            foreach (var part in cookieHeader.Split(';'))
            {
                var item = part.Trim();
                if (item.StartsWith(CookieName + "=", StringComparison.Ordinal) && item.Length > CookieName.Length + 1) return item;
            }
            return null;
        }

        public async Task<Identity> ResolveAsync(HttpContext ctx)
        {
            var cookie = SessionCookie(ctx.Request.Headers.Cookie);
            if (cookie == null) return null;
            if (_cache.TryGetValue(cookie, out var hit) && hit.until > DateTime.UtcNow) return hit.who;

            Identity who = null;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, _root + "/api/me");
                req.Headers.Add("Cookie", cookie);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                using var res = await _http.SendAsync(req, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cts.Token));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var id) && id.TryGetInt32(out int userId))
                    {
                        string name = root.TryGetProperty("display_name", out var dn) && dn.ValueKind == JsonValueKind.String ? dn.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name) && root.TryGetProperty("username", out var un)) name = un.GetString();
                        who = new Identity { UserId = userId, DisplayName = Trim(name) };
                    }
                }
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is JsonException)
            {
                return null; // Chat unreachable: treat as not logged in
            }
            if (_cache.Count > 1000) _cache.Clear();
            _cache[cookie] = (who, DateTime.UtcNow + CacheTtl);
            return who;
        }

        static string Trim(string name)
        {
            name = string.IsNullOrWhiteSpace(name) ? "Người chơi" : name.Trim();
            return name.Length > 24 ? name.Substring(0, 24) : name;
        }
    }

    /// <summary>
    /// Local development without Chat (CHAT_API_URL unset): anyone may join
    /// with ?name=... . Ids are negative so they never collide with Chat ids.
    /// </summary>
    public sealed class GuestIdentityProvider : IIdentityProvider
    {
        int _next;

        public Task<Identity> ResolveAsync(HttpContext ctx)
        {
            string name = ctx.Request.Query["name"];
            int id = -Interlocked.Increment(ref _next);
            if (int.TryParse(ctx.Request.Query["guest"], out int fixedId) && fixedId > 0) id = -fixedId;
            name = string.IsNullOrWhiteSpace(name) ? "Khách " + (-id) : name.Trim();
            if (name.Length > 24) name = name.Substring(0, 24);
            return Task.FromResult(new Identity { UserId = id, DisplayName = name });
        }
    }
}
