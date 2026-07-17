// -----------------------------------------------------------------------
// <copyright file="PgAdvisoryLockIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.SharedEntityFrameworkCorePostgres;

using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using Xunit;

/// <summary>
/// Live-DB integration tests for <see cref="PgAdvisoryLock"/>.
/// Shares the <see cref="PostgresFixture"/> container.
/// <para>
/// Verifies: TryAcquire happy-path (IsHeld=true), TryAcquire skip-if-held
/// (IsHeld=false when held by a concurrent session), blocking acquire,
/// and DisposeAsync explicit-unlock.
/// </para>
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class PgAdvisoryLockIntegrationTests
{
    private const long _TEST_KEY_TRY = 8881001L;
    private const long _TEST_KEY_BLOCK = 8881002L;
    private const long _TEST_KEY_DOUBLE_RELEASE = 8881003L;
    private const long _TEST_KEY_DROP = 8881004L;

    private readonly PostgresFixture r_fixture;

    /// <summary>
    /// Initializes a new instance of <see cref="PgAdvisoryLockIntegrationTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public PgAdvisoryLockIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    // =========================================================================
    // TryAcquireSessionAsync — no contention
    // =========================================================================

    [Fact]
    public async Task TryAcquireSessionAsync_NoContention_IsHeldIsTrue()
    {
        await using var handle = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_TRY);

        handle.IsHeld.Should().BeTrue("no competing session holds this key");
    }

    [Fact]
    public async Task TryAcquireSessionAsync_NoContention_DisposeAsync_ReleasesLock()
    {
        await using (var handle = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_TRY))
        {
            handle.IsHeld.Should().BeTrue();
        }

        // After dispose, a second acquire on the same key from a fresh session must succeed.
        await using var second = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_TRY);
        second.IsHeld.Should().BeTrue(
            "the first lock was released by DisposeAsync — second acquire must succeed");
    }

    // =========================================================================
    // TryAcquireSessionAsync — contention (skip-if-held semantics)
    // =========================================================================

    [Fact]
    public async Task TryAcquireSessionAsync_WhileLockHeld_IsHeldIsFalse()
    {
        // First session holds the lock throughout this test.
        await using var holder = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_TRY + 100);

        holder.IsHeld.Should().BeTrue();

        // Second session on a different connection — should be skipped.
        await using var contender = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_TRY + 100);

        contender.IsHeld.Should().BeFalse(
            "the key is already held by the first session — TryAcquire must skip");
    }

    // =========================================================================
    // AcquireSessionBlockingAsync — happy path
    // =========================================================================

    [Fact]
    public async Task AcquireSessionBlockingAsync_NoContention_IsHeldIsTrue()
    {
        await using var handle = await PgAdvisoryLock.AcquireSessionBlockingAsync(
            r_fixture.ConnectionString, _TEST_KEY_BLOCK);

        handle.IsHeld.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireSessionBlockingAsync_AfterDispose_SecondAcquireSucceeds()
    {
        await using (var first = await PgAdvisoryLock.AcquireSessionBlockingAsync(
            r_fixture.ConnectionString, _TEST_KEY_BLOCK))
        {
            first.IsHeld.Should().BeTrue();
        }

        await using var second = await PgAdvisoryLock.AcquireSessionBlockingAsync(
            r_fixture.ConnectionString, _TEST_KEY_BLOCK);
        second.IsHeld.Should().BeTrue(
            "first lock was released by DisposeAsync — blocking acquire must succeed");
    }

    // =========================================================================
    // TryAcquireSessionAsync — server-side auto-release on connection drop
    // =========================================================================

    [Fact]
    public async Task TryAcquireSessionAsync_ConnectionDropped_LockAutoReleased()
    {
        // Acquire without await using so we control the connection lifetime manually.
        var first = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_DROP);
        first.IsHeld.Should().BeTrue("first acquire on an uncontested key must succeed");

        // Force-drop the underlying connection via the internal test seam.
        // PostgreSQL session advisory locks are automatically released when the
        // connection is dropped — no explicit pg_advisory_unlock is needed.
        await first.ForceDropConnectionForTestAsync();

        // A second acquire on the SAME key from a fresh connection must now succeed,
        // proving the server auto-released the lock when the first connection was dropped.
        await using var second = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_DROP);
        second.IsHeld.Should().BeTrue(
            "PostgreSQL must auto-release the session advisory lock when the " +
            "holding connection is dropped — a second acquire on the same key must succeed");

        // Dispose the leaking first handle to satisfy [MustDisposeResource] (the
        // close already happened; DisposeAsync is idempotent on a closed connection).
        await first.DisposeAsync();
    }

    // =========================================================================
    // DisposeAsync idempotency
    // =========================================================================

    [Fact]
    public async Task DisposeAsync_NotHeld_DoesNotThrow()
    {
        // Acquire while lock is held by another so IsHeld=false.
        await using var holder = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_DOUBLE_RELEASE);
        holder.IsHeld.Should().BeTrue();

        var notHeld = await PgAdvisoryLock.TryAcquireSessionAsync(
            r_fixture.ConnectionString, _TEST_KEY_DOUBLE_RELEASE);
        notHeld.IsHeld.Should().BeFalse();

        // DisposeAsync on a not-held handle must complete without throwing.
        await notHeld.DisposeAsync();

        // No assertion needed — reaching here means no exception was thrown.
    }
}
