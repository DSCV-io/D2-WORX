// -----------------------------------------------------------------------
// <copyright file="SealedKeyDerivationFreezeTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Wire-permanence freeze gate for the sealed key-derivation and AEAD-binding
/// conventions. The pinned values below are FROZEN — HKDF info =
/// <c>"d2-seal-v1"</c> ‖ len16BE(serviceId) ‖ serviceId ‖ len16BE(ephSPKI) ‖
/// ephSPKI, HKDF salt = UTF-8(serviceId), AES-GCM AAD = UTF-8(serviceId).
/// Any drift in the label, the length-delimited info encoding, the salt, the
/// AAD, the HKDF hash, or the frame encoding breaks the known-answer vector
/// and MUST fail here: changing any of these breaks decryption of every
/// frame sealed before the change.
/// </summary>
public sealed class SealedKeyDerivationFreezeTests
{
    // ---------------------------------------------------------------
    // The frozen known-answer vector. All material is synthetic test
    // data generated once and pinned; the recipient/ephemeral keys exist
    // only inside this test.
    // ---------------------------------------------------------------
    private const string _SERVICE_ID = "audit";
    private const string _KAT_KID = "seal-kat-kid";
    private const string _KAT_PLAINTEXT = "d2-seal-known-answer";

    private const string _RECIPIENT_PRIVATE_PKCS8_B64 =
        "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgAGdQitJuiEOZLHEa1ooL5nxm" +
        "9k9UDMauc/9PTbrtmbWhRANCAAQcu3gDUuYgdaan/4uF2SnWekAoJSx3nDj2merWTH0mEcok" +
        "rO0jSFyMpMLRNpOdsFH2i9X8AjOs5+Bk+J6A3U7+";

    private const string _EPHEMERAL_PRIVATE_PKCS8_B64 =
        "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgoI8vkqUSmK9kn09z7OUU7Kz9" +
        "SVh8hMY9A2PAiBldUpWhRANCAASQs7qRSmuWHuJh1CIOjlo9VLzP6HHO1Cgrybiv+/pFGMGo" +
        "2GG+WxJpbfmoueAGycC5sxQh1fWvfCsi4+VkYH5w";

    private const string _EPHEMERAL_PUBLIC_SPKI_B64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEkLO6kUprlh7iYdQiDo5aPVS8z+hxztQoK8m4" +
        "r/v6RRjBqNhhvlsSaW35qLngBsnAubMUIdX1r3wrIuPlZGB+cA==";

    /// <summary>The complete frozen sealed frame produced from the pinned inputs.</summary>
    private const string _KAT_FRAME_HEX =
        "020C7365616C2D6B61742D6B6964005B3059301306072A8648CE3D020106082A8648CE3D" +
        "0301070342000490B3BA914A6B961EE261D4220E8E5A3D54BCCFE871CED4282BC9B8AFFB" +
        "FA4518C1A8D861BE5B12696DF9A8B9E006C9C0B9B31421D5F5AF7C2B22E3E564607E7000" +
        "0102030405060708090A0B24645B3E8AF74C10CB1F8975C91756975014883A1DCF988950" +
        "A08304107CDD64B5DC3718";

    /// <summary>The frozen HKDF info bytes for (serviceId=audit, the pinned ephemeral).</summary>
    private const string _KAT_INFO_HEX =
        "64322D7365616C2D763100056175646974005B3059301306072A8648CE3D020106082A86" +
        "48CE3D0301070342000490B3BA914A6B961EE261D4220E8E5A3D54BCCFE871CED4282BC9" +
        "B8AFFBFA4518C1A8D861BE5B12696DF9A8B9E006C9C0B9B31421D5F5AF7C2B22E3E56460" +
        "7E70";

    private static readonly byte[] sr_katNonce =
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    // ---------------------------------------------------------------
    // The headline known-answer test.
    // ---------------------------------------------------------------

    [Fact]
    public void SealConstruction_FrozenKnownAnswer_RoundTripsPinnedVector()
    {
        // 1. Recompute the frame from the pinned inputs through the REAL
        //    derivation + AEAD + codec — any drift in info/salt/AAD/HKDF/
        //    frame encoding produces different bytes and fails the pin.
        var ephSpki = Convert.FromBase64String(_EPHEMERAL_PUBLIC_SPKI_B64);
        var serviceIdBytes = Encoding.UTF8.GetBytes(_SERVICE_ID);
        var plaintext = Encoding.UTF8.GetBytes(_KAT_PLAINTEXT);

        using var ephemeral = ECDiffieHellman.Create();
        ephemeral.ImportPkcs8PrivateKey(
            Convert.FromBase64String(_EPHEMERAL_PRIVATE_PKCS8_B64), out _);
        using var recipient = ECDiffieHellman.Create();
        recipient.ImportPkcs8PrivateKey(
            Convert.FromBase64String(_RECIPIENT_PRIVATE_PKCS8_B64), out _);
        using var recipientPublic = recipient.PublicKey;

        var sharedSecret = ephemeral.DeriveRawSecretAgreement(recipientPublic);
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

        Convert.ToHexString(frame).Should().Be(_KAT_FRAME_HEX);

        // 2. The full PRODUCTION open path recovers the pinned plaintext
        //    from the pinned frame bytes.
        using var privateRing = new RecipientPrivateKeyring(
            _SERVICE_ID,
            new Dictionary<string, byte[]>
            {
                [_KAT_KID] = Convert.FromBase64String(_RECIPIENT_PRIVATE_PKCS8_B64),
            });
        var opener = new PayloadOpener(privateRing);

        var opened = opener.Open(Convert.FromHexString(_KAT_FRAME_HEX));

        Encoding.UTF8.GetString(opened).Should().Be(_KAT_PLAINTEXT);
    }

