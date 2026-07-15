// -----------------------------------------------------------------------
// <copyright file="LoggingPipelineE2ETests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Tests.Integration.Logging.Infrastructure;
using DcsvIo.D2.Tests.Integration.ServiceDefaults.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Xunit;

/// <summary>
/// Composed-pipeline E2E tests for the Serilog request-logging surface —
/// pins request-completion-line shape, infrastructure-path level
/// downgrade, IRequestContext enrichment (positive + null + not-registered
/// gates), and the NEGATIVE-regression contract that the eight NOT-LOGGED
/// PII fields NEVER appear in the rendered log output even when
/// IRequestContext is fully populated.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class LoggingPipelineE2ETests
{
    [Fact]
    public async Task RequestLogging_OnGetProbe_EmitsCompletionLine_WithStandardFields()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();
        requestEvent.MessageTemplate.Text.Should().Contain("HTTP");

        // RequestMethod / RequestPath / StatusCode / Elapsed are bound
        // by Serilog's request-completion middleware — pin their presence
        // as the wire contract.
        requestEvent.Properties.Keys.Should().Contain("RequestMethod");
        requestEvent.Properties.Keys.Should().Contain("RequestPath");
        requestEvent.Properties.Keys.Should().Contain("StatusCode");
        requestEvent.Properties.Keys.Should().Contain("Elapsed");
    }

    [Fact]
    public async Task RequestLogging_OnGetProbe_LineCarriesTraceId()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();

        // TraceId is set by the diagnostic-context enricher inside
        // UseD2RequestLogging from HttpContext.TraceIdentifier.
        requestEvent.Properties.Should().ContainKey("TraceId");
        requestEvent.Properties["TraceId"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RequestLogging_OnGetHealth_LineCarriesInfrastructureLevelDowngrade()
    {
        // /health is in the infrastructure-path list — UseD2RequestLogging's
        // GetLevel callback returns Verbose so the default min-level gate
        // filters it out. Per-host local logger is at Verbose so the event
        // IS captured for assertion.
        await using var handle = await CompositeTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/health", UriKind.Relative));

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Level.Should().Be(LogEventLevel.Verbose);
    }

    [Fact]
    public async Task RedactionEnforcement_E2E_RedactedFixture_MarkersInOutput_NotRawValues()
    {
        // The /log-redacted endpoint logs a [RedactData]-annotated record
        // through the static Serilog facade — the
        // RedactDataDestructuringPolicy wired by AddD2Logging must replace
        // the inline values with redaction markers.
        await using var handle = await CompositeTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/log-redacted", UriKind.Relative));

        var rendered = string.Join("\n", handle.Sink.Events.Select(handle.Sink.Render));

        // Raw values MUST NOT appear in the rendered log line.
        rendered.Should().NotContain("alice@example.com");
        rendered.Should().NotContain("+1-555-0100");
        rendered.Should().NotContain("742 Evergreen Terrace");

        // The redaction marker is the policy's contract.
        rendered.Should().Contain("REDACTED");
    }

    [Fact]
    public async Task RequestContextEnricher_WhenContextPopulated_LogLineCarriesUserIdOrgIdScopes()
    {
        // Pin a representative subset of the LOG-OK 42-field contract
        // under the COMPOSED pipeline. Drives a real /probe request
        // through the full UseD2DefaultPipeline so request-logging fires.
        var stub = new StubRequestContext
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OrgId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "auth.user.read" },
        };
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.UserId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.OrgId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.Scopes));
    }

    [Fact]
    public async Task RequestContextEnricher_WhenContextNull_NoEnrichment_NoCrash()
    {
        // IRequestContext registered but every field null/empty → enricher
        // emits no LOG-OK keys (per-field null gates suppress every
        // emission). Request must still complete cleanly.
        var stub = new StubRequestContext();
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        var client = handle.Host.GetTestClient();

        var act = async () =>
            await client.GetAsync(new Uri("/probe", UriKind.Relative));
        await act.Should().NotThrowAsync();

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Keys.Should()
            .NotContain(nameof(IRequestContext.UserId));
        requestEvent.Properties.Keys.Should()
            .NotContain(nameof(IRequestContext.OrgId));
    }

    [Fact]
    public async Task RequestContextEnricher_WhenContextNotRegistered_NoEnrichment_NoCrash()
    {
        // Pre-Edge-filler reality: most services don't yet have
        // IRequestContext wired. The enricher must degrade silently — no
        // throw, no crash — under the COMPOSED pipeline.
        await using var handle = await CompositeTestHostBuilder.BuildAsync();
        var client = handle.Host.GetTestClient();

        var act = async () =>
            await client.GetAsync(new Uri("/probe", UriKind.Relative));
        await act.Should().NotThrowAsync();

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();

        // Static fields the middleware emits unconditionally (TraceId via
        // diag-ctx, RequestScheme, UserAgent) ARE present.
        requestEvent.Properties.Keys.Should().Contain("RequestScheme");
        requestEvent.Properties.Keys.Should().Contain("UserAgent");
    }

    [Fact]
    public async Task RequestContextEnricher_NeverEmitsClientIp_OrGeoOrAsnPii()
    {
        // NEGATIVE regression for the 8 NOT-LOGGED PII fields under the
        // COMPOSED pipeline. ClientIp + 7 Geo / network-privacy / ASN
        // fields populated; rendered output checked for ZERO occurrences.
        var stub = new StubRequestContext
        {
            ClientIp = "203.0.113.42",
            City = "San Francisco",
            PostalCode = "94103",
            SubdivisionIso31662Code = "US-CA",
            Latitude = 37.7749,
            Longitude = -122.4194,
            Geohash = "9q8yy",
        };
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));

        var rendered = string.Join("\n", handle.Sink.Events.Select(handle.Sink.Render));
        rendered.Should().NotContain($"\"{nameof(IRequestContext.ClientIp)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.City)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.PostalCode)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.SubdivisionIso31662Code)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.Latitude)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.Longitude)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.Geohash)}\"");
    }

    [Fact]
    public async Task RequestContextEnricher_NeverEmitsRawIpValueInOutput()
    {
        // Reinforces the negative regression with an interpolated-value
        // grep — the raw IP literal must NOT appear anywhere in the
        // rendered output, even if a future enricher added it under a
        // different key name.
        var stub = new StubRequestContext { ClientIp = "203.0.113.42" };
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));

        var rendered = string.Join("\n", handle.Sink.Events.Select(handle.Sink.Render));
        rendered.Should().NotContain("203.0.113.42");
    }

    [Fact]
    public async Task Serilog_AddPropertyIfAbsent_RequestPathPreBindingPrecedence_OnHttpPath()
    {
        // Under the composed pipeline, Serilog's request-completion
        // middleware binds RequestPath via ForContext BEFORE the
        // enricher's EnrichDiagnosticContext runs; AddPropertyIfAbsent
        // semantics drop the enricher's IRequestContext RequestPath
        // emission on the HTTP path — the local path WINS.
        const string propagated_path = "/originating/upstream/handler";
        var stub = new StubRequestContext { RequestPath = propagated_path };
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.RequestPath));
        requestEvent.Properties[nameof(IRequestContext.RequestPath)]
            .ToString().Should().Contain("/probe");
    }

    [Fact]
    public async Task RequestLogging_OnUnhandledThrow_RequestCompletionLineEmitted()
    {
        // The request-logging middleware MUST emit the completion line
        // even when the downstream endpoint throws. We swallow the
        // exception via a thin catch-all middleware (TestServer doesn't
        // install ASP.NET Core's developer-exception page handler by
        // default; without an IExceptionHandler the throw propagates).
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraConfigure: app => app.Use(async (ctx, next) =>
            {
                try
                {
                    await next();
                }
                catch
                {
                    ctx.Response.StatusCode = 500;
                }
            }));
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/throw", UriKind.Relative));

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey("StatusCode");
    }

    [Fact]
    public async Task UsernameLogOk_AppearsInOutputUnderComposedPipeline()
    {
        // Re-pin the user-locked "Username is LOG-OK" decision under the
        // COMPOSED pipeline. Adding [RedactData] to Username later would
        // silently strip it from logs; this test makes that change visible.
        var stub = new StubRequestContext { Username = "alice" };
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.Username));
        requestEvent.Properties[nameof(IRequestContext.Username)]
            .ToString().Should().Contain("alice");
    }

    [Fact]
    public async Task RequestLogging_OptionsBound_ServiceNameFromOTelEnvVar()
    {
        // The CompositeTestHostBuilder pre-populates OTEL_SERVICE_NAME =
        // "service-defaults-tests" so AddD2Logging's env-var binding
        // surfaces it on the bound D2LoggingOptions. The per-test sink
        // does NOT inherit the production-side ServiceName enricher (the
        // local logger is intentionally minimal so test assertions
        // aren't coupled to enricher composition); pin the binding via
        // the resolved options instance instead.
        await using var handle = await CompositeTestHostBuilder.BuildAsync();

        var resolved = handle.Host.Services
            .GetRequiredService<global::Microsoft.Extensions.Options
                .IOptions<DcsvIo.D2.Logging.D2LoggingOptions>>()
            .Value;
        resolved.ServiceName.Should().Be("service-defaults-tests");
    }

    [Fact]
    public async Task ActorChainPopulated_RenderedAsJsonArrayUnderComposedPipeline()
    {
        var actorChain = new[]
        {
            new ActorEntry(ActorKind.Service, "edge", ClientId: "edge"),
        };
        var stub = new StubRequestContext { ActorChain = actorChain };
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/probe", UriKind.Relative));

        var requestEvent = FindRequestEvent(handle.Sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.ActorChain));
        var rendered = string.Join("\n", handle.Sink.Events.Select(handle.Sink.Render));
        rendered.Should().Contain("edge");
    }

    private static LogEvent? FindRequestEvent(InMemorySink sink)
    {
        return sink.Events.FirstOrDefault(e =>
            e.Properties.TryGetValue("SourceContext", out var sc)
            && sc.ToString().Contains("Serilog.AspNetCore.RequestLoggingMiddleware"));
    }
}
