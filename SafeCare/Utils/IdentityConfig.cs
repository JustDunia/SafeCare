using Microsoft.AspNetCore.Identity;
using SafeCare.Data;
using SafeCare.Data.Entities;

namespace SafeCare.Utils
{
    public static class IdentityConfig
    {
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
                });
            });

            services.AddIdentityCore<User>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddSignInManager();

            services.AddCascadingAuthenticationState();

            return services;
        }
    }
}
