// -----------------------------------------------------------------------
// <copyright file="ISealingClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;

using DcsvIo.D2.Encryption;

/// <summary>
/// The package-internal raw seal-keyring fetch seam — the sealed sibling of
/// <c>IKeyringClient</c>. Fetches a service's PUBLIC sealing keyring (to seal a
/// payload TO it) or the caller's OWN PRIVATE sealing keyring (to open payloads
/// sealed to it) from KeyCustodian, over the cross-process gRPC surface
/// (<see cref="GrpcSealingClient"/>) or the in-host leaf facade (the module App's
/// in-process source, via the same-module internals grant).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately <see langword="internal"/>: a raw <see cref="RecipientPrivateKeyring"/>
/// (which holds live private key material) must never be reachable by an external
/// consumer. The only capabilities that leave this assembly are the keyed
/// <see cref="IPayloadSealer"/> / <see cref="IPayloadOpener"/> registered by the
/// sealing DI source; the hot-swap wrappers
/// (<see cref="KeyringBackedPayloadSealer"/> / <see cref="KeyringBackedPayloadOpener"/>)
/// are the sole consumers of this seam.
/// </para>
/// <para>
/// The own-private-key op is targetless — KeyCustodian selects the key purely from
/// the authenticated mTLS peer identity, so a caller can only ever obtain its own
/// private key (<c>AuthorizeSealDecrypt</c>, CrossProcessHop-only). This seam never
/// re-derives that authority.
/// </para>
/// </remarks>
internal interface ISealingClient
{
    /// <summary>
    /// Fetches (lazily provisioning on first use) the caller's OWN Active + Retiring
    /// PRIVATE sealing keyring.
    /// </summary>
    /// <param name="ownServiceId">
    /// The caller's own service id — anchors the local keyring's AEAD binding (the KC
    /// selects the key by the authenticated mTLS peer; a mismatched id fails loud at
    /// first open with a GCM tag mismatch).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A ready-to-use <see cref="RecipientPrivateKeyring"/> on success; a typed failure
    /// (never a thrown exception) on validation / authority / transport / malformed payload.
    /// </returns>
    ValueTask<D2Result<RecipientPrivateKeyring>> GetOwnPrivateKeyringAsync(
        string ownServiceId, CancellationToken ct = default);

    /// <summary>
    /// Fetches (lazily provisioning on first use) a recipient service's Active + Retiring
    /// PUBLIC sealing keyring.
    /// </summary>
    /// <param name="recipientServiceId">The recipient service to seal payloads to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A ready-to-use <see cref="RecipientPublicKeyring"/> on success; a typed failure
    /// (never a thrown exception) on validation / authority / transport / malformed payload.
    /// </returns>
    ValueTask<D2Result<RecipientPublicKeyring>> GetPublicKeyringAsync(
        string recipientServiceId, CancellationToken ct = default);
}
