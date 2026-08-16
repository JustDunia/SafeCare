using SafeCare.E2ETests.Infrastructure;

namespace SafeCare.E2ETests;

public class PublicFormLoadTests(AppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task ServesThePublicFormAnonymously()
    {
        var page = await NewPageAsync();

        var response = await page.GotoAsync("/");

        Assert.NotNull(response);
        Assert.True(response!.Ok);

        // Home.razor sets <PageTitle>Zgłoszenie zdarzenia</PageTitle>; App.razor has no
        // fallback <title>, so this is the exact rendered title, not just a substring check.
        Assert.Equal("Zgłoszenie zdarzenia", await page.TitleAsync());
    }

    [Fact]
    public async Task RendersTheFormWithoutClientSideErrors()
    {
        var page = await NewPageAsync();
        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                consoleErrors.Add(message.Text);
            }
        };

        await page.GotoAsync("/");
        await page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        Assert.Empty(consoleErrors);
    }
}
