// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtOutboundLogCaptureTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Logging;

using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Outbound.Grpc;
using D2.Shared.Headers.Grpc;
using D2.Shared.Logging.Destructuring;
using D2.Shared.Tests.Integration.Logging.Infrastructure;
using global::Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Xunit;

/// <summary>
/// Transmission-point leak pin for the outbound forwarding credential — the ONE
/// place the live forwarded bearer is revealed AND attached to the wire. Two
/// guarantees, both proven against a real Serilog logger (with the production
/// <see cref="RedactDataDestructuringPolicy"/> registered):
/// <list type="number">
///   <item><b>The credential emits NOTHING.</b> Running its interceptor across a
///   full reveal-and-attach (with the sentinel genuinely revealed onto the wire
///   metadata) produces zero log events — the credential holds no logger by
///   construction, so there is no path through which the live bearer could be
///   logged at the transmission point.</item>
///   <item><b>The held wrapper stays redacted.</b> Logging the
///   <see cref="ForwardedJwt"/> the credential reads — every way (structural /
///   plain / interpolated) — never surfaces the bytes (mirrors the inbound
///   <c>ForwardedJwtLogCaptureTests</c> wrapper-redaction proof, here from the
///   outbound read side).</item>
/// </list>
/// </summary>
public sealed class ForwardedJwtOutboundLogCaptureTests
{
    // Distinctive, JWT-shaped sentinel — a substring hit anywhere in the rendered
    // output is unambiguous evidence of a leak.
    private const string _KNOWN_JWT =
        "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJPVVRCT1VORF9TRU5USU5FTCJ9.OuTbOuNdSeNtInEl_77";

    [Fact]
    public async Task ForwardingCredential_EmitsNoLog_EvenWhenItRevealsAndAttachesTheBearer()
    {
        var (_, sink) = BuildLogger();

        // The ambient holder carries the sentinel, exactly as an inbound request
        // would have captured it.
        var scope = new ServiceCollection()
            .AddScoped<IForwardedJwtAccessor, MutableForwardedJwtAccessor>()
            .BuildServiceProvider();
        scope.GetRequiredService<IForwardedJwtAccessor>().Capture(_KNOWN_JWT);
        var ambient = new FixedAmbientAccessor(scope);

        var credentials = ForwardedJwtCallCredentials.FromAmbientRequestScope(ambient);
        var interceptor = ExtractInterceptor(credentials);

        var metadata = new Metadata();
        var context = new AuthInterceptorContext("https://callee.internal", "/svc/Method", default);

        // The reveal + attach (the forward leg). The credential genuinely reveals
        // the bytes onto the wire metadata here.
        await interceptor(context, metadata);

        metadata.Get(GrpcHeaders.AUTHORIZATION)!.Value
            .Should().Be("Bearer " + _KNOWN_JWT, "the attach genuinely happened — this is the reveal point");

        // The credential holds no logger: its execution emitted ZERO events. There
        // is no transmission-point log path through which the bearer could leak.
        sink.Events.Should().BeEmpty(
            "the forwarding credential logs nothing by construction — the live "
            + "bearer is revealed and attached without any log statement");
    }

    [Fact]
    public void HeldWrapper_LoggedEveryWay_FromOutboundReadSide_BytesNeverSurface()
    {
        var (logger, sink) = BuildLogger();

        // The wrapper the credential reads back through the ambient holder.
        var holder = new MutableForwardedJwtAccessor();
        holder.Capture(_KNOWN_JWT);
        var current = holder.Current!.Value;

        // (1) Structural destructuring — masked by [RedactData] policy.
        logger.Information("held-structural {@Held}", current);

        // (2) Plain ToString path — the wrapper's ToString yields the placeholder.
        logger.Information("held-plain {Held}", current);

        // (3) String-interpolation path — also routes through ToString().
        logger.Information("held-interp token={Token}", $"{current}");

        var rendered = string.Join("\n", sink.Events.Select(sink.Render));

        rendered.Should().NotContain(_KNOWN_JWT);
        rendered.Should().NotContain("OuTbOuNdSeNtInEl");
        rendered.Should().Contain(ForwardedJwt.REDACTION_PLACEHOLDER);
    }

    private static (ILogger Logger, InMemorySink Sink) BuildLogger()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Destructure.With<RedactDataDestructuringPolicy>()
            .WriteTo.Sink(sink, restrictedToMinimumLevel: LogEventLevel.Verbose)
            .CreateLogger();
        return (logger, sink);
    }

    private static AsyncAuthInterceptor ExtractInterceptor(CallCredentials credentials)
    {
        var configurator = new InterceptorCapturingConfigurator();
        credentials.InternalPopulateConfiguration(configurator, credentials);
        return configurator.Captured!;
    }

    private sealed class FixedAmbientAccessor(IServiceProvider scope) : IAmbientRequestScopeAccessor
    {
        // ReSharper disable once ReturnTypeCanBeNotNullable — interface contract requires nullable.
        public IServiceProvider? Current => scope;
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
