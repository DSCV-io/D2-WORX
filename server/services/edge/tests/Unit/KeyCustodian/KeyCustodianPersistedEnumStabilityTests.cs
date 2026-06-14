// -----------------------------------------------------------------------
// <copyright file="KeyCustodianPersistedEnumStabilityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Pins the persisted string names of <see cref="KeyStatus"/> and
/// <see cref="KeyAuditAction"/>. Both enums are stored via EF
/// <c>HasConversion&lt;string&gt;()</c> — reordering ordinals is harmless, but
/// renaming a member silently rewrites existing DB rows. These tests are the
/// rename gate (§1.18 per-VALUE pin; NOT ordinal stability).
/// </summary>
public sealed class KeyCustodianPersistedEnumStabilityTests
{
    [Fact]
    public void KeyStatus_Enum_HasExactlyFiveMembers()
    {
        const int expected_count = 5;

        Enum.GetNames<KeyStatus>().Should().HaveCount(expected_count);
    }

    [Theory]
    [InlineData(KeyStatus.Pending, "Pending")]
    [InlineData(KeyStatus.Active, "Active")]
    [InlineData(KeyStatus.Retiring, "Retiring")]
    [InlineData(KeyStatus.Retired, "Retired")]
    [InlineData(KeyStatus.Compromised, "Compromised")]
    public void KeyStatus_MemberName_EqualsPersistedStringName(
        KeyStatus status, string expectedPersistedName)
    {
        status.ToString().Should().Be(expectedPersistedName);
        Enum.GetName(status).Should().Be(expectedPersistedName);
    }

    [Fact]
    public void KeyAuditAction_Enum_HasExactlyFiveMembers()
    {
        const int expected_count = 5;

        Enum.GetNames<KeyAuditAction>().Should().HaveCount(expected_count);
    }

    [Theory]
    [InlineData(KeyAuditAction.Generated, "Generated")]
    [InlineData(KeyAuditAction.Activated, "Activated")]
    [InlineData(KeyAuditAction.Rotated, "Rotated")]
    [InlineData(KeyAuditAction.Retired, "Retired")]
    [InlineData(KeyAuditAction.Compromised, "Compromised")]
    public void KeyAuditAction_MemberName_EqualsPersistedStringName(
        KeyAuditAction action, string expectedPersistedName)
    {
        action.ToString().Should().Be(expectedPersistedName);
        Enum.GetName(action).Should().Be(expectedPersistedName);
    }
}
