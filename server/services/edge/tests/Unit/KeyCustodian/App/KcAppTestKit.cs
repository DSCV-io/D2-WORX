// -----------------------------------------------------------------------
// <copyright file="KcAppTestKit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Persistence;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Shared.Context.Abstractions;
using D2.Shared.Encryption;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

/// <summary>
/// Shared test helpers for the KeyCustodian App-layer unit tests: a real
/// <see cref="PayloadCrypto"/> over a throwaway keyring, the pure key-generation
/// rule, a null DB-exception classifier, and a handler-context builder. The
/// recording announcer fake lives in
/// <see cref="D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures.RecordingAnnouncer"/>.
/// </summary>
internal static class KcAppTestKit
{
    /// <summary>An arbitrary, deterministic baseline instant for tests.</summary>
    public static readonly Instant SR_BaseInstant = Instant.FromUtc(2026, 1, 1, 0, 0);

    /// <summary>
    /// Builds a real <see cref="IPayloadCrypto"/> over a throwaway random 32-byte
    /// keyring — exercises the genuine wrap/unwrap path (no crypto mock).
    /// </summary>
    /// <returns>A real payload crypto bound to a fresh test keyring.</returns>
    public static IPayloadCrypto BuildTestRootCrypto()
    {
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(
            PayloadCryptoKeyring.KEY_SIZE_BYTES);
        var keyring = new PayloadCryptoKeyring(
            activeKid: "test-root",
            keys: new Dictionary<string, byte[]> { ["test-root"] = key },
            aadContext: "keycustodian-test"u8.ToArray());
        return new PayloadCrypto(keyring);
    }

    /// <summary>
    /// Builds default options with a short, valid policy for every domain (cadence
    /// 4h, grace 2h, smoke-soak 1h) and an RSA size small enough for fast tests.
    /// </summary>
    /// <param name="rsaKeySizeBits">RSA modulus size (default 2048; pass 2048 minimum).</param>
    /// <returns>The options.</returns>
    public static KeyCustodianOptions BuildOptions(int rsaKeySizeBits = 2048)
    {
        var options = new KeyCustodianOptions
        {
            RsaKeySizeBits = rsaKeySizeBits,
            SecretLengthBytes = 64,
            Default = new RotationPolicyOptions
            {
                Cadence = TimeSpan.FromHours(4),
                Grace = TimeSpan.FromHours(2),
                SmokeSoak = TimeSpan.FromHours(1),
            },
        };
        return options;
    }

    /// <summary>
    /// Builds an <see cref="IOptions{T}"/> accessor over the default test options.
    /// </summary>
    /// <returns>The options accessor.</returns>
    public static IOptions<KeyCustodianOptions> BuildOptionsAccessor() =>
        Options.Create(BuildOptions());

    /// <summary>Builds the options-backed rotation-policy provider.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The provider.</returns>
    public static IRotationPolicyProvider BuildPolicyProvider(KeyCustodianOptions options) =>
        new OptionsRotationPolicyProvider(Options.Create(options));

    /// <summary>
    /// Builds a handler context for the given handler type with an empty request.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>A handler context with a null logger + empty request context.</returns>
    public static HandlerContext<THandler> Context<THandler>() =>
        new(new MutableRequestContext(), NullLogger<THandler>.Instance);

    /// <summary>
    /// Builds a null DB-exception classifier (no provider mapping in unit tests).
    /// </summary>
    /// <returns>The classifier.</returns>
    public static IDbExceptionClassifier NullClassifier() => new NullDbExceptionClassifier();

