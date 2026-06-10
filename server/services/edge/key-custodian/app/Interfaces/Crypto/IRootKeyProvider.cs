// -----------------------------------------------------------------------
// <copyright file="IRootKeyProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.Crypto;

using D2.Shared.Encryption;

/// <summary>
/// App-layer port that resolves the root keyring used to wrap every managed
/// key's material at rest.
/// </summary>
/// <remarks>
/// The concrete implementation (a file-backed provider reading
/// <c>secrets/keycustodian/root.key</c>) lives in the Infra layer; the App layer
/// depends only on this port so the Domain + App stay free of file / secret
/// access. The returned <see cref="PayloadCryptoKeyring"/> is the same instance
/// the keyed <see cref="IPayloadCrypto"/> wraps, so this port is mainly a seam
/// for tests + startup checks that need the keyring directly.
/// </remarks>
public interface IRootKeyProvider
{
    /// <summary>
    /// Gets the root keyring used to wrap and unwrap managed-key material.
    /// </summary>
    /// <returns>The root keyring.</returns>
    PayloadCryptoKeyring GetRootKeyring();
}
