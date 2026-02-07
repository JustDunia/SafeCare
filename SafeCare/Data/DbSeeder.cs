using Microsoft.EntityFrameworkCore;

namespace SafeCare.Data;

public static class DbSeeder
{
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
