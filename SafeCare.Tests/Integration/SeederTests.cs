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

        // A specific, known department name from SeedData.sql rather than just "some row
        // exists" - a stronger check that the actual seed content, not merely a row count,
        // made it into the database.
        Assert.True(await db.Departments.AnyAsync(d => d.Name == "Ortopedia"));
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
        // PostgresFixture.InitializeAsync already runs the seeder once, and ResetAsync never
        // touches AspNetRoles or the admin's AspNetUserRoles row, so all the assertions below
        // would already hold before the SUT ran. Tear both roles and the admin's role
        // assignment down first so the seeder is the thing that has to recreate them.
        await using (var setup = CreateDbContext())
        {
            var adminUserId = PostgresFixture.SeededAdminId;
            var roleAssignments = await setup.UserRoles
                .Where(ur => ur.UserId == adminUserId)
                .ToListAsync();
            setup.UserRoles.RemoveRange(roleAssignments);

            var roles = await setup.Roles
                .Where(r => r.Name == AppRoles.Admin || r.Name == AppRoles.User)
                .ToListAsync();
            setup.Roles.RemoveRange(roles);

            await setup.SaveChangesAsync();
        }

        var provider = IdentityServiceProvider.Build(Fixture.Options);
        using var scope = provider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        Assert.False(await roleManager.RoleExistsAsync(AppRoles.Admin));

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
