using System.Text;
using Microsoft.AspNetCore.Identity;
using QRCoder;
using SafeCare.Data.Entities;
using SafeCare.Dtos;

namespace SafeCare.Services;

/// <summary>
/// A user's own two-factor authentication: setting up the authenticator app, recovery codes,
/// and moving to a new phone. Resetting another user's is in <see cref="IUserManagementService"/>.
/// </summary>
/// <remarks>
/// Two-factor authentication is mandatory. An account without it can sign in with its
/// password, but that session reaches nothing except the setup page — see
/// <c>AuthPolicies</c>.
/// </remarks>
public interface ITwoFactorService
{
    /// <summary>
    /// Whether the account has two-factor authentication on; null if the account is gone.
    /// </summary>
    Task<bool?> IsEnabledAsync(Guid userId);

    /// <summary>
    /// Returns the key and QR code to add the account to an authenticator app, creating the
    /// key on first use. Calling it again returns the same key until setup is restarted.
    /// </summary>
    Task<AuthenticatorSetupDto> GetAuthenticatorSetupAsync(Guid userId);

    /// <summary>
    /// Turns two-factor authentication on, once the user proves the app works by entering
    /// a code from it.
    /// </summary>
    /// <returns>The new recovery codes, to be shown once; null if the code was wrong.</returns>
    Task<IReadOnlyList<string>?> EnableAsync(Guid userId, string code);

    /// <summary>
    /// Replaces all recovery codes with new ones. The old ones stop working.
    /// </summary>
    Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(Guid userId);

    /// <summary>
    /// How many recovery codes are still unused.
    /// </summary>
    Task<int> CountRecoveryCodesAsync(Guid userId);

    /// <summary>
    /// Turns two-factor authentication off and discards the authenticator key, so the user
    /// can add the account to a new phone. The old phone's codes stop working at once.
    /// </summary>
    Task RestartSetupAsync(Guid userId);
}

/// <summary>
/// ASP.NET Core Identity implementation of <see cref="ITwoFactorService"/>, using TOTP codes
/// as Microsoft Authenticator and other authenticator apps generate them.
/// </summary>
/// <remarks>
/// Each call resolves its own <see cref="UserManager{TUser}"/> in a fresh scope. The circuit's
/// own UserManager shares one long-lived DbContext with every component in the circuit: it
/// refuses concurrent use, and entities it tracks go stale when another scope changes them.
/// This is the factory-per-operation rule, applied to Identity.
/// </remarks>
public class TwoFactorService(IServiceScopeFactory scopeFactory) : ITwoFactorService
{
    /// <summary>
    /// Name shown for the account in the authenticator app.
    /// </summary>
    private const string Issuer = "SafeCare";

    private const int RecoveryCodeCount = 10;

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<bool?> IsEnabledAsync(Guid userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = await userManager.FindByIdAsync(userId.ToString());
        return user?.TwoFactorEnabled;
    }

    public async Task<AuthenticatorSetupDto> GetAuthenticatorSetupAsync(Guid userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await FindUserAsync(userManager, userId);

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user)
                ?? throw new InvalidOperationException("Identity did not store an authenticator key.");
        }

        var label = Uri.EscapeDataString($"{Issuer}:{user.UserName}");
        var uri = $"otpauth://totp/{label}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

        return new AuthenticatorSetupDto(FormatKey(key), ToQrCodeDataUri(uri));
    }

    public async Task<IReadOnlyList<string>?> EnableAsync(Guid userId, string code)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await FindUserAsync(userManager, userId);

        var normalizedCode = code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, normalizedCode);

        if (!isValid)
        {
            return null;
        }

        EnsureSucceeded(await userManager.SetTwoFactorEnabledAsync(user, true));

        return await GenerateRecoveryCodesAsync(userManager, user);
    }

    public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(Guid userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await FindUserAsync(userManager, userId);

        return await GenerateRecoveryCodesAsync(userManager, user);
    }

    public async Task<int> CountRecoveryCodesAsync(Guid userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await FindUserAsync(userManager, userId);

        return await userManager.CountRecoveryCodesAsync(user);
    }

    public async Task RestartSetupAsync(Guid userId)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await FindUserAsync(userManager, userId);

        // Both calls rotate the security stamp, which signs the account's open sessions out
        // at the next validation, so a lost phone stops being any use straight away.
        EnsureSucceeded(await userManager.SetTwoFactorEnabledAsync(user, false));
        EnsureSucceeded(await userManager.ResetAuthenticatorKeyAsync(user));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Identity operation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    private static async Task<User> FindUserAsync(UserManager<User> userManager, Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");

    private static async Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(UserManager<User> userManager, User user)
    {
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return codes?.ToList()
            ?? throw new InvalidOperationException("Identity did not generate recovery codes.");
    }

    /// <summary>
    /// Splits the key into lower-case groups of four, which is far easier to copy by hand.
    /// Authenticator apps ignore both the spaces and the case.
    /// </summary>
    private static string FormatKey(string key)
    {
        var result = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (i > 0)
            {
                result.Append(' ');
            }

            result.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string ToQrCodeDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(data);
        var png = qrCode.GetGraphic(6);

        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }
}
