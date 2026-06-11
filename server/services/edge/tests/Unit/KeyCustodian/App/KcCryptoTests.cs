// -----------------------------------------------------------------------
// <copyright file="KcCryptoTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// Tests for the pure key-generation, smoke-testing, and kid-minting rules — real
/// BCL crypto, fast + deterministic.
/// </summary>
public sealed class KcCryptoTests
{
    private const int _RSA_BITS = 2048;
    private const int _SECRET_BYTES = 64;

    // -----------------------------------------------------------------------
    // Generators
    // -----------------------------------------------------------------------

    [Fact]
    public void RsaSigningGenerator_ProducesImportablePkcs8AndMatchingSpki()
    {
        var material = KeyGeneration.Generate(KeyType.RsaSigning, _RSA_BITS, _SECRET_BYTES).Data!;
        material.PublicSpki.Should().NotBeNull();

        using var fromPrivate = RSA.Create();
        fromPrivate.ImportPkcs8PrivateKey(material.Plaintext, out _);

        using var fromPublic = RSA.Create();
        fromPublic.ImportSubjectPublicKeyInfo(material.PublicSpki!, out _);

        // The SPKI must be the public half of the generated private key.
        var privateSpki = fromPrivate.ExportSubjectPublicKeyInfo();
        privateSpki.Should().Equal(material.PublicSpki!);
    }

    [Fact]
    public void AesPayloadGenerator_Produces32BytesNoPublic()
    {
        var material = KeyGeneration.Generate(KeyType.AesPayload, _RSA_BITS, _SECRET_BYTES).Data!;
        material.Plaintext.Length.Should().Be(32);
        material.PublicSpki.Should().BeNull();
    }

    [Fact]
    public void SecretGenerator_ProducesConfiguredLengthNoPublic()
    {
        var material = KeyGeneration.Generate(KeyType.Secret, _RSA_BITS, _SECRET_BYTES).Data!;
        material.Plaintext.Length.Should().Be(64);
        material.PublicSpki.Should().BeNull();
    }

    [Fact]
    public void SecretGenerator_RespectsConfiguredLength()
    {
        KeyGeneration.Generate(KeyType.Secret, _RSA_BITS, 48).Data!.Plaintext.Length.Should().Be(48);
    }

