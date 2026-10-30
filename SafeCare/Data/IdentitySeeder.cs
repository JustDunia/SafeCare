using Microsoft.AspNetCore.Identity;
using SafeCare.Data.Entities;

namespace SafeCare.Data;

/// <summary>
/// Creates the application roles on startup and guarantees that the seeded <c>admin</c>
/// account actually holds the <see cref="AppRoles.Admin"/> role.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>
    /// Ensures both roles exist and that the seeded administrator is a member of
    /// <see cref="AppRoles.Admin"/> with lockout enabled. Safe to run on every startup.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The <c>admin</c> account is missing, or a role could not be created or assigned.
    /// The account is seeded through EF <c>HasData</c> in <see cref="Entities.User"/>, so its
    /// absence means the initial migration was not applied and startup should fail loudly
    /// rather than leave the system without an administrator.
    /// </exception>
    public static async Task SeedRolesAndAdminAsync(RoleManager<IdentityRole<Guid>> roleManager, UserManager<User> userManager)
    {
        await EnsureRoleExistsAsync(roleManager, AppRoles.Admin);
        await EnsureRoleExistsAsync(roleManager, AppRoles.User);

        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser is null)
        {
            throw new InvalidOperationException("Seeded admin account was not found while assigning roles.");
        }

        if (!await userManager.IsInRoleAsync(adminUser, AppRoles.Admin))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
            if (!addToRoleResult.Succeeded)
            {
                var errors = string.Join(", ", addToRoleResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Failed to assign admin role to seeded account: {errors}");
            }
        }

        // The HasData seed predates lockout and leaves it off, which would give the one
        // account that matters most unlimited guesses at its password and its two-factor code.
        if (!await userManager.GetLockoutEnabledAsync(adminUser))
        {
            var lockoutResult = await userManager.SetLockoutEnabledAsync(adminUser, true);
            if (!lockoutResult.Succeeded)
            {
                var errors = string.Join(", ", lockoutResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Failed to enable lockout for seeded account: {errors}");
            }
        }
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole<Guid>> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var createResult = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(x => x.Description));
            throw new InvalidOperationException($"Failed to create role {roleName}: {errors}");
        }
    }
}
