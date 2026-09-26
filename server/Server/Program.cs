using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Worms.Protocol;

namespace Worms.Server
{
    public class Program
    {
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
            if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]) && string.IsNullOrEmpty(builder.Configuration["urls"]))
                builder.WebHost.UseUrls("http://0.0.0.0:" + config.Port);

            var app = builder.Build();
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

            app.MapGet("/healthz", () => Results.Text("ok"));
            app.Map("/ws", (HttpContext ctx) => HandleSocket(ctx, config));

            WebStatic.Use(app);
            return app;
        }

        static async Task HandleSocket(HttpContext ctx, ServerConfig config)
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

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var hello = new HelloMsg { Server = "worms", UserId = 0, DisplayName = string.Empty }.Encode();
            await socket.SendAsync(hello, WebSocketMessageType.Binary, true, ctx.RequestAborted);

            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(buffer, ctx.RequestAborted);
                }
                catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
                {
                    return;
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    return;
                }
            }
        }
    }

    public sealed class ServerConfig
    {
        public int Port { get; init; } = 8080;
        public string[] AllowedOrigins { get; init; } = Array.Empty<string>();

        public bool IsOriginAllowed(string origin)
        {
            return AllowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));
        }

        public static ServerConfig FromEnvironment(Microsoft.Extensions.Configuration.IConfiguration c)
        {
            return new ServerConfig
            {
                Port = int.TryParse(c["PORT"], out var p) ? p : 8080,
                AllowedOrigins = (c["ALLOWED_ORIGINS"] ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            };
        }
    }
}
