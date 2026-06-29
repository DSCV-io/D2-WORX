// -----------------------------------------------------------------------
// <copyright file="PlaceOrderTolerantReaderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpcPredicate;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.Tests.TypeSpecGrpcPredicate.Generated;
using D2.Services.Protos.PredicateFixturesV2.V1;
using D2.Shared.Resilience.Pipeline;
using D2.Shared.Resilience.Retry;
using D2.Shared.Result;
using D2.Shared.Result.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DtoOrderV2Input = D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderV2FixtureInput;
using DtoOrderV2Output = D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderV2FixtureOutput;
using ProtoOrderFixtureLine = D2.Services.Protos.PredicateFixturesV2.V1.PlaceOrderFixtureLine;
using ProtoOrderV2Customer = D2.Services.Protos.PredicateFixturesV2.V1.PlaceOrderV2FixtureCustomer;
using ProtoOrderV2Output = D2.Services.Protos.PredicateFixturesV2.V1.PlaceOrderV2FixtureOutput;

/// <summary>
/// Pins the Tolerant Reader property of the generated <see cref="PredicateFixturesV2GrpcClient"/>
/// proto decode path. Uses the same in-memory <see cref="TestServer"/> + <see cref="GrpcChannel"/>
/// harness as <see cref="PlaceOrderV2RoundTripTests"/>. The shim emits a wire payload that carries
/// an EXTRA field number (4) not declared in the committed proto; proto3 preserves unknown fields
/// in the parsed message and re-emits them on serialize, so the client-side parser receives them
/// and skips them. The generated client mapper reads only the named getters, so the unknown field
/// never reaches the DTO.
/// <list type="bullet">
///   <item>TR1 — a proto response with an unknown field number decodes the known fields and ignores
///   the unknown one; no exception is thrown.</item>
///   <item>TR2 — the forward-compat case: a "newer producer" adds an optional field; the
///   prior-shaped mapper reads the message without error and the known fields survive with full
///   fidelity.</item>
/// </list>
/// </summary>
public sealed class PlaceOrderTolerantReaderTests
{
    // ---------------------------------------------------------------------------
    // TR1: a proto response carrying an unknown field number decodes the known fields.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderV2_ProtoWithUnknownField_DecodesKnownFields_IgnoresUnknown_NoThrow()
    {
        const string expected_order_code = "ORD-1";
        const string expected_tier = "GOLD";

        // Append an unknown varint field (field 4) to the raw serialized PlaceOrderV2FixtureOutput
        // bytes, then parse back into the proto type so Google.Protobuf preserves the
        // unknown field in UnknownFields.  When the shim serializes the PlaceOrderV2FixtureResponse
        // over gRPC, the unknown bytes are re-emitted; the client-side parser skips them.
        var rawOutput = AppendUnknownVarintField(
            BuildOutputProto(
                expected_order_code,
                [new ProtoOrderFixtureLine { Status = "SHIPPED" }],
                new ProtoOrderV2Customer { Tier = expected_tier }),
            unknownFieldNumber: 4,
            smallVarintValue: 99);

        var dataProto = ProtoOrderV2Output.Parser.ParseFrom(rawOutput);

        var shim = new TolerantReaderShim(() => BuildSuccessResponse(dataProto));
        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        var result = await client.PlaceOrderV2FixtureAsync(new DtoOrderV2Input("cust-1"));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.OrderCode.Should().Be(expected_order_code);
        result.Data!.Customer.Should().NotBeNull();
        result.Data!.Customer!.Tier.Should().Be(expected_tier);
        result.Data!.Lines.Should().ContainSingle(l => l.Status == "SHIPPED");
        shim.CallCount.Should().Be(1, "pass-through pipeline must not retry a successful response");
    }

