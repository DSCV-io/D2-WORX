// -----------------------------------------------------------------------
// <copyright file="FakeSignAuthorityConsumer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;

using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Result;

/// <summary>
/// §1.32 faithful test double standing in for the future SIGN handler's
/// authority-guard seam — the FIRST consumer of the capability-authority foundation.
/// It models exactly what the live sign-service guard will do: take the
/// peer-identity the accessor surfaced + the transport-set cross-process signal +
/// the requested key domain, resolve the caller's allowed-signing-domains policy,
/// and call the pure <see cref="WorkloadCapabilityAuthority.AuthorizeSigning"/>
/// rule — then act on the typed <see cref="D2Result"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Faithful, not hollow.</b> This double does NOT return a canned value. It
/// CAPTURES the exact inputs crossing the seam — the surfaced caller workload id,
/// the <see cref="IsCrossProcessSeen"/> signal, the target domain, and the resolved
/// allowed-set — and exposes the rule's real <see cref="D2Result"/> so a test
/// asserts the seam contract the live guard must honor. When the real sign-service
/// guard is authored, this double is replaced by it; the captured-input assertions
/// pin the contract the real guard must satisfy.
/// </para>
/// <para>
/// <b>Replace trigger</b> (deliverable validation ledger): replaced by the live
/// sign-service authority guard when the sign op is authored. The seal-capability
/// arms are NOT doubled here — the seal op does not exist yet, so a seal double would
/// assert a fiction; the seal arms are proven by the authority unit matrix
/// (<c>WorkloadCapabilityAuthorityTests</c>) and re-asserted against the real seal op
/// when it is authored.
/// </para>
/// </remarks>
/// <param name="policy">The signing-domain authority policy the guard resolves the allowed-set from.</param>
public sealed class FakeSignAuthorityConsumer(ISigningDomainAuthorityPolicy policy)
{
    /// <summary>Gets the caller workload id the guard saw on the last decision (the surfaced-identity input).</summary>
    public string? CallerWorkloadIdSeen { get; private set; }

    /// <summary>Gets a value indicating whether the cross-process signal the guard saw on the last decision was set.</summary>
    public bool IsCrossProcessSeen { get; private set; }

    /// <summary>Gets the target domain the guard saw on the last decision.</summary>
    public string? TargetSeen { get; private set; }

    /// <summary>Gets the allowed-signing-domains set the guard resolved on the last decision.</summary>
    public IReadOnlySet<string> AllowedSetResolved { get; private set; } =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Models the sign-service guard's authority decision: captures the seam inputs,
    /// resolves the policy, and returns the pure rule's typed result.
    /// </summary>
    /// <param name="callerWorkloadId">The peer identity the accessor surfaced (null when none).</param>
    /// <param name="isCrossProcess">The transport-set cross-process signal.</param>
    /// <param name="target">The requested signing key domain.</param>
    /// <returns>The authority rule's typed allow / deny result.</returns>
    public D2Result Authorize(string? callerWorkloadId, bool isCrossProcess, KeyDomain target)
    {
        CallerWorkloadIdSeen = callerWorkloadId;
        IsCrossProcessSeen = isCrossProcess;
        TargetSeen = target.Value;
        AllowedSetResolved = policy.AllowedSigningDomainsFor(callerWorkloadId);

        return WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId, isCrossProcess, target, AllowedSetResolved);
    }
}
