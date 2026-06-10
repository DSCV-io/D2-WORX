// -----------------------------------------------------------------------
// <copyright file="KcCryptoTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Crypto;
using D2.Edge.KeyCustodian.App.Implementations.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Tests for the key generators, the smoke tester, and the kid minter — real
/// BCL crypto, fast + deterministic.
/// </summary>
public sealed class KcCryptoTests
{
    private static readonly ISmokeTester sr_smoke = new SmokeTester();

    // -----------------------------------------------------------------------
    // Generators
    // -----------------------------------------------------------------------

    [Fact]
    public void RsaSigningGenerator_ProducesImportablePkcs8AndMatchingSpki()
    {
        var generator = new RsaSigningKeyGenerator(KcAppTestKit.BuildOptionsAccessor());
        generator.Handles.Should().Be(KeyType.RsaSigning);

        var material = generator.Generate();
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
        var material = new AesPayloadKeyGenerator().Generate();
        material.Plaintext.Length.Should().Be(32);
        material.PublicSpki.Should().BeNull();
    }

    [Fact]
    public void SecretGenerator_ProducesConfiguredLengthNoPublic()
    {
        var generator = new SecretKeyGenerator(KcAppTestKit.BuildOptionsAccessor());
        var material = generator.Generate();
        material.Plaintext.Length.Should().Be(64);
        material.PublicSpki.Should().BeNull();
    }

    [Fact]
    public void SecretGenerator_RespectsConfiguredLength()
    {
        var options = KcAppTestKit.BuildOptions();
        options.SecretLengthBytes = 48;
        var generator = new SecretKeyGenerator(Options.Create(options));
        generator.Generate().Plaintext.Length.Should().Be(48);
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

    // -----------------------------------------------------------------------
    // Smoke tester — per type, round-trip pass
    // -----------------------------------------------------------------------

    [Fact]
    public void Smoke_Rsa_FreshKey_Passes()
    {
        var material = new RsaSigningKeyGenerator(KcAppTestKit.BuildOptionsAccessor()).Generate();
        sr_smoke.Verify(KeyType.RsaSigning, material.Plaintext, material.PublicSpki)
            .Success.Should().BeTrue();
    }

    [Fact]
    public void Smoke_Aes_FreshKey_Passes()
    {
        var material = new AesPayloadKeyGenerator().Generate();
        sr_smoke.Verify(KeyType.AesPayload, material.Plaintext, publicSpki: null)
            .Success.Should().BeTrue();
    }

    [Fact]
    public void Smoke_Secret_FreshKey_Passes()
    {
        var material = new SecretKeyGenerator(KcAppTestKit.BuildOptionsAccessor()).Generate();
        sr_smoke.Verify(KeyType.Secret, material.Plaintext, publicSpki: null)
            .Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Smoke tester — adversarial (no throw, returns failure)
    // -----------------------------------------------------------------------

    [Fact]
    public void Smoke_Rsa_MissingPublic_FailsWithoutThrow()
    {
        var material = new RsaSigningKeyGenerator(KcAppTestKit.BuildOptionsAccessor()).Generate();
        sr_smoke.Verify(KeyType.RsaSigning, material.Plaintext, publicSpki: null)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void Smoke_Rsa_BitFlippedPrivate_FailsWithoutThrow()
    {
        var material = new RsaSigningKeyGenerator(KcAppTestKit.BuildOptionsAccessor()).Generate();
        var corrupted = (byte[])material.Plaintext.Clone();
        corrupted[10] ^= 0xFF;

        var result = sr_smoke.Verify(KeyType.RsaSigning, corrupted, material.PublicSpki);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SMOKE_TEST_FAILED");
    }

    [Fact]
    public void Smoke_Rsa_MismatchedPublic_FailsWithoutThrow()
    {
        var a = new RsaSigningKeyGenerator(KcAppTestKit.BuildOptionsAccessor()).Generate();
        var b = new RsaSigningKeyGenerator(KcAppTestKit.BuildOptionsAccessor()).Generate();

        // a's private with b's public — signature won't verify.
        sr_smoke.Verify(KeyType.RsaSigning, a.Plaintext, b.PublicSpki)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void Smoke_Aes_WrongSizeKey_FailsWithoutThrow()
    {
        // 17 bytes is not a valid AES key length.
        var result = sr_smoke.Verify(KeyType.AesPayload, new byte[17], publicSpki: null);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Smoke_Secret_EmptyKey_FailsWithoutThrow()
    {
        sr_smoke.Verify(KeyType.Secret, ReadOnlyMemory<byte>.Empty, publicSpki: null)
            .Success.Should().BeFalse();
    }

    [Fact]
    public void Smoke_GarbagePkcs8_FailsWithoutThrow()
    {
        var garbage = RandomNumberGenerator.GetBytes(64);
        var result = sr_smoke.Verify(
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
        var kid = KeyCustodianCrypto.MintKid();
        Kid.Create(kid).Success.Should().BeTrue();
    }

    [Fact]
    public void MintKid_ProducesUnpaddedBase64UrlCharset()
    {
        var kid = KeyCustodianCrypto.MintKid();
        kid.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        kid.Should().NotContain("=");
    }

    [Fact]
    public void MintKid_IsUniqueAcrossManyCalls()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 1000; i++)
            seen.Add(KeyCustodianCrypto.MintKid()).Should().BeTrue();

        seen.Should().HaveCount(1000);
    }
}
