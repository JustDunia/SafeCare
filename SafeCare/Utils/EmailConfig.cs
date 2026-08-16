using SafeCare.Email;

namespace SafeCare.Utils
{
    /// <summary>
    /// Registers the fire-and-forget e-mail notification pipeline.
    /// </summary>
    public static class EmailConfig
    {
        /// <summary>
        /// Binds <see cref="EmailSettings"/> and wires the queue, the delivery provider chosen
        /// by <c>Email:Provider</c>, and the background sender.
        /// </summary>
        public static IServiceCollection AddEmailNotifications(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));

            // Singleton — channel must be shared across all Blazor circuits
            services.AddSingleton<IEmailQueue, EmailQueue>();

            // Transient — new client per SendAsync call, no shared state
            var provider = configuration["Email:Provider"] ?? "Smtp";
            if (provider == "MicrosoftGraph")
                services.AddTransient<IEmailService, GraphEmailService>();
            else
                services.AddTransient<IEmailService, MailKitEmailService>();

            // Singleton — registered by AddHostedService<T>
            services.AddHostedService<EmailBackgroundService>();

            return services;
        }
    }
}
