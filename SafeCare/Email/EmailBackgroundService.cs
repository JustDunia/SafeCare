using Serilog;

namespace SafeCare.Email
{
    public class EmailBackgroundService(IEmailQueue queue, IServiceScopeFactory scopeFactory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("EmailBackgroundService started.");

            await foreach (var message in queue.DequeueAllAsync(stoppingToken))
            {
                await ProcessWithRetryAsync(message, stoppingToken);
            }

            Log.Information("EmailBackgroundService stopped.");
        }

        private async Task ProcessWithRetryAsync(EmailMessage message, CancellationToken ct)
        {
            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendAsync(message, ct);

                    Log.Information(
                        "Email sent to {Count} recipients (BCC). Subject: {Subject}",
                        message.BccRecipients.Count, message.Subject);
                    return;
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("Email sending cancelled during shutdown. Subject: {Subject}", message.Subject);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error(ex,
                        "Email attempt {Attempt}/{Max} failed. Subject: {Subject}",
                        attempt, maxAttempts, message.Subject);

                    if (attempt < maxAttempts)
                    {
                        // Exponential backoff: 2 s after attempt 1, 4 s after attempt 2
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    }
                }
            }

            Log.Error(
                "All {Max} email attempts exhausted. Subject: {Subject}. Recipients: {Count}",
                maxAttempts, message.Subject, message.BccRecipients.Count);
        }
    }
}
