using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SafeCare.E2ETests.Infrastructure;

/// <summary>
/// Drives the public incident form. Selectors go through labels, roles and visible text so
/// that a MudBlazor upgrade changing generated class names does not break every test.
/// </summary>
/// <remarks>
/// The form is served by Blazor Interactive Server: the HTML arrives prerendered and only
/// becomes interactive once the SignalR circuit connects, which happens after
/// <c>GotoAsync</c> has already returned. Anything typed or clicked before that moment is
/// silently lost, so <see cref="OpenAsync"/> does not return until the circuit answers.
/// </remarks>
public sealed class PublicFormPage(IPage page)
{
    /// <summary>A department that exists in SeedData.sql; used to probe for interactivity.</summary>
    private const string ProbeDepartment = "Chirurgia";

    public IPage Page { get; } = page;

    private ILocator DepartmentInput => Page.GetByLabel("Miejsce zdarzenia");

    // The description box is the only textarea on the page and, unlike every other field,
    // has no label to reach it by.
    private ILocator DescriptionBox => Page.Locator("textarea");

    /// <summary>
    /// Opens the form and waits until the circuit is live, so that the caller's fill-in time
    /// is measured from an interactive page.
    /// </summary>
    public async Task OpenAsync()
    {
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveTitleAsync("Zgłoszenie zdarzenia");
        await WaitUntilInteractiveAsync();
    }

    /// <summary>
    /// Proves the circuit is connected by asking it for something only it can answer: the
    /// department list is loaded and rendered server-side when the autocomplete opens.
    /// </summary>
    public async Task WaitUntilInteractiveAsync()
    {
        await OpenDepartmentListAsync(ProbeDepartment);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(OptionNamed(ProbeDepartment)).ToBeHiddenAsync();
    }

    /// <summary>
    /// Fills only what the validator requires, leaving reporter and patient details empty.
    /// </summary>
    /// <param name="category">Heading of the collapsed panel that holds the incident type.</param>
    public async Task FillMinimalAsync(string description, string department, string category, string incidentType)
    {
        await SelectDepartmentAsync(department);
        await SelectIncidentTypeAsync(category, incidentType);

        // The inputs are native date/time pickers, so Chromium only accepts ISO values.
        var yesterday = DateTime.Today.AddDays(-1);
        await FillAsync(Page.GetByLabel("Data", new() { Exact = true }), yesterday.ToString("yyyy-MM-dd"));
        await FillAsync(Page.GetByLabel("Czas", new() { Exact = true }), "10:30");

        await FillAsync(DescriptionBox, description);
    }

    /// <summary>
    /// Fills the reporter block. The patient block further down the form carries identically
    /// labelled fields; the reporter's come first in the document.
    /// </summary>
    public async Task FillReporterAsync(string name, string surname)
    {
        await FillAsync(Page.GetByLabel("Imię", new() { Exact = true }).First, name);
        await FillAsync(Page.GetByLabel("Nazwisko", new() { Exact = true }).First, surname);
    }

    public async Task FillPhoneAsync(string phone) =>
        await FillAsync(Page.GetByLabel("Nr telefonu"), phone);

    public async Task FillEmailAsync(string email) =>
        await FillAsync(Page.GetByLabel("Adres e-mail"), email);

    /// <summary>
    /// Types into one of the invisible honeypot fields, the way a form-filling bot would.
    /// </summary>
    public async Task FillHoneypotAsync(string fieldId, string value) =>
        await Page.Locator($"#{fieldId}").FillAsync(value);

    public async Task ClearHoneypotAsync(string fieldId) =>
        await Page.Locator($"#{fieldId}").FillAsync("");

    public async Task SubmitAsync() =>
        await Page.GetByRole(AriaRole.Button, new() { Name = "Wyślij", Exact = true }).ClickAsync();

    /// <summary>Waits for the success snackbar shown once a report has been stored.</summary>
    public async Task ExpectSubmissionConfirmedAsync() =>
        await Expect(Page.GetByText("Zgłoszenie zostało wysłane, dziękujemy.")).ToBeVisibleAsync();

    /// <summary>Asserts that no confirmation snackbar and no failure snackbar appeared.</summary>
    public async Task ExpectNoSnackbarAsync()
    {
        await Expect(Page.GetByText("Zgłoszenie zostało wysłane")).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Wystąpił błąd")).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Zbyt wiele zgłoszeń")).ToHaveCountAsync(0);
    }

    public ILocator DescriptionField => DescriptionBox;

    private ILocator OptionNamed(string name) => Page.GetByText(name, new() { Exact = true });

    private async Task SelectDepartmentAsync(string department)
    {
        await OpenDepartmentListAsync(department);
        await OptionNamed(department).ClickAsync();
        await Expect(DepartmentInput).ToHaveValueAsync(department);
    }

    /// <summary>
    /// Opens the department dropdown, retrying while the circuit is still connecting. A click
    /// that lands before then is inert, so retrying cannot toggle the list shut again; the
    /// visibility check up front covers a click that was merely slow to show its effect.
    /// </summary>
    private async Task OpenDepartmentListAsync(string department)
    {
        var option = OptionNamed(department);
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            if (await option.IsVisibleAsync())
            {
                return;
            }

            await DepartmentInput.ClickAsync();

            try
            {
                await Expect(option).ToBeVisibleAsync(new() { Timeout = 2_000 });
                return;
            }
            catch (PlaywrightException) when (DateTime.UtcNow < deadline)
            {
                // circuit not connected yet - click again
            }
        }
    }

    private async Task SelectIncidentTypeAsync(string category, string incidentType)
    {
        // Incident types sit inside collapsed expansion panels; the checkbox is not
        // actionable until its panel header has been opened.
        await Page.GetByText(category, new() { Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = incidentType, Exact = true }).CheckAsync();
    }

    /// <summary>
    /// MudBlazor commits a text field on <c>change</c>, which the browser only raises when the
    /// field loses focus; filling without blurring can leave the last value unsent.
    /// </summary>
    private static async Task FillAsync(ILocator field, string value)
    {
        await field.FillAsync(value);
        await field.BlurAsync();
    }
}
