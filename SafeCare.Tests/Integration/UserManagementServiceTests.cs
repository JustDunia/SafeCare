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
        var twoFactor = new TwoFactorService(_provider.GetRequiredService<IServiceScopeFactory>());
        return new UserManagementService(userManager, DbFactory, EmailQueue, twoFactor);
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

    [Fact]
    public async Task ReportsWhetherEachAccountHasTwoFactorAuthentication()
    {
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser());
        var nowak = await EnableTwoFactorAsync("nowak");

        var users = await sut.GetUsersAsync();

        Assert.True(users.Single(u => u.Id == nowak.Id).TwoFactorEnabled);
        Assert.False(users.Single(u => u.Id == PostgresFixture.SeededAdminId).TwoFactorEnabled);
    }

    [Fact]
    public async Task ResetsAnotherUsersTwoFactorAuthenticationAndTellsThem()
    {
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser());
        var nowak = await EnableTwoFactorAsync("nowak");
        var oldKey = await AuthenticatorKeyAsync(nowak.Id);
        var oldStamp = nowak.SecurityStamp;

        var (success, error) = await sut.ResetTwoFactorAsync(nowak.Id, PostgresFixture.SeededAdminId);

        Assert.True(success, error);

        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var reloaded = await userManager.FindByIdAsync(nowak.Id.ToString());
        Assert.NotNull(reloaded);
        Assert.False(reloaded.TwoFactorEnabled);
        // The lost phone's codes must stop working, and its open sessions must end.
        Assert.NotEqual(oldKey, await userManager.GetAuthenticatorKeyAsync(reloaded));
        Assert.NotEqual(oldStamp, reloaded.SecurityStamp);

        var notice = Assert.Single(EmailQueue.Sent);
        Assert.Equal(["nowak@szpital.pl"], notice.BccRecipients);
        Assert.Contains("nowak", notice.HtmlBody);
    }

    [Fact]
    public async Task RefusesToResetTheCallersOwnTwoFactorAuthentication()
    {
        var sut = CreateSut();

        var (success, error) = await sut.ResetTwoFactorAsync(PostgresFixture.SeededAdminId, PostgresFixture.SeededAdminId);

        Assert.False(success);
        Assert.Equal("Własną weryfikację dwuetapową zmienisz w ustawieniach konta.", error);
        Assert.Empty(EmailQueue.Sent);
    }

    [Fact]
    public async Task ResetsTwoFactorAuthenticationEvenWhenTheMailQueueFails()
    {
        // The user has lost their phone and needs to get back in; a broken mail server must
        // not stand in the way.
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser());
        var nowak = await EnableTwoFactorAsync("nowak");
        EmailQueue.ThrowOnEnqueue = true;

        var (success, error) = await sut.ResetTwoFactorAsync(nowak.Id, PostgresFixture.SeededAdminId);

        Assert.True(success, error);
    }

    [Fact]
    public async Task ReportsAMissingAccountOnTwoFactorReset()
    {
        var sut = CreateSut();

        var (success, error) = await sut.ResetTwoFactorAsync(Guid.NewGuid(), PostgresFixture.SeededAdminId);

        Assert.False(success);
        Assert.Equal("Nie znaleziono użytkownika.", error);
    }

    [Fact]
    public async Task ResetsTwoFactorAuthenticationAgainAfterTheUserSetItUpAgain()
    {
        // One admin session, two resets, with the user setting the app up again in between -
        // the second reset must not trip over what the first one left behind.
        var sut = CreateSut();
        await sut.CreateUserAsync(ValidUser());
        var nowak = await EnableTwoFactorAsync("nowak");
        await sut.ResetTwoFactorAsync(nowak.Id, PostgresFixture.SeededAdminId);
        await EnableTwoFactorAsync("nowak");

        var (success, error) = await sut.ResetTwoFactorAsync(nowak.Id, PostgresFixture.SeededAdminId);

        Assert.True(success, error);
    }

    /// <summary>
    /// Sets the authenticator app up the way the setup page does, through scopes of its own.
    /// </summary>
    private async Task<User> EnableTwoFactorAsync(string userName)
    {
        var userId = await UserIdAsync(userName);

        var twoFactor = new TwoFactorService(_provider.GetRequiredService<IServiceScopeFactory>());
        var setup = await twoFactor.GetAuthenticatorSetupAsync(userId);
        Assert.NotNull(await twoFactor.EnableAsync(userId, Totp.Compute(setup.SharedKey)));

        using var scope = _provider.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByIdAsync(userId.ToString()))!;
    }

    private async Task<Guid> UserIdAsync(string userName)
    {
        using var scope = _provider.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByNameAsync(userName);
        Assert.NotNull(user);
        return user.Id;
    }

    private async Task<string?> AuthenticatorKeyAsync(Guid userId)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return await userManager.GetAuthenticatorKeyAsync(user!);
    }
}
