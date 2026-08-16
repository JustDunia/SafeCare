using FluentValidation.Results;
using SafeCare.Data.Entities;
using SafeCare.Validators;
using SafeCare.ViewModels;

namespace SafeCare.Tests.Unit.Validators;

public class AdminUserCreateValidatorTests
{
    private readonly AdminUserCreateValidator _sut = new();

    private static AdminUserCreateVm ValidForm() => new()
    {
        UserName = "nowak",
        FirstName = "Jan",
        LastName = "Nowak",
        Email = "jan.nowak@szpital.pl",
        Password = "Haslo123!",
        ConfirmPassword = "Haslo123!",
        Role = AppRoles.User
    };

    private static bool HasErrorFor(ValidationResult result, string propertyName) =>
        result.Errors.Any(e => e.PropertyName == propertyName);

    [Fact]
    public void AcceptsAWellFormedAccount()
    {
        var result = _sut.Validate(ValidForm());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void RequiresAUserName()
    {
        var form = ValidForm();
        form.UserName = "";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.UserName)));
    }

    [Fact]
    public void RejectsAFirstNameOverTwentyCharacters()
    {
        var form = ValidForm();
        form.FirstName = new string('a', 21);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.FirstName)));
    }

    [Fact]
    public void RejectsALastNameOverThirtyCharacters()
    {
        var form = ValidForm();
        form.LastName = new string('a', 31);

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.LastName)));
    }

    [Fact]
    public void RejectsAMalformedEmail()
    {
        var form = ValidForm();
        form.Email = "nie-jest-adresem";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Email)));
    }

    [Fact]
    public void RejectsMismatchedPasswordConfirmation()
    {
        var form = ValidForm();
        form.ConfirmPassword = "CosInnego123!";

        var result = _sut.Validate(form);

        Assert.Contains(result.Errors, e => e.ErrorMessage == "Hasła muszą być takie same");
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.User)]
    public void AcceptsTheTwoKnownRoles(string role)
    {
        var form = ValidForm();
        form.Role = role;

        Assert.False(HasErrorFor(_sut.Validate(form), nameof(form.Role)));
    }

    [Fact]
    public void RejectsAnUnknownRole()
    {
        var form = ValidForm();
        form.Role = "Superadmin";

        Assert.True(HasErrorFor(_sut.Validate(form), nameof(form.Role)));
    }
}
