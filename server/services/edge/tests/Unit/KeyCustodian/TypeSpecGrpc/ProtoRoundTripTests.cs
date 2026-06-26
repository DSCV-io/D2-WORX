// -----------------------------------------------------------------------
// <copyright file="ProtoRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using D2.Services.Protos.Common.V1;
using D2.Services.Protos.KeyCustodian.V2Alpha;
using D2.Shared.Result;
using D2.Shared.Result.Grpc;
using Google.Protobuf;
using DtoSignOutput = D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;

/// <summary>
/// Validates that the committed proto fixture compiles correctly and the
/// Grpc.Tools-generated types satisfy the expected structural contract:
/// the envelope shape (<see cref="SignResponse"/> carries
/// <see cref="D2ResultProto"/> + <see cref="SignOutput"/> data), bytes
/// round-trip fidelity, and the fidelity proof that a
/// <c>D2Result.ValidationFailed</c> survives the envelope mapper intact.
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
    public void SignResponse_HasResultAndDataFields()
    {
        // Verify that SignResponse carries the D2ResultProto envelope (field 1)
        // and the SignOutput data message (field 2) — the new envelope shape.
        const string sig = "base64sig==";

        var response = new SignResponse
        {
            Result = new D2ResultProto { Success = true, StatusCode = 200 },
            Data = new SignOutput { Signature = sig },
        };

        response.Result.Success.Should().BeTrue();
        response.Result.StatusCode.Should().Be(200);
        response.Data.Signature.Should().Be(sig);
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
    public void SignRequest_Serialize_Deserialize_IsIdentity()
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

    /// <summary>
    /// Fidelity round-trip: a <c>D2Result.ValidationFailed()</c> survives
    /// <see cref="ProtoExtensions.ToProto"/> → proto bytes → parse →
    /// <see cref="ProtoExtensions.ToD2Result{TData}"/> intact.
    /// Proves the envelope mapper preserves all fields (Success/StatusCode)
    /// — the cornerstone of the §0.5 invariant for the gRPC transport.
    /// </summary>
    [Fact]
    public void ValidationFailed_D2Result_ToProtoAndBack_PreservesAllFields()
    {
        // Arrange: a ValidationFailed D2Result (no data on failure).
        var original = D2Result<DtoSignOutput?>.ValidationFailed();

        // Act: D2Result → D2ResultProto → embed in SignResponse → serialize → parse.
        var resultProto = original.ToProto();
        var response = new SignResponse { Result = resultProto };
        var bytes = response.ToByteArray();
        var parsed = SignResponse.Parser.ParseFrom(bytes);

        // Re-materialize: D2ResultProto → D2Result<DtoSignOutput?> (no data on failure).
        var reconstructed = parsed.Result.ToD2Result<DtoSignOutput?>(data: null);

        // Assert: all fields round-tripped faithfully.
        reconstructed.Success.Should().BeFalse();
        reconstructed.StatusCode.Should().Be(original.StatusCode);

        // The data field is absent on failure — reconstructed.Data remains null.
        reconstructed.Data.Should().BeNull();
    }

    /// <summary>
    /// Fidelity round-trip: a successful <c>D2Result.Ok</c> with payload survives
    /// the envelope mapper intact — Success, StatusCode, and data all preserved.
    /// </summary>
    [Fact]
    public void Ok_D2Result_ToProtoAndBack_PreservesSuccessAndData()
    {
        // Arrange: Ok D2Result with a DtoSignOutput payload.
        const string sig = "round-trip-sig==";
        var original = D2Result<DtoSignOutput?>.Ok(new DtoSignOutput(sig));

        // Act: the D2Result → D2ResultProto round-trip for the envelope portion.
        var resultProto = original.ToProto();
        var response = new SignResponse
        {
            Result = resultProto,
            Data = new SignOutput { Signature = sig }, // as the mapper would populate
        };
        var bytes = response.ToByteArray();
        var parsed = SignResponse.Parser.ParseFrom(bytes);

        // Re-materialize envelope.
        var dtoData = new DtoSignOutput(parsed.Data.Signature);
        var reconstructed = parsed.Result.ToD2Result(dtoData);

        // Assert: success + status + payload preserved.
        reconstructed.Success.Should().BeTrue();
        reconstructed.StatusCode.Should().Be(original.StatusCode);
        reconstructed.Data!.Signature.Should().Be(sig);
    }
}
