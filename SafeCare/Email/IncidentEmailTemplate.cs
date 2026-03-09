using SafeCare.Data.Entities;
using System.Net;

namespace SafeCare.Email
{
    public static class IncidentEmailTemplate
    {
        public static EmailMessage Build(IncidentReport report, IReadOnlyList<string> bccRecipients)
        {
            var subject = $"[SafeCare] Nowe zgłoszenie zdarzenia #{report.Id}";
            var html = BuildHtml(report);
            return new EmailMessage(bccRecipients, subject, html);
        }

        private static string BuildHtml(IncidentReport report)
        {
            var incidentDate = report.Date.HasValue
                ? report.Date.Value.ToString("dd.MM.yyyy HH:mm")
                : report.DateFrom.HasValue && report.DateTo.HasValue
                    ? $"{report.DateFrom.Value:dd.MM.yyyy} \u2013 {report.DateTo.Value:dd.MM.yyyy}"
                    : "Nie podano";

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
                    table { border-collapse: collapse; width: 100%; max-width: 600px; }
                    td { padding: 8px 12px; border: 1px solid #ddd; vertical-align: top; }
                    td:first-child { font-weight: bold; width: 200px; background-color: #f5f5f5; white-space: nowrap; }
                    .footer { padding: 16px 24px; font-size: 12px; color: #777; border-top: 1px solid #eee; }
                  </style>
                </head>
                <body>
                  <div class="header">
                    <h2>SafeCare - Nowe zgłoszenie zdarzenia</h2>
                  </div>
                  <div class="content">
                    <p>Do systemu SafeCare wpłynęło nowe zgłoszenie zdarzenia. Poniżej znajdują się szczegóły.</p>
                    <table>
                      <tr>
                        <td>Numer zgłoszenia</td>
                        <td>#{{report.Id}}</td>
                      </tr>
                      <tr>
                        <td>Data wpłynięcia</td>
                        <td>{{report.CreatedAt:dd.MM.yyyy HH:mm}}</td>
                      </tr>
                      <tr>
                        <td>Czas zdarzenia</td>
                        <td>{{HtmlEncode(incidentDate)}}</td>
                      </tr>
                      <tr>
                        <td>Oddział</td>
                        <td>{{HtmlEncode(report.Department?.Name)}}</td>
                      </tr>
                      <tr>
                        <td>Opis zdarzenia</td>
                        <td>{{HtmlEncode(report.IncidentDescription)}}</td>
                      </tr>
                    </table>
                  </div>
                  <div class="footer">
                    Wiadomość wygenerowana automatycznie przez system SafeCare. Nie odpowiadaj na tę wiadomść.
                  </div>
                </body>
                </html>
                """;
        }

        private static string HtmlEncode(string? value) =>
            WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
