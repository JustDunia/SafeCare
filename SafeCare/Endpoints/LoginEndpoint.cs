using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SafeCare.Data.Entities;
using Serilog;

namespace SafeCare.Endpoints
{
    public static class LoginEndpoint
    {
        public static void MapLoginEndpoint(this IEndpointRouteBuilder app)
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

                // Validate returnUrl to prevent open redirect attacks
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
    }
}
