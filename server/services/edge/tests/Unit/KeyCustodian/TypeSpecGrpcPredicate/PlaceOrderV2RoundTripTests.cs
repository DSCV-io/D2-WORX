// -----------------------------------------------------------------------
// <copyright file="PlaceOrderV2RoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpcPredicate;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.Tests.TypeSpecGrpcPredicate.Generated;
using D2.Services.Protos.PredicateFixturesV2.V1;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Resilience.Pipeline;
using D2.Shared.Resilience.Retry;
using D2.Shared.Result;
using D2.Shared.Result.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DtoOrderLine = D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderLine;
using DtoOrderV2Customer = D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderV2Customer;
using DtoOrderV2Input = D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderV2Input;
using DtoOrderV2Output = D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderV2Output;
using ProtoOrderLine = D2.Services.Protos.PredicateFixturesV2.V1.PlaceOrderLine;
using ProtoOrderV2Customer = D2.Services.Protos.PredicateFixturesV2.V1.PlaceOrderV2Customer;
using ProtoOrderV2Output = D2.Services.Protos.PredicateFixturesV2.V1.PlaceOrderV2Output;

/// <summary>
/// In-memory harness tests for the generated <see cref="PredicateFixturesV2GrpcClient"/> — the
/// NESTED-MODEL + array-of-MODEL transport-mapper wire path. Hosts a concrete shim extending
/// <see cref="PredicateFixturesOrdersV2.PredicateFixturesOrdersV2Base"/> via an in-process
/// <see cref="TestServer"/> + <see cref="GrpcChannel"/> (no sockets) against the REAL
/// <see cref="D2.Shared.Resilience"/> keyed pipeline + the REAL envelope mapper + the REAL
/// generated nested sub-mappers, proving an optional nested model (<c>customer</c>) + an
/// array-of-MODEL (<c>lines</c>) survive proto ↔ DTO with full fidelity:
/// <list type="bullet">
///   <item>R1 — a populated nested customer (tier="GOLD") + a 2-element lines array round-trip
///   with full fidelity;</item>
///   <item>R2 — an UNSET nested customer → <c>Data.Customer is null</c> (proto3 implicit presence,
///   no NRE);</item>
///   <item>R3 — an EMPTY lines array → <c>Data.Lines</c> empty/non-null (no NRE);</item>
///   <item>R4 — the flat <c>customerId</c> request round-trips (the shim echoes it back);</item>
///   <item>R5 — the @d2Resilience predicate still fires over the now-wire-mappable nested output
///   (customer.tier=="TRIAL" drives the retry sentinel → <c>CallCount &gt; 1</c>, restored verbatim
///   on exhaust);</item>
///   <item>R6 — §1.3 DI resolution: the V2 client + its keyed pipeline resolve.</item>
/// </list>
/// These are IN-MEMORY only; the over-the-wire (two-process) test rides a later step.
/// </summary>
public sealed class PlaceOrderV2RoundTripTests
{
    // ---------------------------------------------------------------------------
    // R1: a populated nested customer + a multi-element array-of-model round-trip.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderV2_NestedCustomerAndLinesArray_RoundTripFullFidelity()
    {
        var shim = new EchoOrderV2ShimBase(
            () => D2Result<DtoOrderV2Output?>.Ok(new DtoOrderV2Output(
                "order-1",
                [new DtoOrderLine("SHIPPED"), new DtoOrderLine("DELIVERED")],
                new DtoOrderV2Customer("GOLD"))));
        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        var result = await client.PlaceOrderV2Async(new DtoOrderV2Input("cust-1"));

        result.Success.Should().BeTrue();
        result.Data!.OrderCode.Should().Be("order-1");

        // Nested model survived proto ↔ DTO.
        result.Data!.Customer.Should().NotBeNull();
        result.Data!.Customer!.Tier.Should().Be("GOLD");

        // Array-of-model survived with BOTH elements + their nested scalar.
        result.Data!.Lines.Should().HaveCount(2);
        result.Data!.Lines[0].Status.Should().Be("SHIPPED");
        result.Data!.Lines[1].Status.Should().Be("DELIVERED");
    }

    // ---------------------------------------------------------------------------
    // R2: an absent (unset) nullable nested model → null on the DTO (no NRE).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderV2_AbsentNullableCustomer_MapsToNull_NoNre()
    {
        var shim = new EchoOrderV2ShimBase(
            () => D2Result<DtoOrderV2Output?>.Ok(new DtoOrderV2Output(
                "order-2",
                [new DtoOrderLine("PENDING")],
                Customer: null)));
        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        var result = await client.PlaceOrderV2Async(new DtoOrderV2Input("cust-1"));

        result.Success.Should().BeTrue();

        // proto3 implicit presence: an unset message field is null, NOT a default instance.
        result.Data!.Customer.Should().BeNull();
        result.Data!.Lines.Should().ContainSingle();
    }

