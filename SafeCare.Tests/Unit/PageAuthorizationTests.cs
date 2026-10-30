using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using SafeCare.Utils;

namespace SafeCare.Tests.Unit;

/// <summary>
/// Two-factor authentication is enforced by the default authorization policy, which a page
/// only gets if its <c>[Authorize]</c> names neither roles nor another policy.
/// </summary>
public class PageAuthorizationTests
{
    private static IEnumerable<(Type Page, AuthorizeAttribute Attribute)> GuardedPages() =>
        typeof(Program).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<RouteAttribute>() is not null)
            .SelectMany(t => t.GetCustomAttributes<AuthorizeAttribute>().Select(a => (t, a)));

    [Fact]
    public void NoPageAuthorizesByRole()
    {
        // [Authorize(Roles = ...)] replaces the default policy rather than adding to it, so a
        // page guarded that way would let in a session that never passed the second factor.
        var byRole = GuardedPages()
            .Where(p => !string.IsNullOrEmpty(p.Attribute.Roles))
            .Select(p => p.Page.Name);

        Assert.Empty(byRole);
    }

    [Fact]
    public void OnlyTheSetupPageAcceptsASessionWithoutASecondFactor()
    {
        var policies = GuardedPages()
            .Where(p => p.Attribute.Policy is not null and not AuthPolicies.Admin)
            .Select(p => (p.Page.Name, p.Attribute.Policy));

        Assert.Equal([("TwoFactorSetup", AuthPolicies.TwoFactorEnrollment)], policies);
    }
}
