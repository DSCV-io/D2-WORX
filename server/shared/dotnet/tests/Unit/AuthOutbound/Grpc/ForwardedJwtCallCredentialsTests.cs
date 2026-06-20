// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtCallCredentialsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.Grpc;

using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Outbound.Grpc;
using D2.Shared.Headers.Grpc;
using global::Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="ForwardedJwtCallCredentials"/> — the
/// outbound reveal-and-attach point for the live forwarded transaction-token and
/// the R2 per-channel-singleton / per-request-token crux. The
/// <see cref="CallCredentials"/> these tests build carries an
/// <see cref="AsyncAuthInterceptor"/>; the tests EXTRACT that interceptor (via a
/// <see cref="CallCredentialsConfiguratorBase"/>) and invoke it against a real
/// <see cref="Metadata"/>, so each proof exercises the production credential
/// object and asserts the exact bytes it writes to the wire metadata.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ForwardedJwtCallCredentialsTests
{
    private const string _KNOWN_JWT =
        "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJBVFRBQ0gifQ.SiGnAtUrE_attach_01";

    private const string _OTHER_JWT =
        "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJDT05DIn0.SiGnAtUrE_conc_02";

    // ------------------------------------------------------------------
    // T-ATTACH — the forward-unchanged proof at the credential level.
    // ------------------------------------------------------------------
    [Fact]
    public async Task FromAmbientRequestScope_AttachesExactBearer_WhenHolderHasToken()
    {
        var ambient = AmbientWithCapturedJwt(_KNOWN_JWT);
        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(ambient);

        var metadata = await InvokeInterceptor(credentials);

        AuthorizationValue(metadata).Should().Be("Bearer " + _KNOWN_JWT);
    }

    // ------------------------------------------------------------------
    // T-CONC — the R1/R2 concurrency proof. ONE credential, two scopes each
    // holding a DISTINCT token; the ambient accessor returns each scope's
    // provider per invocation. Each call must attach ITS OWN bytes — proving a
    // long-lived channel forwards each concurrent request's own token.
    // ------------------------------------------------------------------
    [Fact]
    public async Task FromAmbientRequestScope_ForwardsEachScopesOwnToken_OnSharedCredential()
    {
        var scopeA = ProviderWithCapturedJwt(_KNOWN_JWT);
        var scopeB = ProviderWithCapturedJwt(_OTHER_JWT);

        // A single mutable ambient accessor whose Current is swapped per call —
        // models the AsyncLocal-backed IHttpContextAccessor observing a different
        // request scope on each async flow.
        var ambient = new SwitchableAmbientAccessor();
        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(ambient);

        ambient.Current = scopeA;
        var metadataA = await InvokeInterceptor(credentials);

        ambient.Current = scopeB;
        var metadataB = await InvokeInterceptor(credentials);

        AuthorizationValue(metadataA).Should().Be("Bearer " + _KNOWN_JWT);
        AuthorizationValue(metadataB).Should().Be("Bearer " + _OTHER_JWT);
        AuthorizationValue(metadataA).Should().NotBe(AuthorizationValue(metadataB));
    }

    // ------------------------------------------------------------------
    // T-ABSENT (a) — holder present but Current is empty/absent → Unauthenticated,
    // no Authorization attached.
    // ------------------------------------------------------------------
    [Fact]
    public async Task FromAmbientRequestScope_HardFails_WhenHolderHasNoToken()
    {
        // A registered holder that captured nothing (Current is null).
        var provider = new ServiceCollection()
            .AddScoped<IForwardedJwtAccessor, MutableForwardedJwtAccessor>()
            .BuildServiceProvider();
        var ambient = new SwitchableAmbientAccessor { Current = provider };
        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(ambient);

        var (status, metadata) = await InvokeInterceptorExpectingRpc(credentials);

        status.StatusCode.Should().Be(StatusCode.Unauthenticated);
        metadata.Get(GrpcHeaders.AUTHORIZATION).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // T-ABSENT (b) — no ambient request scope at all → Unauthenticated.
    // ------------------------------------------------------------------
    [Fact]
    public async Task FromAmbientRequestScope_HardFails_WhenNoAmbientScope()
    {
        var ambient = new SwitchableAmbientAccessor { Current = null };
        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(ambient);

        var (status, metadata) = await InvokeInterceptorExpectingRpc(credentials);

        status.StatusCode.Should().Be(StatusCode.Unauthenticated);
        metadata.Get(GrpcHeaders.AUTHORIZATION).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // T-ABSENT (c) — ambient scope present but the holder is NOT registered →
    // Unauthenticated (never a silent no-header).
    // ------------------------------------------------------------------
    [Fact]
    public async Task FromAmbientRequestScope_HardFails_WhenHolderNotRegistered()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var ambient = new SwitchableAmbientAccessor { Current = provider };
        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(ambient);

        var (status, metadata) = await InvokeInterceptorExpectingRpc(credentials);

        status.StatusCode.Should().Be(StatusCode.Unauthenticated);
        metadata.Get(GrpcHeaders.AUTHORIZATION).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // T-VERBATIM — a structurally-hostile JWT (dots, base64url -/_, trailing ==,
    // an embedded "Bearer " substring) is revealed + attached byte-for-byte.
    // ------------------------------------------------------------------
    [Fact]
    public async Task FromAmbientRequestScope_AttachesHostileBytesVerbatim()
    {
        const string hostile_jwt =
            "eyJ-_.eyJ-_payload.Bearer SIG-with_trailing==";
        var ambient = AmbientWithCapturedJwt(hostile_jwt);
        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(ambient);

        var metadata = await InvokeInterceptor(credentials);

        // Exactly one scheme prefix + the verbatim bytes (no trim, no re-encode),
        // even though the token itself contains a "Bearer " substring.
        AuthorizationValue(metadata).Should().Be("Bearer " + hostile_jwt);
    }

    // ------------------------------------------------------------------
    // T-NULL-ARG — null accessor → ArgumentNullException.
    // ------------------------------------------------------------------
    [Fact]
    public void FromAmbientRequestScope_NullAccessor_Throws()
    {
        var act = () => ForwardedJwtCallCredentials.FromAmbientRequestScope(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------
    // T-CANCEL — a canceled call token does not change the attach behavior (the
    // body is synchronous; a present token still attaches). The interceptor
    // honors the supplied AuthInterceptorContext.CancellationToken without
    // faulting on a present token.
    // ------------------------------------------------------------------
    [Fact]
    public async Task FromAmbientRequestScope_CanceledToken_StillAttaches_NoFault()
    {
        var ambient = AmbientWithCapturedJwt(_KNOWN_JWT);
        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(ambient);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var metadata = await InvokeInterceptor(credentials, cts.Token);

        AuthorizationValue(metadata).Should().Be("Bearer " + _KNOWN_JWT);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private static IAmbientRequestScopeAccessor AmbientWithCapturedJwt(string jwt) =>
        new SwitchableAmbientAccessor { Current = ProviderWithCapturedJwt(jwt) };

    private static ServiceProvider ProviderWithCapturedJwt(string jwt)
    {
        var provider = new ServiceCollection()
            .AddScoped<IForwardedJwtAccessor, MutableForwardedJwtAccessor>()
            .BuildServiceProvider();
        provider.GetRequiredService<IForwardedJwtAccessor>().Capture(jwt);
        return provider;
    }

    private static string? AuthorizationValue(Metadata metadata) =>
        metadata.Get(GrpcHeaders.AUTHORIZATION)?.Value;

    private static async Task<Metadata> InvokeInterceptor(
        CallCredentials credentials,
        CancellationToken ct = default)
    {
        var interceptor = ExtractInterceptor(credentials);
        var metadata = new Metadata();
        var context = new AuthInterceptorContext("https://callee.internal", "/svc/Method", ct);

        await interceptor(context, metadata);

        return metadata;
    }

    private static async Task<(Status Status, Metadata Metadata)> InvokeInterceptorExpectingRpc(
        CallCredentials credentials)
    {
        var interceptor = ExtractInterceptor(credentials);
        var metadata = new Metadata();
        var context = new AuthInterceptorContext("https://callee.internal", "/svc/Method", default);

        var ex = await Assert.ThrowsAsync<RpcException>(() => interceptor(context, metadata));

        return (ex.Status, metadata);
    }

    // Pulls the AsyncAuthInterceptor the CallCredentials carries — proving the
    // credential OBJECT wires our interceptor (not merely that the static builder
    // produces one).
    private static AsyncAuthInterceptor ExtractInterceptor(CallCredentials credentials)
    {
        var configurator = new InterceptorCapturingConfigurator();
        credentials.InternalPopulateConfiguration(configurator, credentials);

        configurator.Captured.Should().NotBeNull(
            "the forwarding credential must be built from an AsyncAuthInterceptor");

        return configurator.Captured!;
    }

    private sealed class SwitchableAmbientAccessor : IAmbientRequestScopeAccessor
    {
        public IServiceProvider? Current { get; set; }
    }

    private sealed class InterceptorCapturingConfigurator : CallCredentialsConfiguratorBase
    {
        public AsyncAuthInterceptor? Captured { get; private set; }

        public override void SetAsyncAuthInterceptorCredentials(
            object? state,
            AsyncAuthInterceptor interceptor)
            => Captured = interceptor;

        public override void SetCompositeCredentials(
            object? state,
            IReadOnlyList<CallCredentials> credentials)
        {
            // The forwarding credential is built from a single interceptor, never
            // a composite — no-op for this scan.
        }
    }
}
