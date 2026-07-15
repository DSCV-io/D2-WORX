// -----------------------------------------------------------------------
// <copyright file="PredicateRetryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpcPredicate;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Outbound;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Result;
using DcsvIo.D2.Result.Grpc;
using global::D2.Services.Protos.PredicateFixtures.V1;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DtoPlaceOrderFixtureInput = DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderFixtureInput;
using DtoPlaceOrderFixtureOutput = DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderFixtureOutput;
using ProtoPlaceOrderFixtureOutput = global::D2.Services.Protos.PredicateFixtures.V1.PlaceOrderFixtureOutput;

/// <summary>
/// In-memory harness tests for the generated <see cref="PredicateFixturesGrpcClient"/> — the
/// @d2Resilience <c>retryWhen</c> / <c>failWhen</c> custom-predicate integration. Hosts concrete
/// shims extending <see cref="PredicateFixturesOrders.PredicateFixturesOrdersBase"/> via an
/// in-process <see cref="TestServer"/> + <see cref="GrpcChannel"/> (no sockets) against the REAL
/// <see cref="DcsvIo.D2.Resilience"/> keyed pipeline + the REAL envelope mapper, to pin the
/// emitter-owned <see cref="D2GeneratedBusinessRetrySignal"/> mechanism:
/// <list type="bullet">
///   <item><c>retryWhen</c> opts a BUSINESS result into the retry decision — the closure throws
///   the sentinel, the extended <c>IsTransient</c> recognizes it, the pipeline retries
///   (<c>CallCount &gt; 1</c>); on budget-exhaust the last business result is restored VERBATIM
///   (its real status / category / data — NOT a 503 / 500);</item>
///   <item><c>retryWhen</c> recovers — a flaky shim returns the retry-matching result once then a
///   clean success, and the client returns the success (<c>CallCount == 2</c>);</item>
///   <item><c>failWhen</c> suppresses retry — a <c>failWhen</c>-matching result is returned
///   verbatim on the first call (<c>CallCount == 1</c>);</item>
///   <item><c>failWhen</c> WINS — a result that matches BOTH predicates is NOT retried;</item>
///   <item>a result matching NEITHER predicate is returned verbatim, never retried;</item>
///   <item>§1.3 DI resolution — the predicate-bearing module's client + keyed pipeline resolve.</item>
/// </list>
/// These are IN-MEMORY only; the over-the-wire (two-process) predicate test is covered by multi-process harness tests.
/// The <c>retryWhen</c> trigger uses a SUCCESS business result whose data matches the predicate
/// (an array <c>itemStatuses.contains("PENDING")</c> or the flat <c>partial == true</c> arm) so the
/// retry fires WITHOUT also tripping <c>failWhen</c> (whose <c>itemStatuses.count == 0</c> arm
/// matches a null/empty payload). The predicate is evaluated against the REAL reconstructed
/// business result; the full cross-language accessor surface is additionally pinned by the
/// emitter's direct-call unit + cross-runtime parity tests.
/// </summary>
public sealed class PredicateRetryTests
{
    // ---------------------------------------------------------------------------
    // Case 1: retryWhen retries a BUSINESS result, restores it verbatim on exhaust.
    // A SUCCESS with partial==true matches retryWhen (the flat-bool arm) but NOT
    // failWhen (itemStatuses is non-empty, errorCode is not VALIDATION_FAILED), so
    // the sentinel fires; on budget-exhaust the SUCCESS is restored VERBATIM (200 +
    // data — NOT mapped to 503 / 500). FAILS WITHOUT the retry-sentinel (the plain
    // gRPC client returns it after one call).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderAsync_RetryWhenMatches_RetriesThenRestoresBusinessResultVerbatim()
    {
        var signer = new BusinessResultSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.Ok(
                new DtoPlaceOrderFixtureOutput("order-partial", ["SHIPPED"], Partial: true)),
            itemStatuses: ["SHIPPED"]);

