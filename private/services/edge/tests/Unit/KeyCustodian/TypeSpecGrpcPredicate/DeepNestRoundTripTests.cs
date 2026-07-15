// -----------------------------------------------------------------------
// <copyright file="DeepNestRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpcPredicate;

using System.Threading.Tasks;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Outbound;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Result;
using DcsvIo.D2.Result.Grpc;
using global::D2.Services.Protos.PredicateFixturesDeep.V1;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DtoDeepFixturePart = DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated.DeepFixturePart;
using DtoDeepFixtureWidget = DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated.DeepFixtureWidget;
using DtoDeepInput = DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated.DeepNestFixtureInput;
using DtoDeepOutput = DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated.DeepNestFixtureOutput;
using ProtoDeepFixturePart = global::D2.Services.Protos.PredicateFixturesDeep.V1.DeepFixturePart;
using ProtoDeepFixtureWidget = global::D2.Services.Protos.PredicateFixturesDeep.V1.DeepFixtureWidget;
using ProtoDeepOutput = global::D2.Services.Protos.PredicateFixturesDeep.V1.DeepNestFixtureOutput;

/// <summary>
/// In-memory harness tests for the generated <see cref="PredicateFixturesDeepGrpcClient"/> — the
/// ARBITRARY-DEPTH (depth-3) nested-model transport-mapper recursion. Proves an
/// output → optional <c>DeepFixtureWidget</c> (depth 2) → <c>DeepFixturePart[]</c> (depth 3, array-of-MODEL
/// inside a nested model) survives proto ↔ DTO with full fidelity through the per-nested-level
/// sub-mappers (every level mapped, deduped). Hosts a concrete shim extending
/// <see cref="PredicateFixturesGizmosDeep.PredicateFixturesGizmosDeepBase"/> over an in-process
/// <see cref="TestServer"/> + <see cref="GrpcChannel"/>.
/// </summary>
public sealed class DeepNestRoundTripTests
{
    // ---------------------------------------------------------------------------
    // Depth-3 full-fidelity round-trip: output → widget → parts[].
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeepNest_Depth3_NestedWidgetAndPartsArray_RoundTripFullFidelity()
    {
        var shim = new EchoDeepShimBase(
            () => D2Result<DtoDeepOutput?>.Ok(new DtoDeepOutput(
                "gizmo-1",
                new DtoDeepFixtureWidget(
                    "widget-a",
                    [new DtoDeepFixturePart("P-1"), new DtoDeepFixturePart("P-2")]))));

        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        var result = await client.DeepNestFixtureAsync(new DtoDeepInput("gizmo-1"));

        result.Success.Should().BeTrue();
        result.Data!.GizmoId.Should().Be("gizmo-1");

        // Depth 2 — the nested widget survived.
        result.Data!.Widget.Should().NotBeNull();
        result.Data!.Widget!.Name.Should().Be("widget-a");

        // Depth 3 — the array-of-MODEL INSIDE the nested widget survived (both parts).
        result.Data!.Widget!.Parts.Should().HaveCount(2);
        result.Data!.Widget!.Parts[0].Code.Should().Be("P-1");
        result.Data!.Widget!.Parts[1].Code.Should().Be("P-2");
    }

