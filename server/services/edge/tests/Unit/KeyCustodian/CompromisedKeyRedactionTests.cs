// -----------------------------------------------------------------------
// <copyright file="CompromisedKeyRedactionTests.cs" company="DCSV">
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
/// Regression tests for the <see cref="CompromisedKey"/> <c>ToString</c> /
/// <c>PrintMembers</c> PII trap (S-2). The compiler-generated <c>ToString</c>
/// on a <c>sealed record</c> emits all property values; without an override the
/// operator-supplied <see cref="CompromisedKey.Reason"/> — which is marked
/// <c>[RedactData(PersonalInformation)]</c> — would appear raw in any string
/// interpolation, exception message, or assertion output.
/// </summary>
public sealed class CompromisedKeyRedactionTests
{
    private static readonly Kid sr_kid = Kid.FromTrusted("test-key-abc");
    private static readonly KeyDomain sr_domain = KeyDomain.FromTrusted("audit");
    private static readonly KeyMaterialEncrypted sr_mat = KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3, 4 });
    private static readonly Instant sr_now = Instant.FromUtc(2026, 1, 2, 0, 0, 0);

    // -----------------------------------------------------------------------
    // ToString — sentinel reason must NOT leak
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_DoesNotContainReason()
    {
        // Use a sentinel that would be unmistakably visible if leaked.
        const string sentinel_reason = "SENSITIVE-OPERATOR-REASON-7f3a9b2c";
        var compromised = MakeCompromised(sentinel_reason);

        var str = compromised.ToString();

        str.Should().NotContain(
            sentinel_reason,
            because: "the operator-supplied compromise reason is PII and must not be emitted by ToString");
    }

    [Fact]
    public void ToString_ContainsRedactionMarker()
    {
        var compromised = MakeCompromised("some sensitive reason");

        var str = compromised.ToString();

        str.Should().Contain(
            "REDACTED:PersonalInformation",
            because: "the overridden ToString must emit the redaction marker in place of the raw reason");
    }

    // -----------------------------------------------------------------------
    // ToString — non-sensitive fields ARE present (marker replaces only Reason)
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_ContainsKeyId()
    {
        var compromised = MakeCompromised("some reason");

        var str = compromised.ToString();

        // Kid is opaque but intentionally visible (JWKS, audit logs).
        str.Should().Contain("test-key-abc");
    }

    // -----------------------------------------------------------------------
    // Adversarial — reason at max length does not escape redaction
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_MaxLengthReason_DoesNotLeak()
    {
        var maxReason = new string('X', CompromisedKey.REASON_MAX);
        var compromised = MakeCompromised(maxReason);

        var str = compromised.ToString();

        str.Should().NotContain(maxReason);
        str.Should().Contain("REDACTED:PersonalInformation");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static CompromisedKey MakeCompromised(string reason)
    {
        var pending = PendingKey.Create(
            sr_kid,
            sr_domain,
            KeyType.AesPayload,
            sr_mat,
            null,
            Instant.FromUtc(2026, 1, 1, 0, 0, 0)).Data!;

        return pending.Compromise(reason, new TestClock(sr_now)).Data!;
    }
}
