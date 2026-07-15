// -----------------------------------------------------------------------
// <copyright file="KeyCustodianPersistedEnumStabilityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Pins the persisted string names of <see cref="KeyStatus"/>,
/// <see cref="KeyAuditAction"/>, and <see cref="KeyType"/>. All three enums are
/// stored via EF <c>HasConversion&lt;string&gt;()</c> — reordering ordinals is
/// harmless, but renaming a member silently rewrites existing DB rows. These tests
/// are the rename gate (§1.18 per-VALUE pin; NOT ordinal stability).
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

    // =========================================================================
    // KeyType — persisted by string name via HasConversion<string>()
    // =========================================================================

    [Fact]
    public void KeyType_Enum_HasExactlyFiveMembers()
    {
        const int expected_count = 5;

        Enum.GetNames<KeyType>().Should().HaveCount(expected_count);
    }

    [Theory]
    [InlineData(KeyType.RsaSigning, "RsaSigning")]
    [InlineData(KeyType.AesPayload, "AesPayload")]
    [InlineData(KeyType.Secret, "Secret")]
    [InlineData(KeyType.X509CaCertificate, "X509CaCertificate")]
    [InlineData(KeyType.EcdhSealing, "EcdhSealing")]
    public void KeyType_MemberName_EqualsPersistedStringName(
        KeyType keyType, string expectedPersistedName)
    {
        keyType.ToString().Should().Be(expectedPersistedName);
        Enum.GetName(keyType).Should().Be(expectedPersistedName);
    }
}
