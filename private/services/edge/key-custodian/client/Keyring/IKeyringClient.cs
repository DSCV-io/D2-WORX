// -----------------------------------------------------------------------
// <copyright file="IKeyringClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;

using DcsvIo.D2.Encryption;

/// <summary>
/// The package-internal raw-keyring fetch seam. Fetches a domain's Active + Retiring
/// payload keyring from KeyCustodian — either over the cross-process gRPC surface
/// (<see cref="GrpcKeyringClient"/>) or through the in-host leaf facade (the module
/// App's <c>InProcessKeyringClient</c>, which implements this seam via the
/// same-module internals grant).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately <see langword="internal"/>: a raw <see cref="PayloadCryptoKeyring"/>
/// (which holds live key material) must never be reachable by an external consumer.
/// The only capability that leaves this assembly is the keyed
/// <see cref="IPayloadCrypto"/> registered by the two DI sources; the wrapper
/// (<see cref="KeyringBackedPayloadCrypto"/>) is the sole consumer of this seam.
/// </para>
/// <para>
/// Every fetch — over either transport — is authority-gated KeyCustodian-side by
/// the fail-closed <c>AuthorizeKeyringFetch</c> rule at workload granularity; this
/// seam never re-derives that authority (it consumes the KeyCustodian decision).
/// </para>
/// </remarks>
internal interface IKeyringClient
{
    /// <summary>
    /// Fetches the Active + Retiring payload keyring for <paramref name="domain"/>.
    /// </summary>
    /// <param name="domain">The payload key domain (e.g. <c>"audit"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A ready-to-use <see cref="PayloadCryptoKeyring"/> on success; a typed failure
    /// (never a thrown exception) on validation / authority / transport / malformed-payload.
    /// </returns>
    ValueTask<D2Result<PayloadCryptoKeyring>> GetKeyringAsync(
        string domain, CancellationToken ct = default);
}
