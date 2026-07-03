// -----------------------------------------------------------------------
// <copyright file="SeedCertificateAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Security.Cryptography.X509Certificates;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;

/// <summary>
/// Tests for <see cref="SeedCertificateAuthorityHandler"/>: it seeds both CA tiers
/// as active managed keys through the genuine smoke → activate path, is idempotent
/// (a re-run on a seeded store is a no-op), and fails loud when the provider fails.
/// </summary>
public sealed class SeedCertificateAuthorityTests
{
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();

    [Fact]
    public async Task Seed_EmptyStore_SeedsActiveRootAndIntermediate_WithAuditEntries()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var provider = BuildValidChainProvider(clock);

        var result = await Build(db, clock, provider).HandleAsync(new SeedCertificateAuthorityInput());

        result.Success.Should().BeTrue();
        result.Data!.Seeded.Should().BeTrue();

        var root = db.Keys.Single(k => k.KeyDomain == KeyDomain.MTLS_CA_ROOT);
        var intermediate = db.Keys.Single(k => k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE);
        root.Status.Should().Be(KeyStatus.Active);
        intermediate.Status.Should().Be(KeyStatus.Active);
        root.KeyType.Should().Be(KeyType.X509CaCertificate);
        intermediate.KeyType.Should().Be(KeyType.X509CaCertificate);
        root.CaCertificate.Should().NotBeNullOrEmpty();
        intermediate.CaCertificate.Should().NotBeNullOrEmpty();

        // Each tier gets a Generated + Activated audit entry (4 total).
        db.Audit.Count(a => a.Action == KeyAuditAction.Generated).Should().Be(2);
        db.Audit.Count(a => a.Action == KeyAuditAction.Activated).Should().Be(2);

