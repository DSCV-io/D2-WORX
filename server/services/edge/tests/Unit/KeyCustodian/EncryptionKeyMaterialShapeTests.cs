// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyMaterialShapeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

using AwesomeAssertions;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Keys;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using NodaTime;
using Xunit;

/// <summary>
/// Tests that enforce the RSA↔public-key material shape invariant.
/// </summary>
public sealed class EncryptionKeyMaterialShapeTests
{
    private static readonly Kid s_kid = Kid.FromTrusted("shape-test");
    private static readonly KeyDomain s_domain = KeyDomain.FromTrusted("audit");
    private static readonly KeyMaterialEncrypted s_mat = KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3 });
    private static readonly PublicKeyMaterial s_pub = PublicKeyMaterial.FromTrusted(new byte[] { 4, 5, 6 });
    private static readonly Instant s_created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);

    // -----------------------------------------------------------------------
    // RSA key — public material required
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_RsaWithPublicMaterial_Succeeds()
    {
        var act = () => PendingKey.Create(s_kid, s_domain, KeyType.RsaSigning, s_mat, s_pub, s_created);
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_RsaWithoutPublicMaterial_ThrowsArgumentException()
    {
        var act = () => PendingKey.Create(s_kid, s_domain, KeyType.RsaSigning, s_mat, null, s_created);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*RsaSigning*");
    }

    // -----------------------------------------------------------------------
    // Symmetric keys — must NOT have public material
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AesWithPublicMaterial_ThrowsArgumentException()
    {
        var act = () => PendingKey.Create(s_kid, s_domain, KeyType.AesPayload, s_mat, s_pub, s_created);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Symmetric*");
    }

    [Fact]
    public void Create_SecretWithPublicMaterial_ThrowsArgumentException()
    {
        var act = () => PendingKey.Create(s_kid, s_domain, KeyType.Secret, s_mat, s_pub, s_created);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Symmetric*");
    }

    [Fact]
    public void Create_AesWithoutPublicMaterial_Succeeds()
    {
        var act = () => PendingKey.Create(s_kid, s_domain, KeyType.AesPayload, s_mat, null, s_created);
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_SecretWithoutPublicMaterial_Succeeds()
    {
        var act = () => PendingKey.Create(s_kid, s_domain, KeyType.Secret, s_mat, null, s_created);
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // Null guards on PendingKey.Create
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_NullKid_ThrowsArgumentNullException()
    {
        var act = () => PendingKey.Create(null!, s_domain, KeyType.AesPayload, s_mat, null, s_created);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullDomain_ThrowsArgumentNullException()
    {
        var act = () => PendingKey.Create(s_kid, null!, KeyType.AesPayload, s_mat, null, s_created);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullEncryptedMaterial_ThrowsArgumentNullException()
    {
        var act = () => PendingKey.Create(s_kid, s_domain, KeyType.AesPayload, null!, null, s_created);
        act.Should().Throw<ArgumentNullException>();
    }
}
