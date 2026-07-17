// -----------------------------------------------------------------------
// <copyright file="RecipientPublicKeyringTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Constructor-boundary matrix for <see cref="RecipientPublicKeyring"/>:
/// service-id grammar, kid bounds, active-kid membership, and the
/// P-256-at-construction validation (garbage / wrong curve / private-where-
/// public-expected all fail loud at the boundary, never at first Seal).
/// </summary>
public sealed class RecipientPublicKeyringTests
{
    private static readonly SealedTestKeys.TestKeypair sr_keypair =
        SealedTestKeys.GenerateKeypair();

    // ---------------------------------------------------------------
    // Happy path.
    // ---------------------------------------------------------------

    [Fact]
    public void Ctor_ValidInput_ExposesServiceIdAndActiveKid()
    {
        var ring = new RecipientPublicKeyring("audit", "kid-1", ValidKeys());

        ring.RecipientServiceId.Should().Be("audit");
        ring.ActiveKid.Should().Be("kid-1");
        ring.TryGetPublicKey("kid-1", out var spki).Should().BeTrue();
        spki.ToArray().Should().Equal(sr_keypair.PublicSpki);
    }

    [Fact]
    public void Ctor_DefensivelyCopiesKeyBytes()
    {
        var mutable = (byte[])sr_keypair.PublicSpki.Clone();
        var ring = new RecipientPublicKeyring(
            "audit", "kid-1", new Dictionary<string, byte[]> { ["kid-1"] = mutable });

        mutable.AsSpan().Clear();

        ring.TryGetPublicKey("kid-1", out var spki).Should().BeTrue();
        spki.ToArray().Should().Equal(sr_keypair.PublicSpki);
    }

    [Fact]
    public void TryGetPublicKey_UnknownOrNullKid_ReturnsFalse()
    {
        var ring = new RecipientPublicKeyring("audit", "kid-1", ValidKeys());

        ring.TryGetPublicKey("other", out _).Should().BeFalse();
        ring.TryGetPublicKey(null, out _).Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Service id grammar (mirrors the workload service-id grammar).
    // ---------------------------------------------------------------

    [Fact]
    public void Ctor_NullServiceId_Throws()
    {
        var act = () => new RecipientPublicKeyring(null!, "kid-1", ValidKeys());

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Audit")]
    [InlineData("audit service")]
    [InlineData(" audit")]
    [InlineData("audit_1")]
    [InlineData("aüdit")]
    public void Ctor_InvalidServiceIdGrammar_Throws(string serviceId)
    {
        var act = () => new RecipientPublicKeyring(serviceId, "kid-1", ValidKeys());

        act.Should().Throw<ArgumentException>().WithParameterName("recipientServiceId");
    }

    [Fact]
    public void Ctor_ServiceIdOver64Chars_Throws()
    {
        var tooLong = new string('a', 65);
        var act = () => new RecipientPublicKeyring(tooLong, "kid-1", ValidKeys());

        act.Should().Throw<ArgumentException>().WithParameterName("recipientServiceId");
    }

    [Fact]
    public void Ctor_ServiceIdExactly64Chars_Succeeds()
    {
        var maxLength = new string('a', 64);
        var ring = new RecipientPublicKeyring(maxLength, "kid-1", ValidKeys());

        ring.RecipientServiceId.Should().Be(maxLength);
    }

    // ---------------------------------------------------------------
    // Kid + map invariants.
    // ---------------------------------------------------------------

    [Fact]
    public void Ctor_NullActiveKid_Throws()
    {
        var act = () => new RecipientPublicKeyring("audit", null!, ValidKeys());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullKeys_Throws()
    {
        var act = () => new RecipientPublicKeyring("audit", "kid-1", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_ActiveKidNotInKeys_Throws()
    {
        var act = () => new RecipientPublicKeyring("audit", "absent", ValidKeys());

        act.Should().Throw<ArgumentException>().WithParameterName("activeKid");
    }

    [Fact]
    public void Ctor_EmptyKeys_Throws()
    {
        // Empty map ⇒ the active kid can never be a member.
        var act = () => new RecipientPublicKeyring(
            "audit", "kid-1", new Dictionary<string, byte[]>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_KidOverMaxLength_Throws()
    {
        var longKid = new string('k', SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH + 1);
        var keys = new Dictionary<string, byte[]>
        {
            ["kid-1"] = sr_keypair.PublicSpki,
            [longKid] = sr_keypair.PublicSpki,
        };

        var act = () => new RecipientPublicKeyring("audit", "kid-1", keys);

        act.Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------------------
    // Key material validation (fail loud at the boundary).
    // ---------------------------------------------------------------

    [Fact]
    public void Ctor_GarbageSpki_Throws()
    {
        var keys = new Dictionary<string, byte[]> { ["kid-1"] = [1, 2, 3, 4] };
        var act = () => new RecipientPublicKeyring("audit", "kid-1", keys);

        act.Should().Throw<ArgumentException>().WithParameterName("publicKeysByKid");
    }

    [Fact]
    public void Ctor_P384PublicKey_Throws()
    {
        var p384 = SealedTestKeys.GenerateP384Keypair();
        var keys = new Dictionary<string, byte[]> { ["kid-1"] = p384.PublicSpki };

        var act = () => new RecipientPublicKeyring("audit", "kid-1", keys);

        act.Should().Throw<ArgumentException>().WithParameterName("publicKeysByKid");
    }

    [Fact]
    public void Ctor_PrivateKeyWherePublicExpected_Throws()
    {
        var keys = new Dictionary<string, byte[]> { ["kid-1"] = sr_keypair.PrivatePkcs8 };

        var act = () => new RecipientPublicKeyring("audit", "kid-1", keys);

        act.Should().Throw<ArgumentException>().WithParameterName("publicKeysByKid");
    }

    [Fact]
    public void Ctor_NullKeyBytes_Throws()
    {
        var keys = new Dictionary<string, byte[]> { ["kid-1"] = null! };

        var act = () => new RecipientPublicKeyring("audit", "kid-1", keys);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_SpkiWithTrailingBytes_Throws()
    {
        var padded = new byte[sr_keypair.PublicSpki.Length + 4];
        sr_keypair.PublicSpki.CopyTo(padded, 0);
        var keys = new Dictionary<string, byte[]> { ["kid-1"] = padded };

        var act = () => new RecipientPublicKeyring("audit", "kid-1", keys);

        act.Should().Throw<ArgumentException>().WithParameterName("publicKeysByKid");
    }

    // ---------------------------------------------------------------
    // Rendering.
    // ---------------------------------------------------------------

    [Fact]
    public void ToString_NeverContainsKeyBytes()
    {
        var ring = new RecipientPublicKeyring("audit", "kid-1", ValidKeys());

        var rendered = ring.ToString();

        rendered.Should().Contain("audit").And.Contain("kid-1");
        rendered.Should().NotContain(Convert.ToBase64String(sr_keypair.PublicSpki));
        rendered.Should().NotContain(Convert.ToHexString(sr_keypair.PublicSpki));
    }

    private static Dictionary<string, byte[]> ValidKeys(string kid = "kid-1")
        => new() { [kid] = sr_keypair.PublicSpki };
}