        using var host = await BuildHost(signer);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 3);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-1"));

        signer.CallCount.Should().BeGreaterThan(
            1,
            "retryWhen (partial==true) matched and failWhen did not → the sentinel opted it into retry");
        result.Success.Should().BeTrue(
            "the last captured business result is restored VERBATIM on budget-exhaust (a 200, not a 503/500)");
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        result.Data!.OrderCode.Should().Be("order-partial");
    }

    // ---------------------------------------------------------------------------
    // Case 2: retryWhen recovers — flaky retry-matching result once, then clean success.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderAsync_RetryWhenThenSuccess_RecoversAndReturnsSuccess()
    {
        // First attempt: a partial==true success → retryWhen fires (failWhen does not).
        // Second attempt: a partial==false success → neither predicate fires → returned.
        var signer = new FlakyThenSuccessSignerBase(successItemStatuses: ["SHIPPED"]);

        using var host = await BuildHost(signer);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 3);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-1"));

        result.Success.Should().BeTrue("the second attempt did not match retryWhen → recovery");
        result.Data!.OrderCode.Should().Be("order-ok");
        result.Data!.Partial.Should().BeFalse();
        signer.CallCount.Should().Be(2);
    }

    // ---------------------------------------------------------------------------
    // Case 3: failWhen suppresses retry — a failWhen-matching result is NOT retried.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderAsync_FailWhenMatches_NotRetried_ReturnedVerbatim()
    {
        // ValidationFailed with errorCode VALIDATION_FAILED → failWhen TRUE → NOT retried.
        var signer = new BusinessResultSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.ValidationFailed(errorCode: "VALIDATION_FAILED"),
            itemStatuses: ["SHIPPED"]);

        using var host = await BuildHost(signer);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 5);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-1"));

        signer.CallCount.Should().Be(1, "failWhen matched → no retry");
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
    }

    // ---------------------------------------------------------------------------
    // Case 4: failWhen WINS — a result matching BOTH retryWhen AND failWhen is NOT retried.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderAsync_BothPredicatesMatch_FailWhenWins_NotRetried()
    {
        // ServiceUnavailable (category infrastructure_unavailable → retryWhen TRUE) WITH
        // errorCode VALIDATION_FAILED (→ failWhen TRUE). failWhen WINS → no retry.
        var signer = new BusinessResultSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.ServiceUnavailable(errorCode: "VALIDATION_FAILED"),
            itemStatuses: ["SHIPPED"]);

        using var host = await BuildHost(signer);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 5);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-1"));

        signer.CallCount.Should().Be(1, "failWhen wins over retryWhen → no retry");
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
    }

    // ---------------------------------------------------------------------------
    // Case 5: failWhen via empty-collection — zero itemStatuses → failWhen (count==0) TRUE.
    // The result is a SUCCESS envelope but failWhen suppresses any retry.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderAsync_FailWhenEmptyCollection_NotRetried()
    {
        // A success with ZERO itemStatuses → failWhen (itemStatuses.count == 0) TRUE → no retry.
        var signer = new BusinessResultSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.Ok(new DtoPlaceOrderFixtureOutput("order-empty", [], false)),
            itemStatuses: []);

        using var host = await BuildHost(signer);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 5);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-1"));

        signer.CallCount.Should().Be(1, "failWhen (count==0) matched → no retry");
        result.Success.Should().BeTrue();
        result.Data!.ItemStatuses.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Case 6: NEITHER predicate matches — a clean success is returned verbatim, never retried.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderAsync_NeitherPredicateMatches_ReturnedVerbatim_NotRetried()
    {
        var signer = new BusinessResultSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.Ok(new DtoPlaceOrderFixtureOutput("order-ok", ["SHIPPED"], false)),
            itemStatuses: ["SHIPPED"]);

        using var host = await BuildHost(signer);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 5);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-1"));

        signer.CallCount.Should().Be(1, "neither predicate matched → the default path, no retry");
        result.Success.Should().BeTrue();
        result.Data!.OrderCode.Should().Be("order-ok");
    }

    // ---------------------------------------------------------------------------
    // Case 7: retryWhen via the array predicate — itemStatuses.contains("PENDING") drives retry.
    // Pins that the generated array-accessor direct-access code evaluates end-to-end.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PlaceOrderAsync_RetryWhenArrayContains_RetriesViaGeneratedAccessor()
    {
        // A SUCCESS whose itemStatuses contains "PENDING" → retryWhen (contains) TRUE; failWhen
        // FALSE (non-empty, no VALIDATION_FAILED). Exhausts the budget on the business result.
        var signer = new BusinessResultSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.Ok(new DtoPlaceOrderFixtureOutput("order-pending", ["PENDING"], false)),
            itemStatuses: ["PENDING"]);

        using var host = await BuildHost(signer);
        using var retryPipeline = BuildRetryPipeline(maxAttempts: 3);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-1"));

        signer.CallCount.Should().BeGreaterThan(
            1,
            "the generated `itemStatuses.Contains(\"PENDING\")` predicate matched → retry");

        // Restored verbatim on exhaust — a 200 success with the PENDING line.
        result.Success.Should().BeTrue();
        result.Data!.ItemStatuses.Should().Contain("PENDING");
    }

    // ---------------------------------------------------------------------------
    // Case 8: §1.3 DI resolution — the predicate-bearing module's client + keyed pipeline resolve.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddD2PredicateFixturesGrpcClients_ResolvesClientAndKeyedPipeline()
    {
        using var host = await BuildHost(new BusinessResultSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.Ok(new DtoPlaceOrderFixtureOutput("o", ["SHIPPED"], false)),
            itemStatuses: ["SHIPPED"]));

        var httpClient = host.GetTestClient();

        var services = new ServiceCollection();
        services.AddD2ForwardedJwtOutbound();
        services.AddD2WorkloadCertificateOutbound();
        services.AddSingleton<IAmbientRequestScopeAccessor>(
            new NoAmbientScopeAccessor());

        services.AddD2PredicateFixturesGrpcClients(new PredicateFixturesGrpcClientOptions
        {
            Address = httpClient.BaseAddress!,
        });

        // Test-only downgrade so the SecureSsl channel the auto-wired chain sets builds against the
        // plaintext in-process TestServer (this case asserts DI RESOLVABILITY only — no RPC is made).
        services
            .AddGrpcClient<PredicateFixturesOrders.PredicateFixturesOrdersClient>()
            .ConfigureChannel(o => o.Credentials = ChannelCredentials.Insecure);

        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IPredicateFixturesGrpcClient>();
        client.Should().BeOfType<PredicateFixturesGrpcClient>();

        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, DtoPlaceOrderFixtureOutput?>>(
            PlaceOrderFixtureClientKeys.PIPELINE);
        pipeline.Should().NotBeNull();
    }

    // ---------------------------------------------------------------------------
    // Helpers — host + channel + client + pipeline construction
    // ---------------------------------------------------------------------------

    private static async Task<IHost> BuildHost(
        PredicateFixturesOrders.PredicateFixturesOrdersBase signer)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(signer);
                    services.AddRouting();
                    services.AddGrpc();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<PredicateFixturesOrders.PredicateFixturesOrdersBase>();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static PredicateFixturesGrpcClient BuildClientWithPipeline(
        IHost host,
        ResilientPipeline<string, DtoPlaceOrderFixtureOutput?> pipeline)
    {
        var httpClient = host.GetTestClient();
        var channel = GrpcChannel.ForAddress(
            httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = httpClient });

        var stub = new PredicateFixturesOrders.PredicateFixturesOrdersClient(channel);
        return new PredicateFixturesGrpcClient(stub, pipeline);
    }

    /// <summary>
    /// Builds a retry pipeline with the SAME extended <c>IsTransient</c> the generated DI extension
    /// emits — it recognizes the emitter-owned <see cref="D2GeneratedBusinessRetrySignal"/> AND the
    /// gRPC transient predicate — with a near-zero backoff so the retry cases do not wall-clock the
    /// suite.
    /// </summary>
    private static ResilientPipeline<string, DtoPlaceOrderFixtureOutput?> BuildRetryPipeline(int maxAttempts)
    {
        var builder = new ResilientPipelineBuilder<string, DtoPlaceOrderFixtureOutput?>(
            new ServiceCollection().BuildServiceProvider());

        builder.UseRetries(new RetryOptions<DtoPlaceOrderFixtureOutput?>
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

    private static PlaceOrderFixtureResponse BuildResponse(
        D2Result<DtoPlaceOrderFixtureOutput?> businessResult,
        string[] itemStatuses)
    {
        var response = new PlaceOrderFixtureResponse { Result = businessResult.ToProto() };
        if (businessResult.Success)
        {
            var data = new ProtoPlaceOrderFixtureOutput
            {
                OrderCode = businessResult.Data?.OrderCode ?? "order",
                Partial = businessResult.Data?.Partial ?? false,
            };
            foreach (var s in itemStatuses)
                data.ItemStatuses.Add(s);
            response.Data = data;
        }

        return response;
    }

    // ---------------------------------------------------------------------------
    // Server shims
    // ---------------------------------------------------------------------------

    /// <summary>Returns the SAME business result + itemStatuses on every call.</summary>
    private sealed class BusinessResultSignerBase(
        Func<D2Result<DtoPlaceOrderFixtureOutput?>> resultFactory,
        string[] itemStatuses)
        : PredicateFixturesOrders.PredicateFixturesOrdersBase
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override Task<PlaceOrderFixtureResponse> PlaceOrderFixture(
            PlaceOrderFixtureRequest request, ServerCallContext context)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(BuildResponse(resultFactory(), itemStatuses));
        }
    }

    /// <summary>
    /// Returns a retryWhen-matching success (partial==true) on the first call, then a
    /// clean non-matching success (partial==false) — the recovery shim.
    /// </summary>
    private sealed class FlakyThenSuccessSignerBase(string[] successItemStatuses)
        : PredicateFixturesOrders.PredicateFixturesOrdersBase
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override Task<PlaceOrderFixtureResponse> PlaceOrderFixture(
            PlaceOrderFixtureRequest request, ServerCallContext context)
        {
            var attempt = Interlocked.Increment(ref _callCount);
            if (attempt == 1)
            {
                var partial = D2Result<DtoPlaceOrderFixtureOutput?>.Ok(
                    new DtoPlaceOrderFixtureOutput("order-partial", successItemStatuses, Partial: true));
                return Task.FromResult(BuildResponse(partial, successItemStatuses));
            }

            var success = D2Result<DtoPlaceOrderFixtureOutput?>.Ok(
                new DtoPlaceOrderFixtureOutput("order-ok", successItemStatuses, Partial: false));
            return Task.FromResult(BuildResponse(success, successItemStatuses));
        }
    }

    /// <summary>
    /// A no-inbound-scope <see cref="IAmbientRequestScopeAccessor"/> for
    /// the DI-resolution case (the inbound transport owns this port in production; this isolated test
    /// makes no RPC, so the auto-wired forwarded-JWT credential never reads it).
    /// </summary>
    private sealed class NoAmbientScopeAccessor : IAmbientRequestScopeAccessor
    {
        public IServiceProvider? Current => null;
    }
}
