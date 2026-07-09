// -----------------------------------------------------------------------
// <copyright file="SealKeyProvisioningConvergeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Deterministic UniqueViolation converge coverage for seal lazy provisioning —
/// ClearChangeTracker + bounded poll serve the winner; budget exhaustion yields 503.
/// </summary>
/// <remarks>
/// <para>
/// Fail-without-fix (Clear): production path calls
/// <see cref="IKeyCustodianDbContext.ClearChangeTracker"/> after a classified
/// UniqueViolation so rejected inserts cannot poison re-reads. On EF InMemory,
/// <c>AsNoTracking</c> already bypasses the tracker, so dual-path "omit Clear and
/// assert fail" is not automatable here; the spy below asserts Clear is invoked,
/// and the live-PG race IT remains the store-true pin.
/// </para>
/// <para>
/// Fail-without-fix (converge / budget): without the UniqueViolation catch arm the
/// handler would surface a repo pipeline failure instead of Ok/503; without the
/// poll budget, a permanently-invisible winner would hang. The exhaustion test
/// pins the 503 terminal.
/// </para>
/// </remarks>
public sealed class SealKeyProvisioningConvergeTests
{
    private const string _FILES = "files";
    private const string _SERVICE = "convergefxt";

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task UniqueViolation_WithSiblingWinner_ClearsTracker_AndServesWinnerActiveKid()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var root = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
        var domain = "seal:" + _SERVICE;
        await using var db = RaceLoserDbContext.Create(
            dbName, root, r_crypto, r_options, domain);

        var result = await BuildPublic(db)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_SERVICE));

        result.Success.Should().BeTrue(
            "UniqueViolation converges on the sibling winner (not a 409); got {0}/{1}",
            result.StatusCode,
            result.ErrorCode);

        result.Data!.ActiveKid.Should().Be(db.WinnerKid);

        db.ClearCallCount.Should().BeGreaterThan(
            0, "ClearChangeTracker must run before re-read after UniqueViolation");

        // Loser inserts never committed; store holds only the sibling winner.
        await using var verify = CreateShared(dbName, root);
        var actives = verify.Keys
            .Where(k => k.KeyDomain == domain && k.Status == KeyStatus.Active)
            .ToList();

        actives.Should().ContainSingle("only the sibling winner Active remains");
        actives[0].Kid.Should().Be(db.WinnerKid);
    }

    [Fact]
    public async Task UniqueViolation_NoWinnerVisibleForFullBudget_Returns503Unavailable()
    {
        // Fail-without-fix: if converge returned Ok without an Active, or skipped the
        // budget terminal, this would not assert KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE.
        await using var db = AlwaysUniqueViolationEmptyDbContext.Create();

        var result = await BuildPublic(db)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_SERVICE));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE);
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        db.ClearCallCount.Should().BeGreaterThan(
            0, "budget loop clears the poisoned tracker each poll attempt");
    }

    private static KeyCustodianTestDbContext CreateShared(
        string dbName,
        Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot root)
    {
        var opts = new DbContextOptionsBuilder<KeyCustodianTestDbContext>()
            .UseInMemoryDatabase(databaseName: dbName, databaseRoot: root)
            .EnableServiceProviderCaching(false)
            .Options;

        return new KeyCustodianTestDbContext(opts);
    }

    private GetOrLazyProvisionSealPublicKeyHandler BuildPublic(IKeyCustodianDbContext db)
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal)
        {
            Scopes.Internal.Kc.Seal.Encrypt,
        };

        var ctx = new HandlerContext<GetOrLazyProvisionSealPublicKeyHandler>(
            new MutableRequestContext
            {
                Origin = RequestOrigin.CrossProcessHop,
                ImmediateCaller = _FILES,
                Scopes = scopes,
            },
            NullLogger<GetOrLazyProvisionSealPublicKeyHandler>.Instance);

        return new GetOrLazyProvisionSealPublicKeyHandler(
            ctx,
            new AlwaysUniqueViolationClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));
    }

    private sealed class AlwaysUniqueViolationClassifier : IDbExceptionClassifier
    {
        public DbFailureKind? Classify(Exception exception) =>
            exception is DbUpdateException ? DbFailureKind.UniqueViolation : null;
    }

    /// <summary>
    /// First SaveChanges seeds a sibling winner into the shared InMemory store then
    /// throws DbUpdateException (simulating UniqueViolation / EXCLUDE) so the loser's
    /// tracked inserts never commit; converge Clear+re-read serves the winner.
    /// </summary>
    private sealed class RaceLoserDbContext : KeyCustodianTestDbContext
    {
        private readonly string r_dbName;
        private readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot r_root;
        private readonly IPayloadCrypto r_crypto;
        private readonly KeyCustodianOptions r_options;
        private readonly string r_domain;
        private int _saves;
        private int _clears;

        private RaceLoserDbContext(
            DbContextOptions<KeyCustodianTestDbContext> options,
            string dbName,
            Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot root,
            IPayloadCrypto crypto,
            KeyCustodianOptions optionsConfig,
            string domain)
            : base(options)
        {
            r_dbName = dbName;
            r_root = root;
            r_crypto = crypto;
            r_options = optionsConfig;
            r_domain = domain;
        }

        public string WinnerKid { get; private set; } = string.Empty;

        public int ClearCallCount => Volatile.Read(ref _clears);

        public static RaceLoserDbContext Create(
            string dbName,
            Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot root,
            IPayloadCrypto crypto,
            KeyCustodianOptions options,
            string domain)
        {
            var opts = new DbContextOptionsBuilder<KeyCustodianTestDbContext>()
                .UseInMemoryDatabase(databaseName: dbName, databaseRoot: root)
                .EnableServiceProviderCaching(false)
                .Options;

            return new RaceLoserDbContext(opts, dbName, root, crypto, options, domain);
        }

        public override void ClearChangeTracker()
        {
            Interlocked.Increment(ref _clears);
            base.ClearChangeTracker();
        }

        public override async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _saves) == 1)
            {
                WinnerKid = await SeedWinnerOnSiblingAsync(cancellationToken)
                    .ConfigureAwait(false);

                throw new DbUpdateException(
                    "fixture UniqueViolation / EXCLUDE collision",
                    (Exception?)null);
            }

            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> SeedWinnerOnSiblingAsync(CancellationToken ct)
        {
            await using var sibling = CreateShared(r_dbName, r_root);

            return await KcAppTestKit.SeedKeyAsync(
                sibling,
                r_crypto,
                r_options,
                r_domain,
                KeyType.EcdhSealing,
                KeyStatus.Active,
                KcAppTestKit.SR_BaseInstant).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// SaveChanges always throws UniqueViolation and the store stays empty so the
    /// converge poll exhausts its attempt budget → 503.
    /// </summary>
    private sealed class AlwaysUniqueViolationEmptyDbContext : KeyCustodianTestDbContext
    {
        private int _clears;

        private AlwaysUniqueViolationEmptyDbContext(
            DbContextOptions<KeyCustodianTestDbContext> options)
            : base(options)
        {
        }

        public int ClearCallCount => Volatile.Read(ref _clears);

        public static AlwaysUniqueViolationEmptyDbContext Create()
        {
            var opts = new DbContextOptionsBuilder<KeyCustodianTestDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
                .EnableServiceProviderCaching(false)
                .Options;

            return new AlwaysUniqueViolationEmptyDbContext(opts);
        }

        public override void ClearChangeTracker()
        {
            Interlocked.Increment(ref _clears);
            base.ClearChangeTracker();
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            throw new DbUpdateException(
                "fixture UniqueViolation with no visible winner",
                (Exception?)null);
    }
}
