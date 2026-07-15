// -----------------------------------------------------------------------
// <copyright file="SealKeyProvisioningConvergeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Private.Auth;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using D2.Shared.Handler.Repo.Abstractions;
using D2.Shared.Handler.Repo.Postgres;
using global::Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Deterministic UniqueViolation / EXCLUDE converge coverage for seal lazy
/// provisioning ΓÇö production-shaped PG exceptions + the real
/// <see cref="PostgresDbExceptionClassifier"/>, not a kitchen-sink double.
/// </summary>
/// <remarks>
/// <para>
/// CI flake signature: concurrent first-requests returned
/// <c>ErrorCode=UNIQUE_VIOLATION</c> (BaseRepoHandler mapping) instead of
/// converging on the winner. That means the exception escaped
/// <c>SealKeyProvisioning</c>'s catch. These tests FORCE the exact exception
/// shapes PG/EF emit (SQLSTATE <c>23P01</c> exclusion + <c>23505</c> unique)
/// through the REAL classifier so a catch-shape / classification drift fails
/// here without needing a multi-thread race.
/// </para>
/// <para>
/// Fail-without-fix: narrow the catch back to bare
/// <c>catch (DbUpdateException)</c> without classifier filter on a shape the
/// when-clause misses, or break 23P01 ΓåÆ UniqueViolation mapping ΓÇö these cases
/// return <c>UNIQUE_VIOLATION</c> via BaseRepoHandler instead of Ok/503.
/// </para>
/// </remarks>
public sealed class SealKeyProvisioningConvergeTests
{
    private const string _FILES = "files";
    private const string _SERVICE = "convergefxt";

