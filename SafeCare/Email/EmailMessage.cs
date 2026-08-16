namespace SafeCare.Email
{
    /// <summary>
    /// A ready-to-send notification e-mail.
    /// </summary>
    /// <param name="BccRecipients">
    /// Every recipient is addressed via BCC so that staff members are not exposed to one
    /// another; the visible To: header is the sender's own address.
    /// </param>
    /// <param name="Subject">Subject line, in Polish.</param>
    /// <param name="HtmlBody">Fully rendered HTML body with all values already encoded.</param>
    public record EmailMessage(IReadOnlyList<string> BccRecipients, string Subject, string HtmlBody);
}