    // ---------------------------------------------------------------------------
    // An absent (unset) depth-2 nested widget → null on the DTO (no NRE).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeepNest_AbsentNullableWidget_MapsToNull_NoNre()
    {
        var shim = new EchoDeepShimBase(
            () => D2Result<DtoDeepOutput?>.Ok(new DtoDeepOutput("gizmo-2", Widget: null)));

        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        var result = await client.DeepNestFixtureAsync(new DtoDeepInput("gizmo-2"));

        result.Success.Should().BeTrue();
        result.Data!.Widget.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // A depth-2 widget with an EMPTY depth-3 parts array → empty (non-null), no NRE.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeepNest_EmptyDepth3PartsArray_MapsToEmptyList_NoNre()
    {
        var shim = new EchoDeepShimBase(
            () => D2Result<DtoDeepOutput?>.Ok(new DtoDeepOutput(
                "gizmo-3", new DtoDeepFixtureWidget("widget-empty", []))));

        using var host = await BuildHost(shim);
        using var pipeline = BuildPassThroughPipeline();
        var client = BuildClient(host, pipeline);

        var result = await client.DeepNestFixtureAsync(new DtoDeepInput("gizmo-3"));

        result.Success.Should().BeTrue();
        result.Data!.Widget.Should().NotBeNull();
        result.Data!.Widget!.Parts.Should().NotBeNull();
        result.Data!.Widget!.Parts.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // §1.3 DI resolution — the Deep client + its keyed pipeline resolve.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddD2PredicateFixturesDeepGrpcClients_ResolvesClientAndKeyedPipeline()
    {
        using var host = await BuildHost(new EchoDeepShimBase(
            () => D2Result<DtoDeepOutput?>.Ok(new DtoDeepOutput(
                "g", new DtoDeepFixtureWidget("w", [new DtoDeepFixturePart("p")])))));

        var httpClient = host.GetTestClient();

        var services = new ServiceCollection();
        services.AddD2ForwardedJwtOutbound();
        services.AddD2WorkloadCertificateOutbound();
        services.AddSingleton<IAmbientRequestScopeAccessor>(new NoAmbientScopeAccessor());

        services.AddD2PredicateFixturesDeepGrpcClients(new PredicateFixturesDeepGrpcClientOptions
        {
            Address = httpClient.BaseAddress!,
        });

        services
            .AddGrpcClient<PredicateFixturesGizmosDeep.PredicateFixturesGizmosDeepClient>()
            .ConfigureChannel(o => o.Credentials = ChannelCredentials.Insecure);

        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IPredicateFixturesDeepGrpcClient>();
        client.Should().BeOfType<PredicateFixturesDeepGrpcClient>();

        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, DtoDeepOutput?>>(
            DeepNestFixtureClientKeys.PIPELINE);
        pipeline.Should().NotBeNull();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static async Task<IHost> BuildHost(
        PredicateFixturesGizmosDeep.PredicateFixturesGizmosDeepBase shim)
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
                        endpoints.MapGrpcService<PredicateFixturesGizmosDeep.PredicateFixturesGizmosDeepBase>();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static PredicateFixturesDeepGrpcClient BuildClient(
        IHost host,
        ResilientPipeline<string, DtoDeepOutput?> pipeline)
    {
        var httpClient = host.GetTestClient();
        var channel = GrpcChannel.ForAddress(
            httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = httpClient });

        var stub = new PredicateFixturesGizmosDeep.PredicateFixturesGizmosDeepClient(channel);
        return new PredicateFixturesDeepGrpcClient(stub, pipeline);
    }

    private static ResilientPipeline<string, DtoDeepOutput?> BuildPassThroughPipeline()
    {
        var builder = new ResilientPipelineBuilder<string, DtoDeepOutput?>(
            new ServiceCollection().BuildServiceProvider());

        builder.UseRetries(new RetryOptions<DtoDeepOutput?>
        {
            MaxAttempts = 1,
            BaseDelayMs = 1,
            Jitter = false,
            IsTransient = ex => ex is RpcException r && ProtoExtensions.IsTransientGrpcException(r),
        });

        return builder.Build();
    }

    // Raw proto construction (depth-3) — the shim builds the nested proto tree directly
    // (no committed server transport mapper; see PlaceOrderV2RoundTripTests for why). This
    // harness proves the CLIENT depth-3 proto → DTO recursion end-to-end.
    private static DeepNestFixtureResponse BuildResponse(D2Result<DtoDeepOutput?> businessResult)
    {
        var response = new DeepNestFixtureResponse { Result = businessResult.ToProto() };
        if (businessResult.Success && businessResult.Data is not null)
        {
            var data = businessResult.Data;
            var proto = new ProtoDeepOutput { GizmoId = data.GizmoId };

            if (data.Widget is not null)
            {
                var widget = new ProtoDeepFixtureWidget { Name = data.Widget.Name };

                foreach (var part in data.Widget.Parts)
                    widget.Parts.Add(new ProtoDeepFixturePart { Code = part.Code });

                proto.Widget = widget;
            }

            response.Data = proto;
        }

        return response;
    }

    private sealed class EchoDeepShimBase(Func<D2Result<DtoDeepOutput?>> resultFactory)
        : PredicateFixturesGizmosDeep.PredicateFixturesGizmosDeepBase
    {
        public override Task<DeepNestFixtureResponse> DeepNestFixture(
            DeepNestFixtureRequest request, ServerCallContext context)
            => Task.FromResult(BuildResponse(resultFactory()));
    }

    private sealed class NoAmbientScopeAccessor : IAmbientRequestScopeAccessor
    {
        public IServiceProvider? Current => null;
    }
}
