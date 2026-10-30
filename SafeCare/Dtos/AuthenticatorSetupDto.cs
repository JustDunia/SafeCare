namespace SafeCare.Dtos;

/// <summary>
/// What the setup page shows so a user can add their account to an authenticator app.
/// </summary>
/// <param name="SharedKey">The secret in groups of four, for typing in by hand.</param>
/// <param name="QrCodeDataUri">A PNG <c>data:</c> URI of the <c>otpauth://</c> link, for scanning.</param>
public record AuthenticatorSetupDto(string SharedKey, string QrCodeDataUri);
