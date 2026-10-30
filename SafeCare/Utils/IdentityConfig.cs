using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
        /// role should land somewhere useful rather than on a dead end. A user who has not
        /// passed a second factor lands on the authenticator setup page instead.
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

                    // Page authorization already runs at the HTTP level, before Blazor renders.
                    // A session without a second factor is refused by /dashboard too, so
                    // sending it there would redirect forever; send it to set two-factor
                    // authentication up instead. Identity has already populated Events, so
                    // this adds a handler rather than replacing them.
                    c.Events.OnRedirectToAccessDenied = context =>
                    {
                        var hasSecondFactor = context.HttpContext.User.HasClaim(
                            AuthPolicies.AuthenticationMethodClaim, AuthPolicies.MultiFactorAuthenticationMethod);

                        context.Response.Redirect(hasSecondFactor ? context.RedirectUri : "/account/2fa-setup");
                        return Task.CompletedTask;
                    };
                });
            });

            services.AddIdentityCore<User>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddSignInManager()
                .AddTokenProvider<AuthenticatorTokenProvider<User>>(TokenOptions.DefaultAuthenticatorProvider);

            // Re-check the security stamp every 5 minutes instead of the 30-minute default.
            // This is what signs out a user whose account an administrator has just deleted
            // or whose role has changed, so a shorter interval keeps stale sessions brief.
            services.Configure<SecurityStampValidatorOptions>(options =>
            {
                options.ValidationInterval = TimeSpan.FromMinutes(5);

                // The refreshed principal is rebuilt from the database and would lose the
                // "amr" claim that records the second factor — and with it, five minutes
                // after signing in, access to every page. Carry it over from the old one.
                options.OnRefreshingPrincipal = context =>
                {
                    var amrClaims = context.CurrentPrincipal?.FindAll(AuthPolicies.AuthenticationMethodClaim).ToList() ?? [];
                    if (amrClaims.Count > 0 && context.NewPrincipal?.Identity is ClaimsIdentity identity)
                    {
                        identity.AddClaims(amrClaims.Select(c => new Claim(c.Type, c.Value)));
                    }

                    return Task.CompletedTask;
                };
            });

            // Two-factor authentication is mandatory: a bare [Authorize] requires a session
            // signed in with a second factor. A password-only session can reach nothing but
            // the authenticator setup page — see AuthPolicies.
            var twoFactorPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(AuthPolicies.AuthenticationMethodClaim, AuthPolicies.MultiFactorAuthenticationMethod)
                .Build();

            services.AddAuthorizationBuilder()
                .SetDefaultPolicy(twoFactorPolicy)
                .AddPolicy(AuthPolicies.Admin, policy => policy
                    .Combine(twoFactorPolicy)
                    .RequireRole(AppRoles.Admin))
                .AddPolicy(AuthPolicies.TwoFactorEnrollment, policy => policy
                    .RequireAuthenticatedUser());
            services.AddCascadingAuthenticationState();

            return services;
        }
    }
}
