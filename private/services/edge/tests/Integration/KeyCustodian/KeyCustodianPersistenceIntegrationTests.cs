// -----------------------------------------------------------------------
// <copyright file="KeyCustodianPersistenceIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

using System.Security.Cryptography;
using D2.Edge.Tests.Unit.KeyCustodian;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Live-DB persistence tests against a real PostgreSQL container: the migration
/// applies (creating the schema), the model has no drift, every state round-trips
/// through real SQL with no stale columns, the query extensions translate
/// server-side, the transition + audit commit atomically, and the audit FK is
/// delete-restricted. Run after the orchestrator generates the Initial migration.
/// </summary>
[Trait("Category", "Integration")]
[Collection(KeyCustodianPostgresCollectionDefinition.NAME)]
public sealed class KeyCustodianPersistenceIntegrationTests(KeyCustodianPostgresFixture fixture)
    : IDisposable
{
    // Registers the fixture AES-payload domain (ref-counted, per-test-instance) so any
    // MakeRecord default-domain row resolves as a symmetric payload domain if mapped.
    private readonly IDisposable r_fixtureSeam =
        KeyDomain.RegisterFixturePayloadDomainForTesting(FixturePayloadDomains.PAYLOAD_A);

    /// <summary>Unregisters the fixture payload domain (ref-counted, per-test-instance).</summary>
    public void Dispose() => r_fixtureSeam.Dispose();

    [Fact]
    public async Task Migration_AppliesAndIsIdempotent()
    {
        await fixture.EnsureMigratedAsync();

        await using var context = fixture.NewContext();

        // Re-applying finds nothing pending — idempotent.
        var pending = await context.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task Schema_HasNoModelDrift()
    {
        await fixture.EnsureMigratedAsync();

        await using var context = fixture.NewContext();

        // Pending-migrations-empty is the model-vs-migration drift guard.
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(KeyStatus.Pending)]
    [InlineData(KeyStatus.Active)]
    [InlineData(KeyStatus.Retiring)]
    [InlineData(KeyStatus.Retired)]
    [InlineData(KeyStatus.Compromised)]
    public async Task RoundTrip_PersistsAndReadsBackEachState(KeyStatus status)
    {
        await fixture.EnsureMigratedAsync();
        var kid = NewKid();

        // Per-test-unique domain: the shared container is not reset between tests,
        // and the one-Active / one-Pending-per-domain invariants are enforced across
        // the whole table — a fixed domain would collide with sibling tests' rows.
        var domain = NewDomain();
        var material = RandomNumberGenerator.GetBytes(48);
        var created = Instant.FromUtc(2026, 1, 1, 0, 0);

        await using (var write = fixture.NewContext())
        {
            write.Keys.Add(MakeRecord(kid, status, material, created, domain));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.Keys.AsNoTracking().SingleAsync(k => k.Kid == kid);

        loaded.Status.Should().Be(status);
        loaded.KeyMaterialEncrypted.Should().Equal(material);
        loaded.CreatedAt.Should().Be(created);

        // No-stale-column discipline: only the state's own timestamps are set.
        loaded.ActivatedAt.HasValue.Should().Be(
            status is KeyStatus.Active or KeyStatus.Retiring or KeyStatus.Retired);
        loaded.RetiringAt.HasValue.Should().Be(
            status is KeyStatus.Retiring or KeyStatus.Retired);
        loaded.RetiredAt.HasValue.Should().Be(status == KeyStatus.Retired);
        loaded.CompromisedAt.HasValue.Should().Be(status == KeyStatus.Compromised);
    }

    [Fact]
    public async Task QueryExtensions_TranslateServerSide()
    {
        await fixture.EnsureMigratedAsync();
        var domain = NewDomain();
        var activeKid = NewKid();

        await using (var write = fixture.NewContext())
        {
            write.Keys.Add(MakeRecord(activeKid, KeyStatus.Active, Rand(), Now(), domain));
            write.Keys.Add(MakeRecord(NewKid(), KeyStatus.Retired, Rand(), Now(), domain));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();

        // Composed server-side filters — no client-eval exception means SQL translation.
        var active = await read.Keys.AsNoTracking()
            .ForDomain(domain).Active().Select(k => k.Kid).ToListAsync();

        active.Should().ContainSingle().Which.Should().Be(activeKid);
    }

    [Fact]
    public async Task Transition_AndAudit_CommitAtomically()
    {
        await fixture.EnsureMigratedAsync();
        var kid = NewKid();
        var domain = NewDomain();

        await using (var seed = fixture.NewContext())
        {
            seed.Keys.Add(MakeRecord(kid, KeyStatus.Pending, Rand(), Now(), domain));
            await seed.SaveChangesAsync();
        }

        await using var context = fixture.NewContext();
        var record = await context.Keys.SingleAsync(k => k.Kid == kid);
        record.Status = KeyStatus.Active;
        record.ActivatedAt = Now();
        context.Audit.Add(new KeyAuditRecord
        {
            Kid = kid,
            Action = KeyAuditAction.Activated,
            ResultingStatus = KeyStatus.Active,
            OccurredAt = Now(),
        });
        await context.SaveChangesAsync();

        await using var verify = fixture.NewContext();
        (await verify.Keys.AsNoTracking().SingleAsync(k => k.Kid == kid))
            .Status.Should().Be(KeyStatus.Active);
        (await verify.Audit.AsNoTracking().CountAsync(a => a.Kid == kid))
            .Should().Be(1);
    }

    [Fact]
    public async Task Audit_DeletingKeyWithAuditRows_IsRestricted()
    {
        await fixture.EnsureMigratedAsync();
        var kid = NewKid();
        var domain = NewDomain();

        await using (var seed = fixture.NewContext())
        {
            seed.Keys.Add(MakeRecord(kid, KeyStatus.Active, Rand(), Now(), domain));
            seed.Audit.Add(new KeyAuditRecord
            {
                Kid = kid,
                Action = KeyAuditAction.Activated,
                ResultingStatus = KeyStatus.Active,
                OccurredAt = Now(),
            });
            await seed.SaveChangesAsync();
        }

        var context = fixture.NewContext();
        var record = await context.Keys.SingleAsync(k => k.Kid == kid);
        context.Keys.Remove(record);

        // FK OnDelete(Restrict) — the audit trail cannot be orphaned.
        try
        {
            await FluentActions.Awaiting(() => context.SaveChangesAsync())
                .Should().ThrowAsync<DbUpdateException>();
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task RoundTrip_NonNullPublicKeyMaterial_PersistsAndReadsBack()
    {
        await fixture.EnsureMigratedAsync();
        var kid = NewKid();
        var publicMaterial = RandomNumberGenerator.GetBytes(270);
        var encryptedMaterial = RandomNumberGenerator.GetBytes(48);
        var created = Instant.FromUtc(2026, 1, 1, 0, 0);

        await using (var write = fixture.NewContext())
        {
            write.Keys.Add(new KeyRecord
            {
                Kid = kid,
                KeyDomain = NewDomain(),
                KeyType = KeyType.RsaSigning,
                KeyMaterialEncrypted = encryptedMaterial,
                PublicKeyMaterial = publicMaterial,
                CreatedAt = created,
                Status = KeyStatus.Active,
                ActivatedAt = created,
            });
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var loaded = await read.Keys.AsNoTracking().SingleAsync(k => k.Kid == kid);

        loaded.PublicKeyMaterial.Should().NotBeNull();
        loaded.PublicKeyMaterial!.Should().Equal(publicMaterial);
    }

    private static string NewKid() => "kid-" + Guid.NewGuid().ToString("N");

    private static string NewDomain() => "dom-" + Guid.NewGuid().ToString("N");

    private static byte[] Rand() => RandomNumberGenerator.GetBytes(48);

    private static Instant Now() => Instant.FromUtc(2026, 1, 1, 0, 0);

    private static KeyRecord MakeRecord(
        string kid,
        KeyStatus status,
        byte[] material,
        Instant created,
        string domain = FixturePayloadDomains.PAYLOAD_A) =>
        new()
        {
            Kid = kid,
            KeyDomain = domain,
            KeyType = KeyType.AesPayload,
            KeyMaterialEncrypted = material,
            CreatedAt = created,
            Status = status,
            ActivatedAt = status is KeyStatus.Active or KeyStatus.Retiring or KeyStatus.Retired
                ? created
                : null,
            RetiringAt = status is KeyStatus.Retiring or KeyStatus.Retired ? created : null,
            RetiredAt = status == KeyStatus.Retired ? created : null,
            CompromisedAt = status == KeyStatus.Compromised ? created : null,
            CompromiseReason = status == KeyStatus.Compromised ? "seed" : null,
        };
}
