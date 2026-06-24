// -----------------------------------------------------------------------
// <copyright file="GrpcClientTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Services.Protos.KeyCustodian.V2Alpha;
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
using DtoSignInput = D2.Edge.Tests.TypeSpecDto.Generated.SignInput;
using DtoSignOutput = D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;
using ProtoSignOutput = D2.Services.Protos.KeyCustodian.V2Alpha.SignOutput;

/// <summary>
/// In-memory harness tests for the generated <see cref="KeyCustodianGrpcClient"/> — the
/// per-module cross-process gRPC client with the captured-envelope body. Hosts concrete
/// shims extending <see cref="KeyCustodianSigner.KeyCustodianSignerBase"/> via an in-process
/// <see cref="TestServer"/> + <see cref="GrpcChannel"/> (no sockets) to exercise:
/// <list type="bullet">
///   <item>business-result round-trip fidelity (envelope reconstruction);</item>
///   <item>transport-fault mapping — a thrown <see cref="RpcException"/> maps to the
///   gRPC-aware code (Cancelled → Canceled, else → ServiceUnavailable / 503), NOT the
///   pipeline's generic <c>UnhandledException</c> (500);</item>
///   <item>transport-only retry — a transient <see cref="RpcException"/> is retried via the
///   custom <c>IsTransient</c>, while a business <see cref="D2Result"/> failure (gRPC status OK)
///   is never retried (nesting-safety);</item>
///   <item>the per-call <c>pipelineOverride</c> mechanism + DI resolution.</item>
/// </list>
/// BOUNDARY: these are IN-MEMORY only. Real two-process over-the-wire validation (real
/// sockets, TLS, forwarded-token issuance) is a later step. The call-path cases construct
/// <see cref="KeyCustodianGrpcClient"/> directly over a plaintext in-process channel, so the
/// auto-wired outbound-auth chain is not exercised by them; the DI-resolution case wires the
/// host config the generated extension auto-chains and asserts the registration resolves.
/// </summary>
public sealed class GrpcClientTests
{
    // ---------------------------------------------------------------------------
    // Case 1: success — business D2Result reconstructed from the captured envelope
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_Success_ReconstructsBusinessD2ResultFromEnvelope()
    {
        const string expected_sig = "sig-abc==";
        using var host = await BuildHost(new SuccessSignerBase(expected_sig));
        var client = BuildClient(host);

        var result = await client.SignAsync(
            new DtoSignInput("key-001", new byte[] { 1, 2, 3 }));

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.Signature.Should().Be(expected_sig);
    }

    // ---------------------------------------------------------------------------
    // Case 2: business ValidationFailed rides the envelope (gRPC status OK).
    // Real 400 survives; NOT retried (one call) — proves nesting-safety: a returned
    // business failure is a VALUE on the envelope, never a thrown transport fault.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_BusinessFailure_ValidationFailed_ReturnsRealCode_NotRetried()
    {
        var signer = new BusinessFailureSignerBase(D2Result<DtoSignOutput?>.ValidationFailed());
        using var host = await BuildHost(signer);

        // Use a retry pipeline that WOULD retry transport faults, to prove a business
        // failure (gRPC status OK) is not mistaken for one.
        using var retryPipeline = BuildGrpcRetryPipeline(maxAttempts: 5);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.SignAsync(new DtoSignInput("k", new byte[] { 0xDE }));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        result.Data.Should().BeNull();
        signer.CallCount.Should().Be(1, "a business failure rides gRPC status OK and is never retried");
    }

