using Microsoft.EntityFrameworkCore;
using SafeCare.Data;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Hands out short-lived contexts exactly the way the application's factory does, so services
/// are exercised through the same contract they use in production.
/// </summary>
public sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
    : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}
