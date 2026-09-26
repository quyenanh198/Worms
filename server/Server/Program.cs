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
            });
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
            app.Map("/ws", (HttpContext ctx, IIdentityProvider ids, RoomManager rooms) => HandleSocket(ctx, config, ids, rooms));

            WebStatic.Use(app);
            return app;
        }

        static async Task HandleSocket(HttpContext ctx, ServerConfig config, IIdentityProvider ids, RoomManager rooms)
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
                await socket.SendAsync(new ErrorMsg { Code = ErrorCodes.Unauthorized }.Encode(), WebSocketMessageType.Binary, true, ctx.RequestAborted);
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, ErrorCodes.Unauthorized, CancellationToken.None);
                return;
            }

            var conn = new Connection(socket, who);
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
                rooms.Disconnected(conn);
                conn.Close();
                await Task.WhenAny(writer, Task.Delay(2000));
                cts.Cancel();
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
                catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
                {
                    return;
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
            };
        }
    }
}