    // ---------------------------------------------------------------------------
    // Case 3: business NotFound rides the envelope (real 404 round-trip).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_BusinessFailure_NotFound_ReturnsRealCode()
    {
        using var host = await BuildHost(new BusinessFailureSignerBase(
            D2Result<DtoSignOutput?>.NotFound()));
        var client = BuildClient(host);

        var result = await client.SignAsync(new DtoSignInput("k", new byte[] { 0x01 }));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------
    // Case 4: transport fault — RpcException(Unavailable) thrown by the service.
    // With PassThrough (zero layers) the throw is captured + remapped to the gRPC-aware
    // code ServiceUnavailable (503) — NOT UnhandledException (500). 503 means "downstream
    // unavailable"; 500 would wrongly imply a bug in the caller's own logic.
    // FAILS WITHOUT the client capture-remap fix (the pipeline's generic mapping → 500).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_TransportFault_Unavailable_ReturnsServiceUnavailable()
    {
        using var host = await BuildHost(new ThrowingSignerBase(StatusCode.Unavailable));
        var client = BuildClient(host);

        var result = await client.SignAsync(
            new DtoSignInput("k", new byte[] { 0x01 }),
            pipelineOverride: ResilientPipeline<string, DtoSignOutput?>.PassThrough);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // ---------------------------------------------------------------------------
    // Case 5: transport fault — RpcException(DeadlineExceeded) → ServiceUnavailable (503).
    // DeadlineExceeded is non-Cancelled, so the gRPC-aware mapping yields 503 (not Canceled,
    // not 500). FAILS WITHOUT the fix.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_TransportFault_DeadlineExceeded_ReturnsServiceUnavailable()
    {
        using var host = await BuildHost(new ThrowingSignerBase(StatusCode.DeadlineExceeded));
        var client = BuildClient(host);

        var result = await client.SignAsync(
            new DtoSignInput("k", new byte[] { 0x01 }),
            pipelineOverride: ResilientPipeline<string, DtoSignOutput?>.PassThrough);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // ---------------------------------------------------------------------------
    // Case 6: transport fault — RpcException(PermissionDenied) → ServiceUnavailable (503).
    // A non-transient gRPC status still maps to 503 via HandleAsync's RpcException arm
    // (the gRPC-aware default for any non-Cancelled transport fault) — NOT 500.
    // FAILS WITHOUT the fix.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_TransportFault_PermissionDenied_ReturnsServiceUnavailable()
    {
        using var host = await BuildHost(new ThrowingSignerBase(StatusCode.PermissionDenied));
        var client = BuildClient(host);

        var result = await client.SignAsync(
            new DtoSignInput("k", new byte[] { 0x01 }),
            pipelineOverride: ResilientPipeline<string, DtoSignOutput?>.PassThrough);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // ---------------------------------------------------------------------------
    // Case 7: transport TRANSIENT retry — a gRPC retry pipeline (custom IsTransient via
    // IsTransientGrpcException) retries a thrown RpcException(Unavailable). The capture
    // RETHROWS, so the retry layer still sees the throw. After the budget exhausts the
    // result maps to ServiceUnavailable (503), and the stub was called > 1 time.
    // Proves the capture-remap fix does NOT suppress retries.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_TransportTransient_RetriesThenServiceUnavailable()
    {
        var signer = new ThrowingSignerBase(StatusCode.Unavailable);
        using var host = await BuildHost(signer);
        using var retryPipeline = BuildGrpcRetryPipeline(maxAttempts: 3);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.SignAsync(new DtoSignInput("k", new byte[] { 0x01 }));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        signer.CallCount.Should().BeGreaterThan(1, "a transient RpcException must be retried");
    }

    // ---------------------------------------------------------------------------
    // Case 8: transport transient RECOVERY — flaky service throws Unavailable once then
    // succeeds. The retry pipeline recovers; the client returns the reconstructed success.
    // Call count == 2 (one failed attempt + one success).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_TransportTransient_RecoversAfterRetry()
    {
        const string recovered_sig = "recovered==";
        var signer = new FlakyThenSuccessSignerBase(StatusCode.Unavailable, recovered_sig);
        using var host = await BuildHost(signer);
        using var retryPipeline = BuildGrpcRetryPipeline(maxAttempts: 3);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.SignAsync(new DtoSignInput("k", new byte[] { 0x01 }));

        result.Success.Should().BeTrue();
        result.Data!.Signature.Should().Be(recovered_sig);
        signer.CallCount.Should().Be(2);
    }

    // ---------------------------------------------------------------------------
    // Case 9: transport PERMANENT (non-transient) — RpcException(InvalidArgument, code 3).
    // The custom IsTransient (IsTransientGrpcException) excludes code 3, so the retry
    // pipeline does NOT retry (one call). The fault still maps to ServiceUnavailable (503).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_TransportPermanent_InvalidArgument_NotRetried_ServiceUnavailable()
    {
        var signer = new ThrowingSignerBase(StatusCode.InvalidArgument);
        using var host = await BuildHost(signer);
        using var retryPipeline = BuildGrpcRetryPipeline(maxAttempts: 5);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.SignAsync(new DtoSignInput("k", new byte[] { 0x01 }));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        signer.CallCount.Should().Be(1, "code 3 is not in IsTransientGrpcException — no retry");
    }

    // ---------------------------------------------------------------------------
    // Case 10: per-call pipelineOverride bypasses the injected retry pipeline.
    // The client is constructed with a high-attempt retry pipeline; the PassThrough
    // override must bypass all retries (one call) and return promptly.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_WithPassThroughOverride_BypassesInjectedRetryPipeline()
    {
        var signer = new ThrowingSignerBase(StatusCode.Unavailable);
        using var host = await BuildHost(signer);
        using var retryPipeline = BuildGrpcRetryPipeline(maxAttempts: 100);
        var client = BuildClientWithPipeline(host, retryPipeline);

        var result = await client.SignAsync(
            new DtoSignInput("k", new byte[] { 0x01 }),
            pipelineOverride: ResilientPipeline<string, DtoSignOutput?>.PassThrough);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        signer.CallCount.Should().Be(1, "the PassThrough override has zero layers — no retry");
    }

    // ---------------------------------------------------------------------------
    // Case 11: null data on a success envelope (void-output op) → Data is null, no NRE.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_NullData_ReturnsSuccessWithNullData()
    {
        using var host = await BuildHost(new NullDataSignerBase());
        var client = BuildClient(host);

        var result = await client.SignAsync(new DtoSignInput("k", new byte[] { 0x01 }));

        result.Success.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // Case 12: cancellation — caller cancels the token before the server responds.
    // The result is non-Success (Canceled / ServiceUnavailable depending on the
    // RpcException status the transport surfaces) and is NEVER UnhandledException (500).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_Canceled_ReturnsNonSuccess_NotUnhandled()
    {
        using var cts = new CancellationTokenSource();
        using var host = await BuildHost(
            new DelayThenSuccessSignerBase(TimeSpan.FromSeconds(10)));
        var client = BuildClient(host);

        await cts.CancelAsync();

        var result = await client.SignAsync(
            new DtoSignInput("k", new byte[] { 0x01 }),
            pipelineOverride: ResilientPipeline<string, DtoSignOutput?>.PassThrough,
            ct: cts.Token);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);

        // Cancellation maps to Canceled (HTTP 400) or ServiceUnavailable (503); never 500.
        // The Canceled arm is the primary path (OperationCanceledException / StatusCode.Cancelled).
        result.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,      // Canceled → 400
            HttpStatusCode.ServiceUnavailable); // fallback for other RpcException statuses
    }

    // ---------------------------------------------------------------------------
    // Case 13: input round-trip — Kid + Payload survive gRPC ↔ proto ↔ DTO mapping.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_InputRoundTrip_KidAndPayloadSurviveProtoMapping()
    {
        var echoSigner = new EchoSignerBase();
        using var host = await BuildHost(echoSigner);
        var client = BuildClient(host);

        const string kid = "rsa-4096-primary";
        var payload = new byte[] { 10, 20, 30, 40, 50 };

        await client.SignAsync(new DtoSignInput(kid, payload));

        echoSigner.ReceivedKid.Should().Be(kid);
        echoSigner.ReceivedPayload.Should().Equal(payload);
    }

    // ---------------------------------------------------------------------------
    // Case 14: null envelope — success response with NO Result field set (server-contract
    // violation). The captured envelope is null → the success branch's `envelope is not
    // null` guard is false → the pipeline result passes through verbatim (no NRE).
    // A null envelope on a 200 is a defect; the code does not crash.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_NullEnvelope_DoesNotThrow_PassesPipelineResultVerbatim()
    {
        using var host = await BuildHost(new NullEnvelopeSignerBase());
        var client = BuildClient(host);

        var act = async () => await client.SignAsync(
            new DtoSignInput("k", new byte[] { 0x01 }),
            pipelineOverride: ResilientPipeline<string, DtoSignOutput?>.PassThrough);

        var result = await act.Should().NotThrowAsync();
        result.Subject.Success.Should().BeTrue("the pipeline Ok-wrapped the mapped data; envelope null → verbatim");
    }

    // ---------------------------------------------------------------------------
    // Case 15: business error-code survives round-trip in the reconstructed envelope.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_BusinessFailure_ConflictWithErrorCode_SurvivesRoundTrip()
    {
        const string error_code = "KC_KEY_NOT_FOUND";
        using var host = await BuildHost(new BusinessFailureSignerBase(
            D2Result<DtoSignOutput?>.Conflict(errorCode: error_code)));
        var client = BuildClient(host);

        var result = await client.SignAsync(new DtoSignInput("k", new byte[] { 0x01 }));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(error_code);
    }

    // ---------------------------------------------------------------------------
    // Case 16: DI resolution — AddD2KeyCustodianGrpcClients resolves the client + every
    // registered seam (the keyed pipeline + the stub) without throwing. The generated DI
    // extension AUTO-CHAINS .AddD2ForwardedJwt().AddD2WorkloadCertificate() on the channel,
    // so resolving the typed stub eagerly runs both ConfigureChannel callbacks — they
    // resolve the IAmbientRequestScopeAccessor port (owned by the inbound transport) plus
    // the WorkloadLeafCache + TimeProvider (from AddD2WorkloadCertificateOutbound). The host's
    // one-time config registrations supply exactly those: AddD2ForwardedJwtOutbound() +
    // AddD2WorkloadCertificateOutbound() (the un-inventable config the emitter documents),
    // and the inbound transport supplies the ambient accessor (here a no-inbound-scope stub,
    // since this isolated DI-resolution test makes no RPC).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddD2KeyCustodianGrpcClients_ResolvesClientAndKeyedPipeline()
    {
        using var host = await BuildHost(new SuccessSignerBase("test-sig"));
        var httpClient = host.GetTestClient();

        var services = new ServiceCollection();

        // The host's one-time config the auto-wired chain resolves at channel build:
        // AddD2WorkloadCertificateOutbound() registers WorkloadLeafCache + TimeProvider;
        // AddD2ForwardedJwtOutbound() keeps the options pipeline symmetric. The ambient
        // accessor is owned by the inbound transport — supplied here as a no-inbound-scope
        // stub (this DI-resolution test makes no RPC, so the credential never runs).
        services.AddD2ForwardedJwtOutbound();
        services.AddD2WorkloadCertificateOutbound();
        services.AddSingleton<IAmbientRequestScopeAccessor>(new NoAmbientScopeAccessor());

        services.AddD2KeyCustodianGrpcClients(new KeyCustodianGrpcClientOptions
        {
            Address = httpClient.BaseAddress!,
        });

        // Test-only: the auto-wired .AddD2ForwardedJwt() sets the channel to SecureSsl
        // ChannelCredentials, which gRPC refuses to pair with this harness's plaintext
        // (http-scheme) in-process TestServer channel at construction. A trailing
        // ConfigureChannel on the SAME named client runs LAST and downgrades the channel to
        // Insecure so it builds — this case asserts DI RESOLVABILITY only (no RPC is made),
        // so the forwarded-JWT CallCredentials are not exercised here (their per-request
        // attach + forward-unchanged behavior is covered by the ForwardedJwtCallCredentials
        // tests). Production stays secure via real mTLS/TLS; this downgrade is never shipped.
        services
            .AddGrpcClient<KeyCustodianSigner.KeyCustodianSignerClient>()
            .ConfigureChannel(o => o.Credentials = ChannelCredentials.Insecure);

        await using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IKeyCustodianGrpcClient>();
        client.Should().NotBeNull();
        client.Should().BeOfType<KeyCustodianGrpcClient>();

        // The keyed pipeline seam must resolve too — descriptor presence ≠ resolvability.
        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, DtoSignOutput?>>(
            SignClientKeys.PIPELINE);
        pipeline.Should().NotBeNull();
    }

    // ---------------------------------------------------------------------------
    // Helpers — host + channel + client + pipeline construction
    // ---------------------------------------------------------------------------

    private static async Task<IHost> BuildHost(KeyCustodianSigner.KeyCustodianSignerBase signer)
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
                        endpoints.MapGrpcService<KeyCustodianSigner.KeyCustodianSignerBase>();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static KeyCustodianGrpcClient BuildClient(IHost host)
        => BuildClientWithPipeline(host, ResilientPipeline<string, DtoSignOutput?>.PassThrough);

    private static KeyCustodianGrpcClient BuildClientWithPipeline(
        IHost host,
        ResilientPipeline<string, DtoSignOutput?> pipeline)
    {
        var httpClient = host.GetTestClient();
        var channel = GrpcChannel.ForAddress(
            httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = httpClient });
        var stub = new KeyCustodianSigner.KeyCustodianSignerClient(channel);
        return new KeyCustodianGrpcClient(stub, pipeline);
    }

    /// <summary>
    /// Builds a retry-only pipeline with the SAME gRPC-aware transient predicate the
    /// generated DI extension emits (<c>IsTransientGrpcException</c>) but with a fast
    /// near-zero backoff so the retry cases do not wall-clock the suite.
    /// </summary>
    private static ResilientPipeline<string, DtoSignOutput?> BuildGrpcRetryPipeline(int maxAttempts)
    {
        var builder = new ResilientPipelineBuilder<string, DtoSignOutput?>(
            new ServiceCollection().BuildServiceProvider());
        builder.UseRetries(new RetryOptions<DtoSignOutput?>
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 1,
            Jitter = false,
            IsTransient = ex => ex is RpcException r && ProtoExtensions.IsTransientGrpcException(r),
        });
        return builder.Build();
    }

    // ---------------------------------------------------------------------------
    // Server shims
    // ---------------------------------------------------------------------------

    /// <summary>Returns a successful SignResponse with a fixed signature.</summary>
    private sealed class SuccessSignerBase(string signature)
        : KeyCustodianSigner.KeyCustodianSignerBase
    {
        public override Task<SignResponse> Sign(SignRequest request, ServerCallContext context)
        {
            var result = D2Result<DtoSignOutput?>.Ok(new DtoSignOutput(signature));
            var response = new SignResponse { Result = result.ToProto() };
            response.Data = new ProtoSignOutput { Signature = signature };
            return Task.FromResult(response);
        }
    }

    /// <summary>Returns a SignResponse carrying a business failure envelope (gRPC status OK).</summary>
    private sealed class BusinessFailureSignerBase(D2Result<DtoSignOutput?> businessResult)
        : KeyCustodianSigner.KeyCustodianSignerBase
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override Task<SignResponse> Sign(SignRequest request, ServerCallContext context)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(new SignResponse { Result = businessResult.ToProto() });
        }
    }

    /// <summary>Throws an RpcException with the given status code on every call (transport fault).</summary>
    private sealed class ThrowingSignerBase(StatusCode statusCode)
        : KeyCustodianSigner.KeyCustodianSignerBase
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override Task<SignResponse> Sign(SignRequest request, ServerCallContext context)
        {
            Interlocked.Increment(ref _callCount);
            throw new RpcException(new Status(statusCode, "simulated transport fault"));
        }
    }

    /// <summary>Throws the given status once, then returns a successful response (recovery shim).</summary>
    private sealed class FlakyThenSuccessSignerBase(StatusCode statusCode, string signature)
        : KeyCustodianSigner.KeyCustodianSignerBase
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override Task<SignResponse> Sign(SignRequest request, ServerCallContext context)
        {
            var attempt = Interlocked.Increment(ref _callCount);
            if (attempt == 1)
                throw new RpcException(new Status(statusCode, "transient transport fault"));

            var result = D2Result<DtoSignOutput?>.Ok(new DtoSignOutput(signature));
            var response = new SignResponse { Result = result.ToProto() };
            response.Data = new ProtoSignOutput { Signature = signature };
            return Task.FromResult(response);
        }
    }

    /// <summary>Returns a SignResponse with only the Ok envelope set (data = null).</summary>
    private sealed class NullDataSignerBase : KeyCustodianSigner.KeyCustodianSignerBase
    {
        public override Task<SignResponse> Sign(SignRequest request, ServerCallContext context)
        {
            var result = D2Result<DtoSignOutput?>.Ok();
            return Task.FromResult(new SignResponse { Result = result.ToProto() });
        }
    }

    /// <summary>Returns a SignResponse with NO Result field set (null D2ResultProto on a 200).</summary>
    private sealed class NullEnvelopeSignerBase : KeyCustodianSigner.KeyCustodianSignerBase
    {
        public override Task<SignResponse> Sign(SignRequest request, ServerCallContext context)
        {
            // response.Result is not set → the proto3 sub-message field is null. The generated
            // client captures envelope = response.Result → null, and must not NRE.
            var response = new SignResponse();
            response.Data = new ProtoSignOutput { Signature = "no-envelope" };
            return Task.FromResult(response);
        }
    }

    /// <summary>Delays for the cancellation test, then would return success.</summary>
    private sealed class DelayThenSuccessSignerBase(TimeSpan delay)
        : KeyCustodianSigner.KeyCustodianSignerBase
    {
        public override async Task<SignResponse> Sign(SignRequest request, ServerCallContext context)
        {
            await Task.Delay(delay, context.CancellationToken);
            var result = D2Result<DtoSignOutput?>.Ok(new DtoSignOutput("late-sig"));
            return new SignResponse { Result = result.ToProto() };
        }
    }

    /// <summary>Records the Kid + Payload received from the client-side transport mapper.</summary>
    private sealed class EchoSignerBase : KeyCustodianSigner.KeyCustodianSignerBase
    {
        internal string ReceivedKid { get; private set; } = string.Empty;

        internal byte[] ReceivedPayload { get; private set; } = [];

        public override Task<SignResponse> Sign(SignRequest request, ServerCallContext context)
        {
            ReceivedKid = request.Kid;
            ReceivedPayload = request.Payload.ToByteArray();
            var result = D2Result<DtoSignOutput?>.Ok(new DtoSignOutput("echo-sig"));
            return Task.FromResult(new SignResponse { Result = result.ToProto() });
        }
    }

    /// <summary>
    /// A no-inbound-scope <see cref="IAmbientRequestScopeAccessor"/> for the DI-resolution
    /// case: the inbound transport owns this port in production, but this isolated test
    /// makes no RPC, so the auto-wired forwarded-JWT credential never reads it — the channel
    /// build only needs the singleton to RESOLVE. Returning null mirrors a hop with no
    /// inbound request on the execution context.
    /// </summary>
    private sealed class NoAmbientScopeAccessor : IAmbientRequestScopeAccessor
    {
        public IServiceProvider? Current => null;
    }
}
