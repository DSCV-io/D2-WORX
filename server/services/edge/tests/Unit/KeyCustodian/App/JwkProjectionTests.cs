// -----------------------------------------------------------------------
// <copyright file="JwkProjectionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Buffers.Text;
using System.Security.Cryptography;
using D2.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// Tests for the pure <see cref="JwkProjection"/>: n/e base64url correctness
/// against <see cref="RSA.ExportParameters"/> and the fixed RS256/sig/RSA header.
/// </summary>
public sealed class JwkProjectionTests
{
    [Fact]
    public void ToJwk_NAndE_MatchExportedRsaParametersBase64Url()
    {
        using var rsa = RSA.Create(2048);
        var spki = rsa.ExportSubjectPublicKeyInfo();
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        var jwk = JwkProjection.ToJwk("kid-jwk", spki);

        jwk.Kid.Should().Be("kid-jwk");
        jwk.N.Should().Be(Base64Url.EncodeToString(parameters.Modulus!));
        jwk.E.Should().Be(Base64Url.EncodeToString(parameters.Exponent!));
    }

    [Fact]
    public void ToJwk_HeaderFields_AreFixedRs256SigRsa()
    {
        using var rsa = RSA.Create(2048);
        var jwk = JwkProjection.ToJwk("kid", rsa.ExportSubjectPublicKeyInfo());

        jwk.Kty.Should().Be("RSA");
        jwk.Use.Should().Be("sig");
        jwk.Alg.Should().Be("RS256");
    }

    [Fact]
    public void ToJwk_NAndE_AreUnpaddedBase64Url()
    {
        using var rsa = RSA.Create(2048);
        var jwk = JwkProjection.ToJwk("kid", rsa.ExportSubjectPublicKeyInfo());

        jwk.N.Should().NotContain("=").And.NotContain("+").And.NotContain("/");
        jwk.E.Should().NotContain("=").And.NotContain("+").And.NotContain("/");
    }

    // -----------------------------------------------------------------------
    // S-L2 — ToJwk must never expose RSA private parameters
    // -----------------------------------------------------------------------

    [Fact]
    public void ToJwk_Jwk_ExposesNoRsaPrivateParameters()
    {
        // Pin that ToJwk — which calls ExportParameters(includePrivateParameters: false) —
        // never leaks D/P/Q/DP/DQ/InverseQ onto the Jwk record.
        using var rsa = RSA.Create(2048);
        var jwk = JwkProjection.ToJwk("kid-priv-check", rsa.ExportSubjectPublicKeyInfo());

        // Verify the returned instance carries the expected kid and public-key type markers.
        jwk.Kid.Should().Be("kid-priv-check", "ToJwk must preserve the supplied kid");
        jwk.Kty.Should().Be("RSA", "RSA keys must carry kty=RSA");

        var privateParamNames = new[]
        {
            nameof(System.Security.Cryptography.RSAParameters.D),
            nameof(System.Security.Cryptography.RSAParameters.P),
            nameof(System.Security.Cryptography.RSAParameters.Q),
            nameof(System.Security.Cryptography.RSAParameters.DP),
            nameof(System.Security.Cryptography.RSAParameters.DQ),
            nameof(System.Security.Cryptography.RSAParameters.InverseQ),
        };

        var jwkProperties = typeof(D2.Edge.KeyCustodian.Domain.ValueObjects.Jwk)
            .GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var paramName in privateParamNames)
        {
            jwkProperties.Should().NotContain(
                p => string.Equals(p.Name, paramName, System.StringComparison.OrdinalIgnoreCase),
                because: $"RSA private parameter {paramName} must never appear on the "
                + "public Jwk record");
        }
    }
}
