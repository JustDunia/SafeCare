namespace SafeCare.Data.Entities;

/// <summary>
/// The role names used for authorization. Always reference these constants rather than
/// repeating the literals, so that <c>[Authorize(Roles = ...)]</c> attributes and role lookups
/// cannot drift apart.
/// </summary>
public static class AppRoles
{
    /// <summary>
    /// Full access, including staff account management at <c>/admin/users</c>.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Regular hospital staff: may read and triage reports, but not manage accounts.
    /// </summary>
    public const string User = "User";
}
