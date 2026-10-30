using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SafeCare.Data.Entities;
using SafeCare.Services;
using SafeCare.Tests.Integration.Infrastructure;

namespace SafeCare.Tests.Integration;

public class TwoFactorServiceTests(PostgresFixture fixture) : IntegrationTestBase(fixture), IDisposable
{
    private readonly ServiceProvider _provider = IdentityServiceProvider.Build(fixture.Options);

    private static readonly Guid AdminId = PostgresFixture.SeededAdminId;

    private TwoFactorService CreateSut() => new(_provider.GetRequiredService<IServiceScopeFactory>());

    /// <summary>
    /// Reads the administrator afresh. The service works in scopes of its own, so an entity
    /// held from an earlier read would show stale values.
    /// </summary>
    private async Task<User> AdminAsync()
    {
        using var scope = _provider.CreateScope();
        var admin = await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByIdAsync(AdminId.ToString());
        Assert.NotNull(admin);
        return admin;
    }

    private async Task<bool> RedeemRecoveryCodeAsync(string code)
    {
        using var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await userManager.FindByIdAsync(AdminId.ToString());
        return (await userManager.RedeemTwoFactorRecoveryCodeAsync(admin!, code)).Succeeded;
    }

    [Fact]
    public async Task OffersTheSameKeyUntilSetupIsFinished()
    {
        // Reloading the setup page must not invalidate a QR code the user has already scanned.
        var sut = CreateSut();

        var first = await sut.GetAuthenticatorSetupAsync(AdminId);
        var second = await sut.GetAuthenticatorSetupAsync(AdminId);

        Assert.Equal(first.SharedKey, second.SharedKey);
        Assert.StartsWith("data:image/png;base64,", first.QrCodeDataUri);
    }

    [Fact]
    public async Task ShowsTheKeyTheAppWillUse()
    {
        // A user who types the key in by hand must end up with the same codes as one who
        // scanned the QR code.
        var sut = CreateSut();

        var setup = await sut.GetAuthenticatorSetupAsync(AdminId);

        Assert.NotNull(await sut.EnableAsync(AdminId, Totp.Compute(setup.SharedKey)));
    }

    [Fact]
    public async Task RejectsAWrongCodeAndLeavesTwoFactorAuthenticationOff()
    {
        var sut = CreateSut();
        var setup = await sut.GetAuthenticatorSetupAsync(AdminId);
        var wrongCode = Totp.Compute(setup.SharedKey, DateTimeOffset.UtcNow.AddHours(-1));

        var codes = await sut.EnableAsync(AdminId, wrongCode);

        Assert.Null(codes);
        Assert.False((await AdminAsync()).TwoFactorEnabled);
    }

    [Fact]
    public async Task EnablesTwoFactorAuthenticationAndHandsOutTenRecoveryCodes()
    {
        var sut = CreateSut();
        var setup = await sut.GetAuthenticatorSetupAsync(AdminId);

        var codes = await sut.EnableAsync(AdminId, Totp.Compute(setup.SharedKey));

        Assert.NotNull(codes);
        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.True((await AdminAsync()).TwoFactorEnabled);
        Assert.Equal(10, await sut.CountRecoveryCodesAsync(AdminId));
    }

    [Fact]
    public async Task NewRecoveryCodesReplaceTheOldOnes()
    {
        var sut = CreateSut();
        var setup = await sut.GetAuthenticatorSetupAsync(AdminId);
        var oldCodes = await sut.EnableAsync(AdminId, Totp.Compute(setup.SharedKey));

        var newCodes = await sut.RegenerateRecoveryCodesAsync(AdminId);

        Assert.Empty(newCodes.Intersect(oldCodes!));
        Assert.False(await RedeemRecoveryCodeAsync(oldCodes![0]));
        Assert.True(await RedeemRecoveryCodeAsync(newCodes[0]));
    }

    [Fact]
    public async Task RestartingSetupTurnsTwoFactorAuthenticationOffAndIssuesANewKey()
    {
        // Moving to a new phone: the old phone's codes must stop working.
        var sut = CreateSut();
        var oldSetup = await sut.GetAuthenticatorSetupAsync(AdminId);
        await sut.EnableAsync(AdminId, Totp.Compute(oldSetup.SharedKey));

        await sut.RestartSetupAsync(AdminId);

        Assert.False((await AdminAsync()).TwoFactorEnabled);
        var newSetup = await sut.GetAuthenticatorSetupAsync(AdminId);
        Assert.NotEqual(oldSetup.SharedKey, newSetup.SharedKey);
    }

    public void Dispose() => _provider.Dispose();
}