    /// <summary>
    /// Seeds a persisted key in a given lifecycle state with REAL wrapped material
    /// (generated + root-wrapped via <paramref name="rootCrypto"/> so it unwraps +
    /// smoke-tests correctly in handler tests). Sets the per-state timestamp
    /// columns relative to <paramref name="createdAt"/>.
    /// </summary>
    /// <param name="db">The test context to seed.</param>
    /// <param name="rootCrypto">The root crypto used to wrap the generated material.</param>
    /// <param name="options">Options driving the generator.</param>
    /// <param name="domain">The key domain wire value.</param>
    /// <param name="keyType">The key type.</param>
    /// <param name="status">The lifecycle status to seed.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="activatedAt">The activation instant (for Active/Retiring/Retired).</param>
    /// <param name="retiringAt">The retiring instant (for Retiring/Retired).</param>
    /// <returns>The seeded kid.</returns>
    public static async Task<string> SeedKeyAsync(
        IKeyCustodianDbContext db,
        IPayloadCrypto rootCrypto,
        KeyCustodianOptions options,
        string domain,
        KeyType keyType,
        KeyStatus status,
        Instant createdAt,
        Instant? activatedAt = null,
        Instant? retiringAt = null)
    {
        var material = KeyGeneration.Generate(
            keyType, options.RsaKeySizeBits, options.SecretLengthBytes).Data!;
        byte[] wrapped;
        try
        {
            wrapped = rootCrypto.Encrypt(material.Plaintext);
        }
        finally
        {
            material.Zero();
        }

        var kid = KidMinting.Mint();
        var record = new KeyRecord
        {
            Kid = kid,
            KeyDomain = domain,
            KeyType = keyType,
            KeyMaterialEncrypted = wrapped,
            PublicKeyMaterial = material.PublicSpki,
            CreatedAt = createdAt,
            Status = status,
            ActivatedAt = status is KeyStatus.Active or KeyStatus.Retiring or KeyStatus.Retired
                ? activatedAt ?? createdAt
                : null,
            RetiringAt = status is KeyStatus.Retiring or KeyStatus.Retired
                ? retiringAt ?? createdAt
                : null,
            RetiredAt = status == KeyStatus.Retired ? createdAt : null,
            CompromisedAt = status == KeyStatus.Compromised ? createdAt : null,
            CompromiseReason = status == KeyStatus.Compromised ? "seed" : null,
        };

        db.Keys.Add(record);
        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return kid;
    }

    /// <summary>
    /// Seeds a persisted key whose stored material is real-wrapped but
    /// cryptographically CORRUPT, so the handler's unwrap-then-smoke-test path
    /// produces a genuine <c>KEYCUSTODIAN_SMOKE_TEST_FAILED</c> (no injected fake).
    /// The supplied <paramref name="corruptPlaintext"/> is wrapped via
    /// <paramref name="rootCrypto"/> exactly like real material, so it unwraps
    /// cleanly and then fails the per-type smoke probe.
    /// </summary>
    /// <param name="db">The test context to seed.</param>
    /// <param name="rootCrypto">The root crypto used to wrap the corrupt material.</param>
    /// <param name="domain">The key domain wire value.</param>
    /// <param name="keyType">The key type.</param>
    /// <param name="status">The lifecycle status to seed.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="corruptPlaintext">
    /// The corrupt plaintext bytes to wrap as the stored material.
    /// </param>
    /// <param name="publicSpki">Optional SPKI public material (RSA only).</param>
    /// <param name="activatedAt">The activation instant (for Active/Retiring/Retired).</param>
    /// <returns>The seeded kid.</returns>
    public static async Task<string> SeedKeyWithCorruptMaterialAsync(
        IKeyCustodianDbContext db,
        IPayloadCrypto rootCrypto,
        string domain,
        KeyType keyType,
        KeyStatus status,
        Instant createdAt,
        byte[] corruptPlaintext,
        byte[]? publicSpki = null,
        Instant? activatedAt = null)
    {
        var wrapped = rootCrypto.Encrypt(corruptPlaintext);
        var kid = KidMinting.Mint();
        var record = new KeyRecord
        {
            Kid = kid,
            KeyDomain = domain,
            KeyType = keyType,
            KeyMaterialEncrypted = wrapped,
            PublicKeyMaterial = publicSpki,
            CreatedAt = createdAt,
            Status = status,
            ActivatedAt = status is KeyStatus.Active or KeyStatus.Retiring or KeyStatus.Retired
                ? activatedAt ?? createdAt
                : null,
        };

        db.Keys.Add(record);
        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return kid;
    }

    private sealed class NullDbExceptionClassifier : IDbExceptionClassifier
    {
        public DbFailureKind? Classify(Exception exception) => null;
    }
}
