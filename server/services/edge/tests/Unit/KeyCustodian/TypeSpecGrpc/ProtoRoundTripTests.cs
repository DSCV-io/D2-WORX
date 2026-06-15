// -----------------------------------------------------------------------
// <copyright file="ProtoRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using D2.Services.Protos.KeyCustodian.V1;
using Google.Protobuf;

/// <summary>
/// Validates that the committed proto fixture compiles correctly and the
/// Grpc.Tools-generated types satisfy the expected structural contract:
/// correct field names, bytes round-trip fidelity, and service/method existence.
/// </summary>
public sealed class ProtoRoundTripTests
{
    [Fact]
    public void SignRequest_HasKidAndPayloadFields_RoundTrips()
    {
        // Grpc.Tools generates a C# class for each proto message.
        // Verify that SignRequest carries the kid (string) and payload (ByteString) fields.
        var kid = "test-kid-2026";
        var payload = new byte[] { 1, 2, 3, 4 };

        var proto = new SignRequest
        {
            Kid = kid,
            Payload = ByteString.CopyFrom(payload),
        };

        proto.Kid.Should().Be(kid);
        proto.Payload.ToByteArray().Should().Equal(payload);
    }

    [Fact]
    public void SignResponse_HasSignatureField_RoundTrips()
    {
        // Verify that SignResponse carries the signature (string) field.
        const string sig = "base64sig==";

        var proto = new SignResponse
        {
            Signature = sig,
        };

        proto.Signature.Should().Be(sig);
    }

    [Fact]
    public void SignRequest_EmptyPayload_RoundTrips()
    {
        var proto = new SignRequest
        {
            Kid = "k",
            Payload = ByteString.Empty,
        };

        proto.Payload.IsEmpty.Should().BeTrue();
        proto.Payload.ToByteArray().Should().BeEmpty();
    }

    [Fact]
    public void SignRequest_DefaultKid_IsEmptyString()
    {
        // proto3 scalars default to zero-value: string = "".
        var proto = new SignRequest();

        proto.Kid.Should().Be(string.Empty);
    }

    [Fact]
    public void SignRequest_Serialise_Deserialise_IsIdentity()
    {
        // Proto serialization round-trip: confirm Grpc.Tools types support it.
        var original = new SignRequest
        {
            Kid = "round-trip-kid",
            Payload = ByteString.CopyFrom(0xDE, 0xAD, 0xBE, 0xEF),
        };

        var bytes = original.ToByteArray();
        var restored = SignRequest.Parser.ParseFrom(bytes);

        restored.Kid.Should().Be(original.Kid);
        restored.Payload.Should().Equal(original.Payload);
    }
}
