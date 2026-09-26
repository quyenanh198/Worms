using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;

namespace Worms.Server
{
    /// <summary>
    /// Serves the Unity Web build from wwwroot. The build uses Brotli with
    /// Unity's decompression fallback, so its files end in .unityweb and are
    /// served as opaque bytes (no Content-Encoding): the loader decompresses
    /// them itself, which works the same behind any proxy or CDN.
    /// </summary>
    static class WebStatic
    {
        public static void Use(WebApplication app)
        {
            var types = new FileExtensionContentTypeProvider();
            types.Mappings[".unityweb"] = "application/octet-stream";
            types.Mappings[".wasm"] = "application/wasm";
            types.Mappings[".data"] = "application/octet-stream";

            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = types,
                OnPrepareResponse = ctx =>
                {
                    // index.html must revalidate so a deploy is picked up; build files are content-hashed.
                    bool isIndex = ctx.File.Name == "index.html";
                    ctx.Context.Response.Headers.CacheControl = isIndex ? "no-cache" : "public, max-age=31536000, immutable";
                },
            });
        }
    }
}
