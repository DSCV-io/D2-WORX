// -----------------------------------------------------------------------
// <copyright file="SealingOutputMapperTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Sealing;

using System.Security.Cryptography;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Shared.Encryption;
using Xunit;

/// <summary>
/// Coverage for <see cref="SealingOutputMapper"/> — the boundary that turns the KeyCustodian
/// seal-keyring leaf DTOs into <see cref="RecipientPublicKeyring"/> /
/// <see cref="RecipientPrivateKeyring"/> primitives, surfacing invariant violations as typed
/// failures rather than unhandled ctor throws.
/// </summary>
public sealed class SealingOutputMapperTests
{
    [Fact]
    public void ToRecipientPublicKeyring_WellFormed_Ok()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var dto = new GetOrLazyProvisionSealPublicKeyOutput(
            "kid-1",
            [new SealPublicEntry("kid-1", ecdh.ExportSubjectPublicKeyInfo())]);

        var result = SealingOutputMapper.ToPublicKeyringResult(D2Result.Ok(), dto, "audit");

        result.Success.Should().BeTrue();
        result.Data!.RecipientServiceId.Should().Be("audit");
        result.Data.ActiveKid.Should().Be("kid-1");
    }

    [Fact]
    public void ToRecipientPublicKeyring_ActiveKidAbsent_TypedFailure()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var dto = new GetOrLazyProvisionSealPublicKeyOutput(
            "missing-kid",
            [new SealPublicEntry("kid-1", ecdh.ExportSubjectPublicKeyInfo())]);

        var result = SealingOutputMapper.ToPublicKeyringResult(D2Result.Ok(), dto, "audit");

        result.Success.Should().BeFalse("an active kid absent from the entries is a broken keyring");
    }

    [Fact]
    public void ToRecipientPrivateKeyring_WellFormed_Ok()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var dto = new GetOrLazyProvisionOwnSealPrivateKeyOutput(
            "kid-1",
            [new SealPrivateEntry("kid-1", ecdh.ExportPkcs8PrivateKey())]);

        var result = SealingOutputMapper.ToPrivateKeyringResult(D2Result.Ok(), dto, "audit");

        result.Success.Should().BeTrue();
        result.Data!.RecipientServiceId.Should().Be("audit");
    }

    [Fact]
    public void ToRecipientPrivateKeyring_PublicKeyBytes_TypedFailure()
    {
        // Feeding SPKI (public) bytes where PKCS#8 (private) is required must fail loud as a
        // typed failure, never an unhandled throw.
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var dto = new GetOrLazyProvisionOwnSealPrivateKeyOutput(
            "kid-1",
            [new SealPrivateEntry("kid-1", ecdh.ExportSubjectPublicKeyInfo())]);

        var result = SealingOutputMapper.ToPrivateKeyringResult(D2Result.Ok(), dto, "audit");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void ToPublicKeyringResult_EnvelopeFailure_Bubbles()
    {
        var result = SealingOutputMapper.ToPublicKeyringResult(
            D2Result.ServiceUnavailable(), data: null, "audit");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void ToPrivateKeyringResult_EnvelopeFailure_Bubbles()
    {
        var result = SealingOutputMapper.ToPrivateKeyringResult(
            D2Result.Forbidden(), data: null, "audit");

        result.Success.Should().BeFalse();
    }
}
