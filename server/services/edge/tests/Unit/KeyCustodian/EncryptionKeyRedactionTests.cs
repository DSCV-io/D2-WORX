// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyRedactionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Verifies that the PII/secret traps in the KeyCustodian domain are properly
/// sealed.
///
/// The test project references Domain, App, and Infra via project references,
/// but none of those transitively pull in
/// <c>D2.Shared.Logging.Destructuring</c> (which lives in a logging lib not
/// referenced by the key-custodian stack). The policy-based redaction test
/// is therefore not possible here without adding an out-of-scope reference.
/// Instead, this file uses:
/// <list type="bullet">
///   <item>Reflection assertions that <c>[RedactData]</c> is present on the
///     expected properties.</item>
///   <item><c>ToString</c>/<c>PrintMembers</c> override assertions confirming
///     that raw secret bytes are never emitted.</item>
/// </list>
/// </summary>
public sealed class EncryptionKeyRedactionTests
{
    // -----------------------------------------------------------------------
    // KeyMaterialEncrypted — [RedactData(SecretInformation)] present
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyMaterialEncrypted_Bytes_HasRedactDataSecretInformation()
    {
        var prop = typeof(KeyMaterialEncrypted)
            .GetProperty(nameof(KeyMaterialEncrypted.Bytes));

        prop.Should().NotBeNull();

        var attr = prop.GetCustomAttribute<RedactDataAttribute>();
        attr.Should().NotBeNull("Bytes must be marked [RedactData] to mask it in Serilog");
        attr.Should().Match<RedactDataAttribute>(a => a.Reason == RedactReason.SecretInformation);
    }

    [Fact]
    public void KeyMaterialEncrypted_ToString_EmitsRedactionSentinel()
    {
        var mat = KeyMaterialEncrypted.FromTrusted(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        var str = mat.ToString();
        str.Should().Contain("REDACTED");
        str.Should().NotContain("222"); // decimal for 0xDE — must not appear
        str.Should().NotContain("173"); // decimal for 0xAD — must not appear
    }

    // -----------------------------------------------------------------------
    // CompromisedKey.Reason — [RedactData(PersonalInformation)] present
    // -----------------------------------------------------------------------

    [Fact]
    public void CompromisedKey_Reason_HasRedactDataPersonalInformation()
    {
        var prop = typeof(CompromisedKey)
            .GetProperty(nameof(CompromisedKey.Reason));

        prop.Should().NotBeNull();

        var attr = prop.GetCustomAttribute<RedactDataAttribute>();
        attr.Should().NotBeNull(
            "Reason must be marked [RedactData] because it can carry operator-entered "
            + "sensitive context");
        attr.Should().Match<RedactDataAttribute>(a => a.Reason == RedactReason.PersonalInformation);
    }

    // -----------------------------------------------------------------------
    // Kid / KeyDomain — intentionally NOT redacted (JWKS + audit visibility)
    // -----------------------------------------------------------------------

    [Fact]
    public void Kid_Value_HasNoRedactDataAttribute()
    {
        // Kid is opaque but intentionally visible in JWKS and audit logs.
        var prop = typeof(Kid).GetProperty(nameof(Kid.Value));
        prop.Should().NotBeNull();
        prop.GetCustomAttribute<RedactDataAttribute>().Should().BeNull(
            "Kid is intentionally visible in JWKS responses and audit telemetry");
    }

    [Fact]
    public void KeyDomain_Value_HasNoRedactDataAttribute()
    {
        var prop = typeof(KeyDomain).GetProperty(nameof(KeyDomain.Value));
        prop.Should().NotBeNull();
        prop.GetCustomAttribute<RedactDataAttribute>().Should().BeNull(
            "KeyDomain is a logical label (e.g. 'audit'), not PII");
    }

    // -----------------------------------------------------------------------
    // PublicKeyMaterial — intentionally NOT redacted
    // -----------------------------------------------------------------------

    [Fact]
    public void PublicKeyMaterial_Bytes_HasNoRedactDataAttribute()
    {
        // Public keys are published via JWKS — they must NOT be redacted.
        var prop = typeof(PublicKeyMaterial).GetProperty(nameof(PublicKeyMaterial.Bytes));
        prop.Should().NotBeNull();
        prop.GetCustomAttribute<RedactDataAttribute>().Should().BeNull(
            "PublicKeyMaterial is published via the JWKS endpoint — intentionally not redacted");
    }
}
