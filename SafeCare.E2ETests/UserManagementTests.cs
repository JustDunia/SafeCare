using Microsoft.Playwright;
using SafeCare.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace SafeCare.E2ETests;

/// <summary>
/// The administrator's user management page.
/// </summary>
public class UserManagementTests(AppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task KeepsTheBrowserFromAutofillingTheNewUserForm()
    {
        var page = await NewPageAsync();
        await LoginAsAdminAsync(page);
        await page.GotoAsync("/admin/users");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dodaj nowego użytkownika" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });

        // An administrator fills this form in on someone else's behalf. Without these hints a
        // browser reads "text field followed by password fields on a site I have a login for"
        // as a sign-in form and drops the administrator's own saved credentials into it.
        // new-password is the value browsers actually honour: it marks the form as creating a
        // credential rather than entering one, which is what stops the fill.
        await Expect(page.GetByLabel("Hasło", new() { Exact = true }))
            .ToHaveAttributeAsync("autocomplete", "new-password");
        await Expect(page.GetByLabel("Potwierdź hasło"))
            .ToHaveAttributeAsync("autocomplete", "new-password");
        await Expect(page.GetByLabel("Nazwa użytkownika"))
            .ToHaveAttributeAsync("autocomplete", "off");
    }
}
