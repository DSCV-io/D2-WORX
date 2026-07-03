// -----------------------------------------------------------------------
// <copyright file="ICaLeafSigningCapability.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application;

/// <summary>
/// The dedicated issuance leaf-signing capability — the ONLY holder of the
/// issuance-path intermediate-CA unwrap. Signing a caller-supplied CSR public key
/// with the intermediate CA key mints a trusted workload identity (the
/// caller-facing confused-deputy surface), so the path routes EXCLUSIVELY through
/// this seam: it is registered ONLY by its own dedicated extension
/// (<c>AddD2CaLeafSigningCapability()</c>), never by <c>AddD2KeyCustodianApp()</c>
/// — a provider built from the general registration alone cannot resolve it and
/// therefore cannot sign a workload leaf via the issuance path.
/// </summary>
/// <remarks>
/// The seam is deliberately narrow: load the active issuing intermediate, unwrap
/// its private key, sign <c>(csrPublicKey, peer-derived SAN, validity)</c> into a
/// leaf, and zero the unwrapped material. Authority, CSR verification, scope
/// enforcement, audit, and telemetry all stay in the issuance handler (the single
/// chokepoint both planes flow through) — possession of this seam grants leaf
/// SIGNING only, never a bypass of the gates in front of it. The CA-root signing
/// paths inside the lifecycle machinery (successor-intermediate minting) are a
/// separate, System-plane-only concern and do not route through this seam.
/// </remarks>
public interface ICaLeafSigningCapability
{
    /// <summary>
    /// Signs a workload leaf certificate over the supplied CSR public key with the
    /// active issuing intermediate.
    /// </summary>
    /// <param name="leafPublicKey">
    /// The CSR's verified ECDSA P-256 public key (extracted by the CSR verification
    /// rule — never an unverified key).
    /// </param>
    /// <param name="workload">
    /// The workload identity for the leaf's SAN — the authenticated mTLS peer
    /// identity, never a caller-supplied subject.
    /// </param>
    /// <param name="validity">How long the leaf is valid for (strictly positive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Ok(<see cref="CaSignedLeaf"/>)</c> carrying the issued certificate
    /// material + the issuing-intermediate kid on success; a typed
    /// <c>KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA</c> (503) failure when no active
    /// intermediate exists; a typed
    /// <c>KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST</c> (500) failure when a crypto
    /// build/sign operation fails.
    /// </returns>
    ValueTask<D2Result<CaSignedLeaf>> SignLeafAsync(
        PublicKey leafPublicKey,
        WorkloadIdentity workload,
        Duration validity,
        CancellationToken ct = default);
}
