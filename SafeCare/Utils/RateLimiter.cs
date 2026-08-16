using SafeCare.Services;
using Serilog;
using System.Threading.RateLimiting;

namespace SafeCare.Utils
{
    /// <summary>
    /// Registers the two independent rate-limiting layers this application uses.
    /// </summary>
    /// <remarks>
    /// The ASP.NET Core middleware limiter configured here counts HTTP requests per IP, which
    /// under Blazor Server means little more than the initial page load plus <c>/signin</c> and
    /// <c>/signout</c> — every later interaction travels over the SignalR circuit and never
    /// reaches it. Throttling form submissions is therefore the job of the separately
    /// registered <see cref="IRateLimitService"/>, which counts per circuit.
    /// </remarks>
    public static class RateLimiter
    {
        /// <summary>
        /// Adds the global per-IP limiter (100 requests/minute, fixed window) and the
        /// per-circuit <see cref="IRateLimitService"/>.
        /// </summary>
        public static IServiceCollection AddCustomRateLimiter(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Global rate limiter: 100 requests per minute per IP
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.ContentType = "text/plain";
                    await context.HttpContext.Response.WriteAsync(
                        "Too many requests. Please try again later.", cancellationToken);

                    Log.Warning("Rate limit exceeded for IP: {IpAddress}",
                        context.HttpContext.Connection.RemoteIpAddress);
                };
            });

            services.AddSingleton<IRateLimitService, RateLimitService>();

            return services;
        }
    }
}
