using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SafeCare.Data.Entities;
using SafeCare.Dtos;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class UserManagementServiceTests(PostgresFixture fixture) : IntegrationTestBase(fixture)
{
    private ServiceProvider _provider = null!;

    private UserManagementService CreateSut()
    {
        _provider = IdentityServiceProvider.Build(Fixture.Options);
        var userManager = _provider.GetRequiredService<UserManager<User>>();
        return new UserManagementService(userManager, DbFactory);
    }

    private static CreateUserDto ValidUser(string userName = "nowak") => new()
    {
        UserName = userName,
        FirstName = "Jan",
        LastName = "Nowak",
        Email = $"{userName}@szpital.pl",
        Password = "Haslo123!",
        Role = AppRoles.User
    };

    [Fact]
    public async Task CreatesAnAccountAndAssignsItsRole()
    {
        var sut = CreateSut();

        var (success, error) = await sut.CreateUserAsync(ValidUser());

        Assert.True(success, error);

        var users = await sut.GetUsersAsync();
        var created = Assert.Single(users, u => u.UserName == "nowak");
        Assert.Equal(AppRoles.User, created.Role);
        Assert.Equal("Jan", created.FirstName);
    }

    [Fact]
    public async Task RejectsADuplicateUserName()
    {
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser());

        var duplicate = ValidUser();
        duplicate.Email = "inny@szpital.pl";

        var (success, error) = await sut.CreateUserAsync(duplicate);

        Assert.False(success);
        Assert.Equal("Użytkownik o podanej nazwie już istnieje.", error);
    }

    [Fact]
    public async Task RejectsADuplicateEmailAddress()
    {
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser());

        var duplicate = ValidUser("inny");
        duplicate.Email = "nowak@szpital.pl";

        var (success, error) = await sut.CreateUserAsync(duplicate);

        Assert.False(success);
        Assert.Equal("Użytkownik o podanym adresie e-mail już istnieje.", error);
    }

    [Fact]
    public async Task RejectsAnUnknownRole()
    {
        var sut = CreateSut();
        var dto = ValidUser();
        dto.Role = "Superadmin";

        var (success, error) = await sut.CreateUserAsync(dto);

        Assert.False(success);
        Assert.Equal("Wybrano nieprawidłową rolę użytkownika.", error);
    }

    [Fact]
    public async Task RejectsAWeakPasswordWithAPolishMessage()
    {
        var sut = CreateSut();
        var dto = ValidUser();
        dto.Password = "abc";

        var (success, error) = await sut.CreateUserAsync(dto);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("Hasło", error);
    }

    [Theory]
    [InlineData("", "Jan", "Nowak", "a@b.pl", "Nazwa użytkownika jest wymagana.")]
    [InlineData("nowak", "", "Nowak", "a@b.pl", "Imię jest wymagane.")]
    [InlineData("nowak", "Jan", "", "a@b.pl", "Nazwisko jest wymagane.")]
    [InlineData("nowak", "Jan", "Nowak", "", "Adres e-mail jest wymagany.")]
    public async Task RejectsMissingRequiredFields(
        string userName, string firstName, string lastName, string email, string expectedMessage)
    {
        var sut = CreateSut();

        var (success, error) = await sut.CreateUserAsync(new CreateUserDto
        {
            UserName = userName,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = "Haslo123!",
            Role = AppRoles.User
        });

        Assert.False(success);
        Assert.Equal(expectedMessage, error);
    }

    [Fact]
    public async Task RefusesToDeleteTheCallersOwnAccount()
    {
        var sut = CreateSut();
        var someoneElse = Guid.NewGuid();

        var (success, error) = await sut.DeleteUserAsync(someoneElse, someoneElse);

        Assert.False(success);
        Assert.Equal("Nie możesz usunąć własnego konta.", error);
    }

    [Fact]
    public async Task RefusesToDeleteTheLastAdministrator()
    {
        // Only the seeded admin holds the Admin role at this point; removing it would leave
        // the system unmanageable.
        var sut = CreateSut();

        var (success, error) = await sut.DeleteUserAsync(PostgresFixture.SeededAdminId, Guid.NewGuid());

        Assert.False(success);
        Assert.Equal("Nie można usunąć ostatniego administratora.", error);
    }

    [Fact]
    public async Task DeletesAnAdministratorWhenAnotherOneRemains()
    {
        var sut = CreateSut();
        var second = ValidUser("drugiadmin");
        second.Role = AppRoles.Admin;
        await sut.CreateUserAsync(second);

        var users = await sut.GetUsersAsync();
        var target = users.Single(u => u.UserName == "drugiadmin");

        var (success, error) = await sut.DeleteUserAsync(target.Id, PostgresFixture.SeededAdminId);

        Assert.True(success, error);
        Assert.DoesNotContain(await sut.GetUsersAsync(), u => u.UserName == "drugiadmin");
    }

    [Fact]
    public async Task ReportsAMissingAccount()
    {
        var sut = CreateSut();

        var (success, error) = await sut.DeleteUserAsync(Guid.NewGuid(), PostgresFixture.SeededAdminId);

        Assert.False(success);
        Assert.Equal("Nie znaleziono użytkownika.", error);
    }

    [Fact]
    public async Task ListsAccountsOrderedByUserName()
    {
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser("zielinski"));
        await sut.CreateUserAsync(ValidUser("adamski"));

        var users = await sut.GetUsersAsync();

        Assert.Equal(users.Select(u => u.UserName).OrderBy(n => n), users.Select(u => u.UserName));
    }
}
