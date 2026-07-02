// -----------------------------------------------------------------------
// <copyright file="KeyCustodianConcurrencyIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using System.Security.Cryptography;
using D2.Shared.EntityFrameworkCore.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

/// <summary>
/// Live-DB concurrency + timeout tests: the <c>xmin</c> token gives exactly-one-
/// winner on a concurrent transition, the rotation advisory lock (the GENERATED
/// <see cref="AdvisoryLocks.KeycustodianDb.ROTATION"/> key) is skip-if-held, and an
/// over-long query is classified as a timeout rather than hanging. Run after the
/// orchestrator generates the Initial migration.
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianConcurrencyIntegrationTests(KeyCustodianPostgresFixture fixture)
{
    [Fact]
    public async Task Xmin_ConcurrentTransition_ExactlyOneWins()
    {
        await fixture.EnsureMigratedAsync();
        var kid = NewKid();

        await using (var seed = fixture.NewContext())
        {
            seed.Keys.Add(MakePending(kid));
            await seed.SaveChangesAsync();
        }

        // Two contexts load the same row, both transition, both save.
        await using var ctxA = fixture.NewContext();
        var ctxB = fixture.NewContext();
        var rowA = await ctxA.Keys.SingleAsync(k => k.Kid == kid);
        var rowB = await ctxB.Keys.SingleAsync(k => k.Kid == kid);

        rowA.Status = KeyStatus.Active;
        rowA.ActivatedAt = Now();
        rowB.Status = KeyStatus.Compromised;
        rowB.CompromisedAt = Now();
        rowB.CompromiseReason = "race";

        await ctxA.SaveChangesAsync();

        // The second writer's xmin no longer matches — it loses.
        try
        {
            await FluentActions.Awaiting(() => ctxB.SaveChangesAsync())
                .Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
        finally
        {
            await ctxB.DisposeAsync();
        }
    }

    [Fact]
    public async Task RotationAdvisoryLock_SkipsWhenHeld()
    {
        await fixture.EnsureMigratedAsync();

        await using var first = await PgAdvisoryLock.TryAcquireSessionAsync(
            fixture.ConnectionString, AdvisoryLocks.KeycustodianDb.ROTATION);
        first.IsHeld.Should().BeTrue();

        // A second attempt at the same rotation key on a different session is skipped.
        await using var second = await PgAdvisoryLock.TryAcquireSessionAsync(
            fixture.ConnectionString, AdvisoryLocks.KeycustodianDb.ROTATION);
        second.IsHeld.Should().BeFalse();
    }

    [Fact]
    public async Task RotationAdvisoryLock_ReleasedOnDispose_NextAcquireSucceeds()
    {
        await fixture.EnsureMigratedAsync();

        await using (var first = await PgAdvisoryLock.TryAcquireSessionAsync(
            fixture.ConnectionString, AdvisoryLocks.KeycustodianDb.ROTATION))
        {
            first.IsHeld.Should().BeTrue();
        }

        await using var second = await PgAdvisoryLock.TryAcquireSessionAsync(
            fixture.ConnectionString, AdvisoryLocks.KeycustodianDb.ROTATION);
        second.IsHeld.Should().BeTrue();
    }

    [Fact]
    public async Task CommandTimeout_OverLongQuery_TimesOutNotHangs()
    {
        await fixture.EnsureMigratedAsync();

        await using var context = fixture.NewContextWithTimeout(commandTimeoutSeconds: 1);

        // pg_sleep(5) under a 1s command timeout must throw, not block indefinitely.
        var db = context.Database;
        await FluentActions.Awaiting(() => db.ExecuteSqlRawAsync("SELECT pg_sleep(5)"))
            .Should().ThrowAsync<NpgsqlException>();
    }

    private static string NewKid() => "kid-" + Guid.NewGuid().ToString("N");

    // Per-test-unique domain: the shared container is not reset between tests, and
    // the one-Active / one-Pending-per-domain invariants hold across the whole
    // table — a fixed domain would collide with sibling tests' rows.
    private static string NewDomain() => "dom-" + Guid.NewGuid().ToString("N");

    private static Instant Now() => Instant.FromUtc(2026, 1, 1, 0, 0);

    private static KeyRecord MakePending(string kid) => new()
    {
        Kid = kid,
        KeyDomain = NewDomain(),
        KeyType = KeyType.AesPayload,
        KeyMaterialEncrypted = RandomNumberGenerator.GetBytes(48),
        CreatedAt = Now(),
        Status = KeyStatus.Pending,
    };
}
