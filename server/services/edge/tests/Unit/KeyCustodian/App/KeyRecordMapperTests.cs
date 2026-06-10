// -----------------------------------------------------------------------
// <copyright file="KeyRecordMapperTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Persistence;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Keys;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using NodaTime;
using Xunit;

/// <summary>
/// Round-trip + anti-stale-column + corrupt-row tests for
/// <see cref="KeyRecordMapper"/>. Every state maps to its sealed domain type and
/// back to an identical record; <c>ProjectOnto</c> nulls every stale per-state
/// column; a structurally corrupt row throws the trusted-store-corruption
/// exception.
/// </summary>
public sealed class KeyRecordMapperTests
{
    private static readonly Instant sr_created = Instant.FromUtc(2026, 1, 1, 0, 0);
    private static readonly Instant sr_activated = Instant.FromUtc(2026, 1, 2, 0, 0);
    private static readonly Instant sr_retiring = Instant.FromUtc(2026, 1, 3, 0, 0);
    private static readonly Instant sr_retired = Instant.FromUtc(2026, 1, 4, 0, 0);
    private static readonly Instant sr_compromised = Instant.FromUtc(2026, 1, 5, 0, 0);

    // -----------------------------------------------------------------------
    // ToDomain — each status maps to the right sealed state
    // -----------------------------------------------------------------------

    [Fact]
    public void ToDomain_Pending_ReturnsPendingKey()
    {
        var record = Sign8(KeyStatus.Pending);
        record.ToDomain().Should().BeOfType<PendingKey>();
    }

    [Fact]
    public void ToDomain_Active_ReturnsActiveKeyWithActivatedAt()
    {
        var record = Sign8(KeyStatus.Active);
        record.ActivatedAt = sr_activated;

        var key = record.ToDomain().Should().BeOfType<ActiveKey>().Subject;
        key.ActivatedAt.Should().Be(sr_activated);
    }

    [Fact]
    public void ToDomain_Retiring_ReturnsRetiringKey()
    {
        var record = Sign8(KeyStatus.Retiring);
        record.ActivatedAt = sr_activated;
        record.RetiringAt = sr_retiring;

        var key = record.ToDomain().Should().BeOfType<RetiringKey>().Subject;
        key.RetiringAt.Should().Be(sr_retiring);
    }

    [Fact]
    public void ToDomain_Retired_ReturnsRetiredKey()
    {
        var record = Sign8(KeyStatus.Retired);
        record.ActivatedAt = sr_activated;
        record.RetiringAt = sr_retiring;
        record.RetiredAt = sr_retired;

        var key = record.ToDomain().Should().BeOfType<RetiredKey>().Subject;
        key.RetiredAt.Should().Be(sr_retired);
    }

    [Fact]
    public void ToDomain_Compromised_ReturnsCompromisedKeyWithReason()
    {
        var record = Sign8(KeyStatus.Compromised);
        record.CompromisedAt = sr_compromised;
        record.CompromiseReason = "operator-initiated";

        var key = record.ToDomain().Should().BeOfType<CompromisedKey>().Subject;
        key.CompromisedAt.Should().Be(sr_compromised);
        key.Reason.Should().Be("operator-initiated");
    }

    // -----------------------------------------------------------------------
    // Round-trip — domain → ToNewRecord → ToDomain → field equality (all states)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(KeyStatus.Pending)]
    [InlineData(KeyStatus.Active)]
    [InlineData(KeyStatus.Retiring)]
    [InlineData(KeyStatus.Retired)]
    [InlineData(KeyStatus.Compromised)]
    public void RoundTrip_DomainToRecordToDomain_PreservesIdentityAndState(KeyStatus status)
    {
        var original = BuildDomain(status);
        var record = original.ToNewRecord();
        var rehydrated = record.ToDomain();

        rehydrated.Status.Should().Be(status);
        rehydrated.Kid.Value.Should().Be(original.Kid.Value);
        rehydrated.KeyDomain.Value.Should().Be(original.KeyDomain.Value);
        rehydrated.KeyType.Should().Be(original.KeyType);
        rehydrated.CreatedAt.Should().Be(original.CreatedAt);
        rehydrated.KeyMaterialEncrypted.Bytes.ToArray()
            .Should().Equal(original.KeyMaterialEncrypted.Bytes.ToArray());
        rehydrated.Should().BeEquivalentTo(
            original,
            o => o
                .Using<KeyMaterialEncrypted>(ctx => ctx.Subject.Should().Be(ctx.Expectation))
                .WhenTypeIs<KeyMaterialEncrypted>());
    }

