// -----------------------------------------------------------------------
// <copyright file="IAuthContextExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AuthContext.Abstractions;

using System.Linq;
using DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Convenience helpers on <see cref="IAuthContext"/>. Hand-written (not
/// codegen-emitted) — the rules are stable enough that adding them to the
/// spec format would be net negative complexity.
/// </summary>
public static class IAuthContextExtensions
{
    /// <param name="auth">The auth context.</param>
    extension(IAuthContext auth)
    {
        /// <summary>
        /// True when <paramref name="auth"/>'s scope set contains <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The scope to check for.</param>
        /// <returns>True if granted.</returns>
        public bool HasScope(string scope) =>
            auth.Scopes.Contains(scope);

        /// <summary>
        /// True when <paramref name="auth"/>'s scope set contains ANY of
        /// <paramref name="scopes"/>.
        /// </summary>
        /// <param name="scopes">The scopes to check for.</param>
        /// <returns>True if at least one is granted.</returns>
        public bool HasAnyScope(params string[] scopes) =>
            scopes.Any(auth.Scopes.Contains);

        /// <summary>
        /// True when <paramref name="auth"/>'s scope set contains ALL of
        /// <paramref name="scopes"/>.
        /// </summary>
        /// <param name="scopes">The scopes to check for.</param>
        /// <returns>True if every scope is granted.</returns>
        public bool HasAllScopes(params string[] scopes) =>
            scopes.All(auth.Scopes.Contains);

        /// <summary>
        /// True when the caller's operating organization is staff
        /// (<see cref="OrgType.Admin"/> or <see cref="OrgType.Support"/>).
        /// Single org context — no agent/target distinction.
        /// </summary>
        /// <returns>True if staff.</returns>
        public bool IsStaff() =>
            auth.OrgType is OrgType.Admin or OrgType.Support;

        /// <summary>
        /// True when the caller's operating organization is <see cref="OrgType.Admin"/>.
        /// </summary>
        /// <returns>True if admin.</returns>
        public bool IsAdmin() =>
            auth.OrgType == OrgType.Admin;

        /// <summary>
        /// True when the caller is being force-impersonated (silent impersonation,
        /// admin-org-only fallback).
        /// </summary>
        /// <returns>True if force-impersonation is active.</returns>
        public bool IsForcedImpersonation() =>
            auth.ImpersonationKind == ImpersonationKind.Force;

        /// <summary>
        /// True when the caller is being consent-impersonated (OTP-authorized
        /// impersonation, available to staff + admin orgs).
        /// </summary>
        /// <returns>True if consent-impersonation is active.</returns>
        public bool IsConsentImpersonation() =>
            auth.ImpersonationKind == ImpersonationKind.Consent;

        /// <summary>
        /// True when the AGENT (impersonator)'s own organization is staff. Only
        /// meaningful when impersonating; returns false otherwise.
        /// </summary>
        /// <returns>True if the impersonator's org is staff.</returns>
        public bool IsImpersonatorStaff() =>
            auth.ImpersonatorOrgType is OrgType.Admin or OrgType.Support;

        /// <summary>
        /// True when the AGENT (impersonator)'s own organization is admin. Only
        /// meaningful when impersonating; returns false otherwise.
        /// </summary>
        /// <returns>True if the impersonator's org is admin.</returns>
        public bool IsImpersonatorAdmin() =>
            auth.ImpersonatorOrgType == OrgType.Admin;
    }
}
