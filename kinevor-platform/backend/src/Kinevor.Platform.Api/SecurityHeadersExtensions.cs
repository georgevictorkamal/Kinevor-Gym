using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Kinevor.Platform.Api;

/// <summary>Security headers applied by every Kinevor API host.</summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds conservative browser security headers. Hosting platforms should
    /// still terminate TLS and add HSTS at the edge.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder for chaining.</returns>
    public static IApplicationBuilder UseKinevorSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Content-Security-Policy"] =
                "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; "
                + "script-src 'self' 'unsafe-inline'; connect-src 'self'; font-src 'self' data:; "
                + "object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

            await next().ConfigureAwait(false);
        });
    }
}
