using SafeCare.Email;
using System.Net;

namespace SafeCare.Tests.Unit.Email;

public class IncidentEmailTemplateTests
{
    [Fact]
    public void AddressesEveryRecipientThroughBcc()
    {
        // Staff addresses must not be exposed to one another.
        var recipients = new[] { "a@szpital.pl", "b@szpital.pl" };

        var message = IncidentEmailTemplate.Build(ReportFactory.Create(), recipients);

        Assert.Equal(recipients, message.BccRecipients);
    }

    [Fact]
    public void PutsTheReportNumberInTheSubject()
    {
        var report = ReportFactory.Create();
        report.Id = 42;

        var message = IncidentEmailTemplate.Build(report, ["a@szpital.pl"]);

        Assert.Contains("#42", message.Subject);
    }

    [Fact]
    public void IncludesTheDepartmentAndDescriptionInTheBody()
    {
        var report = ReportFactory.Create(
            department: ReportFactory.Department("Oddział wewnętrzny", "OW"),
            description: "Pacjent zgłosił ból po podaniu leku.");

        var message = IncidentEmailTemplate.Build(report, ["a@szpital.pl"]);

        // WebUtility.HtmlEncode numeric-escapes a handful of Latin-1 letters that had legacy
        // HTML4 named entities (e.g. "ó" -> "&#243;"), while leaving Polish Extended-A letters
        // like "ł" or "ż" untouched. Decoding first checks the real content instead of tying
        // the assertion to that encoder's selective, framework-level quirk.
        var decodedBody = WebUtility.HtmlDecode(message.HtmlBody);
        Assert.Contains("Oddział wewnętrzny", decodedBody);
        Assert.Contains("Pacjent zgłosił ból po podaniu leku.", decodedBody);
    }

    [Fact]
    public void EncodesMarkupSuppliedByTheReporter()
    {
        // The description is free text typed by an anonymous member of the public.
        var report = ReportFactory.Create(description: "<script>alert('x')</script>");

        var message = IncidentEmailTemplate.Build(report, ["a@szpital.pl"]);

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }

    [Fact]
    public void RendersADateRangeWhenTheEventSpansAPeriod()
    {
        var report = ReportFactory.Create(
            dateFrom: DateTime.Today.AddDays(-5),
            dateTo: DateTime.Today.AddDays(-2));

        var message = IncidentEmailTemplate.Build(report, ["a@szpital.pl"]);

        Assert.Contains(DateTime.Today.AddDays(-5).ToString("dd.MM.yyyy"), message.HtmlBody);
        Assert.Contains(DateTime.Today.AddDays(-2).ToString("dd.MM.yyyy"), message.HtmlBody);
    }
}
