namespace SafeCare.Email
{
    public record EmailMessage(IReadOnlyList<string> BccRecipients, string Subject, string HtmlBody);
}
