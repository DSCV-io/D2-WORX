// -----------------------------------------------------------------------
// <copyright file="OverTheWireResilienceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Edge.Tests.TypeSpecGrpcPredicate.Generated;
using D2.Services.Protos.PredicateFixtures.V1;
using D2.Services.Protos.SignFixtures.V1;
using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Pipeline;
using D2.Shared.Resilience.Retry;
using D2.Shared.Result;
using D2.Shared.Result.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DtoPlaceOrderFixtureInput = D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderFixtureInput;
using DtoPlaceOrderFixtureOutput = D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderFixtureOutput;
using DtoSignFixtureInput = D2.Edge.Tests.TypeSpecDto.Generated.SignFixtureInput;
using DtoSignFixtureOutput = D2.Edge.Tests.TypeSpecDto.Generated.SignFixtureOutput;
using ProtoPlaceOrderFixtureOutput = D2.Services.Protos.PredicateFixtures.V1.PlaceOrderFixtureOutput;
using ProtoSignFixtureOutput = D2.Services.Protos.SignFixtures.V1.SignFixtureOutput;

/// <summary>
/// Over-the-wire resilience and envelope integration tests. Re-proves the resilience /
/// envelope behaviors validated by the in-memory <see cref="GrpcClientTests"/> /
/// <c>PredicateRetryTests</c> over a REAL Kestrel HTTPS endpoint on
/// <c>127.0.0.1:0</c> — a real TCP socket + real TLS 1.3 handshake + real HTTP/2 +
/// real protobuf serialization + real <see cref="RpcException"/> propagation. Server-TLS
/// only (loopback self-signed server cert, client-trust callback — NO client cert; resilience
/// is auth-orthogonal, so the mTLS client-cert requirement is dropped → runs cross-platform
/// including Windows). Self-managed (<see cref="GrpcTestHost.RunningServer"/> : IAsyncDisposable,
/// ephemeral port; no <c>dotnet run</c>, single process). The SAME generated
/// <see cref="SignFixtureGrpcClient"/> / <see cref="PredicateFixturesGrpcClient"/> the
/// in-memory harness drives, re-proven over a real socket.
/// </summary>
/// <remarks>
/// <para>
/// <b>Determinism</b>: breaker window controlled by an injected <c>NowFunc</c>
/// (<see cref="CircuitBreakerOptions"/>); retry backoff uses
/// <c>BaseDelayMs:1 + Jitter:false + DelayFunc:Task.CompletedTask</c>. No elapsed-time
/// assertions; all timing is a controlled input.
/// </para>
/// <para>
/// <b>Cross-platform</b>: no client certificate is presented, so .NET builds NO client
/// <see cref="SslStreamCertificateContext"/> and the Windows-Schannel limitation that
/// gates the mTLS harness's cert-presenting cases to non-Windows does NOT apply — all
/// five scenarios run on every platform.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class OverTheWireResilienceTests
{
    private const string _SERVER_WORKLOAD = "d2-keycustodian-wire";

    // -----------------------------------------------------------------------
    // S1 — transient recovery
    // -----------------------------------------------------------------------

    /// <summary>
    /// A server that faults <c>Unavailable</c> on the first call and succeeds on the
    /// second is recovered by the client's retry pipeline — proven over a real socket.
    /// The call-count assertion pins that exactly two server-side calls occurred (one
    /// failed attempt + one success), proving the transport retry path over the wire.
    /// </summary>
    [Fact]
    public async Task Sign_OverWire_TransientUnavailableThenSuccess_RecoversViaRetry()
    {
        const string recovered_sig = "wire-recovered==";
        var shim = new WireFlakySignerBase(recovered_sig);

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        await using var host = await StartSignerServerAsync(serverCert, shim);
        using var channel = BuildServerTlsChannel(host.Endpoint);

        using var retryPipeline = BuildSignRetryPipeline(maxAttempts: 3);
        var stub = new SignFixtureSigner.SignFixtureSignerClient(channel);
        var client = new SignFixtureGrpcClient(stub, retryPipeline);

        var result = await client.SignFixtureAsync(new DtoSignFixtureInput("key-s1", new byte[] { 1, 2, 3 }));

        result.Success.Should().BeTrue("the retry pipeline recovered from the transient fault over the real socket");
        result.Data!.Signature.Should().Be(recovered_sig);
        shim.CallCount.Should().Be(2, "one failed attempt + one success (retry recovered, not exhausted)");
    }

    // -----------------------------------------------------------------------
    // S2 — breaker open → half-open (deterministic injected clock)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Repeated faults open the circuit breaker (fast-fail / 503); after the injected
    /// fake clock is advanced past the cooldown the breaker half-opens and a probe
    /// succeeds — proven over a real socket. The open-window call does NOT reach the
    /// server (call-count frozen), proving the fast-fail. After the cooldown the probe
    /// closes the breaker and the result is success. The breaker clock is injected via
    /// <see cref="CircuitBreakerOptions.NowFunc"/> — no wall-clock waits.
    /// </summary>
    [Fact]
    public async Task Sign_OverWire_BreakerOpensThenHalfOpensAfterCooldown_ClosesOnProbeSuccess()
    {
        const int threshold = 2;
        const long cooldown_ms = 1000;

        // Injected clock: a single-element array holds the mutable counter so the lambda
        // captures the array reference (stable) while tests mutate the element. Advancing
        // fakeNow[0] past cooldown_ms makes the breaker transition Open → HalfOpen.
        // No Task.Delay ever called — deterministic by construction.
        var fakeNow = new long[] { 0 };
        var breaker = new CircuitBreaker<DtoSignFixtureOutput?>(
            isFailure: _ => false,   // exceptions already record failure via the breaker's catch arm
            options: new CircuitBreakerOptions(
                failureThreshold: threshold,
                cooldownDuration: TimeSpan.FromMilliseconds(cooldown_ms),
                nowFunc: () => Volatile.Read(ref fakeNow[0])));

        var shim = new WireToggleableSignerBase(faultMode: true);

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        await using var host = await StartSignerServerAsync(serverCert, shim);
        using var channel = BuildServerTlsChannel(host.Endpoint);

        var stub = new SignFixtureSigner.SignFixtureSignerClient(channel);

        // Build a pipeline with the test-controlled breaker (bypass-DI overload) and no retry
        // so each call is one independent breaker execution (no retry masking the state).
        using var pipeline = BuildSignBreakerPipeline(breaker);
        var client = new SignFixtureGrpcClient(stub, pipeline);

        var signInput = new DtoSignFixtureInput("key-s2", new byte[] { 0xAB });

        // --- Stage 1: drive `threshold` faults to open the breaker ---
        for (var i = 0; i < threshold; i++)
        {
            var fault = await client.SignFixtureAsync(signInput);
            fault.Success.Should().BeFalse();
        }

        breaker.State.Should().Be(CircuitState.Open, "threshold consecutive faults open the breaker");
        var callCountAfterOpen = shim.CallCount;

        // --- Stage 2: open-window call is fast-failed (does NOT reach the server) ---
        var fastFail = await client.SignFixtureAsync(signInput);
        fastFail.Success.Should().BeFalse();
        fastFail.StatusCode.Should().Be(
            HttpStatusCode.ServiceUnavailable,
            "CircuitOpenException maps to 503 at the pipeline boundary");
        shim.CallCount.Should().Be(
            callCountAfterOpen,
            "the open-circuit fast-fail did not reach the server (call-count frozen)");

        // --- Stage 3: advance the fake clock past cooldown; disable fault; probe succeeds ---
        Volatile.Write(ref fakeNow[0], cooldown_ms);
        shim.SetFaultMode(false);

        var probeResult = await client.SignFixtureAsync(signInput);

        probeResult.Success.Should().BeTrue("the probe call succeeded and closed the breaker");
        breaker.State.Should().Be(CircuitState.Closed, "a successful probe closes the breaker");
        shim.CallCount.Should().Be(
            callCountAfterOpen + 1,
            "exactly one probe call reached the server after the clock advanced past cooldown");
    }

    // -----------------------------------------------------------------------
    // S3 — no-amplification: business ValidationFailed rides the envelope
    // -----------------------------------------------------------------------

    /// <summary>
    /// A server returning a business <c>ValidationFailed</c> on the <c>D2ResultProto</c>
    /// envelope at gRPC status OK is NOT retried by the client — even with a 5-attempt
    /// retry pipeline. The transport layer sees gRPC status OK, so the business failure
    /// is a VALUE, never a thrown transport exception. <c>CallCount == 1</c> is the
    /// headline no-amplification pin, re-proven over a real socket.
    /// </summary>
    [Fact]
    public async Task Sign_OverWire_BusinessValidationFailed_RidesEnvelopeAtGrpcOk_NotRetried()
    {
        var shim = new WireBusinessResultSignerBase(
            D2Result<DtoSignFixtureOutput?>.ValidationFailed());

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        await using var host = await StartSignerServerAsync(serverCert, shim);
        using var channel = BuildServerTlsChannel(host.Endpoint);

        // A 5-attempt retry pipeline that WOULD retry transport faults — but must NOT
        // retry a business result returned at gRPC status OK.
        using var retryPipeline = BuildSignRetryPipeline(maxAttempts: 5);
        var stub = new SignFixtureSigner.SignFixtureSignerClient(channel);
        var client = new SignFixtureGrpcClient(stub, retryPipeline);

        var result = await client.SignFixtureAsync(new DtoSignFixtureInput("key-s3", new byte[] { 0xFF }));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "ValidationFailed is a 400 business result, not a transport-derived 503/500");
        result.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        shim.CallCount.Should().Be(
            1,
            "a business failure rides gRPC status OK and is never retried — the no-amplification pin over the wire");
    }

    // -----------------------------------------------------------------------
    // S4 — envelope byte-fidelity (theory: success+data, Conflict, NotFound)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("success")]
    [InlineData("conflict")]
    [InlineData("notfound")]
    public async Task Sign_OverWire_BusinessResult_RoundTripsEnvelopeFaithfully(string scenario)
    {
        const string error_code = "KC_KEY_NOT_FOUND";
        const string expected_sig = "wire-fidelity-sig==";

        D2Result<DtoSignFixtureOutput?> serverResult = scenario switch
        {
            "success" => D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput(expected_sig)),
            "conflict" => D2Result<DtoSignFixtureOutput?>.Conflict(errorCode: error_code),
            _ => D2Result<DtoSignFixtureOutput?>.NotFound(),
        };

        var shim = new WireBusinessResultSignerBase(serverResult);

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        await using var host = await StartSignerServerAsync(serverCert, shim);
        using var channel = BuildServerTlsChannel(host.Endpoint);

        var stub = new SignFixtureSigner.SignFixtureSignerClient(channel);
        var client = new SignFixtureGrpcClient(
            stub, ResilientPipeline<string, DtoSignFixtureOutput?>.PassThrough);

        var result = await client.SignFixtureAsync(new DtoSignFixtureInput("key-s4", new byte[] { 0x42 }));

        switch (scenario)
        {
            case "success":
                result.Success.Should().BeTrue();
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                result.Data!.Signature.Should().Be(
                    expected_sig,
                    "the signature survives real protobuf-over-HTTP/2 serialization");
                break;

            case "conflict":
                result.Success.Should().BeFalse();
                result.StatusCode.Should().Be(HttpStatusCode.Conflict);
                result.ErrorCode.Should().Be(
                    error_code,
                    "the error code survives the D2ResultProto round-trip over the wire");
                break;

            case "notfound":
                result.Success.Should().BeFalse();
                result.StatusCode.Should().Be(HttpStatusCode.NotFound);
                break;
        }
    }

    // -----------------------------------------------------------------------
    // S5 — @d2Resilience predicate behavior over the wire (retryWhen + failWhen)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A server returning a SUCCESS <c>PlaceOrderFixtureOutput</c> with <c>partial==true</c>
    /// matches <c>retryWhen</c> (the flat-bool arm) and opts the business result into
    /// retry via the generated sentinel — proven over a real socket. <c>CallCount &gt; 1</c>
    /// confirms the predicate-driven retry executed real server calls.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_OverWire_RetryWhenMatches_RetriesViaSentinel()
    {
        var shim = new WirePredicateSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.Ok(
                new DtoPlaceOrderFixtureOutput("order-partial-wire", ["SHIPPED"], Partial: true)),
            itemStatuses: ["SHIPPED"]);

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        await using var host = await StartPredicateServerAsync(serverCert, shim);
        using var channel = BuildServerTlsChannel(host.Endpoint);

        using var retryPipeline = BuildPredicateRetryPipeline(maxAttempts: 3);
        var stub = new PredicateFixturesOrders.PredicateFixturesOrdersClient(channel);
        var client = new PredicateFixturesGrpcClient(stub, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-wire-1"));

        shim.CallCount.Should().BeGreaterThan(
            1,
            "retryWhen (partial==true) matched → the sentinel opted the business result into retry over the wire");
        result.Success.Should().BeTrue(
            "the last captured business result is restored verbatim on budget-exhaust (a 200, not a 503/500)");
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A server returning a business <c>ValidationFailed</c> with error-code
    /// <c>VALIDATION_FAILED</c> matches <c>failWhen</c> — the result is returned on
    /// the first call without retry, proven over a real socket. <c>CallCount == 1</c>
    /// is the failWhen suppression pin.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_OverWire_FailWhenMatches_NotRetried()
    {
        var shim = new WirePredicateSignerBase(
            () => D2Result<DtoPlaceOrderFixtureOutput?>.ValidationFailed(errorCode: "VALIDATION_FAILED"),
            itemStatuses: ["SHIPPED"]);

        using var ca = new RealCertAuthority();
        using var serverCert = ca.IssueServerCertificate(_SERVER_WORKLOAD);
        await using var host = await StartPredicateServerAsync(serverCert, shim);
        using var channel = BuildServerTlsChannel(host.Endpoint);

        using var retryPipeline = BuildPredicateRetryPipeline(maxAttempts: 5);
        var stub = new PredicateFixturesOrders.PredicateFixturesOrdersClient(channel);
        var client = new PredicateFixturesGrpcClient(stub, retryPipeline);

        var result = await client.PlaceOrderFixtureAsync(new DtoPlaceOrderFixtureInput("cust-wire-2"));

        shim.CallCount.Should().Be(1, "failWhen matched → no retry, returned verbatim on the first call");
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
    }

    // -----------------------------------------------------------------------
    // Host helpers — server-TLS only (no client cert, no AddD2MutualTls)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Starts a real Kestrel HTTPS host on <c>127.0.0.1:0</c> hosting a
    /// <see cref="SignFixtureSigner.SignFixtureSignerBase"/> shim. Server-TLS only —
    /// no client certificate is required (resilience is auth-orthogonal). The loopback
    /// self-signed server cert chains to itself and builds on a clean Windows box, so no
    /// OS-store mutation is needed. Cross-platform: no client-cert context is built. The
    /// real-socket host plumbing lives in the shared <see cref="GrpcTestHost"/> test-infra
    /// helper; this harness supplies only the bare-shim registration + the service map.
    /// </summary>
    private static Task<GrpcTestHost.RunningServer> StartSignerServerAsync(
        X509Certificate2 serverCert,
        SignFixtureSigner.SignFixtureSignerBase shim) =>
        GrpcTestHost.StartAsync(
            serverCert,
            services => services.AddSingleton(shim),
            app => app.MapGrpcService<SignFixtureSigner.SignFixtureSignerBase>());

    /// <summary>
    /// Starts a real Kestrel HTTPS host on <c>127.0.0.1:0</c> hosting a
    /// <see cref="PredicateFixturesOrders.PredicateFixturesOrdersBase"/> shim, via the
    /// shared <see cref="GrpcTestHost"/> test-infra helper.
    /// </summary>
    private static Task<GrpcTestHost.RunningServer> StartPredicateServerAsync(
        X509Certificate2 serverCert,
        PredicateFixturesOrders.PredicateFixturesOrdersBase shim) =>
        GrpcTestHost.StartAsync(
            serverCert,
            services => services.AddSingleton(shim),
            app => app.MapGrpcService<PredicateFixturesOrders.PredicateFixturesOrdersBase>());

    /// <summary>
    /// Builds a gRPC channel that dials the loopback HTTPS endpoint with server-TLS only.
    /// The client trusts the loopback self-signed server cert via the shared
    /// <see cref="GrpcTestHost.BuildChannel(Uri, Action{SslClientAuthenticationOptions}?)"/>
    /// plumbing. No client certificate is presented — no
    /// <see cref="SslStreamCertificateContext"/> is built — so the Windows-Schannel
    /// limitation that gates the mTLS harness's cert-presenting cases to non-Windows does
    /// NOT apply here. All five scenarios run cross-platform including Windows.
    /// </summary>
    private static GrpcChannel BuildServerTlsChannel(Uri endpoint) =>
        GrpcTestHost.BuildChannel(endpoint);

    // -----------------------------------------------------------------------
    // Pipeline builders (deterministic — no wall-clock)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a retry-only pipeline with the gRPC-aware transient predicate, near-zero
    /// backoff, and an instant <c>DelayFunc</c> so no real delay occurs between attempts.
    /// </summary>
    private static ResilientPipeline<string, DtoSignFixtureOutput?> BuildSignRetryPipeline(int maxAttempts)
    {
        var builder = new ResilientPipelineBuilder<string, DtoSignFixtureOutput?>(
            new ServiceCollection().BuildServiceProvider());
        builder.UseRetries(new RetryOptions<DtoSignFixtureOutput?>
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 1,
            Jitter = false,
            IsTransient = ex => ex is RpcException r && ProtoExtensions.IsTransientGrpcException(r),
            DelayFunc = (_, _) => Task.CompletedTask,
        });
        return builder.Build();
    }

    /// <summary>
    /// Builds a circuit-breaker-only pipeline wrapping the supplied test-controlled
    /// <paramref name="breaker"/> via the bypass-DI overload. No retry layer — each call
    /// is one independent breaker execution (no retry masking the state transitions).
    /// </summary>
    private static ResilientPipeline<string, DtoSignFixtureOutput?> BuildSignBreakerPipeline(
        CircuitBreaker<DtoSignFixtureOutput?> breaker)
    {
        var builder = new ResilientPipelineBuilder<string, DtoSignFixtureOutput?>(
            new ServiceCollection().BuildServiceProvider());
        builder.UseCircuitBreaker(breaker);
        return builder.Build();
    }

    /// <summary>
    /// Builds the predicate retry pipeline with the SAME extended <c>IsTransient</c> the
    /// generated DI extension emits (recognizes both the business-retry sentinel AND
    /// gRPC-transient exceptions), near-zero backoff, and an instant <c>DelayFunc</c>.
    /// </summary>
    private static ResilientPipeline<string, DtoPlaceOrderFixtureOutput?> BuildPredicateRetryPipeline(
        int maxAttempts)
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
            DelayFunc = (_, _) => Task.CompletedTask,
        });
        return builder.Build();
    }

    // -----------------------------------------------------------------------
    // Helper shared by predicate shims — must precede the nested classes (SA1201)
    // -----------------------------------------------------------------------

    private static PlaceOrderFixtureResponse BuildResponse(
        D2Result<DtoPlaceOrderFixtureOutput?> businessResult,
        string[] statuses)
    {
        var response = new PlaceOrderFixtureResponse { Result = businessResult.ToProto() };

        if (businessResult.Success)
        {
            var data = new ProtoPlaceOrderFixtureOutput
            {
                OrderCode = businessResult.Data?.OrderCode ?? "order",
                Partial = businessResult.Data?.Partial ?? false,
            };

            foreach (var s in statuses)
                data.ItemStatuses.Add(s);

            response.Data = data;
        }

        return response;
    }

    // -----------------------------------------------------------------------
    // Fault-injecting service shims (real Kestrel socket — server-side)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Throws <see cref="StatusCode.Unavailable"/> on the first call, returns a success
    /// envelope on the second — the transient-recovery shim for S1.
    /// </summary>
    private sealed class WireFlakySignerBase(string signature)
        : SignFixtureSigner.SignFixtureSignerBase
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override Task<SignFixtureResponse> SignFixture(SignFixtureRequest request, ServerCallContext context)
        {
            var attempt = Interlocked.Increment(ref _callCount);

            if (attempt == 1)
                throw new RpcException(new Status(StatusCode.Unavailable, "transient wire fault"));

            var result = D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput(signature));
            var response = new SignFixtureResponse { Result = result.ToProto() };
            response.Data = new ProtoSignFixtureOutput { Signature = signature };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Throws <see cref="StatusCode.Unavailable"/> while fault-mode is <c>true</c>
    /// (the initial state), returns a success envelope when fault-mode is <c>false</c>.
    /// Used by S2 to drive the breaker open, then prove the probe succeeds after cooldown.
    /// </summary>
    private sealed class WireToggleableSignerBase(bool faultMode)
        : SignFixtureSigner.SignFixtureSignerBase
    {
        private int _callCount;
        private volatile bool _faultMode = faultMode;

        public int CallCount => Volatile.Read(ref _callCount);

        public void SetFaultMode(bool enabled) => _faultMode = enabled;

        public override Task<SignFixtureResponse> SignFixture(SignFixtureRequest request, ServerCallContext context)
        {
            Interlocked.Increment(ref _callCount);

            if (_faultMode)
                throw new RpcException(new Status(StatusCode.Unavailable, "breaker-open wire fault"));

            const string probe_sig = "probe-success-sig==";
            var result = D2Result<DtoSignFixtureOutput?>.Ok(new DtoSignFixtureOutput(probe_sig));
            var response = new SignFixtureResponse { Result = result.ToProto() };
            response.Data = new ProtoSignFixtureOutput { Signature = probe_sig };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Returns the supplied <see cref="D2Result{T}"/> as a <c>D2ResultProto</c> envelope
    /// at gRPC status OK on every call (business failure / success fidelity shim for S3 + S4).
    /// </summary>
    private sealed class WireBusinessResultSignerBase(D2Result<DtoSignFixtureOutput?> businessResult)
        : SignFixtureSigner.SignFixtureSignerBase
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override Task<SignFixtureResponse> SignFixture(SignFixtureRequest request, ServerCallContext context)
        {
            Interlocked.Increment(ref _callCount);
            var response = new SignFixtureResponse { Result = businessResult.ToProto() };

            if (businessResult.Success && businessResult.Data is not null)
                response.Data = new ProtoSignFixtureOutput { Signature = businessResult.Data.Signature };

            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Returns the result produced by the supplied factory + the supplied
    /// <paramref name="itemStatuses"/> on every call — the predicate-behavior shim for S5.
    /// </summary>
    private sealed class WirePredicateSignerBase(
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
}
