using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.Dtos;

namespace SafeCare.Services;

/// <summary>
/// Staff account administration behind <c>/admin/users</c>, restricted to the
/// <see cref="AppRoles.Admin"/> role.
/// </summary>
/// <remarks>
/// The write operations report failure as a <c>(Success, Error)</c> tuple carrying a
/// ready-to-display Polish message rather than throwing. Rejecting a duplicate user name or a
/// weak password is an expected outcome of the form, not an exceptional condition, and the
/// page binds the message straight into a snackbar.
/// </remarks>
public interface IUserManagementService
{
    /// <summary>
    /// Returns every account with its effective role, ordered by user name.
    /// </summary>
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken token = default);

    /// <summary>
    /// Creates an account and assigns it a role, rolling the account back if the role
    /// assignment fails so that no user is left without one.
    /// </summary>
    /// <returns>Success, or a Polish message explaining why the account was rejected.</returns>
    Task<(bool Success, string? Error)> CreateUserAsync(CreateUserDto dto, CancellationToken token = default);

    /// <summary>
    /// Deletes an account. Refuses to delete the caller's own account, and refuses to remove
    /// the last remaining administrator, which would leave the system unmanageable.
    /// </summary>
    /// <param name="currentUserId">The signed-in administrator performing the deletion.</param>
    /// <returns>Success, or a Polish message explaining why the deletion was refused.</returns>
    Task<(bool Success, string? Error)> DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken token = default);
}

/// <summary>
/// ASP.NET Core Identity implementation of <see cref="IUserManagementService"/>.
/// </summary>
public class UserManagementService(
    UserManager<User> userManager,
    IDbContextFactory<AppDbContext> dbContextFactory) : IUserManagementService
{
    private readonly UserManager<User> _userManager = userManager;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory = dbContextFactory;

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken token = default)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var usersWithRoles = await (from u in dbContext.Users.AsNoTracking()
                                    join ur in dbContext.Set<IdentityUserRole<Guid>>() on u.Id equals ur.UserId into userRoleJoin
                                    from ur in userRoleJoin.DefaultIfEmpty()
                                    join r in dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id into roleJoin
                                    from r in roleJoin.DefaultIfEmpty()
                                    select new
                                    {
                                        u.Id,
                                        u.UserName,
                                        u.FirstName,
                                        u.LastName,
                                        u.Email,
                                        Role = r != null ? r.Name : null
                                    })
            .ToListAsync(token);

        var result = usersWithRoles
            .GroupBy(x => new { x.Id, x.UserName, x.FirstName, x.LastName, x.Email })
            .Select(group =>
            {
                var role = group.Any(x => x.Role == AppRoles.Admin) ? AppRoles.Admin : AppRoles.User;

                return new AdminUserDto
                {
                    Id = group.Key.Id,
                    UserName = group.Key.UserName ?? string.Empty,
                    FirstName = group.Key.FirstName,
                    LastName = group.Key.LastName,
                    Email = group.Key.Email ?? string.Empty,
                    Role = role
                };
            })
            .OrderBy(x => x.UserName)
            .ToList();

        return result;
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(CreateUserDto dto, CancellationToken token = default)
    {
        var userName = dto.UserName.Trim();
        var firstName = dto.FirstName.Trim();
        var lastName = dto.LastName.Trim();
        var email = dto.Email.Trim();
        var role = dto.Role.Trim();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return (false, "Nazwa użytkownika jest wymagana.");
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            return (false, "Imię jest wymagane.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return (false, "Nazwisko jest wymagane.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "Adres e-mail jest wymagany.");
        }

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            return (false, "Hasło jest wymagane.");
        }

        if (userName.Length > 256)
        {
            return (false, "Nazwa użytkownika może mieć maksymalnie 256 znaków.");
        }

        if (firstName.Length > 20)
        {
            return (false, "Imię może mieć maksymalnie 20 znaków.");
        }

        if (lastName.Length > 30)
        {
            return (false, "Nazwisko może mieć maksymalnie 30 znaków.");
        }

        if (email.Length > 256)
        {
            return (false, "Adres e-mail może mieć maksymalnie 256 znaków.");
        }

        if (dto.Password.Length > 256)
        {
            return (false, "Hasło może mieć maksymalnie 256 znaków.");
        }

        if (role is not (AppRoles.Admin or AppRoles.User))
        {
            return (false, "Wybrano nieprawidłową rolę użytkownika.");
        }

        var existingByName = await _userManager.FindByNameAsync(userName);
        if (existingByName is not null)
        {
            return (false, "Użytkownik o podanej nazwie już istnieje.");
        }

        var existingByEmail = await _userManager.FindByEmailAsync(email);
        if (existingByEmail is not null)
        {
            return (false, "Użytkownik o podanym adresie e-mail już istnieje.");
        }

        var user = new User
        {
            UserName = userName,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            ReceiveEmailNotifications = false
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            return (false, TranslateIdentityErrors(createResult.Errors));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, role);
        if (!addRoleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return (false, TranslateIdentityErrors(addRoleResult.Errors));
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken token = default)
    {
        if (userId == currentUserId)
        {
            return (false, "Nie możesz usunąć własnego konta.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, "Nie znaleziono użytkownika.");
        }

        var isAdmin = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
        if (isAdmin)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var adminCount = await (from u in dbContext.Users
                                    join ur in dbContext.Set<IdentityUserRole<Guid>>() on u.Id equals ur.UserId
                                    join r in dbContext.Roles on ur.RoleId equals r.Id
                                    where r.Name == AppRoles.Admin
                                    select u.Id)
                                   .CountAsync(token);

            if (adminCount <= 1)
            {
                return (false, "Nie można usunąć ostatniego administratora.");
            }
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            return (false, TranslateIdentityErrors(deleteResult.Errors));
        }

        return (true, null);
    }

    /// <summary>
    /// Joins Identity's English error descriptions into a single Polish message for the UI.
    /// </summary>
    private static string TranslateIdentityErrors(IEnumerable<IdentityError> errors)
    {
        var translated = errors
            .Select(TranslateIdentityError)
            .Distinct()
            .ToList();

        return translated.Count > 0
            ? string.Join(" ", translated)
            : "Operacja nie powiodła się.";
    }

    /// <summary>
    /// Maps a single Identity error code to its Polish equivalent, falling back to a generic
    /// message for codes the form cannot produce.
    /// </summary>
    private static string TranslateIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateUserName" => "Użytkownik o podanej nazwie już istnieje.",
            "DuplicateEmail" => "Użytkownik o podanym adresie e-mail już istnieje.",
            "InvalidUserName" => "Nazwa użytkownika ma nieprawidłowy format.",
            "InvalidEmail" => "Adres e-mail ma nieprawidłowy format.",
            "PasswordTooShort" => "Hasło jest zbyt krótkie.",
            "PasswordRequiresNonAlphanumeric" => "Hasło musi zawierać znak specjalny.",
            "PasswordRequiresDigit" => "Hasło musi zawierać cyfrę.",
            "PasswordRequiresLower" => "Hasło musi zawierać małą literę.",
            "PasswordRequiresUpper" => "Hasło musi zawierać dużą literę.",
            _ => "Operacja nie powiodła się. Sprawdź poprawność danych."
        };
    }
}
