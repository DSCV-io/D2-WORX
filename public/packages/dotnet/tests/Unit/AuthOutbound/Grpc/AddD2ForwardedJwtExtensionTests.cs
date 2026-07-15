// -----------------------------------------------------------------------
// <copyright file="AddD2ForwardedJwtExtensionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.Grpc;

using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Outbound.Grpc;
using D2.Shared.Headers.Grpc;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Protos;
using global::Grpc.Core;
using global::Grpc.Net.Client;
using global::Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Coverage for the per-channel <c>.AddD2ForwardedJwt()</c> opt-in extension —
/// the host-facing surface the generated gRPC-client DI extension auto-chains.
/// Pins the call shape, the argument-validation contract, the compose-don't-
/// clobber composition with the mTLS sibling and with a pre-existing credential,
/// and the EXACT-bytes-on-the-wire forward-unchanged proof (the configured
/// channel's <c>options.Credentials</c> carries our interceptor, which attaches
/// the verbatim bearer).
/// </summary>
[Trait("Category", "Unit")]
public sealed class AddD2ForwardedJwtExtensionTests
{
    private const string _KNOWN_JWT =
        "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJXSVJFIn0.SiGnAtUrE_wire_03";

    private const string _TEST_CLIENT = nameof(TestEcho.TestEchoClient);

    // ------------------------------------------------------------------
    // T-SHAPE — .AddD2ForwardedJwt() compiles on a bare IHttpClientBuilder (no
    // explicit generic) and wires through to gRPC's ConfigureChannel: chained on a
    // non-gRPC AddHttpClient it surfaces the *gRPC client* InvalidOperationException
    // (confirms it didn't silently no-op).
    // ------------------------------------------------------------------
    [Fact]
    public void AddD2ForwardedJwt_CompilesWithoutExplicitGeneric_AndWiresToConfigureChannel()
    {
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("test");

        var act = () => builder.AddD2ForwardedJwt();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*gRPC client*");
    }

