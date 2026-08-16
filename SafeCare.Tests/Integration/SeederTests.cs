using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class SeederTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task LoadsDictionaryDataIntoAnEmptyDatabase()
    {
        await using var db = CreateDbContext();

        await DbSeeder.SeedAsync(db);

        Assert.True(await db.Departments.AnyAsync());
        Assert.True(await db.IncidentDefinitions.AnyAsync());
    }

    [Fact]
    public async Task DoesNotDuplicateDataOnASecondRun()
    {
        await using var db = CreateDbContext();
        await DbSeeder.SeedAsync(db);
        var departmentCount = await db.Departments.CountAsync();

        await DbSeeder.SeedAsync(db);

        Assert.Equal(departmentCount, await db.Departments.CountAsync());
    }

    [Fact]
    public async Task LeavesAnAlreadyPopulatedDatabaseAlone()
    {
        await SeedDepartmentAsync("Jedyny oddział", "JO");

        await using var db = CreateDbContext();
        await DbSeeder.SeedAsync(db);

        Assert.Equal(1, await db.Departments.CountAsync());
    }

    [Fact]
    public async Task CreatesRolesAndGivesTheSeededAccountItsAdminRole()
    {
        var provider = IdentityServiceProvider.Build(Fixture.Options);
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager);

        Assert.True(await roleManager.RoleExistsAsync(AppRoles.Admin));
        Assert.True(await roleManager.RoleExistsAsync(AppRoles.User));

        var admin = await userManager.FindByNameAsync("admin");
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin!, AppRoles.Admin));
    }

    [Fact]
    public async Task IsSafeToRunRepeatedly()
    {
        var provider = IdentityServiceProvider.Build(Fixture.Options);
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager);
        var exception = await Record.ExceptionAsync(() =>
            IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager));

        Assert.Null(exception);
    }
}
