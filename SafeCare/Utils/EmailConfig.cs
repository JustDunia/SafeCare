using SafeCare.Email;

namespace SafeCare.Utils
{
    public static class EmailConfig
    {
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
