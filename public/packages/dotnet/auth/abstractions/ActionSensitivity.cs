// -----------------------------------------------------------------------
// <copyright file="ActionSensitivity.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Per-scope discriminator that captures "how dangerous is this action if it
/// succeeds?" — used to drive audit verbosity, OTP step-up triggers,
/// impersonation defaults, and alerting thresholds. Lives on every scope (the
/// claims-driven source of truth) including anonymous (<c>anon.*</c>) ones.
/// </summary>
/// <remarks>
/// <para>
/// This is <b>distinct from</b> the rate-limit tier (an Edge-side endpoint
/// concern documented in <c>docs/RATE-LIMITING.md</c>; that enum lives in
/// the Edge / rate-limit lib, not here in <c>auth-abstractions</c>). Action
/// sensitivity captures the security / blast-radius dimension; rate limit
/// captures the resource-cost / abuse-surface dimension. The two are
/// orthogonal: a sign-in endpoint is <see cref="Routine"/> in sensitivity
/// (just an authentication attempt — nothing sensitive happens unless it
/// succeeds and creates a session) but tightly capped on rate limit
/// (brute-force surface). An admin destructive endpoint is
/// <see cref="Critical"/> in sensitivity but standard on rate limit
/// (low call volume).
/// </para>
/// <para>
/// Sensitivity is informational metadata — it drives audit verbosity and
/// risk-score thresholds, NOT runtime impersonation gating. Impersonation
/// gating is per-scope (the <c>impersonationBlocked</c> field in
/// <c>contracts/auth-scopes/scopes.spec.json</c>) and is enforced at Edge
/// mint time by stripping blocked scopes from impersonation tokens. There
/// is no <c>[ImpersonationAllowed]</c> attribute; to opt a Sensitive scope
/// into being available during impersonation, set
/// <c>impersonationBlocked: false</c> on that scope in the spec.
/// </para>
/// </remarks>
public enum ActionSensitivity
{
    /// <summary>
    /// Normal action with no special handling. Standard audit; risk-score
    /// thresholds at platform defaults (step-up at 50, block at 80). Most read
    /// operations and routine writes (e.g., updating display name).
    /// </summary>
    Routine,

    /// <summary>
    /// Action affects account integrity, modifies auth-relevant state, or
    /// exposes PII beyond the user's own basic profile. Higher audit verbosity;
    /// tighter risk thresholds (step-up at 30, block at 60). Examples: change
    /// email, change password, view billing history, set up MFA.
    /// </summary>
    Sensitive,

    /// <summary>
    /// Irreversible, financial, or admin-org-only action. Maximum audit
    /// verbosity; strictest risk thresholds (step-up at 20, block at 40).
    /// Examples: charge a payment, delete an org, ban a user, initiate
    /// force-impersonation.
    /// </summary>
    Critical,
}