        // No fabricated active state — the seeded intermediate chains to the seeded root.
        CaTestAssertions.AssertChainsToRoot(intermediate.CaCertificate!, root.CaCertificate!);
    }

    [Fact]
    public async Task Seed_AlreadySeeded_IsNoOp_NoDuplicateRows()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var provider = BuildValidChainProvider(clock);

        var first = await Build(db, clock, provider).HandleAsync(new SeedCertificateAuthorityInput());
        first.Data!.Seeded.Should().BeTrue();

        var second = await Build(db, clock, provider).HandleAsync(new SeedCertificateAuthorityInput());

        second.Success.Should().BeTrue();
        second.Data!.Seeded.Should().BeFalse(because: "the active-CA gate makes a re-run a no-op");
        db.Keys.Count(k => k.KeyDomain == KeyDomain.MTLS_CA_ROOT).Should().Be(1);
        db.Keys.Count(k => k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE).Should().Be(1);
    }

    [Fact]
    public async Task Seed_ProviderFails_Bubbles_NothingPersisted()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var provider = new FakeCaProvider(
            D2Result<LoadedCaMaterial>.ServiceUnavailable());

        var result = await Build(db, clock, provider).HandleAsync(new SeedCertificateAuthorityInput());

        result.Success.Should().BeFalse(because: "a fail-loud provider result must bubble");
        db.Keys.Should().BeEmpty(because: "nothing is persisted on a provider failure");
        db.Audit.Should().BeEmpty();
    }

    [Fact]
    public async Task Seed_ThenIssueLeaf_LeafChainsToSeededRoot()
    {
        // Seed via the provider, then issue a leaf via the pure CSR-flow issuance
        // rule using the seeded intermediate (unwrapped exactly like the issuance
        // capability does) — the leaf must chain to the seeded root. The chain
        // property is proven at the rule seam; the handler matrix has its own suite.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var provider = BuildValidChainProvider(clock);

        await Build(db, clock, provider).HandleAsync(new SeedCertificateAuthorityInput());

        var rootCertDer = db.Keys.Single(k => k.KeyDomain == KeyDomain.MTLS_CA_ROOT).CaCertificate!;
        var intermediateRow = db.Keys.Single(k => k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE);

        var (csrDer, _) = KcAppTestKit.BuildP256Csr();
        var leafPublicKey = CsrVerification.Verify(csrDer).Data!;

        var issuerKeyPkcs8 = r_crypto.Decrypt(intermediateRow.KeyMaterialEncrypted);
        D2Result<IssuedWorkloadCertificate> issued;

        try
        {
            using var issuerKey = ECDsa.Create();
            issuerKey.ImportPkcs8PrivateKey(issuerKeyPkcs8, out _);

            using var issuerCert =
                X509CertificateLoader.LoadCertificate(intermediateRow.CaCertificate!);

            issued = WorkloadCertificateIssuance.IssueLeaf(
                WorkloadIdentity.Create("edge").Data!,
                leafPublicKey,
                issuerCert,
                issuerKey,
                Duration.FromTimeSpan(r_options.LeafValidity),
                clock);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(issuerKeyPkcs8);
        }

        issued.Success.Should().BeTrue();

        // leaf → intermediate → root must chain (full path).
        AssertLeafChainsToRoot(
            issued.Data!.CertificateDer, intermediateRow.CaCertificate!, rootCertDer);
    }

    // Regression pin for partial-seed idempotency.
    // If a crash left the root active but intermediate absent, a re-run must seed only
    // the intermediate — NOT re-insert a duplicate root row.
    [Fact]
    public async Task Seed_RootAlreadyActive_OnlySeedsIntermediate_NoRootDuplicate()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var provider = BuildValidChainProvider(clock);

        // Simulate the partial-seed: root is active, intermediate is not.
        var firstResult = await Build(db, clock, provider).HandleAsync(new SeedCertificateAuthorityInput());
        firstResult.Data!.Seeded.Should().BeTrue();

        // Remove the intermediate to simulate a crash after root but before intermediate.
        db.Keys.RemoveRange(
            db.Keys.Where(k => k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE));
        db.Audit.RemoveRange(
            db.Audit.Where(a => db.Keys
                .Where(k => k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE)
                .Select(k => k.Kid)
                .Contains(a.Kid)));
        await db.SaveChangesAsync(CancellationToken.None);

        // Re-run the seeder — should succeed and seed only the intermediate.
        var secondResult = await Build(db, clock, provider).HandleAsync(new SeedCertificateAuthorityInput());

        secondResult.Success.Should().BeTrue();
        secondResult.Data!.Seeded.Should().BeTrue(
            because: "the intermediate was missing so it must be re-seeded");

        // Exactly one root row — no duplicate.
        db.Keys.Count(k => k.KeyDomain == KeyDomain.MTLS_CA_ROOT)
            .Should().Be(1, because: "the root was already active; no second row may be inserted");
        db.Keys.Count(k => k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE)
            .Should().Be(1, because: "only the missing intermediate must be seeded");
    }

    // Regression test: BubbleOnFailure guard is now live.
    // When the provider returns a typed failure, the handler must propagate it and
    // persist nothing — the host continues degraded (not crashed).
    [Fact]
    public async Task Seed_ProviderReturnsTypedFailure_BubbleOnFailureFires_NothingPersisted()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);

        // NoActiveIssuingCa = the 503 ServiceUnavailable returned when CA files are absent.
        var failingProvider = new FakeCaProvider(
            KeyCustodianFailures<LoadedCaMaterial>.NoActiveIssuingCa());

        var result = await Build(db, clock, failingProvider).HandleAsync(new SeedCertificateAuthorityInput());

        result.Success.Should().BeFalse(
            because: "BubbleOnFailure must propagate the typed CA-load failure");
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA",
            because: "the failure code must pass through from the provider");
        db.Keys.Should().BeEmpty(because: "nothing is persisted when the CA chain cannot be loaded");
        db.Audit.Should().BeEmpty();
    }

    private static ICaProvider BuildValidChainProvider(TestClock clock)
    {
        var root = CaCertificateGeneration.GenerateRootCa(
            "D2 Test Root CA", Duration.FromDays(3650), clock).Data!;

        byte[] rootCertDer = root.CertificateDer;
        byte[] rootKeyPkcs8 = (byte[])root.PrivateKeyPkcs8.Clone();

        byte[] intermediateCertDer;
        byte[] intermediateKeyPkcs8;

        using (var rootKey = ECDsa.Create())
        {
            rootKey.ImportPkcs8PrivateKey(root.PrivateKeyPkcs8, out _);

            using var rootCert = X509CertificateLoader.LoadCertificate(root.CertificateDer);

            var intermediate = CaCertificateGeneration.GenerateIntermediateCa(
                "D2 Test Issuing CA", rootCert, rootKey, Duration.FromDays(365), clock).Data!;

            intermediateCertDer = intermediate.CertificateDer;
            intermediateKeyPkcs8 = (byte[])intermediate.PrivateKeyPkcs8.Clone();
            intermediate.Zero();
        }

        root.Zero();

        // Clone-per-call provider: the seeder zeroes the byte arrays inside LoadedCaMaterial
        // after wrapping (ICaProvider contract — single-use material). A provider used for
        // multiple seeder invocations (idempotency + partial-seed tests) must return a fresh
        // clone on each call so zeroing one call's material cannot poison subsequent calls.
        return new CloningFakeCaProvider(rootCertDer, rootKeyPkcs8, intermediateCertDer, intermediateKeyPkcs8);
    }

    private static void AssertLeafChainsToRoot(
        byte[] leafDer, byte[] intermediateDer, byte[] rootDer)
    {
        using var leaf = X509CertificateLoader.LoadCertificate(leafDer);
        using var intermediate = X509CertificateLoader.LoadCertificate(intermediateDer);
        using var root = X509CertificateLoader.LoadCertificate(rootDer);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.ExtraStore.Add(intermediate);

        // Verify at the leaf's issuance instant — the 24h leaf is otherwise expired by
        // real wall-clock time when the test runs.
        chain.ChainPolicy.VerificationTime = leaf.NotBefore.AddMinutes(1);

        chain.Build(leaf).Should().BeTrue(
            because: "leaf → intermediate → root must form a complete chain to the seeded root");
    }

    private SeedCertificateAuthorityHandler Build(
        KeyCustodianTestDbContext db, TestClock clock, ICaProvider provider) =>
        new(
            KcAppTestKit.SystemContext<SeedCertificateAuthorityHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            provider,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            clock);

    /// <summary>
    /// Fake CA provider returning a fixed pre-baked result (success or failure). Used
    /// by single-invocation tests where zeroing is not a concern.
    /// </summary>
    private sealed class FakeCaProvider(D2Result<LoadedCaMaterial> result) : ICaProvider
    {
        public D2Result<LoadedCaMaterial> GetSeedCaMaterial() => result;
    }

    /// <summary>
    /// Fake CA provider that returns a FRESH clone of the source byte arrays on every
    /// call. Required for multi-invocation tests (idempotency, partial-seed) because
    /// the seeder zeroes the arrays inside <see cref="LoadedCaMaterial"/> after
    /// wrapping — a single shared instance would be poisoned on the second call.
    /// </summary>
    private sealed class CloningFakeCaProvider(
        byte[] rootCertDer,
        byte[] rootKeyPkcs8,
        byte[] intermediateCertDer,
        byte[] intermediateKeyPkcs8) : ICaProvider
    {
        public D2Result<LoadedCaMaterial> GetSeedCaMaterial() =>
            D2Result<LoadedCaMaterial>.Ok(
                new LoadedCaMaterial(
                    (byte[])rootCertDer.Clone(),
                    (byte[])rootKeyPkcs8.Clone(),
                    (byte[])intermediateCertDer.Clone(),
                    (byte[])intermediateKeyPkcs8.Clone()));
    }
}
