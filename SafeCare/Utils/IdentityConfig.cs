using Microsoft.AspNetCore.Identity;
using SafeCare.Data;
using SafeCare.Data.Entities;

namespace SafeCare.Utils
{
    /// <summary>
    /// Registers cookie-based ASP.NET Core Identity with <see cref="Guid"/> keys.
    /// </summary>
    public static class IdentityConfig
    {
        /// <summary>
        /// Wires up authentication cookies, the Identity core services, roles and the
        /// cascading authentication state that <c>AuthorizeView</c> depends on.
        /// </summary>
        /// <remarks>
        /// <c>AccessDeniedPath</c> points at <c>/dashboard</c>: a signed-in user who lacks a
        /// role should land somewhere useful rather than on a dead end.
        /// </remarks>
        public static IServiceCollection AddIdentity(this IServiceCollection services)
        {
            services.AddAuthentication(x =>
            {
                x.DefaultScheme = IdentityConstants.ApplicationScheme;
                x.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies(options =>
            {
                options.ApplicationCookie!.Configure(c =>
                {
                    c.LoginPath = "/login";
                    c.AccessDeniedPath = "/dashboard";
                });
            });

            services.AddIdentityCore<User>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddSignInManager();

            // Re-check the security stamp every 5 minutes instead of the 30-minute default.
            // This is what signs out a user whose account an administrator has just deleted
            // or whose role has changed, so a shorter interval keeps stale sessions brief.
            services.Configure<SecurityStampValidatorOptions>(options =>
            {
                options.ValidationInterval = TimeSpan.FromMinutes(5);
            });

            services.AddAuthorization();
            services.AddCascadingAuthenticationState();

            return services;
        }
    }
}
