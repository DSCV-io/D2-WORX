// -----------------------------------------------------------------------
// <copyright file="KcAppTestKit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Security.Cryptography.X509Certificates;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// Builds a handler context for the given handler type with an empty request
    /// (the fail-closed <see cref="RequestOrigin.Unestablished"/> default).
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>A handler context with a null logger + empty request context.</returns>
    public static HandlerContext<THandler> Context<THandler>() =>
        ContextWithOrigin<THandler>(RequestOrigin.Unestablished);

    /// <summary>
    /// Builds a handler context whose request carries the given established
    /// <see cref="RequestOrigin"/> — used by the per-origin authority deny matrices.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="origin">The established origin to stamp on the request context.</param>
    /// <param name="logger">Optional logger (used by tests that assert log output).</param>
    /// <returns>A handler context bound to the given origin.</returns>
    public static HandlerContext<THandler> ContextWithOrigin<THandler>(
        RequestOrigin origin, ILogger<THandler>? logger = null) =>
        new(new MutableRequestContext { Origin = origin }, logger ?? NullLogger<THandler>.Instance);

    /// <summary>
    /// Builds a handler context whose request carries the established
    /// <see cref="RequestOrigin.System"/> plane — what the in-host workers establish
    /// via <c>EstablishSystemContext</c>, and the only plane the lifecycle authority
    /// admits. Every lifecycle-handler happy-path test drives through this.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="logger">Optional logger (used by tests that assert log output).</param>
    /// <returns>A handler context on the System plane.</returns>
    public static HandlerContext<THandler> SystemContext<THandler>(
        ILogger<THandler>? logger = null) =>
        ContextWithOrigin(RequestOrigin.System, logger);

    /// <summary>
    /// Builds a handler context whose request carries the given established
    /// <see cref="RequestOrigin"/> + <c>ImmediateCaller</c> + granted scopes — the
    /// faithful stand-in for the interceptor-established peer context: the authority
    /// rules consume exactly the fields the peer-workload boundary establishes
    /// (<c>Origin</c> recomputed locally; <c>ImmediateCaller</c> from the validated
    /// mTLS peer SPIFFE SAN). Replace-trigger: the live Edge-host wiring.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="origin">The established origin to stamp on the request context.</param>
    /// <param name="immediateCaller">The established caller id (the validated peer view), or null.</param>
    /// <param name="scopes">The granted scopes (null → empty set — the scope gate denies).</param>
    /// <param name="logger">Optional logger (used by tests that assert log output).</param>
    /// <returns>A handler context bound to the given origin + caller + scopes.</returns>
    public static HandlerContext<THandler> ContextWithOriginAndCaller<THandler>(
        RequestOrigin origin,
        string? immediateCaller,
        IReadOnlySet<string>? scopes = null,
        ILogger<THandler>? logger = null) =>
        new(
            new MutableRequestContext
            {
                Origin = origin,
                ImmediateCaller = immediateCaller,
                Scopes = scopes ?? new HashSet<string>(StringComparer.Ordinal),
            },
            logger ?? NullLogger<THandler>.Instance);

    /// <summary>
    /// Builds a well-formed ECDSA P-256 PKCS#10 certificate-signing request (the
    /// shape a real workload submits) and returns its DER + the certified public
    /// key's SubjectPublicKeyInfo (for pairing assertions).
    /// </summary>
    /// <param name="subject">The CSR subject (the fixed client placeholder by default).</param>
    /// <returns>The CSR DER + the SPKI of the key it certifies.</returns>
    public static (byte[] Der, byte[] PublicKeySpki) BuildP256Csr(
        string subject = "CN=d2-workload")
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);

        return (request.CreateSigningRequest(), key.ExportSubjectPublicKeyInfo());
    }

    /// <summary>
    /// Builds a well-formed P-256 CSR that REQUESTS a forged SPIFFE SAN (plus a
    /// forged subject) — the no-forgery-invariant input: the issuance surface must
    /// ignore both and mint the SAN from the authenticated peer instead.
    /// </summary>
    /// <param name="forgedServiceId">The service id the CSR tries to claim.</param>
    /// <returns>The CSR DER + the SPKI of the key it certifies.</returns>
    public static (byte[] Der, byte[] PublicKeySpki) BuildP256CsrWithForgedSan(
        string forgedServiceId)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            $"CN={forgedServiceId}", key, HashAlgorithmName.SHA256);

        var sanBuilder = new System.Security.Cryptography.X509Certificates
            .SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri($"spiffe://d2.internal/workload/{forgedServiceId}"));
        request.CertificateExtensions.Add(sanBuilder.Build(critical: false));

        return (request.CreateSigningRequest(), key.ExportSubjectPublicKeyInfo());
    }

    /// <summary>
    /// Builds a structurally-valid P-256 CSR whose self-signature is BROKEN (one
    /// signature byte flipped) — the failed proof-of-possession input.
    /// </summary>
    /// <returns>The tampered CSR DER.</returns>
    public static byte[] BuildPopBrokenCsr()
    {
        var (der, _) = BuildP256Csr();

        // The self-signature is the trailing BIT STRING of the PKCS#10 structure —
        // flipping the LAST byte keeps the DER parseable but invalidates the
        // signature over certificationRequestInfo.
        der[^1] ^= 0x01;
        return der;
    }

    /// <summary>
    /// Builds a well-formed RSA-2048 CSR — the wrong-KEY-TYPE input the leaf key
    /// policy rejects.
    /// </summary>
    /// <returns>The CSR DER.</returns>
    public static byte[] BuildRsaCsr()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=d2-workload",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSigningRequest();
    }

    /// <summary>
    /// Builds a well-formed ECDSA P-384 CSR — the right-type-WRONG-CURVE input
    /// pinning that the leaf key policy checks the curve OID, not merely
    /// key-type-is-elliptic-curve.
    /// </summary>
    /// <returns>The CSR DER.</returns>
    public static byte[] BuildP384Csr()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var request = new CertificateRequest(
            "CN=d2-workload", key, HashAlgorithmName.SHA384);

        return request.CreateSigningRequest();
    }

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

    /// <summary>
    /// Seeds a CA hierarchy (self-signed root + intermediate signed by it) into the
    /// test context and returns the intermediate as a managed <c>X509CaCertificate</c>
    /// key in the requested lifecycle state in the <c>mtls-ca-intermediate</c> domain.
    /// The intermediate's private key is generated + root-wrapped via the supplied
    /// <paramref name="rootCrypto"/> exactly like the real handler, so the issuance
    /// path unwraps + reconstructs it correctly. The root certificate is returned
    /// (DER) so chain-building assertions can use it as the trust anchor.
    /// </summary>
    /// <param name="db">The test context to seed.</param>
    /// <param name="rootCrypto">The root crypto used to wrap the intermediate's private key.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="status">The lifecycle status to seed the intermediate in.</param>
    /// <returns>The seeded intermediate kid + the root certificate DER (trust anchor).</returns>
    public static async Task<(string IntermediateKid, byte[] RootCertificateDer)> SeedCaAsync(
        IKeyCustodianDbContext db,
        IPayloadCrypto rootCrypto,
        Instant createdAt,
        KeyStatus status = KeyStatus.Active)
    {
        var clock = new TestClock(createdAt);

        var rootResult = CaCertificateGeneration.GenerateRootCa(
            "D2 Test Root CA", Duration.FromDays(3650), clock);

        var root = rootResult.Data!;

        byte[] intermediatePkcs8;
        byte[] intermediateCertDer;
        byte[] rootCertDer = root.CertificateDer;

        using (var rootKey = ECDsa.Create())
        {
            rootKey.ImportPkcs8PrivateKey(root.PrivateKeyPkcs8, out _);

            using var rootCert = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                .LoadCertificate(root.CertificateDer);

            var intermediateResult = CaCertificateGeneration.GenerateIntermediateCa(
                "D2 Test Issuing CA", rootCert, rootKey, Duration.FromDays(365), clock);

            var intermediate = intermediateResult.Data!;
            intermediatePkcs8 = intermediate.PrivateKeyPkcs8;
            intermediateCertDer = intermediate.CertificateDer;
        }

        root.Zero();

        var wrapped = rootCrypto.Encrypt(intermediatePkcs8);
        CryptographicOperations.ZeroMemory(intermediatePkcs8);

        var kid = KidMinting.Mint();
        var record = new KeyRecord
        {
            Kid = kid,
            KeyDomain = KeyDomain.MTLS_CA_INTERMEDIATE,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = wrapped,
            PublicKeyMaterial = null,
            CaCertificate = intermediateCertDer,
            CreatedAt = createdAt,
            Status = status,
            ActivatedAt = status is KeyStatus.Active or KeyStatus.Retiring or KeyStatus.Retired
                ? createdAt
                : null,
            RetiringAt = status is KeyStatus.Retiring or KeyStatus.Retired ? createdAt : null,
            RetiredAt = status == KeyStatus.Retired ? createdAt : null,
            CompromisedAt = status == KeyStatus.Compromised ? createdAt : null,
            CompromiseReason = status == KeyStatus.Compromised ? "seed" : null,
        };

        db.Keys.Add(record);
        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return (kid, rootCertDer);
    }

    /// <summary>
    /// Seeds a COHERENT two-tier CA hierarchy: ONE self-signed root persisted as the
    /// active <c>mtls-ca-root</c> managed key AND the intermediate it signed persisted
    /// as the active <c>mtls-ca-intermediate</c> managed key — the shape the real
    /// seeder produces, where the served intermediate chains to the served root.
    /// (<see cref="SeedCaAsync"/> + <see cref="SeedCaRootAsync"/> each mint an
    /// INDEPENDENT hierarchy — fine for single-tier tests, wrong for chain proofs.)
    /// </summary>
    /// <param name="db">The test context to seed.</param>
    /// <param name="rootCrypto">The root crypto used to wrap both private keys.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <returns>The seeded kids + the root certificate DER (the trust anchor).</returns>
    public static async Task<(string RootKid, string IntermediateKid, byte[] RootCertificateDer)>
        SeedCaHierarchyAsync(
            IKeyCustodianDbContext db,
            IPayloadCrypto rootCrypto,
            Instant createdAt)
    {
        var clock = new TestClock(createdAt);

        var root = CaCertificateGeneration.GenerateRootCa(
            "D2 Test Root CA", Duration.FromDays(3650), clock).Data!;

        byte[] intermediateCertDer;
        byte[] wrappedIntermediate;
        var rootCertDer = root.CertificateDer;

        using (var rootKey = ECDsa.Create())
        {
            rootKey.ImportPkcs8PrivateKey(root.PrivateKeyPkcs8, out _);

            using var rootCert = System.Security.Cryptography.X509Certificates
                .X509CertificateLoader.LoadCertificate(root.CertificateDer);

            var intermediate = CaCertificateGeneration.GenerateIntermediateCa(
                "D2 Test Issuing CA", rootCert, rootKey, Duration.FromDays(365), clock).Data!;

            intermediateCertDer = intermediate.CertificateDer;
            wrappedIntermediate = rootCrypto.Encrypt(intermediate.PrivateKeyPkcs8);
            intermediate.Zero();
        }

        var wrappedRoot = rootCrypto.Encrypt(root.PrivateKeyPkcs8);
        root.Zero();

        var rootKid = KidMinting.Mint();
        db.Keys.Add(new KeyRecord
        {
            Kid = rootKid,
            KeyDomain = KeyDomain.MTLS_CA_ROOT,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = wrappedRoot,
            PublicKeyMaterial = null,
            CaCertificate = rootCertDer,
            CreatedAt = createdAt,
            Status = KeyStatus.Active,
            ActivatedAt = createdAt,
        });

        var intermediateKid = KidMinting.Mint();
        db.Keys.Add(new KeyRecord
        {
            Kid = intermediateKid,
            KeyDomain = KeyDomain.MTLS_CA_INTERMEDIATE,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = wrappedIntermediate,
            PublicKeyMaterial = null,
            CaCertificate = intermediateCertDer,
            CreatedAt = createdAt,
            Status = KeyStatus.Active,
            ActivatedAt = createdAt,
        });

        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return (rootKid, intermediateKid, rootCertDer);
    }

    /// <summary>
    /// Seeds a self-signed root CA as a managed <c>X509CaCertificate</c> key in the
    /// <c>mtls-ca-root</c> domain, in the requested lifecycle state. The root's
    /// private key is generated + root-wrapped via <paramref name="rootCrypto"/>
    /// (exactly like the real seeder) so the CA-successor path can unwrap +
    /// reconstruct it to sign an intermediate. Returns the seeded kid + the root
    /// certificate DER (trust anchor for chain assertions).
    /// </summary>
    /// <param name="db">The test context to seed.</param>
    /// <param name="rootCrypto">The root crypto used to wrap the root's private key.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="status">The lifecycle status to seed the root in.</param>
    /// <returns>The seeded root kid + the root certificate DER.</returns>
    public static async Task<(string RootKid, byte[] RootCertificateDer)> SeedCaRootAsync(
        IKeyCustodianDbContext db,
        IPayloadCrypto rootCrypto,
        Instant createdAt,
        KeyStatus status = KeyStatus.Active)
    {
        var clock = new TestClock(createdAt);

        var rootResult = CaCertificateGeneration.GenerateRootCa(
            "D2 Test Root CA", Duration.FromDays(3650), clock);

        var root = rootResult.Data!;

        var rootCertDer = root.CertificateDer;
        var wrapped = rootCrypto.Encrypt(root.PrivateKeyPkcs8);
        root.Zero();

        var kid = KidMinting.Mint();
        var record = new KeyRecord
        {
            Kid = kid,
            KeyDomain = KeyDomain.MTLS_CA_ROOT,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = wrapped,
            PublicKeyMaterial = null,
            CaCertificate = rootCertDer,
            CreatedAt = createdAt,
            Status = status,
            ActivatedAt = status is KeyStatus.Active or KeyStatus.Retiring or KeyStatus.Retired
                ? createdAt
                : null,
            RetiringAt = status is KeyStatus.Retiring or KeyStatus.Retired ? createdAt : null,
            RetiredAt = status == KeyStatus.Retired ? createdAt : null,
            CompromisedAt = status == KeyStatus.Compromised ? createdAt : null,
            CompromiseReason = status == KeyStatus.Compromised ? "seed" : null,
        };

        db.Keys.Add(record);
        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        return (kid, rootCertDer);
    }

    private sealed class NullDbExceptionClassifier : IDbExceptionClassifier
    {
        public DbFailureKind? Classify(Exception exception) => null;
    }
}
