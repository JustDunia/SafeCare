using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SafeCare.Data;
using SafeCare.Data.Entities;
using Testcontainers.PostgreSql;

namespace SafeCare.Tests.Integration.Infrastructure;

/// <summary>
/// Owns the PostgreSQL container shared by every integration test. Migrations are expensive,
/// so they run once for the whole session; isolation between tests comes from
/// <see cref="ResetAsync"/> emptying the tables instead.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Seeded through EF HasData in the initial migration; must survive cleanup.</summary>
    public static readonly Guid SeededAdminId = Guid.Parse("62228aa3-8032-4d31-8b99-719629d26bb7");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public DbContextOptions<AppDbContext> Options { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new AppDbContext(Options);
        await dbContext.Database.MigrateAsync();

        await SeedRolesAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Empties the domain tables and every account except the seeded administrator, which
    /// cannot be recreated without re-running the migration that carries it.
    /// </summary>
    public async Task ResetAsync()
    {
        const string sql = """
            TRUNCATE TABLE "IncidentDefinitionIncidentReport", "IncidentReports" RESTART IDENTITY CASCADE;
            TRUNCATE TABLE "Departments" RESTART IDENTITY CASCADE;
            TRUNCATE TABLE "IncidentDefinitions" RESTART IDENTITY CASCADE;
            DELETE FROM "AspNetUserRoles" WHERE "UserId" <> @adminId;
            DELETE FROM "AspNetUsers" WHERE "Id" <> @adminId;
            """;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("adminId", SeededAdminId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Creates the application roles and gives the seeded administrator its role, mirroring
    /// what <see cref="IdentitySeeder"/> does at application startup.
    /// </summary>
    private async Task SeedRolesAsync()
    {
        var provider = IdentityServiceProvider.Build(Options);
        using var scope = provider.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager);
    }
}

[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
