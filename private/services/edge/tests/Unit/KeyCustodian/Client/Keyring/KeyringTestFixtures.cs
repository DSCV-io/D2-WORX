// -----------------------------------------------------------------------
// <copyright file="KeyringTestFixtures.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using System.Text;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Result.Grpc;
using global::D2.Services.Protos.KeyCustodian.V2Alpha;
using Google.Protobuf;
using ProtoKeyringEntry = global::D2.Services.Protos.KeyCustodian.V2Alpha.KeyringEntry;

/// <summary>
/// Shared keyring test material: a fixture key-domain, deterministic key bytes, keyring
/// builders, and gRPC reply builders used across the keyring unit tests.
/// </summary>
internal static class KeyringTestFixtures
{
    // An obviously-named fixture domain (never a deploy domain).
    public const string FIXTURE_DOMAIN = "fixture-keyring-domain";

    public const string KID_ONE = "fixture-kid-1";
    public const string KID_TWO = "fixture-kid-2";

    // Deterministic 32-byte keys so a frame encrypted under an early keyring still
    // decrypts against a later (rotated) keyring that carries the same kid.
    public static readonly byte[] SR_KeyOne = MakeKey(0x11);
    public static readonly byte[] SR_KeyTwo = MakeKey(0x22);

    public static byte[] AadFor(string domain) => Encoding.UTF8.GetBytes("d2/" + domain);

    // Reads the kid a frame was encrypted under, via the public spec-emitted layout
    // constants (the frame codec itself is internal to the encryption lib).
    public static string ReadFrameKid(byte[] frame)
    {
        int kidLength = frame[EncryptionFrameLayout.KID_LENGTH_OFFSET];

        return Encoding.UTF8.GetString(frame, EncryptionFrameLayout.KID_OFFSET, kidLength);
    }

    // A keyring with a single active kid.
    public static PayloadCryptoKeyring SingleKidKeyring(string domain = FIXTURE_DOMAIN)
        => new(
            KID_ONE,
            new Dictionary<string, byte[]>(StringComparer.Ordinal) { [KID_ONE] = SR_KeyOne },
            AadFor(domain));

    // A rotated keyring: kid2 active, kid1 retiring (same kid1 key bytes).
    public static PayloadCryptoKeyring RotatedKeyring(string domain = FIXTURE_DOMAIN)
        => new(
            KID_TWO,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [KID_ONE] = SR_KeyOne,
                [KID_TWO] = SR_KeyTwo,
            },
            AadFor(domain));

    // Builds a gRPC keyring reply body carrying the envelope + optional data.
    public static GetKeyringResponse Reply(D2Result envelope, GetKeyringOutput? data = null)
    {
        var response = new GetKeyringResponse { Result = envelope.ToProto() };

        if (data is not null)
            response.Data = data;

        return response;
    }

    // A well-formed proto keyring output (active kid present in the entries).
    public static GetKeyringOutput WellFormedOutput(string domain = FIXTURE_DOMAIN)
    {
        var output = new GetKeyringOutput
        {
            ActiveKid = KID_ONE,
            AadContext = ByteString.CopyFrom(AadFor(domain)),
        };
        output.Entries.Add(new ProtoKeyringEntry
        {
            Kid = KID_ONE,
            KeyBytes = ByteString.CopyFrom(SR_KeyOne),
        });

        return output;
    }

    private static byte[] MakeKey(byte seed)
    {
        var key = new byte[PayloadCryptoKeyring.KEY_SIZE_BYTES];

        for (var i = 0; i < key.Length; i++)
            key[i] = (byte)(seed + i);

        return key;
    }
}
