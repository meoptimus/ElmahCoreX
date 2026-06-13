using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ElmahCore.Mvc.Handlers;

internal static class ErrorResourceHandler
{
    private static readonly string[] ResourceNames =
        typeof(ErrorLogMiddleware).GetTypeInfo().Assembly.GetManifestResourceNames();

    public static async Task ProcessRequest(HttpContext context, string path, string elmahRoot)
    {
        var assembly = typeof(ErrorResourceHandler).GetTypeInfo().Assembly;

        var resName = $"{assembly.GetName().Name}.wwwroot.{path.Replace('/', '.').Replace('\\', '.')}";
        var actualResName = ResourceNames.FirstOrDefault(r => string.Equals(r, resName, StringComparison.OrdinalIgnoreCase));

        if (!path.Contains('.'))
        {
            var indexResName = $"{assembly.GetName().Name}.wwwroot.index.html";
            await using var stream2 = assembly.GetManifestResourceStream(indexResName);
            using var reader = new StreamReader(stream2 ?? throw new InvalidOperationException());
            var html = await reader.ReadToEndAsync();
            html = html.Replace("/ELMAH_ROOT/", elmahRoot + "/").Replace("ELMAH_ROOT", elmahRoot);
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);
            return;
        }

        if (actualResName == null)
        {
            context.Response.StatusCode = 404;
            return;
        }

        var ext = Path.GetExtension(path).ToLower();
        context.Response.ContentType = ext switch
        {
            ".svg" => "image/svg+xml",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".ico" => "image/x-icon",
            ".png" => "image/png",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".html" => "text/html",
            _ => context.Response.ContentType
        };

        await using var resource = assembly.GetManifestResourceStream(actualResName);
        if (resource != null) await resource.CopyToAsync(context.Response.Body);
    }
}