using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace SafeCare.Email
{
    public interface IEmailService
    {
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

    public class MailKitEmailService(IOptions<EmailSettings> options) : IEmailService
    {
        private readonly EmailSettings _settings = options.Value;

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            var smtp = _settings.Smtp;

            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));

            // To: sender's own address — actual delivery goes via BCC so recipients stay hidden
            mime.To.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));

            foreach (var address in message.BccRecipients)
            {
                mime.Bcc.Add(MailboxAddress.Parse(address));
            }

            mime.Subject = message.Subject;
            mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

            using var client = new SmtpClient();

            var secureOption = smtp.UseSsl
                ? SecureSocketOptions.SslOnConnect           // port 465 – implicit TLS
                : SecureSocketOptions.StartTlsWhenAvailable; // port 587 – STARTTLS (default)

            await client.ConnectAsync(smtp.Host, smtp.Port, secureOption, cancellationToken);

            if (smtp.AuthMode == "UsernamePassword")
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}