    // ------------------------------------------------------------------
    // T-NULL-BUILDER — null builder → ArgumentNullException.
    // ------------------------------------------------------------------
    [Fact]
    public void AddD2ForwardedJwt_NullBuilder_Throws()
    {
        IHttpClientBuilder? builder = null;

        var act = () => builder!.AddD2ForwardedJwt();

        act.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------
    // T-WIRE — the EXACT bytes hit the wire unchanged. Build a real gRPC client
    // with .AddD2ForwardedJwt(), run the configured ChannelOptionsActions to obtain
    // the channel's options.Credentials, extract the AsyncAuthInterceptor it
    // carries, and assert it attaches "Bearer " + knownJwt verbatim to a real
    // Metadata (the metadata gRPC transmits).
    // ------------------------------------------------------------------
    [Fact]
    public async Task AddD2ForwardedJwt_AttachesExactBearerToWireMetadata()
    {
        var provider = BuildProviderWithForwardedJwtClient(_KNOWN_JWT);
        var options = ConfiguredChannelOptions(provider);

        options.Credentials.Should().NotBeNull("the forwarded JWT sets options.Credentials");

        var metadata = await InvokeCredential(options.Credentials!);

        metadata.Get(GrpcHeaders.AUTHORIZATION)?.Value.Should().Be("Bearer " + _KNOWN_JWT);
    }

    // ------------------------------------------------------------------
    // T-COMPOSE (a) — .AddD2ForwardedJwt().AddD2WorkloadCertificate(): BOTH take
    // effect. The forwarded JWT sets options.Credentials; the mTLS leaf chain sets
    // the channel handler's SslOptions — neither clobbers the other.
    // ------------------------------------------------------------------
    [Fact]
    public void AddD2ForwardedJwt_ComposesAlongsideWorkloadCertificate_NeitherClobbered()
    {
        var services = BaseServices(_KNOWN_JWT);

        // mTLS sibling needs the leaf cache + a clock. An empty cache (no current
        // leaf) is fine — the extension still installs the SocketsHttpHandler.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<D2.Shared.Auth.Outbound.WorkloadCertificate.WorkloadLeafCache>();

        services
            .AddGrpcClient<TestEcho.TestEchoClient>(o => o.Address = new Uri("https://callee.internal"))
            .AddD2ForwardedJwt()
            .AddD2WorkloadCertificate();

        var provider = services.BuildServiceProvider();
        var options = ConfiguredChannelOptions(provider);

        options.Credentials.Should().NotBeNull("the forwarded JWT axis set options.Credentials");
        options.HttpHandler.Should().BeOfType<SocketsHttpHandler>(
            "the mTLS axis installed the channel handler — orthogonal, not clobbered");
    }

    // ------------------------------------------------------------------
    // T-COMPOSE (b) — a pre-existing options.Credentials is COMPOSED with, not
    // replaced. Pre-seed a credential via a prior ConfigureChannel, then
    // .AddD2ForwardedJwt(); the resulting credential is a composite (both survive).
    // ------------------------------------------------------------------
    [Fact]
    public async Task AddD2ForwardedJwt_ComposesWithPreExistingCredential_OriginalNotLost()
    {
        var services = BaseServices(_KNOWN_JWT);

        // A sentinel pre-existing call credential that stamps its own header.
        var preExisting = CallCredentials.FromInterceptor((_, metadata) =>
        {
            metadata.Add("x-pre-existing", "present");
            return Task.CompletedTask;
        });

        services
            .AddGrpcClient<TestEcho.TestEchoClient>(o => o.Address = new Uri("https://callee.internal"))
            .ConfigureChannel(options => options.Credentials =
                ChannelCredentials.Create(ChannelCredentials.SecureSsl, preExisting))
            .AddD2ForwardedJwt();

        var provider = services.BuildServiceProvider();
        var options = ConfiguredChannelOptions(provider);

        options.Credentials.Should().NotBeNull();

        // The composite carries BOTH the pre-existing interceptor (its sentinel
        // header) AND ours (the Bearer) — the original was composed, not lost.
        var metadata = await InvokeAllCallCredentials(options.Credentials!);

        metadata.Get("x-pre-existing")?.Value.Should().Be("present", "original credential survived");
        metadata.Get(GrpcHeaders.AUTHORIZATION)?.Value.Should().Be("Bearer " + _KNOWN_JWT);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private static ServiceCollection BaseServices(string jwt)
    {
        var services = new ServiceCollection();

        // The ambient adapter the channel-build resolves, backed by a fixed scope
        // whose holder carries the token (so the per-call resolution finds it).
        var scope = new ServiceCollection()
            .AddScoped<IForwardedJwtAccessor, MutableForwardedJwtAccessor>()
            .BuildServiceProvider();
        scope.GetRequiredService<IForwardedJwtAccessor>().Capture(jwt);
        services.AddSingleton<IAmbientRequestScopeAccessor>(new FixedAmbientAccessor(scope));

        return services;
    }

    private static ServiceProvider BuildProviderWithForwardedJwtClient(string jwt)
    {
        var services = BaseServices(jwt);

        services
            .AddGrpcClient<TestEcho.TestEchoClient>(o => o.Address = new Uri("https://callee.internal"))
            .AddD2ForwardedJwt();

        return services.BuildServiceProvider();
    }

    private static GrpcChannelOptions ConfiguredChannelOptions(IServiceProvider provider)
    {
        var factoryOptions = provider
            .GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>()
            .Get(_TEST_CLIENT);

        var channelOptions = new GrpcChannelOptions();
        foreach (var action in factoryOptions.ChannelOptionsActions)
            action(channelOptions);

        return channelOptions;
    }

    private static Task<Metadata> InvokeCredential(ChannelCredentials credentials) =>
        InvokeInterceptors(credentials, composite: false);

    private static Task<Metadata> InvokeAllCallCredentials(ChannelCredentials credentials) =>
        InvokeInterceptors(credentials, composite: true);

    private static async Task<Metadata> InvokeInterceptors(
        ChannelCredentials credentials,
        bool composite)
    {
        var configurator = new ChannelCredentialsInspector();
        credentials.InternalPopulateConfiguration(configurator, credentials);

        configurator.CallCredentials.Should().NotBeNull(
            "the composite channel credential must carry call credentials");

        var metadata = new Metadata();
        var context = new AuthInterceptorContext("https://callee.internal", "/svc/Method", default);

        var interceptors = configurator.CollectInterceptors();
        if (!composite)
            interceptors = [interceptors[^1]]; // the forwarding interceptor is composed last

        foreach (var interceptor in interceptors)
            await interceptor(context, metadata);

        return metadata;
    }

    private sealed class FixedAmbientAccessor(IServiceProvider scope) : IAmbientRequestScopeAccessor
    {
        // ReSharper disable once ReturnTypeCanBeNotNullable — interface contract requires nullable.
        public IServiceProvider? Current => scope;
    }

    // Walks a (possibly composite) ChannelCredentials, capturing the nested
    // CallCredentials, then flattens any composite CallCredentials into the ordered
    // AsyncAuthInterceptor list.
    private sealed class ChannelCredentialsInspector : ChannelCredentialsConfiguratorBase
    {
        public CallCredentials? CallCredentials { get; private set; }

        public override void SetInsecureCredentials(object? state)
        {
        }

        public override void SetSslCredentials(
            object? state,
            string? rootCertificates,
            KeyCertificatePair? keyCertificatePair,
            VerifyPeerCallback? verifyPeerCallback)
        {
        }

        public override void SetCompositeCredentials(
            object? state,
            ChannelCredentials channelCredentials,
            CallCredentials callCredentials)
            => CallCredentials = callCredentials;

        public List<AsyncAuthInterceptor> CollectInterceptors()
        {
            var interceptors = new List<AsyncAuthInterceptor>();
            Collect(CallCredentials, interceptors);
            return interceptors;
        }

        private static void Collect(CallCredentials? credentials, List<AsyncAuthInterceptor> sink)
        {
            if (credentials is null)
                return;

            var inspector = new CallCredentialsFlattener();
            credentials.InternalPopulateConfiguration(inspector, credentials);

            if (inspector.Interceptor is not null)
                sink.Add(inspector.Interceptor);

            if (inspector.Composite is not null)
            {
                foreach (var inner in inspector.Composite)
                    Collect(inner, sink);
            }
        }
    }

    private sealed class CallCredentialsFlattener : CallCredentialsConfiguratorBase
    {
        public AsyncAuthInterceptor? Interceptor { get; private set; }

        public IReadOnlyList<CallCredentials>? Composite { get; private set; }

        public override void SetAsyncAuthInterceptorCredentials(
            object? state,
            AsyncAuthInterceptor interceptor)
            => Interceptor = interceptor;

        public override void SetCompositeCredentials(
            object? state,
            IReadOnlyList<CallCredentials> credentials)
            => Composite = credentials;
    }
}
