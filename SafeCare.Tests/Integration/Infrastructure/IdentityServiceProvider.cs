using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SafeCare.Data;
using SafeCare.Data.Entities;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Builds a minimal service provider carrying a real <see cref="UserManager{TUser}"/> backed by
/// the test database, so account rules are exercised against genuine Identity behaviour.
/// </summary>
public static class IdentityServiceProvider
{
    public static ServiceProvider Build(DbContextOptions<AppDbContext> options)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        // AddEntityFrameworkStores needs a scoped context; the "always use the factory" rule
        // guards Blazor circuits, which do not exist here.
        services.AddScoped(_ => new AppDbContext(options));
        services.AddSingleton<IDbContextFactory<AppDbContext>>(new TestDbContextFactory(options));

        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        return services.BuildServiceProvider();
    }
}
