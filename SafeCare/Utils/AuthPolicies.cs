namespace SafeCare.Utils
{
    /// <summary>
    /// Named authorization policies. Always use these constants, never string literals.
    /// </summary>
    /// <remarks>
    /// Two-factor authentication is mandatory, so the default policy — the one a bare
    /// <c>[Authorize]</c> applies — requires a session that was established with a second
    /// factor. <c>[Authorize(Roles = ...)]</c> must not be used on pages: an attribute that
    /// names roles replaces the default policy instead of adding to it, which would let a
    /// password-only session through. Use <see cref="Admin"/> instead.
    /// </remarks>
    public static class AuthPolicies
    {
        /// <summary>
        /// Signed in with a second factor and holding the Admin role.
        /// </summary>
        public const string Admin = "Admin";

        /// <summary>
        /// Signed in with a password alone. Only the authenticator setup page uses it — it is
        /// where a user without two-factor authentication is sent to set it up.
        /// </summary>
        public const string TwoFactorEnrollment = "TwoFactorEnrollment";

        /// <summary>
        /// The claim Identity adds to a principal signed in through a second factor
        /// (<c>amr</c> = authentication methods references, <c>mfa</c> = multi-factor).
        /// </summary>
        public const string AuthenticationMethodClaim = "amr";

        public const string MultiFactorAuthenticationMethod = "mfa";
    }
}
