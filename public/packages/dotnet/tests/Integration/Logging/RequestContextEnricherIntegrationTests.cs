// -----------------------------------------------------------------------
// <copyright file="RequestContextEnricherIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Logging;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Tests.Integration.Logging.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Collection("LogLoggerStaticState")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests.")]
public sealed class RequestContextEnricherIntegrationTests
{
    [Fact]
    public async Task PopulatedContext_AllLogOkFieldsEmittedOnRequestEvent()
    {
        var stub = BuildFullyPopulatedStub();
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();

        // Tracing
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.TraceId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.RequestId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.RequestPath));

        // Auth/Identity
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.IsAuthenticated));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.Subject));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.UserId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.Username));
        requestEvent.Properties.Keys.Should().Contain(
            nameof(IRequestContext.RequestedByClientId));
        requestEvent.Properties.Keys.Should().Contain(
            nameof(IRequestContext.ImmediateCallerClientId));
        requestEvent.Properties.Keys.Should().Contain(
            nameof(IRequestContext.OriginatingClientId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.IsServiceIdentity));

        // Auth/Token+Trust
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.Audience));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.SessionId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.TokenIssuedAt));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.TokenExpiresAt));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.ActorChain));

        // Auth/Org
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.OrgId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.OrgName));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.OrgType));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.OrgRole));

        // Auth/Impersonation
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.IsImpersonating));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.ImpersonationKind));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.ImpersonatedBy));
        requestEvent.Properties.Keys.Should().Contain(
            nameof(IRequestContext.ImpersonationSessionId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.ImpersonatorOrgId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.ImpersonatorOrgName));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.ImpersonatorOrgType));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.ImpersonatorOrgRole));

        // Scopes
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.Scopes));

        // Trust/Risk
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.RiskScore));

        // Fingerprints
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.SessionFingerprint));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.CurrentFingerprint));

        // WhoIs/Geo
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.WhoIsHashId));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.AdminLocationHashId));
        requestEvent.Properties.Keys.Should().Contain(
            nameof(IRequestContext.CountryIso31661Alpha2Code));

        // WhoIs/Network-Privacy
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.IsVpn));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.IsProxy));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.IsTor));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.IsHosting));

        // WhoIs/ASN
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.Asn));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.AsnName));
        requestEvent.Properties.Keys.Should().Contain(nameof(IRequestContext.AsnType));
    }

    [Fact]
    public async Task NullContextFields_NoneOfTheLogOkKeysEmitted()
    {
        var stub = new StubRequestContext();
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();

        // None of the LOG-OK keys EXCLUSIVELY emitted by the enricher should
        // appear when every spec field is null / empty (per-field null-gates
        // + empty-collection-gates suppress every emission).
        //
        // Excluded from this assertion because they're emitted by Serilog
        // / the middleware INDEPENDENTLY of the enricher (the enricher's
        // contribution is silently overridden — see precedence notes in the
        // type-level remarks of D2RequestContextEnricher):
        //   - TraceId    (middleware sets from HttpContext.TraceIdentifier
        //                 via diag-ctx; enricher's overrides via same diag-ctx
        //                 work, but a null IRequestContext.TraceId leaves the
        //                 middleware's value intact)
        //   - RequestId  (Serilog 9.x request-logging middleware emits
        //                 RequestId from its own ForContext binding —
        //                 enricher's emission via diag-ctx is silently
        //                 dropped by Serilog's AddPropertyIfAbsent semantics)
        //   - RequestPath (Serilog's request-completion message template
        //                 binds RequestPath from HttpContext.Request.Path
        //                 via ForContext — same AddPropertyIfAbsent
        //                 limitation applies)
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.IsAuthenticated));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.Subject));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.UserId));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.Username));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.RequestedByClientId));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.ImmediateCallerClientId));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.OriginatingClientId));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.IsServiceIdentity));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.Audience));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.SessionId));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.TokenIssuedAt));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.TokenExpiresAt));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.ActorChain));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.OrgId));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.OrgName));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.OrgType));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.OrgRole));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.IsImpersonating));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.ImpersonationKind));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.ImpersonatedBy));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.ImpersonationSessionId));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.ImpersonatorOrgId));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.ImpersonatorOrgName));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.ImpersonatorOrgType));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.ImpersonatorOrgRole));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.Scopes));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.RiskScore));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.SessionFingerprint));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.CurrentFingerprint));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.WhoIsHashId));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.AdminLocationHashId));
        requestEvent.Properties.Keys.Should().NotContain(
            nameof(IRequestContext.CountryIso31661Alpha2Code));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.IsVpn));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.IsProxy));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.IsTor));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.IsHosting));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.Asn));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.AsnName));
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.AsnType));
    }

    [Fact]
    public async Task NoIRequestContextRegistered_GracefulNoOp_StaticFieldsStillEmitted()
    {
        // Pre-Edge-filler reality: most services don't yet have IRequestContext
        // wired. The enricher must degrade silently — no throw, no crash.
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync();
        using var hostScope = host;
        var client = host.GetTestClient();

        var act = async () => await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        await act.Should().NotThrowAsync();
        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Keys.Should().Contain("RequestScheme");
        requestEvent.Properties.Keys.Should().Contain("UserAgent");
    }

    [Fact]
    public async Task PiiFields_NeverEmittedEvenWhenPopulated()
    {
        // Pin the conservative-by-default §3 PII contract — every NOT-LOGGED
        // field populated, not one of them shows up in the JSON output.
        // Adding a new PII field to IRequestContext WITHOUT adding it to
        // either the enricher's LOG-OK set or this contract requires this
        // test to be updated, which forces the §3 review.
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
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var rendered = string.Join("\n", sink.Events.Select(sink.Render));
        rendered.Should().NotContain($"\"{nameof(IRequestContext.ClientIp)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.City)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.PostalCode)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.SubdivisionIso31662Code)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.Latitude)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.Longitude)}\"");
        rendered.Should().NotContain($"\"{nameof(IRequestContext.Geohash)}\"");
    }

    [Fact]
    public async Task LocalTraceIdOverridden_WhenIRequestContextTraceIdPopulated()
    {
        // Pins precedence: when IRequestContext.TraceId is populated, the
        // enricher's last-writer-wins emission OVERRIDES the middleware's
        // local TraceId (set from HttpContext.TraceIdentifier earlier in the
        // EnrichDiagnosticContext callback).
        const string w3c_trace_id = "w3c-distributed-trace-id-1";
        var stub = new StubRequestContext { TraceId = w3c_trace_id };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.TraceId));
        requestEvent.Properties[nameof(IRequestContext.TraceId)]
            .ToString().Should().Contain(w3c_trace_id);
    }

    [Fact]
    public async Task LocalTraceIdSurvives_WhenIRequestContextTraceIdNull()
    {
        // Graceful degradation pin: when IRequestContext is registered but
        // its TraceId is null, the enricher does NOT override the
        // middleware's locally-set TraceId — the local value survives intact.
        // Current reality is that IRequestContext.TraceId is null on every
        // request; this test pins the no-op outcome.
        var stub = new StubRequestContext();
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();

        // The middleware ALWAYS emits TraceId from HttpContext.TraceIdentifier
        // — so the key must be present, and the value must be non-empty
        // (HttpContext.TraceIdentifier is a non-null synthetic identifier).
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.TraceId));
        var traceIdValue = requestEvent.Properties[nameof(IRequestContext.TraceId)].ToString();
        traceIdValue.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LocalRequestPath_SurvivesEnricherEmission_OnHttpPath()
    {
        // Pins the OBSERVED precedence on the HTTP path: Serilog's
        // request-completion message template binds RequestPath from
        // HttpContext.Request.Path via ForContext BEFORE the enricher's
        // EnrichDiagnosticContext callback runs. Once Serilog's LogEvent
        // property is set, IDiagnosticContext.Set("RequestPath", ...) is
        // silently dropped by AddPropertyIfAbsent semantics — so the
        // enricher's IRequestContext.RequestPath emission is LOST on the
        // HTTP path.
        //
        // The emission is preserved in the enricher's contract anyway —
        // when this enricher is reused on transports where Serilog does
        // NOT pre-bind RequestPath (e.g. AMQP message-handling pipelines
        // wired through a custom diag-ctx integration), the IRequestContext
        // value will appear. The HTTP-path limitation is documented in the
        // README's "Precedence note" section.
        const string propagated_path = "/originating/upstream/handler";
        var stub = new StubRequestContext { RequestPath = propagated_path };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.RequestPath));

        // The local path WINS on HTTP — IRequestContext.RequestPath is
        // dropped by Serilog's property-merge semantics.
        requestEvent.Properties[nameof(IRequestContext.RequestPath)]
            .ToString().Should().Contain("/api/echo");
    }

    [Fact]
    public async Task ActorChainPopulated_RenderedAsJsonArrayWithEntries()
    {
        var actorChain = new[]
        {
            new ActorEntry(ActorKind.Service, "edge", ClientId: "edge"),
            new ActorEntry(
                ActorKind.Impersonation,
                Subject: "00000000-0000-0000-0000-0000000000aa",
                ImpersonationKind: ImpersonationKind.Consent),
        };
        var stub = new StubRequestContext { ActorChain = actorChain };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.ActorChain));

        // The destructured ActorChain renders into the JSON output. We assert
        // contains-shape-pattern rather than exact JSON because Serilog's
        // structure-value formatting is implementation-detail; what matters
        // is the chain is non-empty and includes the ActorEntry shape.
        var rendered = string.Join("\n", sink.Events.Select(sink.Render));
        rendered.Should().Contain($"\"{nameof(IRequestContext.ActorChain)}\"");
        rendered.Should().Contain("edge");
    }

    [Fact]
    public async Task ActorChainEmpty_NotPresentInRenderedOutput()
    {
        // ActorChain defaults to []; the empty-collection gate must suppress
        // it so end-user-direct requests don't pollute logs with
        // "ActorChain":[] on every line.
        var stub = new StubRequestContext { CountryIso31661Alpha2Code = "US" };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.ActorChain));
    }

    [Fact]
    public async Task ScopesPopulated_RenderedAsJsonArray()
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal)
        {
            "auth.user.read",
            "auth.user.write",
        };
        var stub = new StubRequestContext { Scopes = scopes };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.Scopes));

        var rendered = string.Join("\n", sink.Events.Select(sink.Render));
        rendered.Should().Contain($"\"{nameof(IRequestContext.Scopes)}\"");
        rendered.Should().Contain("auth.user.read");
    }

    [Fact]
    public async Task ScopesEmpty_NotPresentInRenderedOutput()
    {
        var stub = new StubRequestContext { CountryIso31661Alpha2Code = "US" };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.Scopes));
    }

    [Fact]
    public async Task AudiencePopulated_RenderedAsJsonArray()
    {
        var audience = new[] { "edge", "audit" };
        var stub = new StubRequestContext { Audience = audience };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.Audience));

        var rendered = string.Join("\n", sink.Events.Select(sink.Render));
        rendered.Should().Contain($"\"{nameof(IRequestContext.Audience)}\"");
        rendered.Should().Contain("audit");
    }

    [Fact]
    public async Task AudienceEmpty_NotPresentInRenderedOutput()
    {
        var stub = new StubRequestContext { CountryIso31661Alpha2Code = "US" };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Keys.Should().NotContain(nameof(IRequestContext.Audience));
    }

    [Fact]
    public async Task UsernameLogOk_AppearsInOutput()
    {
        // Pins the user-locked decision: Username is LOG-OK (operator
        // debug-ability outweighs the marginal privacy delta from a user
        // CHOOSING email-shaped identifiers — see the README's per-cluster
        // rationale). Adding a [RedactData] to Username later would silently
        // strip it from logs; this test makes that change visible.
        var stub = new StubRequestContext { Username = "alice" };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.Username));
        requestEvent.Properties[nameof(IRequestContext.Username)]
            .ToString().Should().Contain("alice");
    }

    [Fact]
    public async Task OrgNameLogOk_AppearsInOutput()
    {
        // Pins the user-locked decision: OrgName is LOG-OK (internal logs
        // behind auth; no incremental disclosure beyond what operators can
        // already query). Same rationale applies to ImpersonatorOrgName.
        var stub = new StubRequestContext { OrgName = "Acme Corp" };
        var (host, sink) = await LoggingTestHostBuilder.BuildAsync(
            extraServices: services =>
                services.AddScoped<IRequestContext>(_ => stub));
        using var hostScope = host;
        var client = host.GetTestClient();

        await client.GetAsync(new Uri("/api/echo", UriKind.Relative));

        var requestEvent = FindRequestEvent(sink);
        requestEvent.Should().NotBeNull();
        requestEvent.Properties.Should().ContainKey(nameof(IRequestContext.OrgName));
        requestEvent.Properties[nameof(IRequestContext.OrgName)]
            .ToString().Should().Contain("Acme Corp");
    }

    private static Serilog.Events.LogEvent? FindRequestEvent(InMemorySink sink)
    {
        return sink.Events.FirstOrDefault(e =>
            e.Properties.TryGetValue("SourceContext", out var sc)
            && sc.ToString().Contains("Serilog.AspNetCore.RequestLoggingMiddleware"));
    }

    private static StubRequestContext BuildFullyPopulatedStub()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var orgId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var impersonatedBy = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var impersonationSessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var impersonatorOrgId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var iat = DateTimeOffset.Parse(
            "2026-05-12T10:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture);
        var exp = DateTimeOffset.Parse(
            "2026-05-12T10:15:00Z",
            System.Globalization.CultureInfo.InvariantCulture);
        var actorChain = new[]
        {
            new ActorEntry(ActorKind.Service, "edge", ClientId: "edge"),
        };
        var scopes = new HashSet<string>(StringComparer.Ordinal) { "auth.user.read" };
        var audience = new[] { "edge" };

        return new StubRequestContext
        {
            // Tracing
            TraceId = "trace-w3c-1",
            RequestId = "req-1",
            RequestPath = "/api/echo",

            // Auth/Identity
            IsAuthenticated = true,
            Subject = userId.ToString(),
            UserId = userId,
            Username = "alice",
            RequestedByClientId = "edge",
            ImmediateCallerClientId = "edge",
            OriginatingClientId = "edge",
            IsServiceIdentity = false,

            // Auth/Token+Trust
            Audience = audience,
            SessionId = sessionId,
            TokenIssuedAt = iat,
            TokenExpiresAt = exp,
            ActorChain = actorChain,

            // Auth/Org
            OrgId = orgId,
            OrgName = "Acme Corp",
            OrgType = OrgType.Customer,
            OrgRole = Role.Owner,

            // Auth/Impersonation
            IsImpersonating = true,
            ImpersonationKind = ImpersonationKind.Consent,
            ImpersonatedBy = impersonatedBy,
            ImpersonationSessionId = impersonationSessionId,
            ImpersonatorOrgId = impersonatorOrgId,
            ImpersonatorOrgName = "Customer Support",
            ImpersonatorOrgType = OrgType.Support,
            ImpersonatorOrgRole = Role.Agent,

            // Scopes
            Scopes = scopes,

            // Trust/Risk
            RiskScore = 25,

            // Fingerprints
            SessionFingerprint = "fp-session-1",
            CurrentFingerprint = "fp-current-1",

            // WhoIs/Geo
            WhoIsHashId = "whois-hash-1",
            AdminLocationHashId = "admin-loc-hash-1",
            CountryIso31661Alpha2Code = "US",

            // WhoIs/Network-Privacy
            IsVpn = false,
            IsProxy = false,
            IsTor = true,
            IsHosting = false,

            // WhoIs/ASN
            Asn = 7018,
            AsnName = "AT&T",
            AsnType = "isp",
        };
    }
}
