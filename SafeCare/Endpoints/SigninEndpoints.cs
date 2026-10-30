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
    /// response headers are long gone. <c>Login.razor</c> and <c>LoginTwoFactor.razor</c> are
    /// therefore plain <c>&lt;form method="post"&gt;</c>s that post here, and failures come
    /// back as <c>?error=</c> query parameters rather than as rendered state.
    /// </remarks>
    public static class SigninEndpoints
    {
        /// <summary>
        /// Handles <c>POST /signin</c>, the first step of signing in: validates the password
        /// and sends the user on to the code step, or — for an account that has not set up
        /// two-factor authentication yet — to the authenticator setup page.
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

                var safeReturnUrl = ToSafeReturnUrl(returnUrl);

                var result = await signInManager.PasswordSignInAsync(userName, password, isPersistent: rememberMe ?? false, lockoutOnFailure: true);

                if (result.RequiresTwoFactor)
                {
                    // The password was right. Identity has set its short-lived two-factor
                    // cookie; the session itself is issued only once a code is supplied.
                    return Results.Redirect(TwoFactorPageUrl(safeReturnUrl, rememberMe ?? false, error: null));
                }

                if (result.Succeeded)
                {
                    // Only an account without two-factor authentication gets here. Its session
                    // carries no second factor, so the setup page is the one page it can open.
                    Log.Information("User {UserName} logged in without two-factor authentication; sending to setup", userName);
                    return Results.Redirect("/account/2fa-setup");
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
        /// Handles <c>POST /signin-2fa</c>, the second step of signing in: checks a code from
        /// the authenticator app, or a recovery code, for the user whose password
        /// <c>/signin</c> has just accepted, then issues the auth cookie and redirects to
        /// <c>returnUrl</c> when it is safe, otherwise to <c>/dashboard</c>.
        /// </summary>
        /// <remarks>
        /// One field takes both kinds of code: six digits are an authenticator code, anything
        /// else is tried as a recovery code. Wrong codes count towards the same lockout as
        /// wrong passwords.
        /// </remarks>
        public static void MapTwoFactorSignInEndpoint(this IEndpointRouteBuilder app)
        {
            app.MapPost("/signin-2fa", async (
               [FromForm] string? code,
               [FromForm] string? returnUrl,
               [FromForm] bool? rememberMe,
               SignInManager<User> signInManager) =>
            {
                var safeReturnUrl = ToSafeReturnUrl(returnUrl);
                var persistent = rememberMe ?? false;

                // Identity's two-factor cookie lives for five minutes. Without it there is no
                // half-finished sign-in to complete, and the user has to start over.
                var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
                if (user is null)
                {
                    return Results.Redirect("/login?error=TwoFactorExpired");
                }

                var normalizedCode = (code ?? string.Empty)
                    .Replace(" ", string.Empty)
                    .Replace("-", string.Empty)
                    .Trim();

                if (normalizedCode.Length is 0 or > 32)
                {
                    return Results.Redirect(TwoFactorPageUrl(safeReturnUrl, persistent, "InvalidCode"));
                }

                var isAuthenticatorCode = normalizedCode.Length == 6 && normalizedCode.All(char.IsAsciiDigit);

                var result = isAuthenticatorCode
                    ? await signInManager.TwoFactorAuthenticatorSignInAsync(normalizedCode, persistent, rememberClient: false)
                    : await signInManager.TwoFactorRecoveryCodeSignInAsync(ToRecoveryCodeFormat(normalizedCode));

                if (result.Succeeded)
                {
                    if (isAuthenticatorCode)
                    {
                        Log.Information("User {UserName} logged in successfully", user.UserName);
                    }
                    else
                    {
                        Log.Warning("User {UserName} logged in with a recovery code", user.UserName);
                    }

                    return Results.Redirect(safeReturnUrl ?? "/dashboard");
                }

                if (result.IsLockedOut)
                {
                    Log.Warning("User {UserName} account locked out after invalid two-factor codes", user.UserName);
                    return Results.Redirect("/login?error=LockedOut");
                }

                Log.Warning("Invalid two-factor code for user {UserName}", user.UserName);
                return Results.Redirect(TwoFactorPageUrl(safeReturnUrl, persistent, "InvalidCode"));
            });
        }

        /// <summary>
        /// Handles <c>POST /signout</c>: clears the auth cookie and returns to the public
        /// incident form at <c>/</c>, which is where a signed-out visitor belongs — the login
        /// page is reached from there, and landing on it instead invites signing straight
        /// back in.
        /// </summary>
        /// <remarks>
        /// The exception is <c>?relogin=true</c>, which the authenticator setup page sends once
        /// setup is done: the password-only session it ran in is replaced by signing in again,
        /// this time with a code. The flag is a query parameter rather than a form field
        /// because binding a form field would make this endpoint demand an antiforgery token,
        /// which the layout's sign-out form does not carry.
        /// </remarks>
        public static void MapSignOutEndpoint(this IEndpointRouteBuilder app)
        {
            app.MapPost("/signout", async ([FromQuery] bool? relogin, SignInManager<User> signInManager, HttpContext httpContext) =>
            {
                var userName = httpContext.User.Identity?.Name ?? "Unknown";
                await signInManager.SignOutAsync();

                Log.Information("User {UserName} signed out successfully", userName);

                return Results.Redirect(relogin == true ? "/login?info=TwoFactorEnabled" : "/");
            });
        }

        /// <summary>
        /// Returns <paramref name="returnUrl"/> if it is a path on this site, otherwise null.
        /// </summary>
        /// <remarks>
        /// Only paths starting with a single <c>/</c> are accepted, so a crafted
        /// <c>?returnUrl=https://evil</c> cannot turn the login form into an open redirect.
        /// <c>//evil</c> and <c>/\evil</c> are refused as well: browsers read both as a
        /// different host.
        /// </remarks>
        private static string? ToSafeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrEmpty(returnUrl))
            {
                return null;
            }

            var decodedUrl = Uri.UnescapeDataString(returnUrl);
            var isLocalPath = Uri.TryCreate(decodedUrl, UriKind.Relative, out _)
                && decodedUrl.StartsWith('/')
                && !decodedUrl.StartsWith("//")
                && !decodedUrl.StartsWith("/\\");

            return isLocalPath ? decodedUrl : null;
        }

        private static string TwoFactorPageUrl(string? returnUrl, bool rememberMe, string? error)
        {
            var url = $"/login/2fa?rememberMe={(rememberMe ? "true" : "false")}";

            if (returnUrl is not null)
            {
                url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            }

            if (error is not null)
            {
                url += $"&error={error}";
            }

            return url;
        }

        /// <summary>
        /// Identity stores recovery codes as two groups of five characters joined by a hyphen
        /// and compares them exactly; this restores that form from whatever the user typed.
        /// </summary>
        private static string ToRecoveryCodeFormat(string normalizedCode)
        {
            var upper = normalizedCode.ToUpperInvariant();
            return upper.Length == 10 ? $"{upper[..5]}-{upper[5..]}" : upper;
        }
    }
}
