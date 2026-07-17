// -----------------------------------------------------------------------
// <copyright file="SealedCryptoKatFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Emits the SEALED (version-2) crypto known-answer fixture — the .NET → TS
/// cross-runtime gate for the ECDH-ES → HKDF-SHA256 → AES-256-GCM hybrid. The
/// pinned inputs are the SAME frozen vector as
/// <c>SealedKeyDerivationFreezeTests</c> (synthetic, fixture-only P-256 key
/// material); the emitted frame + intermediate pins (info / shared-secret / DEK
/// hex) are computed through the REAL production derivation + codec, so the TS
/// twin reproducing them proves byte-for-byte agreement and a drift localizes to
/// the failing stage. The recipient private key is emitted so the TS opener can
/// open the frame end-to-end.
/// </summary>
public sealed class SealedCryptoKatFixtureEmitter
{
    private const string CATALOG = "sealed-crypto-kat";

    private const string _SERVICE_ID = "audit";
    private const string _KAT_KID = "seal-kat-kid";
    private const string _KAT_PLAINTEXT = "d2-seal-known-answer";

    private const string _RECIPIENT_PRIVATE_PKCS8_B64 =
        "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgAGdQitJuiEOZLHEa1ooL5nxm" +
        "9k9UDMauc/9PTbrtmbWhRANCAAQcu3gDUuYgdaan/4uF2SnWekAoJSx3nDj2merWTH0mEcok" +
        "rO0jSFyMpMLRNpOdsFH2i9X8AjOs5+Bk+J6A3U7+";

    private const string _EPHEMERAL_PUBLIC_SPKI_B64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEkLO6kUprlh7iYdQiDo5aPVS8z+hxztQoK8m4" +
        "r/v6RRjBqNhhvlsSaW35qLngBsnAubMUIdX1r3wrIuPlZGB+cA==";

    private static readonly byte[] sr_katNonce =
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_KnownAnswer()
    {
        var ephSpki = Convert.FromBase64String(_EPHEMERAL_PUBLIC_SPKI_B64);
        var recipientPkcs8 = Convert.FromBase64String(_RECIPIENT_PRIVATE_PKCS8_B64);
        var serviceIdBytes = Encoding.UTF8.GetBytes(_SERVICE_ID);
        var plaintext = Encoding.UTF8.GetBytes(_KAT_PLAINTEXT);

        using var recipient = ECDiffieHellman.Create();
        recipient.ImportPkcs8PrivateKey(recipientPkcs8, out _);
        using var ephemeral = EcdhP256ImportPublic(ephSpki);
        using var ephemeralPublic = ephemeral.PublicKey;

        var sharedSecret = recipient.DeriveRawSecretAgreement(ephemeralPublic);
        var info = SealedKeyDerivation.BuildInfo(serviceIdBytes, ephSpki);
        var dek = new byte[SealedKeyDerivation.DEK_SIZE_BYTES];
        SealedKeyDerivation.DeriveDek(sharedSecret, serviceIdBytes, ephSpki, dek);

        var ctWithTag = new byte[plaintext.Length + SealedFrameLayout.CONSTRAINT_TAG_LENGTH];

        using (var gcm = new AesGcm(dek, SealedFrameLayout.CONSTRAINT_TAG_LENGTH))
        {
            gcm.Encrypt(
                sr_katNonce,
                plaintext,
                ctWithTag.AsSpan(0, plaintext.Length),
                ctWithTag.AsSpan(plaintext.Length, SealedFrameLayout.CONSTRAINT_TAG_LENGTH),
                serviceIdBytes);
        }

        var frame = SealedFrame.Encode(_KAT_KID, ephSpki, sr_katNonce, ctWithTag);

        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["serviceId"] = _SERVICE_ID,
            ["recipientKid"] = _KAT_KID,
            ["plaintextUtf8"] = _KAT_PLAINTEXT,
            ["recipientPrivatePkcs8Base64"] = _RECIPIENT_PRIVATE_PKCS8_B64,
            ["ephemeralPublicSpkiBase64"] = _EPHEMERAL_PUBLIC_SPKI_B64,
            ["nonceHex"] = Convert.ToHexString(sr_katNonce),
            ["infoHex"] = Convert.ToHexString(info),
            ["sharedSecretHex"] = Convert.ToHexString(sharedSecret),
            ["dekHex"] = Convert.ToHexString(dek),
            ["frameHex"] = Convert.ToHexString(frame),
        };

        CryptographicOperations.ZeroMemory(sharedSecret);
        CryptographicOperations.ZeroMemory(dek);

        FixturePathHelpers.WriteFixture(CATALOG, "known-answer", data);
    }

    private static ECDiffieHellman EcdhP256ImportPublic(byte[] spki)
    {
        var ecdh = ECDiffieHellman.Create();
        ecdh.ImportSubjectPublicKeyInfo(spki, out _);

        return ecdh;
    }
}
