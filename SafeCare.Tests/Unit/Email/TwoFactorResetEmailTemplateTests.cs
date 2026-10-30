using SafeCare.Data.Entities;
using SafeCare.Email;

namespace SafeCare.Tests.Unit.Email;

public class TwoFactorResetEmailTemplateTests
{
    private static User UserNamed(string userName) => new()
    {
        UserName = userName,
        Email = "nowak@szpital.pl",
        FirstName = "Jan",
        LastName = "Nowak"
    };

    [Fact]
    public void GoesOnlyToTheAffectedUser()
    {
        var message = TwoFactorResetEmailTemplate.Build(UserNamed("nowak"));

        Assert.Equal(["nowak@szpital.pl"], message.BccRecipients);
    }

    [Fact]
    public void EncodesTheUserName()
    {
        var message = TwoFactorResetEmailTemplate.Build(UserNamed("<b>nowak</b>"));

        Assert.Contains("&lt;b&gt;nowak&lt;/b&gt;", message.HtmlBody);
        Assert.DoesNotContain("<b>nowak</b>", message.HtmlBody);
    }
}
