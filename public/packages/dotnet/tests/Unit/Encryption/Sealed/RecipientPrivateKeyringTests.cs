// -----------------------------------------------------------------------
// <copyright file="RecipientPrivateKeyringTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Constructor-boundary + lifecycle matrix for
/// <see cref="RecipientPrivateKeyring"/>: grammar/kid/material validation,
/// zeroize-on-dispose (asserted through a held internal-buffer reference),
/// dispose idempotence, and post-dispose rejection.
/// </summary>
public sealed class RecipientPrivateKeyringTests
{
    private static readonly SealedTestKeys.TestKeypair sr_keypair =
        SealedTestKeys.GenerateKeypair();

    // ---------------------------------------------------------------
    // Happy path.
    // ---------------------------------------------------------------

    [Fact]
    public void Ctor_ValidInput_ExposesServiceIdAndResolvesKid()
    {
        using var ring = new RecipientPrivateKeyring("audit", ValidKeys());

        ring.RecipientServiceId.Should().Be("audit");
        ring.TryGetPrivateKey("kid-1", out var pkcs8).Should().BeTrue();
        pkcs8.ToArray().Should().Equal(sr_keypair.PrivatePkcs8);
    }

    [Fact]
    public void Ctor_DefensivelyCopiesKeyBytes()
    {
        var mutable = (byte[])sr_keypair.PrivatePkcs8.Clone();
        using var ring = new RecipientPrivateKeyring(
            "audit", new Dictionary<string, byte[]> { ["kid-1"] = mutable });

        mutable.AsSpan().Clear();

        ring.TryGetPrivateKey("kid-1", out var pkcs8).Should().BeTrue();
        pkcs8.ToArray().Should().Equal(sr_keypair.PrivatePkcs8);
    }

    [Fact]
    public void TryGetPrivateKey_UnknownOrNullKid_ReturnsFalse()
    {
        using var ring = new RecipientPrivateKeyring("audit", ValidKeys());

        ring.TryGetPrivateKey("other", out _).Should().BeFalse();
        ring.TryGetPrivateKey(null, out _).Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Constructor validation.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("Audit")]
    [InlineData("audit!")]
    public void Ctor_InvalidServiceIdGrammar_Throws(string serviceId)
    {
        var act = () => new RecipientPrivateKeyring(serviceId, ValidKeys());

        act.Should().Throw<ArgumentException>().WithParameterName("recipientServiceId");
    }

    [Fact]
    public void Ctor_NullKeys_Throws()
    {
        var act = () => new RecipientPrivateKeyring("audit", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_EmptyKeys_Throws()
    {
        var act = () => new RecipientPrivateKeyring(
            "audit", new Dictionary<string, byte[]>());

        act.Should().Throw<ArgumentException>().WithParameterName("privateKeysByKid");
    }

    [Fact]
    public void Ctor_KidOverMaxLength_Throws()
    {
        var longKid = new string('k', SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH + 1);
        var keys = new Dictionary<string, byte[]> { [longKid] = sr_keypair.PrivatePkcs8 };

        var act = () => new RecipientPrivateKeyring("audit", keys);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_GarbagePkcs8_Throws()
    {
        var keys = new Dictionary<string, byte[]> { ["kid-1"] = [9, 9, 9, 9] };

        var act = () => new RecipientPrivateKeyring("audit", keys);

        act.Should().Throw<ArgumentException>().WithParameterName("privateKeysByKid");
    }

    [Fact]
    public void Ctor_PublicKeyWherePrivateExpected_Throws()
    {
        var keys = new Dictionary<string, byte[]> { ["kid-1"] = sr_keypair.PublicSpki };

        var act = () => new RecipientPrivateKeyring("audit", keys);

        act.Should().Throw<ArgumentException>().WithParameterName("privateKeysByKid");
    }

    [Fact]
    public void Ctor_P384PrivateKey_Throws()
    {
        var p384 = SealedTestKeys.GenerateP384Keypair();
        var keys = new Dictionary<string, byte[]> { ["kid-1"] = p384.PrivatePkcs8 };

        var act = () => new RecipientPrivateKeyring("audit", keys);

        act.Should().Throw<ArgumentException>().WithParameterName("privateKeysByKid");
    }

    // ---------------------------------------------------------------
    // Zeroize + lifecycle.
    // ---------------------------------------------------------------

    [Fact]
    public void Dispose_ZeroesStoredKeyBytes()
    {
        var ring = new RecipientPrivateKeyring("audit", ValidKeys());

        // Snapshot the keyring's internal copy via the ROM token — the token
        // aliases the internal byte[].
        ring.TryGetPrivateKey("kid-1", out var live).Should().BeTrue();
        live.ToArray().Should().NotBeEquivalentTo(new byte[live.Length]);

        ring.Dispose();

        live.ToArray().Should().BeEquivalentTo(new byte[live.Length]);
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var ring = new RecipientPrivateKeyring("audit", ValidKeys());
        ring.Dispose();

        var act = ring.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public void TryGetPrivateKey_AfterDispose_Throws()
    {
        var ring = new RecipientPrivateKeyring("audit", ValidKeys());
        ring.Dispose();

        var act = () => ring.TryGetPrivateKey("kid-1", out _);

        act.Should().Throw<ObjectDisposedException>();
    }

    // ---------------------------------------------------------------
    // Rendering.
    // ---------------------------------------------------------------

    [Fact]
    public void ToString_NeverContainsKeyBytes()
    {
        using var ring = new RecipientPrivateKeyring("audit", ValidKeys());

        var rendered = ring.ToString();

        rendered.Should().Contain("audit");
        rendered.Should().NotContain(Convert.ToBase64String(sr_keypair.PrivatePkcs8));
        rendered.Should().NotContain(Convert.ToHexString(sr_keypair.PrivatePkcs8));
    }

    [Fact]
    public void ToString_AfterDispose_RendersDisposedMarker()
    {
        var ring = new RecipientPrivateKeyring("audit", ValidKeys());
        ring.Dispose();

        ring.ToString().Should().Contain("disposed");
    }

    private static Dictionary<string, byte[]> ValidKeys(string kid = "kid-1")
        => new() { [kid] = sr_keypair.PrivatePkcs8 };
}