    // ---------------------------------------------------------------------------
    // TR2: forward-compat — a "newer producer" appended an optional field; the
    //      prior-shaped mapper reads it and populates the known fields faithfully.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderV2_ForwardCompatAddedOptionalField_ReadByCurrentMapper_KnownFieldsSurvive()
    {
        // Imagine a future spec revision adds optional field 4 to PlaceOrderV2FixtureOutput.
        // The current generated mapper only knows fields 1-3; field 4 is silently
        // dropped at the proto parser level, and the DTO is populated with full fidelity.
        const string expected_order_code = "ORD-FC";
        const string expected_tier = "SILVER";

        var rawOutput = AppendUnknownVarintField(
            BuildOutputProto(
                expected_order_code,
                [new ProtoOrderFixtureLine { Status = "DELIVERED" }, new ProtoOrderFixtureLine { Status = "SHIPPED" }],
                new ProtoOrderV2Customer { Tier = expected_tier }),
            unknownFieldNumber: 4,
            smallVarintValue: 42);

        var dataProto = ProtoOrderV2Output.Parser.ParseFrom(rawOutput);

        var shim = new TolerantReaderShim(() => BuildSuccessResponse(dataProto));
        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        var result = await client.PlaceOrderV2FixtureAsync(new DtoOrderV2Input("cust-fc"));

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data.Should().NotBeNull();
        result.Data!.OrderCode.Should().Be(expected_order_code);
        result.Data!.Customer!.Tier.Should().Be(expected_tier);
        result.Data!.Lines.Should().HaveCount(2);
        result.Data!.Lines[0].Status.Should().Be("DELIVERED");
        result.Data!.Lines[1].Status.Should().Be("SHIPPED");
        shim.CallCount.Should().Be(1, "pass-through pipeline must not retry a successful response");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static byte[] BuildOutputProto(
        string orderCode,
        IEnumerable<ProtoOrderFixtureLine> lines,
        ProtoOrderV2Customer? customer)
    {
        var proto = new ProtoOrderV2Output { OrderCode = orderCode };

        foreach (var line in lines)
            proto.Lines.Add(line);

        if (customer is not null)
            proto.Customer = customer;

        using var ms = new MemoryStream();
        proto.WriteTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Appends an unknown varint field (wire type 0) to <paramref name="knownBytes"/>.
    /// The tag is encoded as (fieldNumber &lt;&lt; 3) | 0.  For field numbers and values
    /// &lt; 16 and &lt; 128 respectively (both true for our test fixtures) the tag and
    /// value each fit in a single byte — no multi-byte varint encoding is needed.
    /// </summary>
    private static byte[] AppendUnknownVarintField(
        byte[] knownBytes, int unknownFieldNumber, byte smallVarintValue)
    {
        // Proto tag: (fieldNumber << 3) | wireType(0 = varint).
        // Field 4 → tag = (4 << 3) | 0 = 32 = 0x20.
        // Both tag and value fit in one byte each (< 128).
        byte tagByte = (byte)((unknownFieldNumber << 3) | 0);

        var result = new byte[knownBytes.Length + 2];
        knownBytes.CopyTo(result, 0);
        result[knownBytes.Length] = tagByte;
        result[knownBytes.Length + 1] = smallVarintValue;

        return result;
    }

    private static PlaceOrderV2FixtureResponse BuildSuccessResponse(ProtoOrderV2Output data)
    {
        var businessResult = D2Result<DtoOrderV2Output?>.Ok();
        return new PlaceOrderV2FixtureResponse { Result = businessResult.ToProto(), Data = data };
    }

    private static async Task<IHost> BuildHost(
        PredicateFixturesOrdersV2.PredicateFixturesOrdersV2Base shim)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(shim);
                    services.AddRouting();
                    services.AddGrpc();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<PredicateFixturesOrdersV2.PredicateFixturesOrdersV2Base>();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static PredicateFixturesV2GrpcClient BuildClient(
        IHost host,
        ResilientPipeline<string, DtoOrderV2Output?> pipeline)
    {
        var httpClient = host.GetTestClient();
        var channel = GrpcChannel.ForAddress(
            httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = httpClient });

        var stub = new PredicateFixturesOrdersV2.PredicateFixturesOrdersV2Client(channel);
        return new PredicateFixturesV2GrpcClient(stub, pipeline);
    }

    private static ResilientPipeline<string, DtoOrderV2Output?> BuildPassThroughPipeline()
    {
        var builder = new ResilientPipelineBuilder<string, DtoOrderV2Output?>(
            new ServiceCollection().BuildServiceProvider());

        builder.UseRetries(new RetryOptions<DtoOrderV2Output?>
        {
            MaxAttempts = 1,
            BaseDelayMs = 1,
            Jitter = false,
            IsTransient = ex =>
                ex is D2GeneratedBusinessRetrySignal
                || (ex is RpcException r && ProtoExtensions.IsTransientGrpcException(r)),
        });

        return builder.Build();
    }

    // ---------------------------------------------------------------------------
    // Server shim — returns the pre-built response (which carries the unknown field
    // preserved in its PlaceOrderV2FixtureOutput.UnknownFields, so the wire bytes the
    // client receives include the unknown field tag + value).
    // ---------------------------------------------------------------------------

    private sealed class TolerantReaderShim(Func<PlaceOrderV2FixtureResponse> responseFactory)
        : PredicateFixturesOrdersV2.PredicateFixturesOrdersV2Base
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override Task<PlaceOrderV2FixtureResponse> PlaceOrderV2Fixture(
            PlaceOrderV2FixtureRequest request, ServerCallContext context)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(responseFactory());
        }
    }
}
