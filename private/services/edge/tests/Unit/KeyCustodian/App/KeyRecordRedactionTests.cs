// -----------------------------------------------------------------------
// <copyright file="KeyRecordRedactionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// Reflection-pins the <c>[RedactData]</c> attributes on <see cref="KeyRecord"/>
/// properties that carry sensitive material. Mirrors the pattern established in
/// <c>EncryptionKeyRedactionTests</c> for the domain value objects.
/// </summary>
public sealed class KeyRecordRedactionTests
{
    // -----------------------------------------------------------------------
    // KeyRecord.KeyMaterialEncrypted — [RedactData(SecretInformation)] present
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyRecord_KeyMaterialEncrypted_HasRedactDataSecretInformation()
    {
        var prop = typeof(KeyRecord)
            .GetProperty(nameof(KeyRecord.KeyMaterialEncrypted));

        prop.Should().NotBeNull();

        var attr = prop.GetCustomAttribute<RedactDataAttribute>();
        attr.Should().NotBeNull(
            "KeyMaterialEncrypted holds root-wrapped ciphertext and must be masked in Serilog");
        attr.Reason.Should().Be(
            RedactReason.SecretInformation,
            "key material is a secret, not PII");
    }

    // -----------------------------------------------------------------------
    // KeyRecord.CompromiseReason — [RedactData(PersonalInformation)] present
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyRecord_CompromiseReason_HasRedactDataPersonalInformation()
    {
        var prop = typeof(KeyRecord)
            .GetProperty(nameof(KeyRecord.CompromiseReason));

        prop.Should().NotBeNull();

        var attr = prop.GetCustomAttribute<RedactDataAttribute>();
        attr.Should().NotBeNull(
            "CompromiseReason is operator-supplied context and can carry names or references "
            + "to individuals");
        attr.Reason.Should().Be(
            RedactReason.PersonalInformation,
            "the compromise reason is classified as PersonalInformation, not SecretInformation");
    }

    // -----------------------------------------------------------------------
    // KeyRecord.PublicKeyMaterial — intentionally NOT redacted
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyRecord_PublicKeyMaterial_HasNoRedactDataAttribute()
    {
        // Public keys are published via the JWKS endpoint — must NOT be redacted.
        var prop = typeof(KeyRecord)
            .GetProperty(nameof(KeyRecord.PublicKeyMaterial));

        prop.Should().NotBeNull();
        prop.GetCustomAttribute<RedactDataAttribute>().Should().BeNull(
            "PublicKeyMaterial is the SPKI public key published via JWKS — intentionally "
            + "not redacted");
    }

    // -----------------------------------------------------------------------
    // KeyRecord.Kid — intentionally NOT redacted (audit / JWKS visibility)
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyRecord_Kid_HasNoRedactDataAttribute()
    {
        var prop = typeof(KeyRecord)
            .GetProperty(nameof(KeyRecord.Kid));

        prop.Should().NotBeNull();
        prop.GetCustomAttribute<RedactDataAttribute>().Should().BeNull(
            "Kid is opaque and intentionally visible in JWKS responses and audit telemetry");
    }
}