    // ---------------------------------------------------------------
    // Component freeze pins.
    // ---------------------------------------------------------------

    [Fact]
    public void HkdfInfo_IsD2SealV1_LengthDelimitedServiceIdAndEphSpki()
    {
        var ephSpki = Convert.FromBase64String(_EPHEMERAL_PUBLIC_SPKI_B64);
        var serviceIdBytes = Encoding.UTF8.GetBytes(_SERVICE_ID);

        var info = SealedKeyDerivation.BuildInfo(serviceIdBytes, ephSpki);

        // Pinned bytes.
        Convert.ToHexString(info).Should().Be(_KAT_INFO_HEX);

        // Independent structural reconstruction of the delimited form:
        // "d2-seal-v1" ‖ len16BE(serviceId) ‖ serviceId ‖ len16BE(ephSPKI)
        // ‖ ephSPKI.
        var expected = new List<byte>();
        expected.AddRange("d2-seal-v1"u8.ToArray());
        expected.Add((byte)(serviceIdBytes.Length >> 8));
        expected.Add((byte)(serviceIdBytes.Length & 0xFF));
        expected.AddRange(serviceIdBytes);
        expected.Add((byte)(ephSpki.Length >> 8));
        expected.Add((byte)(ephSpki.Length & 0xFF));
        expected.AddRange(ephSpki);

        info.Should().Equal(expected);
    }

    [Fact]
    public void InfoLabel_IsFrozenD2SealV1()
        => SealedKeyDerivation.INFO_LABEL.Should().Be("d2-seal-v1");

    [Fact]
    public void HkdfSalt_IsRecipientServiceId()
    {
        // DeriveDek must equal a direct HKDF-SHA256 with salt =
        // UTF-8(serviceId) and info = BuildInfo(...) — pinning that the salt
        // is the service id (and nothing else) and the hash is SHA-256.
        var ephSpki = Convert.FromBase64String(_EPHEMERAL_PUBLIC_SPKI_B64);
        var serviceIdBytes = Encoding.UTF8.GetBytes(_SERVICE_ID);
        var sharedSecret = new byte[32];

        for (var i = 0; i < sharedSecret.Length; i++) sharedSecret[i] = (byte)(i * 3);

        var viaDerivation = new byte[SealedKeyDerivation.DEK_SIZE_BYTES];
        SealedKeyDerivation.DeriveDek(sharedSecret, serviceIdBytes, ephSpki, viaDerivation);

        var direct = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: sharedSecret,
            outputLength: SealedKeyDerivation.DEK_SIZE_BYTES,
            salt: serviceIdBytes,
            info: SealedKeyDerivation.BuildInfo(serviceIdBytes, ephSpki));

        viaDerivation.Should().Equal(direct);
    }

    [Fact]
    public void Aad_IsRecipientServiceId()
    {
        // Seal via the PRODUCTION sealer, then decrypt manually: only
        // aad = UTF-8(serviceId) authenticates; any other AAD fails. That IS
        // the frozen AEAD binding.
        var keypair = SealedTestKeys.GenerateKeypair();
        var sealer = new PayloadSealer(
            SealedTestKeys.PublicKeyring(_SERVICE_ID, "kid-aad", keypair));
        var plaintext = Encoding.UTF8.GetBytes(_KAT_PLAINTEXT);

        var framed = sealer.Seal(plaintext);
        var view = SealedFrame.Decode(framed);

        using var recipient = ECDiffieHellman.Create();
        recipient.ImportPkcs8PrivateKey(keypair.PrivatePkcs8, out _);
        using var ephemeral = ECDiffieHellman.Create();
        ephemeral.ImportSubjectPublicKeyInfo(view.EphemeralPublicSpki, out _);
        using var ephemeralPublic = ephemeral.PublicKey;

        var sharedSecret = recipient.DeriveRawSecretAgreement(ephemeralPublic);
        var dek = new byte[SealedKeyDerivation.DEK_SIZE_BYTES];
        SealedKeyDerivation.DeriveDek(
            sharedSecret,
            Encoding.UTF8.GetBytes(_SERVICE_ID),
            view.EphemeralPublicSpki,
            dek);

        var ciphertextLength =
            view.CiphertextWithTag.Length - SealedFrameLayout.CONSTRAINT_TAG_LENGTH;
        var recovered = new byte[ciphertextLength];
        var nonce = view.Nonce.ToArray();
        var ciphertext = view.CiphertextWithTag[..ciphertextLength].ToArray();
        var tag = view.CiphertextWithTag[ciphertextLength..].ToArray();

        using (var gcm = new AesGcm(dek, SealedFrameLayout.CONSTRAINT_TAG_LENGTH))
        {
            // The frozen AAD authenticates...
            gcm.Decrypt(nonce, ciphertext, tag, recovered, Encoding.UTF8.GetBytes(_SERVICE_ID));
            recovered.Should().Equal(plaintext);

            // ...and any other AAD does not.
            // ReSharper disable once AccessToDisposedClosure -- wrongAad is invoked
            // synchronously inside Should().Throw(), before gcm disposes.
            var wrongAad = () => gcm.Decrypt(
                nonce, ciphertext, tag, recovered, Encoding.UTF8.GetBytes("courier"));
            wrongAad.Should().Throw<AuthenticationTagMismatchException>();
        }
    }
}