    // -----------------------------------------------------------------------
    // ProjectOnto — every stale per-state column is nulled on transition
    // -----------------------------------------------------------------------

    [Fact]
    public void ProjectOnto_CompromisingAnActiveKey_NullsActivatedAt()
    {
        // Start as an Active record (ActivatedAt set), then project a Compromised
        // aggregate onto it — ActivatedAt MUST be nulled (anti-stale-column).
        var record = Sign8(KeyStatus.Active);
        record.ActivatedAt = sr_activated;

        var compromised = BuildCompromised();
        compromised.ProjectOnto(record);

        record.Status.Should().Be(KeyStatus.Compromised);
        record.ActivatedAt.Should().BeNull(because: "a compromised key carries no ActivatedAt");
        record.RetiringAt.Should().BeNull();
        record.RetiredAt.Should().BeNull();
        record.CompromisedAt.Should().Be(sr_compromised);
        record.CompromiseReason.Should().Be("operator-initiated");
    }

    [Fact]
    public void ProjectOnto_RetiringToRetired_PreservesActivatedAndRetiringAndSetsRetired()
    {
        var record = Sign8(KeyStatus.Retiring);
        record.ActivatedAt = sr_activated;
        record.RetiringAt = sr_retiring;

        var retired = BuildRetired();
        retired.ProjectOnto(record);

        record.Status.Should().Be(KeyStatus.Retired);
        record.ActivatedAt.Should().Be(sr_activated);
        record.RetiringAt.Should().Be(sr_retiring);
        record.RetiredAt.Should().Be(sr_retired);
        record.CompromisedAt.Should().BeNull();
        record.CompromiseReason.Should().BeNull();
    }

