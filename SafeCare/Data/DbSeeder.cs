using Microsoft.EntityFrameworkCore;

namespace SafeCare.Data;

/// <summary>
/// Loads the dictionary data (wards, incident definitions) and the demo reports from the
/// embedded <c>Data/SeedData.sql</c> script.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Runs the seed script, but only on a database that has not been seeded yet.
    /// </summary>
    /// <remarks>
    /// A non-empty <c>Departments</c> table is the marker for "already seeded", which makes
    /// this safe to call on every startup. Note that the <c>admin</c> account is not created
    /// here — it ships as EF <c>HasData</c> inside the initial migration.
    /// </remarks>
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Departments.AnyAsync())
        {
            return;
        }

        var assembly = typeof(DbSeeder).Assembly;
        var resourceName = "SafeCare.Data.SeedData.sql";

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");

        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        await context.Database.ExecuteSqlRawAsync(sql);
    }
}
