// -----------------------------------------------------------------------
// <copyright file="LifecycleAuthorityTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// The single deny-path telemetry seam shared by every lifecycle command handler that
/// fronts <see cref="Domain.Rules.KeyLifecycleAuthority.AuthorizeLifecycleMutation"/>:
/// maps the rule's typed failure to a bounded reason tag from the
/// <see cref="KeyCustodianMetrics.AuthorityRejections"/> named-constant closed set,
/// fires the <c>AuthorityRejected</c> forensic log + the
/// <see cref="KeyCustodianMetrics.SR_AuthorityRejectionsTotal"/> counter
/// (<c>capability = lifecycle</c>), and bubbles the failure. One seam so the seven
/// handlers cannot drift from each other on the deny shape.
/// </summary>
internal static class LifecycleAuthorityTelemetry
{
    /// <summary>
    /// Emits the lifecycle-authority deny telemetry and bubbles the rule's typed
    /// failure as the handler's result.
    /// </summary>
    /// <typeparam name="TOutput">The denying handler's output type.</typeparam>
    /// <param name="logger">The handler's logger (from its <c>HandlerContext</c>).</param>
    /// <param name="authorityResult">The failed authority-rule result to bubble.</param>
    /// <param name="immediateCaller">
    /// The established caller workload id, or <see langword="null"/> when none (logged
    /// as the <see cref="KeyCustodianMetrics.AuthorityRejections.Workload.NONE"/> sentinel).
    /// </param>
    /// <param name="operation">
    /// The denied lifecycle operation label (bounded closed set — one call site per
    /// handler, e.g. <c>"generate-key"</c>), logged as the rejection target.
    /// </param>
    /// <returns>The bubbled typed failure.</returns>
    public static D2Result<TOutput?> Deny<TOutput>(
        ILogger logger,
        D2Result authorityResult,
        string? immediateCaller,
        string operation)
        where TOutput : class
    {
        // Switch on the EMITTED error-code constants, never raw string literals. The
        // lifecycle rule denies on exactly two arms: an unestablished origin, or an
        // established origin that is not the in-host System plane (Forbidden).
        var reason = authorityResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,

            // Forbidden — an established plane that is not the System worker plane.
            _ => KeyCustodianMetrics.AuthorityRejections.Reason.NOT_SYSTEM,
        };

        KeyCustodianLog.AuthorityRejected(
            logger,
            immediateCaller ?? KeyCustodianMetrics.AuthorityRejections.Workload.NONE,
            KeyCustodianMetrics.AuthorityRejections.Capability.LIFECYCLE,
            operation);

        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.LIFECYCLE),
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON, reason));

        return D2Result<TOutput?>.BubbleFail(authorityResult);
    }
}