    [Fact]
    public void GeneratedKeyMaterial_Zero_WipesPlaintext()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var material = new GeneratedKeyMaterial(bytes, publicSpki: null);
        material.Zero();
        material.Plaintext.Should().OnlyContain(b => b == 0);
    }

    [Fact]
    public void GeneratedKeyMaterial_ToString_RedactsPlaintext()
    {
        var material = new GeneratedKeyMaterial(RandomNumberGenerator.GetBytes(8), publicSpki: null);
        material.ToString().Should().Contain("REDACTED").And.NotContain(
            Convert.ToHexString(material.Plaintext));
    }

    [Fact]
    public void GeneratedKeyMaterial_EmptyPlaintext_Throws()
    {
        var act = () => new GeneratedKeyMaterial([], publicSpki: null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_UnknownKeyType_FailsPreconditionViolated()
    {
        // The closed KeyType enum makes the default arm unreachable from valid
        // call sites; an out-of-range value surfaces as a flagged
        // KEYCUSTODIAN_PRECONDITION_VIOLATED result rather than a thrown exception
        // (zero-throw domain rule — D2Result carries the telemetry instead).
        var result = KeyGeneration.Generate((KeyType)999, _RSA_BITS, _SECRET_BYTES);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_PRECONDITION_VIOLATED");
    }

    // -----------------------------------------------------------------------
    // Smoke tester — per type, round-trip pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Smoke_Rsa_FreshKey_Passes()
    {
        var material = KeyGeneration.Generate(KeyType.RsaSigning, _RSA_BITS, _SECRET_BYTES).Data!;
        SmokeTesting.Verify(KeyType.RsaSigning, material.Plaintext, material.PublicSpki)
            .Success.Should().BeTrue();
    }

    [Fact]
    public void Smoke_Aes_FreshKey_Passes()
    {
        var material = KeyGeneration.Generate(KeyType.AesPayload, _RSA_BITS, _SECRET_BYTES).Data!;
        SmokeTesting.Verify(KeyType.AesPayload, material.Plaintext, publicSpki: null)
            .Success.Should().BeTrue();
    }

    [Fact]
    public void Smoke_Secret_FreshKey_Passes()
    {
        var material = KeyGeneration.Generate(KeyType.Secret, _RSA_BITS, _SECRET_BYTES).Data!;
        SmokeTesting.Verify(KeyType.Secret, material.Plaintext, publicSpki: null)
            .Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Smoke tester — adversarial (no throw, returns failure)
    // -----------------------------------------------------------------------

    [Fact]
    public void Smoke_Rsa_MissingPublic_FailsWithoutThrow()
    {
        var material = KeyGeneration.Generate(KeyType.RsaSigning, _RSA_BITS, _SECRET_BYTES).Data!;
        SmokeTesting.Verify(KeyType.RsaSigning, material.Plaintext, publicSpki: null)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void Smoke_Rsa_BitFlippedPrivate_FailsWithoutThrow()
    {
        var material = KeyGeneration.Generate(KeyType.RsaSigning, _RSA_BITS, _SECRET_BYTES).Data!;
        var corrupted = (byte[])material.Plaintext.Clone();
        corrupted[10] ^= 0xFF;

        var result = SmokeTesting.Verify(KeyType.RsaSigning, corrupted, material.PublicSpki);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SMOKE_TEST_FAILED");
    }

    [Fact]
    public void Smoke_Rsa_MismatchedPublic_FailsWithoutThrow()
    {
        var a = KeyGeneration.Generate(KeyType.RsaSigning, _RSA_BITS, _SECRET_BYTES).Data!;
        var b = KeyGeneration.Generate(KeyType.RsaSigning, _RSA_BITS, _SECRET_BYTES).Data!;

        // a's private with b's public — signature won't verify.
        SmokeTesting.Verify(KeyType.RsaSigning, a.Plaintext, b.PublicSpki)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void Smoke_Aes_WrongSizeKey_FailsWithoutThrow()
    {
        // 17 bytes is not a valid AES key length.
        var result = SmokeTesting.Verify(KeyType.AesPayload, new byte[17], publicSpki: null);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Smoke_Secret_EmptyKey_FailsWithoutThrow()
    {
        SmokeTesting.Verify(KeyType.Secret, ReadOnlyMemory<byte>.Empty, publicSpki: null)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void Smoke_GarbagePkcs8_FailsWithoutThrow()
    {
        var garbage = RandomNumberGenerator.GetBytes(64);
        var result = SmokeTesting.Verify(
            KeyType.RsaSigning, garbage, RandomNumberGenerator.GetBytes(64));
        result.Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Wrap → unwrap round-trip through real PayloadCrypto
    // -----------------------------------------------------------------------

    [Fact]
    public void RootCrypto_WrapUnwrap_RoundTrips()
    {
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var plaintext = RandomNumberGenerator.GetBytes(64);

        var wrapped = crypto.Encrypt(plaintext);
        var unwrapped = crypto.Decrypt(wrapped);

        unwrapped.Should().Equal(plaintext);
    }

    // -----------------------------------------------------------------------
    // KidMinter — output passes Kid.Create + is unique + JWKS-safe
    // -----------------------------------------------------------------------

    [Fact]
    public void MintKid_PassesKidCreate()
    {
        var kid = KidMinting.Mint();
        Kid.Create(kid).Success.Should().BeTrue();
    }

    [Fact]
    public void MintKid_ProducesUnpaddedBase64UrlCharset()
    {
        var kid = KidMinting.Mint();
        kid.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        kid.Should().NotContain("=");
    }

    [Fact]
    public void MintKid_IsUniqueAcrossManyCalls()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 1000; i++)
            seen.Add(KidMinting.Mint()).Should().BeTrue();

        seen.Should().HaveCount(1000);
    }
}
