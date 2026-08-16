using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SafeCare.Data.Entities;
using Serilog;

namespace SafeCare.Endpoints
{
    /// <summary>
    /// Minimal API form-post endpoints for signing in and out.
    /// </summary>
    /// <remarks>
    /// Authentication cannot happen inside a Blazor circuit: setting or clearing the auth
    /// cookie requires a real HTTP response, and by the time a SignalR message arrives the
    /// response headers are long gone. <c>Login.razor</c> is therefore a plain
    /// <c>&lt;form method="post"&gt;</c> that posts here, and failures come back as
    /// <c>?error=</c> query parameters rather than as rendered state.
    /// </remarks>
    public static class SigninEndpoints
    {
        /// <summary>
        /// Handles <c>POST /signin</c>: validates the credentials, issues the auth cookie and
        /// redirects to <c>returnUrl</c> when it is safe, otherwise to <c>/dashboard</c>.
        /// </summary>
        public static void MapSignInEndpoint(this IEndpointRouteBuilder app)
        {
            app.MapPost("/signin", async (
               [FromForm] string userName,
               [FromForm] string password,
               [FromForm] string? returnUrl,
               [FromForm] bool? rememberMe,
               SignInManager<User> signInManager) =>
            {
                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
                {
                    return Results.Redirect("/login?error=InvalidCredentials");
                }

                userName = userName.Trim();
                if (userName.Length > 256 || password.Length > 256)
                {
                    return Results.Redirect("/login?error=InvalidCredentials");
                }

                // Only site-relative paths are accepted, so a crafted "?returnUrl=https://evil"
                // cannot turn the login form into an open redirect. Anything else is dropped
                // and the user lands on the dashboard.
                string? safeReturnUrl = null;
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    var decodedUrl = Uri.UnescapeDataString(returnUrl);
                    // Only allow relative URLs (paths starting with /)
                    if (Uri.TryCreate(decodedUrl, UriKind.Relative, out _) && decodedUrl.StartsWith('/'))
                    {
                        safeReturnUrl = decodedUrl;
                    }
                }

                var result = await signInManager.PasswordSignInAsync(userName, password, isPersistent: rememberMe ?? false, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    Log.Information("User {UserName} logged in successfully", userName);
                    return Results.Redirect(safeReturnUrl ?? "/dashboard");
                }

                if (result.IsLockedOut)
                {
                    Log.Warning("User {UserName} account locked out", userName);
                    return Results.Redirect("/login?error=LockedOut");
                }

                Log.Warning("Failed login attempt for user {UserName}", userName);
                return Results.Redirect("/login?error=InvalidCredentials");
            });
        }

        /// <summary>
        /// Handles <c>POST /signout</c>: clears the auth cookie and returns to the login page.
        /// </summary>
        public static void MapSignOutEndpoint(this IEndpointRouteBuilder app)
        {
            app.MapPost("/signout", async (SignInManager<User> signInManager, HttpContext httpContext) =>
            {
                var userName = httpContext.User.Identity?.Name ?? "Unknown";
                await signInManager.SignOutAsync();

                Log.Information("User {UserName} signed out successfully", userName);

                return Results.Redirect("/login");
            });
        }
    }
}
