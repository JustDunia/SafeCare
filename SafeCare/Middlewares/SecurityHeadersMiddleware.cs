namespace SafeCare.Middlewares
{
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
