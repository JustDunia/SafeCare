namespace SafeCare.Middlewares
{
    /// <summary>
    /// Adds hardening response headers to every response. Registered first in the pipeline so
    /// that the headers are present on error pages and static assets too.
    /// </summary>
    /// <remarks>
    /// There is deliberately no Content-Security-Policy here: Blazor Server and MudBlazor need
    /// inline styles and a WebSocket connection, so a CSP would have to be written and tested
    /// against those requirements rather than added blind.
    /// </remarks>
    public class SecurityHeadersMiddleware(RequestDelegate next)
    {
        public Task Invoke(HttpContext context)
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

            return next(context);
        }
    }
}
