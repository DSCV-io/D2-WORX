// -----------------------------------------------------------------------
// <copyright file="KeyCustodianLifecycleIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.KeyCustodian.App.Application.Signing;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
using D2.Edge.KeyCustodian.App.Infrastructure.Persistence;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Client.CaCertificate;
using D2.Edge.KeyCustodian.Client.Issuance;
using D2.Edge.KeyCustodian.Client.Signing;
using D2.Edge.KeyCustodian.Infra;
using D2.Edge.KeyCustodian.Infra.Persistence.Postgres;
using D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
using D2.Private.Auth;
using D2.Shared.Context.Abstractions;
using D2.Shared.EntityFrameworkCore.Postgres;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Full-lifecycle live-DB tests through the real handler graph against PostgreSQL:
/// generate ΓåÆ soak ΓåÆ activate ΓåÆ cadence elapse ΓåÆ rotate ΓåÆ grace elapse ΓåÆ retire,
/// plus the rotation-exactly-one guarantee under the advisory lock. Uses a
/// <c>TestClock</c> to advance time and a <see cref="RecordingAnnouncer"/> to
/// capture the event sequence. Run after the orchestrator generates the Initial
/// migration.
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianLifecycleIntegrationTests(KeyCustodianPostgresFixture fixture)
{
    [Fact]
    public async Task FullLifecycle_GenerateActivateRotateRetire_AgainstRealDb()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var announcer = new RecordingAnnouncer();
        await using var provider = BuildProvider(clock, announcer);
        var domain = KeyDomain.ClientSecret.Value;

        // 1. Generate a pending key.
        var generated = await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.Secret), CancellationToken.None);
        generated.Success.Should().BeTrue();
        var pendingKid = await SingleKidAsync(domain, KeyStatus.Pending);

        // 2. Soak elapses, then activate.
        clock.Advance(Duration.FromHours(2));
        var activated = await Handler<IActivateKeyHandler>(provider)
            .HandleAsync(new ActivateKeyInput(pendingKid), CancellationToken.None);
        activated.Success.Should().BeTrue();
        await SingleKidAsync(domain, KeyStatus.Active);

        // 3. Generate a pending successor â€” the step-4 cadence advance soaks it.
        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.Secret), CancellationToken.None);

        // 4. Rotate â€” the original incumbent enters retiring.
        clock.Advance(Duration.FromDays(180));
        var rotated = await Handler<IRotateKeyHandler>(provider)
            .HandleAsync(new RotateKeyInput(domain), CancellationToken.None);
        rotated.Success.Should().BeTrue();

        // Overlap guarantee: exactly one Active AND one Retiring must exist in a single
        // SaveChangesAsync â€” the "never zero active keys" contract.
        await using (var overlap = fixture.NewContext())
        {
            (await overlap.Keys.AsNoTracking()
                    .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Active))
                .Should().Be(1, "the successor must be Active immediately after rotation");
            (await overlap.Keys.AsNoTracking()
                    .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Retiring))
                .Should().Be(1, "the incumbent must be Retiring immediately after rotation");
        }

        // 5. Grace elapses, then retire the retiring key.
        clock.Advance(Duration.FromDays(180));
        var retiringKid = await SingleKidAsync(domain, KeyStatus.Retiring);
        var retired = await Handler<IRetireKeyHandler>(provider)
            .HandleAsync(new RetireKeyInput(retiringKid), CancellationToken.None);
        retired.Success.Should().BeTrue();

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Retired))
            .Should().BeGreaterThanOrEqualTo(1);

        // The rotation announced at least once (routine, non-urgent).
        announcer.Calls.Should().Contain(c => !c.Urgent);
    }

    [Fact]
    public async Task Rotation_ConcurrentTicks_ExactlyOneExecutes()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var domain = KeyDomain.Cookie.Value;
        await using var provider = BuildProvider(clock, new RecordingAnnouncer());

        // Seed an active + soaked-pending successor so a rotation is due.
        await SeedRotatableAsync(provider, clock, domain);
        clock.Advance(Duration.FromDays(365));

        // Two concurrent rotation attempts under the advisory lock â€” exactly one runs.
        IReadOnlyDictionary<string, KeyType> bootstrap =
            new Dictionary<string, KeyType>(StringComparer.Ordinal);

        await using var lockHandle = await PgAdvisoryLock.TryAcquireSessionAsync(
            fixture.ConnectionString, AdvisoryLocks.D2Keycustodian.ROTATION);
        lockHandle.IsHeld.Should().BeTrue();

        // While the lock is held, a competing tick's try-acquire fails (skip).
        await using var competitor = await PgAdvisoryLock.TryAcquireSessionAsync(
            fixture.ConnectionString, AdvisoryLocks.D2Keycustodian.ROTATION);
        competitor.IsHeld.Should().BeFalse();

        // The holder performs the rotation directly.
        var run = await Handler<IRunDueRotationsHandler>(provider)
            .HandleAsync(new RunDueRotationsInput(bootstrap), CancellationToken.None);
        run.Success.Should().BeTrue();

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Retiring))
            .Should().Be(1);
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Active))
            .Should().Be(1, "the successor must be Active and no other key active after rotation");
    }

    [Fact]
    public async Task Sign_MinterCapability_VerifiesAgainstPublishedKey_AgainstRealDb()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));

        // ONE root crypto shared across the providers â€” the System-plane provider
        // wraps the generated key, the minter provider must unwrap the same material.
        var rootCrypto = BuildRootCrypto();

        // 1) Generate + activate the cluster-signing key through the REAL handler
        //    graph over real PostgreSQL, on the System plane (the only plane the
        //    lifecycle authority admits). jwks-signing is the sole RSA-signing-bound
        //    domain, and no other test in this shared-DB collection seeds it.
        await using (var systemProvider = BuildProvider(
            clock, new RecordingAnnouncer(), rootCrypto: rootCrypto))
        {
            await Handler<IGenerateKeyHandler>(systemProvider)
                .HandleAsync(
                    new GenerateKeyInput(KeyDomain.JWKS_SIGNING, KeyType.RsaSigning),
                    CancellationToken.None);
            var pendingKid = await SingleKidAsync(KeyDomain.JWKS_SIGNING, KeyStatus.Pending);
            clock.Advance(Duration.FromHours(2));
            var activated = await Handler<IActivateKeyHandler>(systemProvider)
                .HandleAsync(new ActivateKeyInput(pendingKid), CancellationToken.None);
            activated.Success.Should().BeTrue();
        }

        var kid = await SingleKidAsync(KeyDomain.JWKS_SIGNING, KeyStatus.Active);

        // 2) Sign through the dedicated minter capability (the ONLY path to the
        //    cluster-signing root) on the in-process-module plane, over real PG.
        var minterContext = new MutableRequestContext { Origin = RequestOrigin.InProcessModule };
        var payload = "header.payload"u8.ToArray();
        SignOutput signOutput;

        await using (var minterProvider = BuildProvider(
            clock, new RecordingAnnouncer(), minterContext, rootCrypto: rootCrypto, minter: true))
        {
            var signed = await Handler<IJwtSigningCapability>(minterProvider)
                .SignJwtAsync(
                    new SignInput(KeyDomain.JWKS_SIGNING, payload), CancellationToken.None);

            signed.Success.Should().BeTrue();
            signed.Data!.Kid.Should().Be(kid);
            signOutput = signed.Data!;
        }

        // 3) Verify the signature against the published signing key (the stored SPKI
        //    public half) exactly as a cluster consumer would after fetching the JWKS.
        byte[] spki;
        await using (var verifyCtx = fixture.NewContext())
        {
            spki = (await verifyCtx.Keys.AsNoTracking()
                .FirstAsync(k => k.Kid == kid)).PublicKeyMaterial!;
        }

        using var verifier = RSA.Create();
        verifier.ImportSubjectPublicKeyInfo(spki, out _);

        var signature = Base64Url.DecodeFromChars(signOutput.Signature);
        verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue("the signature verifies against the published signing key over real PG");
    }

    [Fact]
    public async Task Sign_GeneralSurface_NonSigningBoundDomain_Returns400Mismatch_AgainstRealDb()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));

        // Distinct per-test domain: the shared-collection PostgreSQL is not reset between
        // tests, so this test uses a domain no other test seeds. notifications is now SEALED
        // (removed from the catalog); use a registered fixture payload domain
        // (registered BEFORE BuildProvider so the boot validator accepts the grant).
        const string domain = "payload-fixture-sign";
        using var fixtureSeam = KeyDomain.RegisterFixturePayloadDomainForTesting(domain);

        var requestContext = new MutableRequestContext
        {
            Origin = RequestOrigin.CrossProcessHop,
            ImmediateCaller = "edge",

            // The SignHandler's per-handler ScopeRequirement is fail-closed: BaseHandler
            // enforces internal.kc.sign in-process from IRequestContext.Scopes before the
            // authority rule or any crypto runs. A cross-process caller carries it here.
            Scopes = new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Sign },
        };
        var signingAuth = new SigningDomainAuthorityOptions();
        signingAuth.AllowedSigningDomainsByWorkload["edge"] = [domain];

        await using var provider = BuildProvider(
            clock, new RecordingAnnouncer(), requestContext, signingAuth);

        // The caller + policy authorize, but the domain's bound key type (AES payload)
        // can never hold a signing key â€” a permanent 400 over real PG, never the
        // retryable 503 a not-yet-provisioned key would produce.
        var signed = await Handler<ISignHandler>(provider)
            .HandleAsync(
                new SignInput(domain, "header.payload"u8.ToArray()), CancellationToken.None);

        signed.Success.Should().BeFalse();
        signed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        signed.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH);
    }

    [Fact]
    public async Task CompromiseKey_PendingWithReplacement_SwapsInOneSave_AgainstRealDb()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));

        // Distinct per-test domain the shared-collection PostgreSQL is not reset between
        // tests; courier is now SEALED (removed from the catalog), so use a
        // registered fixture AES-payload domain no other test seeds.
        const string domain = "payload-fixture-compromise";
        using var fixtureSeam = KeyDomain.RegisterFixturePayloadDomainForTesting(domain);
        await using var provider = BuildProvider(clock, new RecordingAnnouncer());

        // Generate a pending key, then compromise it WITH a replacement: the handler
        // marks the original Compromised AND inserts a fresh Pending in ONE
        // SaveChangesAsync, exercising the "one Pending per domain" partial unique
        // index's release-before-acquire statement ordering against the real schema.
        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.AesPayload), CancellationToken.None);
        var pendingKid = await SingleKidAsync(domain, KeyStatus.Pending);

        var compromised = await Handler<ICompromiseKeyHandler>(provider)
            .HandleAsync(
                new CompromiseKeyInput
                {
                    Kid = pendingKid,
                    Reason = "integration",
                    GenerateReplacement = true,
                },
                CancellationToken.None);

        compromised.Success.Should().BeTrue();

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Pending))
            .Should().Be(1, "the replacement Pending is the only Pending after the swap");
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Compromised))
            .Should().Be(1, "the original key is Compromised after the swap");
    }

    [Fact]
    public async Task CompromiseKey_ExistingPending_CommitsWithoutSecondPending_AgainstRealDb()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));

        // Distinct per-test domain the shared-collection PostgreSQL is not reset between
        // tests; notifications is now SEALED (removed from the catalog), so use a
        // registered fixture AES-payload domain no other test persists keys in.
        const string domain = "payload-fixture-pending";
        using var fixtureSeam = KeyDomain.RegisterFixturePayloadDomainForTesting(domain);
        await using var provider = BuildProvider(clock, new RecordingAnnouncer());

        // Seed the mid-rotation state: an Active incumbent AND a live Pending successor.
        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.AesPayload), CancellationToken.None);
        var incumbentKid = await SingleKidAsync(domain, KeyStatus.Pending);
        clock.Advance(Duration.FromHours(2));
        await Handler<IActivateKeyHandler>(provider)
            .HandleAsync(new ActivateKeyInput(incumbentKid), CancellationToken.None);

        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.AesPayload), CancellationToken.None);
        var successorKid = await SingleKidAsync(domain, KeyStatus.Pending);

        // Compromise the ACTIVE incumbent WITH a replacement requested. A live pending
        // successor already exists, so the handler must NOT insert a second Pending
        // (that would breach the one-pending-per-domain unique index and roll the WHOLE
        // transaction â€” including the compromise â€” back). The compromise MUST commit and
        // the pre-existing successor is reported as the replacement.
        var activeKid = await SingleKidAsync(domain, KeyStatus.Active);
        var compromised = await Handler<ICompromiseKeyHandler>(provider)
            .HandleAsync(
                new CompromiseKeyInput
                {
                    Kid = activeKid,
                    Reason = "integration",
                    GenerateReplacement = true,
                },
                CancellationToken.None);

        compromised.Success.Should().BeTrue("the compromise always commits, never rolls back");
        compromised.Data!.ReplacementKid.Should().Be(
            successorKid, "the pre-existing successor is reported as the replacement");

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Pending))
            .Should().Be(1, "no second Pending is inserted; only the successor remains");
        (await verify.Keys.AsNoTracking()
                .SingleAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Compromised))
            .Kid.Should().Be(activeKid, "the compromised incumbent is durably Compromised");
    }

    [Fact]
    public async Task CompromiseKey_ReplacementBuildFails_CompromiseStillCommitsDurably_AgainstRealDb()
    {
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();
        var logs = new CapturingLoggerProvider();
        await using var provider = BuildProvider(
            clock, new RecordingAnnouncer(), rootCrypto: rootCrypto, loggerProvider: logs);

        // The intermediate-CA domain: no other test in this shared-DB collection seeds it.
        // Seed a LIVE (active) intermediate but leave its issuing root ABSENT â€” so building a
        // replacement intermediate (which must be signed by an active root) cannot succeed.
        const string domain = KeyDomain.MTLS_CA_INTERMEDIATE;
        var incumbentKid = await SeedActiveCaKeyAsync(domain, clock, rootCrypto);

        // Compromise the active intermediate WITH a replacement requested. The replacement
        // build fails (no active root to sign a new intermediate), but that is a best-effort
        // follow-up AFTER the durable kill: the compromise MUST still commit and the op
        // returns Ok with a null replacement â€” never a rollback that leaves the key live.
        var compromised = await Handler<ICompromiseKeyHandler>(provider)
            .HandleAsync(
                new CompromiseKeyInput
                {
                    Kid = incumbentKid,
                    Reason = "integration",
                    GenerateReplacement = true,
                },
                CancellationToken.None);

        compromised.Success.Should().BeTrue(
            "the compromise commits durably even when the replacement cannot be built");
        compromised.Data!.ReplacementKid.Should().BeNull(
            "the replacement build failed â€” a null kid is returned, never a rollback");

        // Re-query the DB: the incumbent is DURABLY Compromised (the kill survived the
        // failed replacement generation), and no orphan replacement Pending was written.
        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .SingleAsync(k => k.Kid == incumbentKid))
            .Status.Should().Be(
                KeyStatus.Compromised, "the compromised intermediate is durably Compromised");
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Pending))
            .Should().Be(0, "no replacement Pending is inserted when the build fails");

        // The best-effort failure is observable, not silent.
        logs.Entries.Should().Contain(
            e => e.EventId.Id == 9508,
            "a replacement-generation-failed warning is logged after the durable commit");
    }

    [Fact]
    public async Task IssueLeafThenFetchChain_LeafValidatesAgainstFetchedRootOnly_AgainstRealDb()
    {
        // The full certificate-authority consumer round-trip against PostgreSQL:
        // seed a coherent two-tier CA â†’ workload-side keypair + CSR â†’ issue a leaf
        // through the generated-op shell (peer-derived SAN) â†’ fetch the chain â†’
        // an X509Chain over (leaf, issuance-returned intermediate) validates with
        // the FETCHED root as the ONLY trust anchor â€” and the leaf certifies
        // exactly the workload keypair's public key.
        await fixture.EnsureMigratedAsync();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0));
        var rootCrypto = BuildRootCrypto();

        // The certificate-authority consumer plane: an authenticated cross-process
        // peer carrying both CA-surface scopes (the interceptor-established shape).
        var context = new MutableRequestContext
        {
            Origin = RequestOrigin.CrossProcessHop,
            ImmediateCaller = "edge",
            Scopes = new HashSet<string>(StringComparer.Ordinal)
            {
                D2.Private.Auth.ProductScopes.Internal.Kc.Issue,
                D2.Private.Auth.ProductScopes.Internal.Kc.Cacert,
            },
        };

        await using var provider = BuildProvider(
            clock, new RecordingAnnouncer(), requestContext: context, rootCrypto: rootCrypto);

        // Shared-DB hygiene: other tests in this collection may have left rows in
        // the CA domains; retire any still-active ones so the coherent hierarchy
        // seeded below is the single active pair.
        await using (var clean = fixture.NewContext())
        {
            var stale = await clean.Keys
                .Where(k =>
                    (k.KeyDomain == KeyDomain.MTLS_CA_ROOT
                        || k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE)
                    && k.Status == KeyStatus.Active)
                .ToListAsync();

            foreach (var record in stale)
            {
                record.Status = KeyStatus.Retired;
                record.RetiringAt = clock.GetCurrentInstant();
                record.RetiredAt = clock.GetCurrentInstant();
            }

            await clean.SaveChangesAsync();
        }

        await using (var seed = fixture.NewContext())
        {
            await Unit.KeyCustodian.App.KcAppTestKit.SeedCaHierarchyAsync(
                seed, rootCrypto, clock.GetCurrentInstant());
        }

        // The workload role: generate the keypair + CSR locally.
        var (csrDer, workloadSpki) = Unit.KeyCustodian.App.KcAppTestKit.BuildP256Csr();

        // Issue through the generated-op shell (shell â†’ inner handler â†’ the
        // isolated leaf-signing capability â†’ audit write).
        var issued = await Handler<IIssueLeafHandler>(provider)
            .HandleAsync(new IssueLeafInput(csrDer), CancellationToken.None);
        issued.Success.Should().BeTrue();

        // Fetch the chain through the real query handler.
        var chainFetch = await Handler<IGetCaCertificateHandler>(provider)
            .HandleAsync(new GetCaCertificateInput(), CancellationToken.None);
        chainFetch.Success.Should().BeTrue();

        using var leaf = System.Security.Cryptography.X509Certificates
            .X509CertificateLoader.LoadCertificate(issued.Data!.CertificateDer);
        using var issuanceIssuer = System.Security.Cryptography.X509Certificates
            .X509CertificateLoader.LoadCertificate(issued.Data.IssuerCertificateDer);
        using var fetchedRoot = System.Security.Cryptography.X509Certificates
            .X509CertificateLoader.LoadCertificate(chainFetch.Data!.RootCertificateDer);

        // The leaf certifies EXACTLY the workload keypair's public key.
        leaf.PublicKey.ExportSubjectPublicKeyInfo().Should().Equal(workloadSpki);

        // leaf â†’ issuance-issuer â†’ FETCHED root (the only trust anchor) validates.
        using var chain = new System.Security.Cryptography.X509Certificates.X509Chain();
        chain.ChainPolicy.RevocationMode =
            System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = System.Security.Cryptography.X509Certificates
            .X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.VerificationFlags = System.Security.Cryptography.X509Certificates
            .X509VerificationFlags.IgnoreNotTimeValid;
        chain.ChainPolicy.CustomTrustStore.Add(fetchedRoot);
        chain.ChainPolicy.ExtraStore.Add(issuanceIssuer);
        chain.Build(leaf).Should().BeTrue(
            "the issued leaf validates against the fetched trust anchor alone");

        // The durable audit row landed in PostgreSQL.
        await using var verify = fixture.NewContext();
        (await verify.LeafIssuanceAudit.AsNoTracking()
                .Where(a => a.WorkloadServiceId == "edge")
                .CountAsync())
            .Should().BeGreaterThan(0, "the issuance audit row is the durable record");
    }

    private static THandler Handler<THandler>(ServiceProvider provider)
        where THandler : notnull =>
        provider.CreateScope().ServiceProvider.GetRequiredService<THandler>();

    private static IPayloadCrypto BuildRootCrypto()
    {
        var key = RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES);
        var keyring = new PayloadCryptoKeyring(
            "root",
            new Dictionary<string, byte[]> { ["root"] = key },
            "keycustodian-root"u8.ToArray());
        return new PayloadCrypto(keyring);
    }

    private static KeyCustodianOptions BuildOptions() => new()
    {
        RsaKeySizeBits = 2048,
        SecretLengthBytes = 64,
        Default = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromDays(30),
            Grace = TimeSpan.FromDays(7),
            SmokeSoak = TimeSpan.FromHours(1),
        },
    };

    private async Task SeedRotatableAsync(ServiceProvider provider, TestClock clock, string domain)
    {
        // Generate + activate the incumbent; the caller advances 365 days which soaks
        // the pending successor before RunDueRotations fires.
        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.Secret), CancellationToken.None);
        var activeKid = await SingleKidAsync(domain, KeyStatus.Pending);
        clock.Advance(Duration.FromHours(2));
        await Handler<IActivateKeyHandler>(provider)
            .HandleAsync(new ActivateKeyInput(activeKid), CancellationToken.None);

        // Generate a pending successor only â€” leave it pending so RotateKey can find it.
        await Handler<IGenerateKeyHandler>(provider)
            .HandleAsync(new GenerateKeyInput(domain, KeyType.Secret), CancellationToken.None);
    }

    private async Task<string> SingleKidAsync(string domain, KeyStatus status)
    {
        await using var context = fixture.NewContext();
        return await context.Keys.AsNoTracking()
            .Where(k => k.KeyDomain == domain && k.Status == status)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => k.Kid)
            .FirstAsync();
    }

    // Seeds a live (active) CA-certificate key directly. Real CA material is generated (a
    // self-signed cert stands in for the incumbent â€” the compromise path never parses it,
    // and a replacement build fails on the absent active root before touching it). The
    // private key is root-wrapped with the shared crypto so ToDomain rehydrates cleanly.
    private async Task<string> SeedActiveCaKeyAsync(
        string domain, TestClock clock, IPayloadCrypto rootCrypto)
    {
        var generated = CaCertificateGeneration.GenerateRootCa(
            CaCertificateGeneration.ROOT_CA_SUBJECT, Duration.FromDays(365), clock).Data!;

        byte[] wrapped;

        try
        {
            wrapped = rootCrypto.Encrypt(generated.PrivateKeyPkcs8);
        }
        finally
        {
            generated.Zero();
        }

        var kid = KidMinting.Mint();

        await using var seed = fixture.NewContext();
        seed.Keys.Add(new KeyRecord
        {
            Kid = kid,
            KeyDomain = domain,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = wrapped,
            CaCertificate = generated.CertificateDer,
            CreatedAt = clock.GetCurrentInstant(),
            Status = KeyStatus.Active,
            ActivatedAt = clock.GetCurrentInstant(),
        });

        await seed.SaveChangesAsync();

        return kid;
    }

    private ServiceProvider BuildProvider(
        TestClock clock,
        IKeyRotationAnnouncer announcer,
        IRequestContext? requestContext = null,
        SigningDomainAuthorityOptions? signingAuthority = null,
        IPayloadCrypto? rootCrypto = null,
        bool minter = false,
        ILoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            if (loggerProvider is not null)
            {
                b.SetMinimumLevel(LogLevel.Trace);
                b.AddProvider(loggerProvider);
            }
        });
        services.AddD2Handler();

        // Default plane: the in-host System worker plane â€” the only plane the
        // lifecycle authority admits, mirroring the schedulers' established context.
        services.AddSingleton(
            requestContext ?? new MutableRequestContext { Origin = RequestOrigin.System });
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(announcer);
        services.AddSingleton(
            Microsoft.Extensions.Options.Options.Create(
                signingAuthority ?? new SigningDomainAuthorityOptions()));

        // Real DbContext against the container (scoped, like production).
        services.AddDbContext<KeyCustodianDbContext>(opts =>
            opts.ApplyD2NpgsqlDefaults(
                fixture.ConnectionString,
                commandTimeoutSeconds: 30,
                migrationsAssemblyName: typeof(KeyCustodianDbContext).Assembly.GetName().Name!));
        services.AddScoped<IKeyCustodianDbContext>(
            sp => sp.GetRequiredService<KeyCustodianDbContext>());

        services.AddD2Postgres();

        // Real root crypto over a throwaway keyring (genuine wrap/unwrap path). Tests
        // spanning multiple providers pass ONE shared instance so material wrapped by
        // one provider unwraps in another.
        var resolvedCrypto = rootCrypto ?? BuildRootCrypto();
        services.AddKeyedSingleton<IPayloadCrypto>(
            KeyCustodianRootKey.ROOT_SERVICE_KEY, (_, _) => resolvedCrypto);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(BuildOptions()));

        services.AddD2KeyCustodianApp();

        // The dedicated issuance leaf-signing capability â€” the composition-root
        // opt-in a host serving the issuance surface makes (the general
        // registration deliberately does not provide it; the isolation property
        // is pinned in the unit DI suite).
        services.AddD2CaLeafSigningCapability();

        // The dedicated Â§9.44 root-signing capability â€” the composition-root opt-in the
        // System-worker host makes. All four lifecycle-mutation handlers (generate /
        // activate / rotate / compromise) take it, so without this call they cannot
        // resolve; the isolation property is pinned in the unit DI suite.
        services.AddD2CaRootSigningCapability();

        // The dedicated minter capability is granted ONLY where the auth-module
        // composition would grant it â€” never in the general registration.
        if (minter)
            services.AddD2JwtSigningCapability();

        return services.BuildServiceProvider();
    }

    // Captures log entries (level + EventId) across all categories so a test can assert a
    // specific delegate fired (e.g. the best-effort replacement-generation-failed warning).
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, EventId EventId)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(
            ConcurrentQueue<(LogLevel Level, EventId EventId)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Enqueue((logLevel, eventId));
        }
    }
}
