namespace AutoCheck.Api.Infrastructure;

public sealed class ApiResponseHeaders(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var http = (HttpContext)state;
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers["Referrer-Policy"] = "no-referrer";
            http.Response.Headers.XFrameOptions = "DENY";
            if (http.Request.Path.StartsWithSegments("/api"))
            {
                // Includes login, refresh and error responses. Tokens and personal
                // data must not enter browser/shared caches.
                http.Response.Headers.CacheControl = "no-store";
                http.Response.Headers.Pragma = "no-cache";
            }

            return Task.CompletedTask;
        }, context);
        return next(context);
    }
}
