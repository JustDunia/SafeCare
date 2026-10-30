namespace SafeCare.Data.Entities;

/// <summary>
/// The role names used for authorization. Always reference these constants rather than
/// repeating the literals, so that authorization policies and role lookups cannot drift
/// apart. Pages are guarded by policy, not by role — see <c>Utils.AuthPolicies</c>.
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