    // ---------------------------------------------------------------------------
    // R3: an empty array-of-model → empty (non-null) list, no NRE.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderV2_EmptyLinesArray_MapsToEmptyList_NoNre()
    {
        var shim = new EchoOrderV2ShimBase(
            () => D2Result<DtoOrderV2Output?>.Ok(new DtoOrderV2Output(
                "order-3",
                [],
                new DtoOrderV2Customer("SILVER"))));
        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        var result = await client.PlaceOrderV2Async(new DtoOrderV2Input("cust-1"));

        result.Success.Should().BeTrue();
        result.Data!.Lines.Should().NotBeNull();
        result.Data!.Lines.Should().BeEmpty();
        result.Data!.Customer!.Tier.Should().Be("SILVER");
    }

    // ---------------------------------------------------------------------------
    // R4: the flat customerId request round-trips (the shim echoes the received id).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderV2_FlatRequest_CustomerIdSurvivesProtoMapping()
    {
        var shim = new EchoOrderV2ShimBase(
            () => D2Result<DtoOrderV2Output?>.Ok(new DtoOrderV2Output(
                "order-4", [new DtoOrderLine("OK")], new DtoOrderV2Customer("GOLD"))));
        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        await client.PlaceOrderV2Async(new DtoOrderV2Input("cust-echo-42"));

        // The request mapper (input.ToPlaceOrderV2Request()) sent the customerId over the wire.
        shim.LastCustomerId.Should().Be("cust-echo-42");
    }

    // ---------------------------------------------------------------------------
    // R5: the predicate fires over the now-wire-mappable nested output.
    // customer.tier=="TRIAL" matches retryWhen (failWhen false: lines non-empty, no
    // VALIDATION_FAILED) → the sentinel drives retry; on exhaust the 200 is restored verbatim.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderV2_NestedTrialTier_DrivesPredicateRetry_RestoredVerbatim()
    {
        var shim = new EchoOrderV2ShimBase(
            () => D2Result<DtoOrderV2Output?>.Ok(new DtoOrderV2Output(
                "order-trial",
                [new DtoOrderLine("SHIPPED")],
                new DtoOrderV2Customer("TRIAL"))));
        using var host = await BuildHost(shim);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 3);
        var client = BuildClient(host, retryPipeline);

        var result = await client.PlaceOrderV2Async(new DtoOrderV2Input("cust-1"));

