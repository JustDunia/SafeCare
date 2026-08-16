using FluentValidation;
using SafeCare.Data.Entities;
using SafeCare.ViewModels;

namespace SafeCare.Validators;

/// <summary>
/// Validation rules for the "add staff account" form, with messages in Polish.
/// </summary>
/// <remarks>
/// These rules give the form immediate feedback; they do not stand alone.
/// <see cref="Services.IUserManagementService"/> re-checks the same constraints server-side,
/// and password strength itself is enforced by ASP.NET Core Identity, whose failures are
/// translated into Polish there.
/// </remarks>
public class AdminUserCreateValidator : AbstractValidator<AdminUserCreateVm>
{
    public AdminUserCreateValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("Nazwa użytkownika jest wymagana")
            .MaximumLength(256)
            .WithMessage("Nazwa użytkownika może mieć maksymalnie 256 znaków");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Imię jest wymagane")
            .MaximumLength(20)
            .WithMessage("Imię może mieć maksymalnie 20 znaków");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Nazwisko jest wymagane")
            .MaximumLength(30)
            .WithMessage("Nazwisko może mieć maksymalnie 30 znaków");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Adres e-mail jest wymagany")
            .EmailAddress()
            .WithMessage("Adres e-mail musi być poprawny")
            .MaximumLength(256)
            .WithMessage("Adres e-mail może mieć maksymalnie 256 znaków");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Hasło jest wymagane")
            .MaximumLength(256)
            .WithMessage("Hasło może mieć maksymalnie 256 znaków");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Potwierdzenie hasła jest wymagane")
            .Equal(x => x.Password)
            .WithMessage("Hasła muszą być takie same");

        RuleFor(x => x.Role)
            .Must(x => x is AppRoles.Admin or AppRoles.User)
            .WithMessage("Rola musi być ustawiona na Admin lub User");
    }

    /// <summary>
    /// Adapter that lets MudBlazor validate a single field at a time. See the identical hook
    /// on <see cref="IncidentRegistrationFormValidator"/>.
    /// </summary>
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var validationCtx = ValidationContext<AdminUserCreateVm>.CreateWithOptions(
            (AdminUserCreateVm)model,
            x => x.IncludeProperties(propertyName));

        var result = await ValidateAsync(validationCtx);

        if (result.IsValid)
        {
            return [];
        }

        return result.Errors.Select(e => e.ErrorMessage);
    };
}
