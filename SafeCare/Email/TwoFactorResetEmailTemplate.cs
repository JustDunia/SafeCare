using System.Net;
using SafeCare.Data.Entities;

namespace SafeCare.Email
{
    /// <summary>
    /// Tells a user that an administrator has reset their two-factor authentication — the
    /// kind of change an account holder should hear about even when they asked for it.
    /// </summary>
    public static class TwoFactorResetEmailTemplate
    {
        public static EmailMessage Build(User user)
        {
            const string subject = "[SafeCare] Zresetowano weryfikację dwuetapową";
            var html = BuildHtml(user);
            return new EmailMessage([user.Email!], subject, html);
        }

        private static string BuildHtml(User user)
        {
            // $$ raw string: single { } are literal, {{ expr }} is interpolation
            return $$"""
                <!DOCTYPE html>
                <html lang="pl">
                <head>
                  <meta charset="UTF-8"/>
                  <style>
                    body { font-family: Arial, sans-serif; font-size: 14px; color: #333; margin: 0; padding: 0; }
                    .header { background-color: #1565C0; color: white; padding: 16px 24px; }
                    .header h2 { margin: 0; font-size: 18px; }
                    .content { padding: 24px; }
                    .content p { margin: 0 0 16px 0; }
                    .footer { padding: 16px 24px; font-size: 12px; color: #777; border-top: 1px solid #eee; }
                  </style>
                </head>
                <body>
                  <div class="header">
                    <h2>SafeCare - Zresetowano weryfikację dwuetapową</h2>
                  </div>
                  <div class="content">
                    <p>Administrator zresetował weryfikację dwuetapową konta <strong>{{HtmlEncode(user.UserName)}}</strong>.</p>
                    <p>Przy najbliższym logowaniu skonfigurujesz aplikację uwierzytelniającą od nowa. Dotychczasowe kody z aplikacji i kody odzyskiwania przestały działać.</p>
                    <p>Jeśli reset nie był przez Ciebie zgłoszony, niezwłocznie skontaktuj się z administratorem.</p>
                  </div>
                  <div class="footer">
                    Wiadomość wygenerowana automatycznie przez system SafeCare. Nie odpowiadaj na tę wiadomość.
                  </div>
                </body>
                </html>
                """;
        }

        private static string HtmlEncode(string? value) =>
            WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