        shim.CallCount.Should().BeGreaterThan(
            1,
            "the nested customer.tier==\"TRIAL\" matched retryWhen (evaluated over the wire-mapped nested output)");
        result.Success.Should().BeTrue(
            "the last captured business result is restored VERBATIM on budget-exhaust (a 200, not a 503/500)");
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.Customer!.Tier.Should().Be("TRIAL");
    }

    [Fact]
    public async Task PlaceOrderV2_NestedLinePending_DrivesPredicateRetryViaArrayQuantifier()
    {
        // lines.any(l => l.status == "PENDING") matches retryWhen via the array-of-model
        // quantifier — proving the wire-mapped array feeds the predicate end-to-end.
        var shim = new EchoOrderV2ShimBase(
            () => D2Result<DtoOrderV2Output?>.Ok(new DtoOrderV2Output(
                "order-pending",
                [new DtoOrderLine("PENDING")],
                new DtoOrderV2Customer("GOLD"))));
        using var host = await BuildHost(shim);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 3);
        var client = BuildClient(host, retryPipeline);

        var result = await client.PlaceOrderV2Async(new DtoOrderV2Input("cust-1"));

        shim.CallCount.Should().BeGreaterThan(
            1,
            "a PENDING line matched the array-of-model quantifier in retryWhen");
        result.Success.Should().BeTrue();
        result.Data!.Lines.Should().ContainSingle(l => l.Status == "PENDING");
    }

    // ---------------------------------------------------------------------------
    // R6: §1.3 DI resolution — the V2 client + its keyed pipeline resolve.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddD2PredicateFixturesV2GrpcClients_ResolvesClientAndKeyedPipeline()
    {
        using var host = await BuildHost(new EchoOrderV2ShimBase(
            () => D2Result<DtoOrderV2Output?>.Ok(new DtoOrderV2Output(
                "o", [new DtoOrderLine("OK")], new DtoOrderV2Customer("GOLD")))));
        var httpClient = host.GetTestClient();

        var services = new ServiceCollection();
        services.AddD2ForwardedJwtOutbound();
        services.AddD2WorkloadCertificateOutbound();
        services.AddSingleton<IAmbientRequestScopeAccessor>(new NoAmbientScopeAccessor());

        services.AddD2PredicateFixturesV2GrpcClients(new PredicateFixturesV2GrpcClientOptions
        {
            Address = httpClient.BaseAddress!,
        });

        // Test-only downgrade so the SecureSsl channel the auto-wired chain sets builds against
        // the plaintext in-process TestServer (this case asserts DI RESOLVABILITY only).
        services
            .AddGrpcClient<PredicateFixturesOrdersV2.PredicateFixturesOrdersV2Client>()
            .ConfigureChannel(o => o.Credentials = ChannelCredentials.Insecure);

        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IPredicateFixturesV2GrpcClient>();
        client.Should().BeOfType<PredicateFixturesV2GrpcClient>();

        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, DtoOrderV2Output?>>(
            PlaceOrderV2ClientKeys.PIPELINE);
        pipeline.Should().NotBeNull();
    }

    // ---------------------------------------------------------------------------
    // Adversarial: a business failure rides the envelope (not retried) with null data.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderV2_BusinessFailure_RidesEnvelope_NotRetried_NullData()
    {
        var shim = new EchoOrderV2ShimBase(
            () => D2Result<DtoOrderV2Output?>.ValidationFailed(errorCode: "VALIDATION_FAILED"));
        using var host = await BuildHost(shim);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 5);
        var client = BuildClient(host, retryPipeline);

        var result = await client.PlaceOrderV2Async(new DtoOrderV2Input("cust-1"));

        shim.CallCount.Should().Be(1, "failWhen (VALIDATION_FAILED) matched → no retry");
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.Data.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

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

    private static ResilientPipeline<string, DtoOrderV2Output?> BuildRetryPipeline(int maxAttempts)
    {
        var builder = new ResilientPipelineBuilder<string, DtoOrderV2Output?>(
            new ServiceCollection().BuildServiceProvider());

        builder.UseRetries(new RetryOptions<DtoOrderV2Output?>
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 1,
            Jitter = false,
            IsTransient = ex =>
                ex is D2GeneratedBusinessRetrySignal
                || (ex is RpcException r && ProtoExtensions.IsTransientGrpcException(r)),
        });

        return builder.Build();
    }

    // The shim builds the proto response DIRECTLY (raw proto construction, like
    // PredicateRetryTests.BuildResponse) — it does NOT use a generated server transport
    // mapper (none is committed; committing both the client + server mappers in the same
    // namespace would collide their per-nested-model sub-mappers). The server-side
    // buildDtoToProto recursion is proven separately by the emitter's direct-unit + byte
    // tests; this harness proves the CLIENT proto → DTO recursion end-to-end.
    private static PlaceOrderV2Response BuildResponse(D2Result<DtoOrderV2Output?> businessResult)
    {
        var response = new PlaceOrderV2Response { Result = businessResult.ToProto() };
        if (businessResult.Success && businessResult.Data is not null)
        {
            var data = businessResult.Data;
            var proto = new ProtoOrderV2Output { OrderCode = data.OrderCode };

            foreach (var line in data.Lines)
                proto.Lines.Add(new ProtoOrderLine { Status = line.Status });

            if (data.Customer is not null)
                proto.Customer = new ProtoOrderV2Customer { Tier = data.Customer.Tier };

            response.Data = proto;
        }

        return response;
    }

    // ---------------------------------------------------------------------------
    // Server shim — builds the proto response DIRECTLY via BuildResponse (raw proto
    // construction, no committed server transport mapper); the CLIENT then maps it back.
    // ---------------------------------------------------------------------------

    private sealed class EchoOrderV2ShimBase(Func<D2Result<DtoOrderV2Output?>> resultFactory)
        : PredicateFixturesOrdersV2.PredicateFixturesOrdersV2Base
    {
        private int _callCount;
        private string? _lastCustomerId;

        public int CallCount => Volatile.Read(ref _callCount);

        public string? LastCustomerId => Volatile.Read(ref _lastCustomerId);

        public override Task<PlaceOrderV2Response> PlaceOrderV2(
            PlaceOrderV2Request request, ServerCallContext context)
        {
            Interlocked.Increment(ref _callCount);
            Volatile.Write(ref _lastCustomerId, request.CustomerId);
            return Task.FromResult(BuildResponse(resultFactory()));
        }
    }

    private sealed class NoAmbientScopeAccessor : IAmbientRequestScopeAccessor
    {
        public IServiceProvider? Current => null;
    }
}
