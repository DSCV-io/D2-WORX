// -----------------------------------------------------------------------
// <copyright file="SealedTestKeys.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using System.Security.Cryptography;
using DcsvIo.D2.Encryption;

/// <summary>
/// Test helpers for the sealed encryption mode. Keypairs are real P-256
/// material generated per call — tests exercise the real ECDH + HKDF +
/// AES-GCM path, never doubles.
/// </summary>
internal static class SealedTestKeys
{
    internal static TestKeypair GenerateKeypair()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        return new TestKeypair(
            ecdh.ExportSubjectPublicKeyInfo(),
            ecdh.ExportPkcs8PrivateKey());
    }

    internal static TestKeypair GenerateP384Keypair()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);

        return new TestKeypair(
            ecdh.ExportSubjectPublicKeyInfo(),
            ecdh.ExportPkcs8PrivateKey());
    }

    internal static RecipientPublicKeyring PublicKeyring(
        string serviceId, string kid, TestKeypair keypair)
        => new(
            serviceId,
            kid,
            new Dictionary<string, byte[]> { [kid] = keypair.PublicSpki });

    internal static RecipientPrivateKeyring PrivateKeyring(
        string serviceId, string kid, TestKeypair keypair)
        => new(
            serviceId,
            new Dictionary<string, byte[]> { [kid] = keypair.PrivatePkcs8 });

    /// <summary>
    /// A ready-made (sealer, opener) pair over ONE fresh keypair for the
    /// given service id + kid.
    /// </summary>
    /// <param name="serviceId">The recipient service id both sides share.</param>
    /// <param name="kid">The single kid both keyrings carry.</param>
    /// <returns>A matched sealer + opener pair.</returns>
    internal static (PayloadSealer Sealer, PayloadOpener Opener) SealerOpenerPair(
        string serviceId, string kid)
    {
        var keypair = GenerateKeypair();
        var sealer = new PayloadSealer(PublicKeyring(serviceId, kid, keypair));
        var opener = new PayloadOpener(PrivateKeyring(serviceId, kid, keypair));

        return (sealer, opener);
    }

    /// <summary>A generated P-256 keypair as exported key material.</summary>
    /// <param name="PublicSpki">SubjectPublicKeyInfo DER bytes.</param>
    /// <param name="PrivatePkcs8">PKCS#8 PrivateKeyInfo DER bytes.</param>
    internal sealed record TestKeypair(byte[] PublicSpki, byte[] PrivatePkcs8);
}
