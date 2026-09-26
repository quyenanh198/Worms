using System;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Worms.Protocol;

namespace Worms.Server
{
    public class Program
    {
        const int MaxMessageBytes = 8 * 1024;

        public static void Main(string[] args)
        {
            var app = Build(args);
            app.Run();
        }

        public static WebApplication Build(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var config = ServerConfig.FromEnvironment(builder.Configuration);
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(new MatchOptions
            {
                Speed = double.TryParse(builder.Configuration["SIM_SPEED"], out var speed) && speed > 0 ? speed : 1,
                MaxRooms = int.TryParse(builder.Configuration["MAX_ROOMS"], out var maxRooms) && maxRooms > 0 ? maxRooms : 200,
            });
            builder.Services.AddSingleton<ServerStats>();
            builder.Services.AddSingleton<RoomManager>();
            if (string.IsNullOrEmpty(config.ChatApiUrl))
                builder.Services.AddSingleton<IIdentityProvider, GuestIdentityProvider>();
            else
                builder.Services.AddSingleton<IIdentityProvider>(new ChatIdentityProvider(new HttpClient(), config.ChatApiUrl));
            if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]) && string.IsNullOrEmpty(builder.Configuration["urls"]))
                builder.WebHost.UseUrls("http://0.0.0.0:" + config.Port);

            var app = builder.Build();
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

            app.MapGet("/healthz", () => Results.Text("ok"));
            // Counters for the operator; disabled unless STATUS_TOKEN is set.
            app.MapGet("/status", (HttpContext ctx, RoomManager rooms, ServerStats stats) =>
            {
                if (string.IsNullOrEmpty(config.StatusToken) || ctx.Request.Query["token"] != config.StatusToken) return Results.NotFound();
                return Results.Json(new
                {
                    connections = stats.Connections,
                    rooms = rooms.RoomCount,
                    playing = rooms.PlayingCount,
                    uptimeSeconds = (long)stats.Uptime.TotalSeconds,
                    protocol = ProtocolInfo.Version,
                });
            });
            app.MapGet("/download", (HttpContext ctx) =>
            {
                var file = System.IO.Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "download.html");
                return System.IO.File.Exists(file) ? Results.File(file, "text/html; charset=utf-8") : Results.NotFound();
            });
            app.Map("/ws", (HttpContext ctx, IIdentityProvider ids, RoomManager rooms, ServerStats stats) => HandleSocket(ctx, config, ids, rooms, stats));

            WebStatic.Use(app);
            return app;
        }

        static async Task HandleSocket(HttpContext ctx, ServerConfig config, IIdentityProvider ids, RoomManager rooms, ServerStats stats)
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            // Browsers always send Origin and would attach the Chat cookie to a
            // cross-site socket; native clients send none.
            string origin = ctx.Request.Headers.Origin;
            if (!string.IsNullOrEmpty(origin) && !config.IsOriginAllowed(origin))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var who = await ids.ResolveAsync(ctx);
            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            if (who == null)
            {
                // Refused after the upgrade so the browser client can read why.
                await Refuse(socket, ErrorCodes.Unauthorized, WebSocketCloseStatus.PolicyViolation);
                return;
            }

            if (stats.Connections >= config.MaxConnections)
            {
                await Refuse(socket, ErrorCodes.ServerFull, WebSocketCloseStatus.EndpointUnavailable);
                return;
            }

            var conn = new Connection(socket, who);
            stats.Opened();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
            var writer = conn.RunWriterAsync(cts.Token);
            conn.Send(new HelloMsg { Server = "worms", UserId = who.UserId, DisplayName = who.DisplayName }.Encode());
            rooms.Connected(conn);
            try
            {
                await ReadLoop(socket, conn, rooms, cts.Token);
            }
            finally
            {
                stats.Closed();
                rooms.Disconnected(conn);
                conn.Close();
                await Task.WhenAny(writer, Task.Delay(2000));
                cts.Cancel();
            }
        }

        static async Task Refuse(WebSocket socket, string code, WebSocketCloseStatus status)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await socket.SendAsync(new ErrorMsg { Code = code }.Encode(), WebSocketMessageType.Binary, true, cts.Token);
                await socket.CloseAsync(status, code, cts.Token);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is System.IO.IOException || e is ObjectDisposedException)
            {
            }
        }

        static async Task ReadLoop(WebSocket socket, Connection conn, RoomManager rooms, CancellationToken ct)
        {
            var buffer = new byte[MaxMessageBytes];
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                int count = 0;
                WebSocketReceiveResult result;
                try
                {
                    do
                    {
                        if (count >= buffer.Length) return; // too large: drop the connection
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), ct);
                        count += result.Count;
                    } while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);
                }
                catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is System.IO.IOException || e is ObjectDisposedException)
                {
                    return; // client went away
                }
                if (result.MessageType == WebSocketMessageType.Close) return;
                if (!Dispatch(buffer, count, conn, rooms)) return;
            }
        }

        static bool Dispatch(byte[] buffer, int count, Connection conn, RoomManager rooms)
        {
            try
            {
                var r = new MsgReader(buffer, count);
                if (r.Version != ProtocolInfo.Version)
                {
                    conn.Send(new ErrorMsg { Code = ErrorCodes.BadVersion }.Encode());
                    conn.Close();
                    return false;
                }
                if (!conn.TakeToken())
                {
                    conn.Send(new ErrorMsg { Code = ErrorCodes.RateLimited }.Encode());
                    conn.Close();
                    return false;
                }
                switch (r.Type)
                {
                    case MsgType.CreateRoom: rooms.CreateRoom(conn); break;
                    case MsgType.JoinRoom: rooms.JoinRoom(conn, r.Str()); break;
                    case MsgType.QuickMatch: rooms.QuickMatch(conn); break;
                    case MsgType.SetReady: rooms.SetReady(conn, r.Bool()); break;
                    case MsgType.StartMatch: rooms.Start(conn); break;
                    case MsgType.LeaveRoom: rooms.Leave(conn); break;
                    case MsgType.Rematch: rooms.Rematch(conn); break;
                    case MsgType.AddBot: rooms.AddBot(conn); break;
                    case MsgType.RemoveBot: rooms.RemoveBot(conn, r.I32()); break;
                    case MsgType.Input: rooms.Input(conn, ClientMsg.ReadInput(r)); break;
                }
            }
            catch (ProtocolException)
            {
                conn.Send(new ErrorMsg { Code = ErrorCodes.BadMessage }.Encode());
            }
            return true;
        }
    }

    public sealed class ServerConfig
    {
        public int Port { get; init; } = 8080;
        public string[] AllowedOrigins { get; init; } = Array.Empty<string>();
        /// <summary>Chat's internal URL (http://chat:8082 on the hub). Empty = guest mode for local development.</summary>
        public string ChatApiUrl { get; init; } = string.Empty;
        public int MaxConnections { get; init; } = 1000;
        /// <summary>Secret for GET /status?token=...; empty disables the endpoint.</summary>
        public string StatusToken { get; init; } = string.Empty;

        public bool IsOriginAllowed(string origin)
        {
            return AllowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));
        }

        public static ServerConfig FromEnvironment(IConfiguration c)
        {
            return new ServerConfig
            {
                Port = int.TryParse(c["PORT"], out var p) ? p : 8080,
                AllowedOrigins = (c["ALLOWED_ORIGINS"] ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                ChatApiUrl = c["CHAT_API_URL"] ?? string.Empty,
                MaxConnections = int.TryParse(c["MAX_CONNECTIONS"], out var mc) && mc > 0 ? mc : 1000,
                StatusToken = c["STATUS_TOKEN"] ?? string.Empty,
            };
        }
    }
}
