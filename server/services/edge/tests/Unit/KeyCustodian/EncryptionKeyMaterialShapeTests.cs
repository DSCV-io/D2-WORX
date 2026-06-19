// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyMaterialShapeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Tests that enforce the per-type material shape invariant and the
/// <see cref="PendingKey.Create"/> null-argument preconditions. <c>RsaSigning</c>
/// keys carry public material only; <c>X509CaCertificate</c> keys carry CA
/// certificate material only; symmetric keys carry neither. Precondition
/// violations (null arguments, inconsistent material shape) surface as flagged
/// <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> internal-error results rather than
/// thrown exceptions.
/// </summary>
public sealed class EncryptionKeyMaterialShapeTests
{
    private static readonly Kid sr_kid = Kid.FromTrusted("shape-test");
    private static readonly KeyDomain sr_domain = KeyDomain.FromTrusted("audit");
    private static readonly KeyMaterialEncrypted sr_mat =
        KeyMaterialEncrypted.FromTrusted(new byte[] { 1, 2, 3 });

    private static readonly PublicKeyMaterial sr_pub =
        PublicKeyMaterial.FromTrusted(new byte[] { 4, 5, 6 });

    private static readonly CaCertificateMaterial sr_caCert =
        CaCertificateMaterial.FromTrusted(new byte[] { 7, 8, 9 });

    private static readonly Instant sr_created = Instant.FromUtc(2026, 1, 1, 0, 0, 0);

    // -----------------------------------------------------------------------
    // RSA key — public material required, no CA cert
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_RsaWithPublicMaterial_Succeeds()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.RsaSigning, sr_mat, sr_pub, null, sr_created);

        result.Success.Should().BeTrue();
        result.Data!.KeyType.Should().Be(KeyType.RsaSigning);
        result.Data!.PublicKeyMaterial.Should().Be(sr_pub);
        result.Data!.CaCertificateMaterial.Should().BeNull();
    }

    [Fact]
    public void Create_RsaWithoutPublicMaterial_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.RsaSigning, sr_mat, null, null, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_RsaWithCaCertificate_FailsPreconditionViolated()
    {
        // An RSA key carrying CA certificate material (wrong slot) is an invalid shape.
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.RsaSigning, sr_mat, sr_pub, sr_caCert, sr_created);

        AssertPreconditionViolated(result);
    }

    // -----------------------------------------------------------------------
    // X509CaCertificate key — CA cert material required, no public material
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CaWithCertificateMaterial_Succeeds()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.X509CaCertificate, sr_mat, null, sr_caCert, sr_created);

        result.Success.Should().BeTrue();
        result.Data!.KeyType.Should().Be(KeyType.X509CaCertificate);
        result.Data!.CaCertificateMaterial.Should().Be(sr_caCert);
        result.Data!.PublicKeyMaterial.Should().BeNull();
    }

    [Fact]
    public void Create_CaWithoutCertificateMaterial_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.X509CaCertificate, sr_mat, null, null, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_CaWithPublicMaterial_FailsPreconditionViolated()
    {
        // A CA key carrying JWKS-scoped public material (wrong slot) is an invalid shape.
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.X509CaCertificate, sr_mat, sr_pub, sr_caCert, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_CaWithPublicMaterialAndNoCert_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.X509CaCertificate, sr_mat, sr_pub, null, sr_created);

        AssertPreconditionViolated(result);
    }

    // -----------------------------------------------------------------------
    // Symmetric keys — must NOT have public material or CA cert
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AesWithPublicMaterial_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.AesPayload, sr_mat, sr_pub, null, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_AesWithCaCertificate_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.AesPayload, sr_mat, null, sr_caCert, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_SecretWithPublicMaterial_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.Secret, sr_mat, sr_pub, null, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_SecretWithCaCertificate_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.Secret, sr_mat, null, sr_caCert, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_AesWithoutPublicMaterial_Succeeds()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.AesPayload, sr_mat, null, null, sr_created);

        result.Success.Should().BeTrue();
        result.Data!.PublicKeyMaterial.Should().BeNull();
        result.Data!.CaCertificateMaterial.Should().BeNull();
    }

    [Fact]
    public void Create_SecretWithoutPublicMaterial_Succeeds()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.Secret, sr_mat, null, null, sr_created);

        result.Success.Should().BeTrue();
        result.Data!.PublicKeyMaterial.Should().BeNull();
        result.Data!.CaCertificateMaterial.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Null guards on PendingKey.Create — each surfaces a flagged result
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_NullKid_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            null, sr_domain, KeyType.AesPayload, sr_mat, null, null, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_NullDomain_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, null, KeyType.AesPayload, sr_mat, null, null, sr_created);

        AssertPreconditionViolated(result);
    }

    [Fact]
    public void Create_NullEncryptedMaterial_FailsPreconditionViolated()
    {
        var result = PendingKey.Create(
            sr_kid, sr_domain, KeyType.AesPayload, null, null, null, sr_created);

        AssertPreconditionViolated(result);
    }

    private static void AssertPreconditionViolated(
        D2.Shared.Result.D2Result<PendingKey> result)
    {
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
        result.Category.Should().Be(ErrorCategory.InternalError);
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);

        // PRECONDITION_VIOLATED is a 500/internal_error opaque code — the internal
        // argument name must NOT leak onto the wire. Assert the message key is
        // present but carries no "arg" parameter.
        var message = result.Messages.Single(
            m => m.Key == "keycustodian_internal_PRECONDITION_VIOLATED");
        var hasArgLeak = message.Parameters?.ContainsKey("arg") ?? false;
        hasArgLeak.Should().BeFalse(
            because: "internal C# parameter names must not be serialized onto the wire");
    }
}
