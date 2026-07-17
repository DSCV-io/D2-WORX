// -----------------------------------------------------------------------
// <copyright file="RsaSigningTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

using System.Buffers.Text;

/// <summary>
/// Tests for the pure <see cref="RsaSigning"/> rule: a valid PKCS#8 key produces an
/// RS256 signature that round-trip-verifies, a different key does not verify, and
/// garbage / truncated key material surfaces a flagged
/// <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure rather than throwing.
/// </summary>
public sealed class RsaSigningTests
{
    private static readonly byte[] sr_input = "header.payload"u8.ToArray();

    [Fact]
    public void Sign_ValidKey_ProducesVerifiableRs256Signature()
    {
        using var rsa = RSA.Create(2048);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();

        var result = RsaSigning.Sign(pkcs8, sr_input);

        result.Success.Should().BeTrue();

        var signature = Base64Url.DecodeFromChars(result.Data!);
        rsa.VerifyData(sr_input, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue("RsaSigning emits an RS256 signature over the exact input bytes");
    }

    [Fact]
    public void Sign_EmptyInput_StillSignsVerifiably()
    {
        // The rule does not reject empty input (that is the handler's concern); signing an
        // empty payload still produces a valid RS256 signature over zero bytes.
        using var rsa = RSA.Create(2048);

        var result = RsaSigning.Sign(rsa.ExportPkcs8PrivateKey(), ReadOnlySpan<byte>.Empty);

        result.Success.Should().BeTrue();

        var signature = Base64Url.DecodeFromChars(result.Data!);
        rsa.VerifyData(
            ReadOnlySpan<byte>.Empty, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue();
    }

    [Fact]
    public void Sign_SignatureDoesNotVerifyUnderDifferentKey()
    {
        // Adversarial: a signature from key A must never verify under key B.
        using var signer = RSA.Create(2048);
        using var other = RSA.Create(2048);

        var result = RsaSigning.Sign(signer.ExportPkcs8PrivateKey(), sr_input);

        var signature = Base64Url.DecodeFromChars(result.Data!);
        other.VerifyData(sr_input, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeFalse();
    }

    [Fact]
    public void Sign_GarbageKey_ReturnsPreconditionViolated_NoThrow()
    {
        var result = RsaSigning.Sign([0x01, 0x02, 0x03], sr_input);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
    }

    [Fact]
    public void Sign_TruncatedKey_ReturnsPreconditionViolated_NoThrow()
    {
        using var rsa = RSA.Create(2048);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();

        var result = RsaSigning.Sign(pkcs8.AsSpan(0, pkcs8.Length / 2), sr_input);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
    }

    [Fact]
    public void Sign_EmptyKey_ReturnsPreconditionViolated_NoThrow()
    {
        var result = RsaSigning.Sign(ReadOnlySpan<byte>.Empty, sr_input);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
    }
}
