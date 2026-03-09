namespace SafeCare.Email
{
    public class EmailSettings
    {
        public const string SectionName = "Email";

        /// <summary>
        /// "Smtp" (default, for testing/non-M365) or "MicrosoftGraph" (for production M365).
        /// </summary>
        public string Provider { get; init; } = "Smtp";

        public string FromAddress { get; init; } = string.Empty;
        public string FromName { get; init; } = "SafeCare";

        public SmtpSettings Smtp { get; init; } = new();
        public OAuth2Settings OAuth2 { get; init; } = new();
    }

    public class SmtpSettings
    {
        public string Host { get; init; } = "localhost";
        public int Port { get; init; } = 1025;

        /// <summary>
        /// false (default) = STARTTLS when available | true = Implicit TLS on port 465
        /// </summary>
        public bool UseSsl { get; init; } = false;

        /// <summary>
        /// "None" — no auth (local dev SMTP like Mailpit) |
        /// "UsernamePassword" — basic SMTP auth (non-M365 servers, SendGrid, etc.)
        /// </summary>
        public string AuthMode { get; init; } = "None";

        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }

    public class OAuth2Settings
    {
        public string TenantId { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
    }
}
