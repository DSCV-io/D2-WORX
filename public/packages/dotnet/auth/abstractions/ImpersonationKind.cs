// -----------------------------------------------------------------------
// <copyright file="ImpersonationKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Sub-discriminator for <see cref="ActorKind.Impersonation"/> entries — which of the
/// two impersonation flavors this actor used. Sourced from the <c>act.d2_kind</c>
/// JWT claim.
/// </summary>
public enum ImpersonationKind
{
    /// <summary>
    /// Consent-based impersonation — the impersonated user authorized the session via OTP.
    /// Available to Staff + Admin orgs (scope <c>auth.user.impersonate</c>).
    /// Standard customer-support flow.
    /// </summary>
    Consent,

    /// <summary>
    /// Force impersonation — no user consent, silent at the time. Audit log visible
    /// to the user later in their account activity.
    /// Available to Admin orgs only (scope <c>auth.user.impersonate.force</c>, granted
    /// only to mgmt + dev). For dev work and supporting users who can't complete the
    /// OTP flow.
    /// </summary>
    Force,
}
