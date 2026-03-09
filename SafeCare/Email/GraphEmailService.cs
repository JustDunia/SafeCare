using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace SafeCare.Email
{
    /// <summary>
    /// Sends email via Microsoft Graph API using Client Credentials (app-to-app).
    /// Requires an Azure AD app registration with the Mail.Send application permission.
    /// Recommended for production with Microsoft 365 / Exchange Online.
    /// </summary>
    public class GraphEmailService(IOptions<EmailSettings> options) : IEmailService
    {
        private readonly EmailSettings _settings = options.Value;

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            var oauth = _settings.OAuth2;

            var credential = new ClientSecretCredential(
                oauth.TenantId,
                oauth.ClientId,
                oauth.ClientSecret);

            var graphClient = new GraphServiceClient(credential);

            var bccRecipients = message.BccRecipients
                .Select(addr => new Recipient
                {
                    EmailAddress = new EmailAddress { Address = addr }
                })
                .ToList();

            var mail = new Message
            {
                Subject = message.Subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = message.HtmlBody
                },
                // To: sender's own address — actual delivery goes via BCC so recipients stay hidden
                ToRecipients =
                [
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Name = _settings.FromName,
                            Address = _settings.FromAddress
                        }
                    }
                ],
                BccRecipients = bccRecipients
            };

            var requestBody = new SendMailPostRequestBody { Message = mail };

            await graphClient.Users[_settings.FromAddress]
                .SendMail
                .PostAsync(requestBody, cancellationToken: cancellationToken);
        }
    }
}
