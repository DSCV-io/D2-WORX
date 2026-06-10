// -----------------------------------------------------------------------
// <copyright file="ISmokeTester.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.Crypto;

using System;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Shared.Result;

/// <summary>
/// Verifies that unwrapped key material is cryptographically usable for its
/// declared <see cref="KeyType"/> — an RSA sign/verify round-trip, an AES-GCM
/// encrypt/decrypt round-trip, or an HMAC usability probe.
/// </summary>
/// <remarks>
/// Returns a <see cref="D2Result"/> rather than throwing: malformed or corrupted
/// material is an expected failure mode (a key that fails its smoke test must
/// never enter service), not an exceptional one. The handler maps a failure to
/// <c>KEYCUSTODIAN_SMOKE_TEST_FAILED</c>.
/// </remarks>
public interface ISmokeTester
{
    /// <summary>
    /// Runs the smoke test for the given material.
    /// </summary>
    /// <param name="type">The key type the material is claimed to be.</param>
    /// <param name="plaintextMaterial">
    /// The unwrapped private/symmetric key bytes (PKCS#8 for RSA, raw bytes for
    /// symmetric).
    /// </param>
    /// <param name="publicSpki">
    /// The SPKI public key for asymmetric keys; <see langword="null"/> for
    /// symmetric keys.
    /// </param>
    /// <returns>
    /// <c>Ok</c> when the material round-trips; a failure (no throw) when it does
    /// not, or when the material shape is inconsistent with <paramref name="type"/>.
    /// </returns>
    D2Result Verify(
        KeyType type,
        ReadOnlyMemory<byte> plaintextMaterial,
        ReadOnlyMemory<byte>? publicSpki);
}
