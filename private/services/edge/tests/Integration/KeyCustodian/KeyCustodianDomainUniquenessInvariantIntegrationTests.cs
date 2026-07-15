// -----------------------------------------------------------------------
// <copyright file="KeyCustodianDomainUniquenessInvariantIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Integration.KeyCustodian;

using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.Handler.Repo.Postgres;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Live-DB tests for the schema-enforced per-domain key invariants against a real
/// PostgreSQL container: at most ONE Pending and at most ONE Active key per domain.
/// The Pending invariant is a partial UNIQUE index; the Active invariant is a
/// partial DEFERRABLE EXCLUSION constraint (added in raw SQL by the
/// OnePendingIndexAndActiveExclusion migration, since EF's fluent API cannot model
/// EXCLUDE). Proves both invariants hold, both single-save swaps that legitimately
/// touch two rows of one domain succeed (RotateKey's Active swap needs deferral;
/// CompromiseKey's Pending swap needs EF's release-before-acquire ordering), and the
/// invariants are per-domain. Uses per-test-unique domains because the shared
/// container is not reset between tests and the constraints span the whole table.
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianDomainUniquenessInvariantIntegrationTests(
    KeyCustodianPostgresFixture fixture)
{
    [Fact]
    public async Task SecondPendingForSameDomain_RejectedByUniqueIndex_LeavesExactlyOnePending()
    {
        await fixture.EnsureMigratedAsync();
        var domain = NewDomain();

        await using (var seed = fixture.NewContext())
        {
            seed.Keys.Add(MakeRecord(NewKid(), KeyStatus.Pending, domain));
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.NewContext();
        ctx.Keys.Add(MakeRecord(NewKid(), KeyStatus.Pending, domain));

        // Capture via try/catch (not a closure) so the await-using context is never
        // captured into a lambda that would outlive it.
        DbUpdateException? thrown = null;

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            thrown = ex;
        }

        thrown.Should().NotBeNull("the duplicate Pending insert must be rejected");

        // The partial UNIQUE index raises unique_violation (23505), which the shared
        // repo pipeline classifies to a typed 409 conflict.
        var pg = PgErrorCodes.TryGetPgException(thrown);
        pg.Should().NotBeNull();
        pg.SqlState.Should().Be("23505");

        var classifier = new PostgresDbExceptionClassifier();
        classifier.Classify(thrown).Should().Be(DbFailureKind.UniqueViolation);

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Pending))
            .Should().Be(1, "exactly one Pending survives the rejected duplicate");
    }

    [Fact]
    public async Task SecondActiveForSameDomain_RejectedByExclusion_LeavesExactlyOneActive()
    {
        await fixture.EnsureMigratedAsync();
        var domain = NewDomain();

        await using (var seed = fixture.NewContext())
        {
            seed.Keys.Add(MakeRecord(NewKid(), KeyStatus.Active, domain));
            await seed.SaveChangesAsync();
        }

        await using var ctx = fixture.NewContext();
        ctx.Keys.Add(MakeRecord(NewKid(), KeyStatus.Active, domain));

        // Capture via try/catch (not a closure) so the await-using context is never
        // captured into a lambda that would outlive it.
        DbUpdateException? thrown = null;

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            thrown = ex;
        }

        thrown.Should().NotBeNull("the duplicate Active insert must be rejected at commit");

        // The partial DEFERRABLE EXCLUSION constraint raises exclusion_violation
        // (23P01) — distinct from a unique index's 23505 — checked at COMMIT.
        var pg = PgErrorCodes.TryGetPgException(thrown);
        pg.Should().NotBeNull();
        pg.SqlState.Should().Be("23P01");

        // The shared repo pipeline classifies 23P01 to the SAME typed conflict as
        // a unique-index 23505 (DbFailureKind.UniqueViolation → 409), NOT an
        // unclassified null that would surface as UnhandledException (500).
        var classifier = new PostgresDbExceptionClassifier();
        classifier.Classify(thrown).Should().Be(DbFailureKind.UniqueViolation);

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Active))
            .Should().Be(1, "exactly one Active survives the rejected duplicate");
    }

    [Fact]
    public async Task ActiveSwap_ReleaseAndAcquireInOneSave_Succeeds()
    {
        await fixture.EnsureMigratedAsync();
        var domain = NewDomain();
        var incumbentKid = NewKid();
        var successorKid = NewKid();

        // One Active incumbent + one Pending successor for the domain.
        await using (var seed = fixture.NewContext())
        {
            seed.Keys.Add(MakeRecord(incumbentKid, KeyStatus.Active, domain));
            seed.Keys.Add(MakeRecord(successorKid, KeyStatus.Pending, domain));
            await seed.SaveChangesAsync();
        }

        // Active -> Retiring AND Pending -> Active in ONE SaveChanges. The DEFERRABLE
        // EXCLUSION constraint tolerates the transient two-Active state until COMMIT,
        // so the swap succeeds regardless of the order EF emits the two UPDATEs (a
        // non-deferrable partial index would reject the mid-transaction collision —
        // the exact failure that drove the deferrable-EXCLUSION design).
        await using (var swap = fixture.NewContext())
        {
            var incumbent = await swap.Keys.SingleAsync(k => k.Kid == incumbentKid);
            var successor = await swap.Keys.SingleAsync(k => k.Kid == successorKid);

            incumbent.Status = KeyStatus.Retiring;
            incumbent.RetiringAt = Now();
            successor.Status = KeyStatus.Active;
            successor.ActivatedAt = Now();

            await swap.SaveChangesAsync();
        }

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Active))
            .Should().Be(1, "the successor is Active immediately after the swap");
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Retiring))
            .Should().Be(1, "the incumbent is Retiring immediately after the swap");
    }

    [Fact]
    public async Task PendingSwap_ReleaseAndInsertInOneSave_Succeeds()
    {
        await fixture.EnsureMigratedAsync();
        var domain = NewDomain();
        var originalKid = NewKid();

        await using (var seed = fixture.NewContext())
        {
            seed.Keys.Add(MakeRecord(originalKid, KeyStatus.Pending, domain));
            await seed.SaveChangesAsync();
        }

        // Pending -> Compromised (release) AND insert a fresh Pending (acquire) in ONE
        // SaveChanges — the CompromiseKey-with-replacement shape. Because the Pending
        // uniqueness is a real EF-modeled index, EF's command-batch preparer emits
        // the releasing UPDATE before the acquiring INSERT, so the swap succeeds
        // against the immediate (non-deferred) partial unique index.
        await using (var swap = fixture.NewContext())
        {
            var original = await swap.Keys.SingleAsync(k => k.Kid == originalKid);

            original.Status = KeyStatus.Compromised;
            original.CompromisedAt = Now();
            original.CompromiseReason = "invariant-test";
            swap.Keys.Add(MakeRecord(NewKid(), KeyStatus.Pending, domain));

            await swap.SaveChangesAsync();
        }

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Pending))
            .Should().Be(1, "the replacement Pending survives the release+insert swap");
        (await verify.Keys.AsNoTracking()
                .CountAsync(k => k.KeyDomain == domain && k.Status == KeyStatus.Compromised))
            .Should().Be(1, "the original key is Compromised after the swap");
    }

    [Fact]
    public async Task PendingAndActive_AcrossDifferentDomains_Coexist()
    {
        await fixture.EnsureMigratedAsync();
        var domainA = NewDomain();
        var domainB = NewDomain();

        // Both invariants are per-domain: two Pendings and two Actives across two
        // domains coexist with no collision.
        await using (var seed = fixture.NewContext())
        {
            seed.Keys.Add(MakeRecord(NewKid(), KeyStatus.Pending, domainA));
            seed.Keys.Add(MakeRecord(NewKid(), KeyStatus.Pending, domainB));
            seed.Keys.Add(MakeRecord(NewKid(), KeyStatus.Active, domainA));
            seed.Keys.Add(MakeRecord(NewKid(), KeyStatus.Active, domainB));
            await seed.SaveChangesAsync();
        }

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking()
                .CountAsync(k =>
                    (k.KeyDomain == domainA || k.KeyDomain == domainB)
                    && k.Status == KeyStatus.Pending))
            .Should().Be(2, "one Pending per domain across two domains");
        (await verify.Keys.AsNoTracking()
                .CountAsync(k =>
                    (k.KeyDomain == domainA || k.KeyDomain == domainB)
                    && k.Status == KeyStatus.Active))
            .Should().Be(2, "one Active per domain across two domains");
    }

    private static string NewKid() => "kid-" + Guid.NewGuid().ToString("N");

    private static string NewDomain() => "dom-" + Guid.NewGuid().ToString("N");

    private static Instant Now() => Instant.FromUtc(2026, 1, 1, 0, 0);

    private static KeyRecord MakeRecord(string kid, KeyStatus status, string domain) => new()
    {
        Kid = kid,
        KeyDomain = domain,
        KeyType = KeyType.AesPayload,
        KeyMaterialEncrypted = RandomNumberGenerator.GetBytes(48),
        CreatedAt = Now(),
        Status = status,
        ActivatedAt = status is KeyStatus.Active or KeyStatus.Retiring ? Now() : null,
        RetiringAt = status == KeyStatus.Retiring ? Now() : null,
    };
}