    // Production classifier â€” the same type AddD2Postgres registers. Do NOT
    // stub Classify; the pin is that 23P01/23505 through this type enter converge.
    private static readonly PostgresDbExceptionClassifier sr_classifier = new();

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Theory]
    [InlineData("23P01")] // exclusion_violation â€” one-Active EXCLUDE (the race)
    [InlineData("23505")] // unique_violation â€” one-Pending unique (related race)
    public async Task ProductionShapedUniqueConflict_DbUpdateWrappingPg_ConvergesToWinner(
        string sqlState)
    {
        var dbName = Guid.NewGuid().ToString("N");
        var root = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
        var domain = "seal:" + _SERVICE;
        await using var db = RaceLoserDbContext.Create(
            dbName,
            root,
            r_crypto,
            r_options,
            domain,
            () => PgUniqueConflict(sqlState));

        var result = await BuildPublic(db)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_SERVICE));

        // The flake was UNIQUE_VIOLATION leaking via BaseRepoHandler â€” pin that gone.
        result.ErrorCode.Should().NotBe(
            DbErrorCodes.UNIQUE_VIOLATION,
            "loser must not surface BaseRepoHandler UniqueViolation; sqlState={0} got {1}/{2}",
            sqlState,
            result.StatusCode,
            result.ErrorCode);

        result.Success.Should().BeTrue(
            "real classifier + sqlState {0} must enter converge and serve winner; got {1}/{2}",
            sqlState,
            result.StatusCode,
            result.ErrorCode);

        result.Data!.ActiveKid.Should().Be(db.WinnerKid);

        db.ClearCallCount.Should().BeGreaterThan(
            0, "ClearChangeTracker must run before re-read after UniqueViolation");
    }

    [Theory]
    [InlineData("23P01")]
    [InlineData("23505")]
    public async Task ProductionShapedUniqueConflict_RawPostgresException_ConvergesToWinner(
        string sqlState)
    {
        // Defense: if EF/Npgsql ever surfaces the bare PostgresException (no
        // DbUpdateException wrap), the catch must still classify + converge.
        // catch (DbUpdateException) alone would miss this and leak UNIQUE_VIOLATION.
        var dbName = Guid.NewGuid().ToString("N");
        var root = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
        var domain = "seal:" + _SERVICE;
        await using var db = RaceLoserDbContext.Create(
            dbName,
            root,
            r_crypto,
            r_options,
            domain,
            () => PgException(sqlState));

        var result = await BuildPublic(db)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_SERVICE));

        result.ErrorCode.Should().NotBe(DbErrorCodes.UNIQUE_VIOLATION);
        result.Success.Should().BeTrue(
            "raw PostgresException sqlState={0} must converge; got {1}/{2}",
            sqlState,
            result.StatusCode,
            result.ErrorCode);
        result.Data!.ActiveKid.Should().Be(db.WinnerKid);
    }

    [Fact]
    public async Task ProductionShapedUniqueConflict_NoWinnerVisibleForFullBudget_Returns503Not409()
    {
        await using var db = AlwaysConflictEmptyDbContext.Create(
            () => PgUniqueConflict("23P01"));

        var result = await BuildPublic(db)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_SERVICE));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().NotBe(
            DbErrorCodes.UNIQUE_VIOLATION,
            "budget exhaustion must be retryable 503, never 409 UniqueViolation");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE);
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        db.ClearCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RealClassifier_MapsExclusionAndUniqueSqlStates_ToUniqueViolation()
    {
        // Fail-loud pin that the production mapping this path depends on still holds.
        // If 23P01 ever stops mapping to UniqueViolation, converge's catch filter is dead
        // and BaseRepoHandler will emit UNIQUE_VIOLATION for EXCLUDE races again.
        sr_classifier.Classify(PgUniqueConflict("23P01"))
            .Should().Be(DbFailureKind.UniqueViolation);
        sr_classifier.Classify(PgUniqueConflict("23505"))
            .Should().Be(DbFailureKind.UniqueViolation);
        sr_classifier.Classify(PgException("23P01"))
            .Should().Be(DbFailureKind.UniqueViolation);
    }

    [Fact]
    public async Task NonUniqueDbFailure_DoesNotConverge_SurfacesAsMappedDbError()
    {
        // Negative: a check violation must NOT be swallowed by the converge arm.
        await using var db = AlwaysConflictEmptyDbContext.Create(
            () => PgUniqueConflict("23514")); // check_violation

        var result = await BuildPublic(db)
            .HandleAsync(new GetOrLazyProvisionSealPublicKeyInput(_SERVICE));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            DbErrorCodes.CHECK_VIOLATION,
            "only UniqueViolation-classified conflicts enter converge");
    }

    private static PostgresException PgException(string sqlState) =>
        new(
            messageText: "fixture pg conflict sqlState=" + sqlState,
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState);

    private static DbUpdateException PgUniqueConflict(string sqlState) =>
        new("fixture EF wrap of pg conflict", PgException(sqlState));

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
            ProductScopes.Internal.Kc.Seal.Encrypt,
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
            sr_classifier,
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));
    }

    /// <summary>
    /// First SaveChanges seeds a sibling winner then throws a caller-supplied
    /// production-shaped exception so converge Clear+re-read serves the winner.
    /// </summary>
    private sealed class RaceLoserDbContext : KeyCustodianTestDbContext
    {
        private readonly string r_dbName;
        private readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot r_root;
        private readonly IPayloadCrypto r_crypto;
        private readonly KeyCustodianOptions r_options;
        private readonly string r_domain;
        private readonly Func<Exception> r_throwOnFirstSave;
        private int _saves;
        private int _clears;

        private RaceLoserDbContext(
            DbContextOptions<KeyCustodianTestDbContext> options,
            string dbName,
            Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot root,
            IPayloadCrypto crypto,
            KeyCustodianOptions optionsConfig,
            string domain,
            Func<Exception> throwOnFirstSave)
            : base(options)
        {
            r_dbName = dbName;
            r_root = root;
            r_crypto = crypto;
            r_options = optionsConfig;
            r_domain = domain;
            r_throwOnFirstSave = throwOnFirstSave;
        }

        public string WinnerKid { get; private set; } = string.Empty;

        public int ClearCallCount => Volatile.Read(ref _clears);

        public static RaceLoserDbContext Create(
            string dbName,
            Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot root,
            IPayloadCrypto crypto,
            KeyCustodianOptions options,
            string domain,
            Func<Exception> throwOnFirstSave)
        {
            var opts = new DbContextOptionsBuilder<KeyCustodianTestDbContext>()
                .UseInMemoryDatabase(databaseName: dbName, databaseRoot: root)
                .EnableServiceProviderCaching(false)
                .Options;

            return new RaceLoserDbContext(
                opts, dbName, root, crypto, options, domain, throwOnFirstSave);
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

                throw r_throwOnFirstSave();
            }

            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> SeedWinnerOnSiblingAsync(CancellationToken cancellationToken)
        {
            // cancellationToken reserved for sibling seed path when SeedKeyAsync gains CT.
            _ = cancellationToken;
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
    /// SaveChanges always throws the supplied conflict and the store stays empty so the
    /// converge poll exhausts â†’ 503 (or maps non-unique kinds via BaseRepoHandler).
    /// </summary>
    private sealed class AlwaysConflictEmptyDbContext : KeyCustodianTestDbContext
    {
        private readonly Func<Exception> r_throw;
        private int _clears;

        private AlwaysConflictEmptyDbContext(
            DbContextOptions<KeyCustodianTestDbContext> options,
            Func<Exception> throwOnSave)
            : base(options)
        {
            r_throw = throwOnSave;
        }

        public int ClearCallCount => Volatile.Read(ref _clears);

        public static AlwaysConflictEmptyDbContext Create(Func<Exception> throwOnSave)
        {
            var opts = new DbContextOptionsBuilder<KeyCustodianTestDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
                .EnableServiceProviderCaching(false)
                .Options;

            return new AlwaysConflictEmptyDbContext(opts, throwOnSave);
        }

        public override void ClearChangeTracker()
        {
            Interlocked.Increment(ref _clears);
            base.ClearChangeTracker();
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            throw r_throw();
    }
}
