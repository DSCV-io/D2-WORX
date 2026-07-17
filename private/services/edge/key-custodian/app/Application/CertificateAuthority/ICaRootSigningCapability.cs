// -----------------------------------------------------------------------
// <copyright file="ICaRootSigningCapability.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.CertificateAuthority;

/// <summary>
/// The dedicated CA-root-signing capability — the ONLY holder of every stored
/// CA-root private-key plaintext materialization. The <c>mtls-ca-root</c> signing
/// key is the mTLS mesh's trust anchor (a cluster-root-grade secret per rules
/// §9.44), so BOTH paths that unwrap it route EXCLUSIVELY through this seam:
/// <list type="number">
///   <item>signing a successor <c>mtls-ca-intermediate</c> with the active root
///     (the trust-conferring path — generate-successor + compromise-replacement);
///     and</item>
///   <item>smoke-verifying a pending / successor root's material when the root
///     itself is being activated or rotated (the non-minting health check).</item>
/// </list>
/// </summary>
/// <remarks>
/// The seam is registered ONLY by its own dedicated extension
/// (<see cref="CaRootSigningCapabilityServiceCollectionExtensions.AddD2CaRootSigningCapability"/>),
/// NEVER by <c>AddD2KeyCustodianApp()</c> — a provider built from the general
/// registration alone cannot resolve it, and therefore cannot mint an intermediate
/// or unwrap a root key for ANY purpose (structural §9.44 deny, proven by the
/// DI-isolation test; not a runtime branch guard). Because all four lifecycle-mutation
/// handlers now take this capability, only the one composition root that holds
/// root-signing authority (the in-host System rotation worker) can construct them.
/// Every method is the single §9.44 chokepoint: it instruments the plaintext use
/// (<c>SR_CaRootKeyUsesTotal</c> + the CA-root-key log delegates) and zeroes the
/// unwrapped bytes on every path. The at-rest KEK that wraps stored key material is a
/// SEPARATE concern (the isolated secret here is the root SIGNING key, not the KEK).
/// </remarks>
public interface ICaRootSigningCapability
{
    /// <summary>
    /// Signs a successor <c>mtls-ca-intermediate</c> with the active
    /// <c>mtls-ca-root</c>: loads the active root, unwraps its private key, signs the
    /// intermediate, and zeroes the unwrapped material in a <c>finally</c>.
    /// </summary>
    /// <param name="successorKid">
    /// The kid the caller has already minted for the pending successor — bound into
    /// the §9.44 chokepoint log so the successor is attributable to this root use.
    /// </param>
    /// <param name="operation">
    /// The closed-set operation label for the chokepoint telemetry
    /// (<see cref="DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Observability.KeyCustodianMetrics.CaRootKeyUses.Operation"/>
    /// — <c>generate-successor</c> or <c>compromise-replacement</c>).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(<see cref="DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects.GeneratedCaMaterial"/>)</c>
    /// carrying the new intermediate certificate + its raw private key (which the
    /// caller root-wraps immediately); a typed
    /// <c>KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA</c> (503) failure when no active root
    /// exists; a typed <c>KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST</c> (500) failure
    /// when the pure certificate build fails.
    /// </returns>
    ValueTask<D2Result<GeneratedCaMaterial>> SignSuccessorIntermediateAsync(
        Kid successorKid, string operation, CancellationToken ct = default);

    /// <summary>
    /// Smoke-tests a pending / successor <c>mtls-ca-root</c>'s material when the root
    /// itself is activated or rotated: unwraps the pending root's private key,
    /// exercises it via <see cref="DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Rules.SmokeTesting.Verify"/>,
    /// and zeroes the unwrapped material in a <c>finally</c>. Behavior is
    /// byte-identical to the generic inline smoke the lifecycle handlers run for every
    /// other domain — a decrypt throw on an undecryptable wrapped blob propagates
    /// unchanged (no new swallow), and a verify failure yields the same failed
    /// <see cref="D2Result"/> the handlers map to <c>KEYCUSTODIAN_SMOKE_TEST_FAILED</c>.
    /// Routing changes WHERE the plaintext materializes, never WHETHER corruption is
    /// detected.
    /// </summary>
    /// <param name="pendingRoot">The pending / successor root key to smoke-test.</param>
    /// <param name="operation">
    /// The closed-set operation label for the chokepoint telemetry
    /// (<see cref="DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Observability.KeyCustodianMetrics.CaRootKeyUses.Operation"/>
    /// — <c>activate-smoke-test</c> or <c>rotate-smoke-test</c>).
    /// </param>
    /// <param name="ct">Cancellation token (the verify probe is CPU-bound; unused today).</param>
    /// <returns>
    /// <c>Ok</c> when the material round-trips; a failed
    /// <c>KEYCUSTODIAN_SMOKE_TEST_FAILED</c> result when it does not. Throws (does not
    /// swallow) when the wrapped root blob cannot be decrypted — the same fail-loud
    /// shape as the inline generic smoke path.
    /// </returns>
    ValueTask<D2Result> SmokeTestRootKeyMaterialAsync(
        PendingKey pendingRoot, string operation, CancellationToken ct = default);
}
