// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyAuditTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using D2.Edge.KeyCustodian.Domain.Entities;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Time;
using NodaTime;

/// <summary>
/// Adversarial unit tests for <see cref="EncryptionKeyAudit"/>.
/// </summary>
public sealed class EncryptionKeyAuditTests
{
    private static readonly Kid sr_kid = Kid.FromTrusted("test-kid-001");

    // -----------------------------------------------------------------------
    // Record — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Record_StampsOccurredAtFromClock()
    {
        var instant = Instant.FromUtc(2026, 6, 1, 12, 0, 0);
        var clock = new TestClock(instant);

        var audit = EncryptionKeyAudit.Record(
            sr_kid,
            KeyAuditAction.Generated,
            KeyStatus.Pending,
            clock);

        audit.OccurredAt.Should().Be(instant);
    }

    [Fact]
    public void Record_CarriesKidActionStatus()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0, 0));

        var audit = EncryptionKeyAudit.Record(
            sr_kid,
            KeyAuditAction.Activated,
            KeyStatus.Active,
            clock,
            "operator-initiated");

        audit.Kid.Should().Be(sr_kid);
        audit.Action.Should().Be(KeyAuditAction.Activated);
        audit.ResultingStatus.Should().Be(KeyStatus.Active);
        audit.Detail.Should().Be("operator-initiated");
    }

    [Fact]
    public void Record_NullDetail_IsAllowed()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0, 0));
        var audit = EncryptionKeyAudit.Record(
            sr_kid, KeyAuditAction.Retired, KeyStatus.Retired, clock);
        audit.Detail.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Record — null guards
    // -----------------------------------------------------------------------

    [Fact]
    public void Record_NullKid_ThrowsArgumentNullException()
    {
        var clock = new TestClock(Instant.FromUtc(2026, 6, 1, 0, 0, 0));
        var act = () => EncryptionKeyAudit.Record(
            null!, KeyAuditAction.Generated, KeyStatus.Pending, clock);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Record_NullClock_ThrowsArgumentNullException()
    {
        var act = () => EncryptionKeyAudit.Record(
            sr_kid, KeyAuditAction.Generated, KeyStatus.Pending, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // PII discipline — no key-material member
    // -----------------------------------------------------------------------

    [Fact]
    public void EncryptionKeyAudit_HasNoKeyMaterialProperty()
    {
        // Reflection pin: the audit type must NOT expose any byte[] or
        // ReadOnlyMemory<byte> property — forensics via lifecycle, not bytes.
        var matProps = typeof(EncryptionKeyAudit)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(byte[])
                || p.PropertyType == typeof(ReadOnlyMemory<byte>))
            .ToList();

        matProps.Should().BeEmpty(
            "EncryptionKeyAudit must not carry key material — forensics via audit lifecycle, "
            + "not key bytes.");
    }
}
