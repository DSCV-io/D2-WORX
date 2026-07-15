// -----------------------------------------------------------------------
// <copyright file="SealingTestFixtures.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Sealing;

using System.Security.Cryptography;
using DcsvIo.D2.Encryption;

/// <summary>
/// Shared sealing test material: a fixture recipient service id, two P-256 sealing keypairs,
/// public / private keyring builders, and a frame-sealing helper used across the sealer /
/// opener runtime unit tests. Fixture key material is generated per test run (never a deploy
/// key) and is clearly fixture-named.
/// </summary>
internal static class SealingTestFixtures
{
    /// <summary>An obviously-named fixture recipient service id (never a deploy service).</summary>
    public const string FIXTURE_SERVICE_ID = "seal-fixture-recipient";

    /// <summary>The <c>seal:&lt;serviceId&gt;</c> rotation domain for the fixture recipient.</summary>
    public const string SEAL_DOMAIN = "seal:seal-fixture-recipient";

    public const string KID_ONE = "seal-fixture-kid-1";
    public const string KID_TWO = "seal-fixture-kid-2";

    // Two independent P-256 keypairs generated once per run (deterministic within a run so an
    // opener holding kid1 can open a frame a sealer sealed under the same kid1 public key).
    private static readonly (byte[] Spki, byte[] Pkcs8) sr_keyOne = GenerateKeyPair();
    private static readonly (byte[] Spki, byte[] Pkcs8) sr_keyTwo = GenerateKeyPair();

    /// <summary>A private keyring holding kid1 only.</summary>
    /// <param name="serviceId">The recipient service id (defaults to the fixture id).</param>
    /// <returns>A single-kid private keyring.</returns>
    public static RecipientPrivateKeyring SingleKidPrivateKeyring(
        string serviceId = FIXTURE_SERVICE_ID)
        => new(
            serviceId,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [KID_ONE] = sr_keyOne.Pkcs8,
            });

    /// <summary>A rotated private keyring holding kid1 (retiring) + kid2.</summary>
    /// <param name="serviceId">The recipient service id (defaults to the fixture id).</param>
    /// <returns>A two-kid private keyring.</returns>
    public static RecipientPrivateKeyring RotatedPrivateKeyring(
        string serviceId = FIXTURE_SERVICE_ID)
        => new(
            serviceId,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [KID_ONE] = sr_keyOne.Pkcs8,
                [KID_TWO] = sr_keyTwo.Pkcs8,
            });

    /// <summary>A public keyring with kid1 active.</summary>
    /// <param name="serviceId">The recipient service id (defaults to the fixture id).</param>
    /// <returns>A single-kid public keyring (kid1 active).</returns>
    public static RecipientPublicKeyring SingleKidPublicKeyring(
        string serviceId = FIXTURE_SERVICE_ID)
        => new(
            serviceId,
            KID_ONE,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [KID_ONE] = sr_keyOne.Spki,
            });

    /// <summary>A rotated public keyring with kid2 active (kid1 retiring).</summary>
    /// <param name="serviceId">The recipient service id (defaults to the fixture id).</param>
    /// <returns>A two-kid public keyring (kid2 active).</returns>
    public static RecipientPublicKeyring RotatedPublicKeyring(
        string serviceId = FIXTURE_SERVICE_ID)
        => new(
            serviceId,
            KID_TWO,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [KID_ONE] = sr_keyOne.Spki,
                [KID_TWO] = sr_keyTwo.Spki,
            });

    /// <summary>Seals a plaintext under a public keyring's active kid (a production sealer).</summary>
    /// <param name="plaintext">The plaintext to seal.</param>
    /// <param name="publicKeyring">The recipient's public keyring.</param>
    /// <returns>A version-2 sealed frame.</returns>
    public static byte[] Seal(byte[] plaintext, RecipientPublicKeyring publicKeyring)
        => new PayloadSealer(publicKeyring).Seal(plaintext);

    private static (byte[] Spki, byte[] Pkcs8) GenerateKeyPair()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return (ecdh.ExportSubjectPublicKeyInfo(), ecdh.ExportPkcs8PrivateKey());
    }
}