    [Fact]
    public void ProjectOnto_PendingToActive_PrePoisonedStaleColumnsAreCleared()
    {
        // Adversarial: pre-poison EVERY per-state column, then project an Active
        // aggregate — only ActivatedAt may survive.
        var record = Sign8(KeyStatus.Pending);
        record.ActivatedAt = sr_activated;
        record.RetiringAt = sr_retiring;
        record.RetiredAt = sr_retired;
        record.CompromisedAt = sr_compromised;
        record.CompromiseReason = "stale";

        var active = BuildActive();
        active.ProjectOnto(record);

        record.Status.Should().Be(KeyStatus.Active);
        record.ActivatedAt.Should().Be(active.ActivatedAt);
        record.RetiringAt.Should().BeNull();
        record.RetiredAt.Should().BeNull();
        record.CompromisedAt.Should().BeNull();
        record.CompromiseReason.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Corrupt rows — trusted-store-corruption carve-out throws
    // -----------------------------------------------------------------------

    [Fact]
    public void ToDomain_ActiveStatusWithNullActivatedAt_Throws()
    {
        var record = Sign8(KeyStatus.Active);
        record.ActivatedAt = null;

        var act = () => record.ToDomain();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToDomain_CompromisedStatusWithNullReason_Throws()
    {
        var record = Sign8(KeyStatus.Compromised);
        record.CompromisedAt = sr_compromised;
        record.CompromiseReason = null;

        var act = () => record.ToDomain();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToDomain_UndefinedStatusValue_Throws()
    {
        var record = Sign8(KeyStatus.Pending);
        record.Status = (KeyStatus)999;

        var act = () => record.ToDomain();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToDomain_RetiringStatusWithNullRetiringAt_Throws()
    {
        var record = Sign8(KeyStatus.Retiring);
        record.ActivatedAt = sr_activated;
        record.RetiringAt = null;

        var act = () => record.ToDomain();
        act.Should().Throw<InvalidOperationException>();
    }

    // -----------------------------------------------------------------------
    // Audit mapping
    // -----------------------------------------------------------------------

    [Fact]
    public void AuditToRecord_FlattensKidAndCopiesFields()
    {
        var audit = D2.Edge.KeyCustodian.Domain.Audit.EncryptionKeyAudit.Record(
            Kid.FromTrusted("kid-1"),
            D2.Edge.KeyCustodian.Domain.Audit.KeyAuditAction.Generated,
            KeyStatus.Pending,
            new D2.Shared.Time.TestClock(sr_created),
            detail: "note");

        var record = audit.ToRecord();

        record.Kid.Should().Be("kid-1");
        record.Action.Should().Be(D2.Edge.KeyCustodian.Domain.Audit.KeyAuditAction.Generated);
        record.ResultingStatus.Should().Be(KeyStatus.Pending);
        record.OccurredAt.Should().Be(sr_created);
        record.Detail.Should().Be("note");
    }

    // -----------------------------------------------------------------------
    // Helpers — build a symmetric (Secret) key in each state
    // -----------------------------------------------------------------------

    private static KeyRecord Sign8(KeyStatus status) =>
        new()
        {
            Kid = "kid-symmetric",
            KeyDomain = KeyDomain.COOKIE,
            KeyType = KeyType.Secret,
            KeyMaterialEncrypted = [1, 2, 3, 4],
            PublicKeyMaterial = null,
            CreatedAt = sr_created,
            Status = status,
        };

    private static EncryptionKey BuildDomain(KeyStatus status) => status switch
    {
        KeyStatus.Pending => BuildPending(),
        KeyStatus.Active => BuildActive(),
        KeyStatus.Retiring => BuildRetiring(),
        KeyStatus.Retired => BuildRetired(),
        KeyStatus.Compromised => BuildCompromised(),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static PendingKey BuildPending() => new()
    {
        Kid = Kid.FromTrusted("kid-symmetric"),
        KeyDomain = KeyDomain.Cookie,
        KeyType = KeyType.Secret,
        KeyMaterialEncrypted = KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 }),
        PublicKeyMaterial = null,
        CreatedAt = sr_created,
    };

    private static ActiveKey BuildActive() => new()
    {
        Kid = Kid.FromTrusted("kid-symmetric"),
        KeyDomain = KeyDomain.Cookie,
        KeyType = KeyType.Secret,
        KeyMaterialEncrypted = KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 }),
        PublicKeyMaterial = null,
        CreatedAt = sr_created,
        ActivatedAt = sr_activated,
    };

    private static RetiringKey BuildRetiring() => new()
    {
        Kid = Kid.FromTrusted("kid-symmetric"),
        KeyDomain = KeyDomain.Cookie,
        KeyType = KeyType.Secret,
        KeyMaterialEncrypted = KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 }),
        PublicKeyMaterial = null,
        CreatedAt = sr_created,
        ActivatedAt = sr_activated,
        RetiringAt = sr_retiring,
    };

    private static RetiredKey BuildRetired() => new()
    {
        Kid = Kid.FromTrusted("kid-symmetric"),
        KeyDomain = KeyDomain.Cookie,
        KeyType = KeyType.Secret,
        KeyMaterialEncrypted = KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 }),
        PublicKeyMaterial = null,
        CreatedAt = sr_created,
        ActivatedAt = sr_activated,
        RetiringAt = sr_retiring,
        RetiredAt = sr_retired,
    };

    private static CompromisedKey BuildCompromised() => new()
    {
        Kid = Kid.FromTrusted("kid-symmetric"),
        KeyDomain = KeyDomain.Cookie,
        KeyType = KeyType.Secret,
        KeyMaterialEncrypted = KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 }),
        PublicKeyMaterial = null,
        CreatedAt = sr_created,
        CompromisedAt = sr_compromised,
        Reason = "operator-initiated",
    };
}
