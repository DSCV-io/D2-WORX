// -----------------------------------------------------------------------
// <copyright file="SmokeTesting.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

using System.Security.Cryptography;

/// <summary>
/// Pure rule that exercises freshly-generated (or freshly-unwrapped) key material
/// to prove it is cryptographically usable before the key is activated.
/// </summary>
/// <remarks>
/// Every probe is wrapped so a malformed-material crypto exception becomes a
/// <c>KEYCUSTODIAN_SMOKE_TEST_FAILED</c> failure rather than a throw — a key that
/// fails its smoke test must be rejected, not crash the handler. The probes:
/// <list type="bullet">
///   <item><c>RsaSigning</c>: import the PKCS#8 private key + the SPKI public
///     key, sign a nonce with the private key, verify with the public key.</item>
///   <item><c>AesPayload</c>: AES-256-GCM encrypt then decrypt a nonce and assert
///     the round-trip recovers the plaintext.</item>
///   <item><c>Secret</c>: derive an HMAC-SHA256 over a nonce and assert it is
///     deterministic + the key length is usable.</item>
/// </list>
/// </remarks>
public static class SmokeTesting
{
    private const int _PROBE_BYTES = 32;
    private const int _GCM_NONCE_BYTES = 12;
    private const int _GCM_TAG_BYTES = 16;

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
    public static D2Result Verify(
        KeyType type,
        ReadOnlyMemory<byte> plaintextMaterial,
        ReadOnlyMemory<byte>? publicSpki)
    {
        try
        {
            return type switch
            {
                KeyType.RsaSigning => VerifyRsa(plaintextMaterial.Span, publicSpki),
                KeyType.AesPayload => VerifyAes(plaintextMaterial.Span),
                KeyType.Secret => VerifySecret(plaintextMaterial.Span),
                _ => KeyCustodianFailures.SmokeTestFailed(),
            };
        }
        catch (CryptographicException)
        {
            // Malformed / corrupted material — an expected smoke-test failure.
            return KeyCustodianFailures.SmokeTestFailed();
        }
        catch (ArgumentException)
        {
            // Wrong-sized material (e.g. an AES key that is not 16/24/32 bytes).
            return KeyCustodianFailures.SmokeTestFailed();
        }
        catch (Exception)
        {
            // Any other BCL exception (ObjectDisposedException, etc.) must also
            // yield SmokeTestFailed — the smoke tester must never throw.
            return KeyCustodianFailures.SmokeTestFailed();
        }
    }

    private static D2Result VerifyRsa(ReadOnlySpan<byte> pkcs8Private, ReadOnlyMemory<byte>? publicSpki)
    {
        if (publicSpki is not { } spki)
            return KeyCustodianFailures.SmokeTestFailed();

        Span<byte> nonce = stackalloc byte[_PROBE_BYTES];
        RandomNumberGenerator.Fill(nonce);

        using var signer = RSA.Create();
        signer.ImportPkcs8PrivateKey(pkcs8Private, out _);
        var signature = signer.SignData(
            nonce.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var verifier = RSA.Create();
        verifier.ImportSubjectPublicKeyInfo(spki.Span, out _);
        var verified = verifier.VerifyData(
            nonce.ToArray(), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return verified ? D2Result.Ok() : KeyCustodianFailures.SmokeTestFailed();
    }

    private static D2Result VerifyAes(ReadOnlySpan<byte> key)
    {
        Span<byte> plaintext = stackalloc byte[_PROBE_BYTES];
        RandomNumberGenerator.Fill(plaintext);

        Span<byte> nonce = stackalloc byte[_GCM_NONCE_BYTES];
        RandomNumberGenerator.Fill(nonce);

        Span<byte> ciphertext = stackalloc byte[_PROBE_BYTES];
        Span<byte> tag = stackalloc byte[_GCM_TAG_BYTES];

        using var gcm = new AesGcm(key, _GCM_TAG_BYTES);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag);

        Span<byte> recovered = stackalloc byte[_PROBE_BYTES];
        gcm.Decrypt(nonce, ciphertext, tag, recovered);

        return CryptographicOperations.FixedTimeEquals(plaintext, recovered)
            ? D2Result.Ok()
            : KeyCustodianFailures.SmokeTestFailed();
    }

    private static D2Result VerifySecret(ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
            return KeyCustodianFailures.SmokeTestFailed();

        Span<byte> message = stackalloc byte[_PROBE_BYTES];
        RandomNumberGenerator.Fill(message);

        Span<byte> first = stackalloc byte[HMACSHA256.HashSizeInBytes];
        Span<byte> second = stackalloc byte[HMACSHA256.HashSizeInBytes];

        HMACSHA256.HashData(key, message, first);
        HMACSHA256.HashData(key, message, second);

        return CryptographicOperations.FixedTimeEquals(first, second)
            ? D2Result.Ok()
            : KeyCustodianFailures.SmokeTestFailed();
    }
}
