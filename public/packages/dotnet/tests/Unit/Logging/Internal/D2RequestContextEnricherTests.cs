// -----------------------------------------------------------------------
// <copyright file="D2RequestContextEnricherTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging.Internal;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Logging.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit;

/// <summary>
/// Unit tests for <see cref="D2RequestContextEnricher"/> — direct enricher
/// invocation without a full Serilog pipeline. Pipeline-level coverage lives
/// in <c>Integration.Logging.RequestContextEnricherIntegrationTests</c>.
/// </summary>
public sealed class D2RequestContextEnricherTests
{
    [Fact]
    public void Enrich_NullDiagnosticContext_Throws()
    {
        var act = () => D2RequestContextEnricher.Enrich(null!, new DefaultHttpContext());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Enrich_NullHttpContext_Throws()
    {
        var diag = new RecordingDiagnosticContext();

        var act = () => D2RequestContextEnricher.Enrich(diag, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Enrich_IRequestContextNotRegistered_NoOps()
    {
        var diag = new RecordingDiagnosticContext();
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().BeEmpty();
    }

    [Fact]
    public void Enrich_IRequestContextWithAllNullFields_NoFieldsEmitted()
    {
        var stub = new StubRequestContext();
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().BeEmpty();
    }

    [Fact]
    public void Enrich_AllLogOkFieldsPopulated_AllEmitted()
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

        var stub = new StubRequestContext
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
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().HaveCount(42);

        // Tracing
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.TraceId))
            .WhoseValue.Should().Be("trace-w3c-1");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.RequestId))
            .WhoseValue.Should().Be("req-1");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.RequestPath))
            .WhoseValue.Should().Be("/api/echo");

        // Auth/Identity
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.IsAuthenticated))
            .WhoseValue.Should().Be(true);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.Subject))
            .WhoseValue.Should().Be(userId.ToString());
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.UserId))
            .WhoseValue.Should().Be(userId);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.Username))
            .WhoseValue.Should().Be("alice");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.RequestedByClientId))
            .WhoseValue.Should().Be("edge");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ImmediateCallerClientId))
            .WhoseValue.Should().Be("edge");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.OriginatingClientId))
            .WhoseValue.Should().Be("edge");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.IsServiceIdentity))
            .WhoseValue.Should().Be(false);

        // Auth/Token+Trust
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.Audience))
            .WhoseValue.Should().BeSameAs(audience);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.SessionId))
            .WhoseValue.Should().Be(sessionId);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.TokenIssuedAt))
            .WhoseValue.Should().Be(iat);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.TokenExpiresAt))
            .WhoseValue.Should().Be(exp);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ActorChain))
            .WhoseValue.Should().BeSameAs(actorChain);

        // Auth/Org
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.OrgId))
            .WhoseValue.Should().Be(orgId);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.OrgName))
            .WhoseValue.Should().Be("Acme Corp");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.OrgType))
            .WhoseValue.Should().Be(OrgType.Customer);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.OrgRole))
            .WhoseValue.Should().Be(Role.Owner);

        // Auth/Impersonation
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.IsImpersonating))
            .WhoseValue.Should().Be(true);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ImpersonationKind))
            .WhoseValue.Should().Be(ImpersonationKind.Consent);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ImpersonatedBy))
            .WhoseValue.Should().Be(impersonatedBy);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ImpersonationSessionId))
            .WhoseValue.Should().Be(impersonationSessionId);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ImpersonatorOrgId))
            .WhoseValue.Should().Be(impersonatorOrgId);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ImpersonatorOrgName))
            .WhoseValue.Should().Be("Customer Support");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ImpersonatorOrgType))
            .WhoseValue.Should().Be(OrgType.Support);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ImpersonatorOrgRole))
            .WhoseValue.Should().Be(Role.Agent);

        // Scopes
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.Scopes))
            .WhoseValue.Should().BeSameAs(scopes);

        // Trust/Risk
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.RiskScore))
            .WhoseValue.Should().Be(25);

        // Fingerprints
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.SessionFingerprint))
            .WhoseValue.Should().Be("fp-session-1");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.CurrentFingerprint))
            .WhoseValue.Should().Be("fp-current-1");

        // WhoIs/Geo
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.WhoIsHashId))
            .WhoseValue.Should().Be("whois-hash-1");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.AdminLocationHashId))
            .WhoseValue.Should().Be("admin-loc-hash-1");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.CountryIso31661Alpha2Code))
            .WhoseValue.Should().Be("US");

        // WhoIs/Network-Privacy
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.IsVpn))
            .WhoseValue.Should().Be(false);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.IsProxy))
            .WhoseValue.Should().Be(false);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.IsTor))
            .WhoseValue.Should().Be(true);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.IsHosting))
            .WhoseValue.Should().Be(false);

        // WhoIs/ASN
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.Asn))
            .WhoseValue.Should().Be(7018);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.AsnName))
            .WhoseValue.Should().Be("AT&T");
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.AsnType))
            .WhoseValue.Should().Be("isp");
    }

    [Fact]
    public void Enrich_PartiallyPopulated_OnlyNonNullFieldsEmitted()
    {
        var stub = new StubRequestContext
        {
            CountryIso31661Alpha2Code = "US",
            IsVpn = true,
        };
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().HaveCount(2);
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.CountryIso31661Alpha2Code));
        diag.Recorded.Should().ContainKey(nameof(IRequestContext.IsVpn));
    }

    [Fact]
    public void Enrich_NeverEmitsPiiFields_EvenWhenPopulated()
    {
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
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.ClientIp));
        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.City));
        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.PostalCode));
        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.SubdivisionIso31662Code));
        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.Latitude));
        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.Longitude));
        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.Geohash));
    }

    [Fact]
    public void Enrich_ActorChainEmpty_ChainNotEmitted()
    {
        // ActorChain is non-nullable per spec; defaults to []. End-user-direct
        // tokens have an empty chain and we don't want to pollute logs with
        // "ActorChain":[] on every direct request.
        var stub = new StubRequestContext
        {
            // Touch one field so the enricher emits SOMETHING (otherwise the
            // partial-populated path emits zero items, which doesn't isolate
            // the empty-collection-gate behavior).
            CountryIso31661Alpha2Code = "US",
        };
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.ActorChain));
    }

    [Fact]
    public void Enrich_ActorChainPopulated_DestructuredAsArray()
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
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().ContainKey(nameof(IRequestContext.ActorChain))
            .WhoseValue.Should().BeSameAs(actorChain);
    }

    [Fact]
    public void Enrich_ScopesEmpty_NotEmitted()
    {
        var stub = new StubRequestContext { CountryIso31661Alpha2Code = "US" };
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.Scopes));
    }

    [Fact]
    public void Enrich_ScopesPopulated_DestructuredAsArray()
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal)
        {
            "auth.user.read",
            "auth.user.write",
        };
        var stub = new StubRequestContext { Scopes = scopes };
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().ContainKey(nameof(IRequestContext.Scopes))
            .WhoseValue.Should().BeSameAs(scopes);
    }

    [Fact]
    public void Enrich_AudienceEmpty_NotEmitted()
    {
        var stub = new StubRequestContext { CountryIso31661Alpha2Code = "US" };
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().NotContainKey(nameof(IRequestContext.Audience));
    }

    [Fact]
    public void Enrich_AudiencePopulated_DestructuredAsArray()
    {
        var audience = new[] { "edge", "audit" };
        var stub = new StubRequestContext { Audience = audience };
        var diag = new RecordingDiagnosticContext();
        var http = BuildHttpContextWith(stub);

        D2RequestContextEnricher.Enrich(diag, http);

        diag.Recorded.Should().ContainKey(nameof(IRequestContext.Audience))
            .WhoseValue.Should().BeSameAs(audience);
    }

    private static DefaultHttpContext BuildHttpContextWith(IRequestContext ctx)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
    }

    /// <summary>
    /// Recording <see cref="IDiagnosticContext"/> implementation — captures
    /// every <c>Set</c> call so tests can introspect what the enricher
    /// emitted.
    /// </summary>
    private sealed class RecordingDiagnosticContext : IDiagnosticContext
    {
        public Dictionary<string, object?> Recorded { get; } = new(StringComparer.Ordinal);

        public void Set(string propertyName, object? value, bool destructureObjects = false)
        {
            Recorded[propertyName] = value;
        }

        public void SetException(Exception? exception)
        {
            // Not exercised by these tests.
        }
    }

    /// <summary>
    /// Hand-rolled <see cref="IRequestContext"/> stub for unit-level enricher
    /// coverage. Every property defaults to its type's default (null for
    /// nullables) so each test sets only what it needs.
    /// </summary>
    /// <remarks>
    /// Property setters that no test exercises are marked unused by analyzers
    /// — that's by design. The stub MUST cover the entire interface surface
    /// so adding a new IRequestContext property surfaces as a fail-to-compile
    /// here.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Local",
        Justification = "Stub mirrors the full IRequestContext surface; "
            + "not every test sets every prop.")]
    private sealed class StubRequestContext : IRequestContext
    {
        // IRequestContext (Network)
        public string? ClientIp { get; init; }

        // Tracing
        public string? TraceId { get; init; }

        public string? RequestId { get; init; }

        public string? RequestPath { get; init; }

        public string? HttpMethod { get; init; }

        public DateTimeOffset? RequestStartedAt { get; init; }

        public string? IdempotencyKey { get; init; }

        // Fingerprints
        public string? SessionFingerprint { get; init; }

        public string? CurrentFingerprint { get; init; }

        public int? RiskScore { get; init; }

        // Infrastructure
        public string? EdgeNodeId { get; init; }

        // User Preferences
        public string? LocaleIetfBcp47Tag { get; init; }

        public string? TimezoneIanaName { get; init; }

        public string? CurrencyIso4217Code { get; init; }

        // Entitlements
        public string? OrgPlanTier { get; init; }

        public string? FeatureFlagsCsv { get; init; }

        // WhoIs — Admin Location
        public string? WhoIsHashId { get; init; }

        public string? AdminLocationHashId { get; init; }

        public string? City { get; init; }

        public string? SubdivisionIso31662Code { get; init; }

        public string? CountryIso31661Alpha2Code { get; init; }

        public string? PostalCode { get; init; }

        // WhoIs — Coordinates
        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public string? Geohash { get; init; }

        // WhoIs — Network Privacy
        public bool? IsVpn { get; init; }

        public bool? IsProxy { get; init; }

        public bool? IsTor { get; init; }

        public bool? IsHosting { get; init; }

        // WhoIs — ASN
        public int? Asn { get; init; }

        public string? AsnName { get; init; }

        public string? AsnType { get; init; }

        // Establishment
        public RequestOrigin Origin { get; init; } = RequestOrigin.Unestablished;

        public string? ImmediateCaller { get; init; }

        public IReadOnlyList<CallPathEntry> CallPath { get; init; } = [];

        // IAuthContext — Token + Trust
        public bool? IsAuthenticated { get; init; }

        public IReadOnlyList<string> Audience { get; init; } = [];

        public Guid? SessionId { get; init; }

        public DateTimeOffset? TokenIssuedAt { get; init; }

        public DateTimeOffset? TokenExpiresAt { get; init; }

        public IReadOnlyList<ActorEntry> ActorChain { get; init; } = [];

        public string? AuthMethod { get; init; }

        public DateTimeOffset? LastStepUpAt { get; init; }

        // IAuthContext — Identity
        public string? Subject { get; init; }

        public Guid? UserId { get; init; }

        public string? Username { get; init; }

        public string? RequestedByClientId { get; init; }

        public string? ImmediateCallerClientId { get; init; }

        public string? OriginatingClientId { get; init; }

        public bool? IsServiceIdentity { get; init; }

        // IAuthContext — Organization
        public Guid? OrgId { get; init; }

        public string? OrgName { get; init; }

        public OrgType? OrgType { get; init; }

        public Role? OrgRole { get; init; }

        // IAuthContext — Impersonation
        public bool? IsImpersonating { get; init; }

        public ImpersonationKind? ImpersonationKind { get; init; }

        public Guid? ImpersonatedBy { get; init; }

        public Guid? ImpersonationSessionId { get; init; }

        public Guid? ImpersonatorOrgId { get; init; }

        public string? ImpersonatorOrgName { get; init; }

        public OrgType? ImpersonatorOrgType { get; init; }

        public Role? ImpersonatorOrgRole { get; init; }

        // IAuthContext — Scopes
        public IReadOnlySet<string> Scopes { get; init; } = new HashSet<string>();
    }
}
