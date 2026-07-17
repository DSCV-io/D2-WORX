// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtEnrichmentExclusionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging.Internal;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.AuthContext.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Logging.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit;

/// <summary>
/// Pins the structural-isolation invariant for the forwarded JWT: the live
/// bearer credential is held in a dedicated request-scoped holder
/// (<see cref="IForwardedJwtAccessor"/>), NEVER as a field of the request
/// context — so the broadly-projected request-context log/telemetry enricher
/// (<see cref="D2RequestContextEnricher"/>) can never reach it. Two complementary
/// proofs: a contract-level reflection assertion (no credential field on the
/// context interfaces) and a projection-behavior assertion (the enricher emits
/// nothing credential-shaped even with a populated holder in the same scope).
/// </summary>
public sealed class ForwardedJwtEnrichmentExclusionTests
{
    // Name fragments that would betray a forwarded-credential field sneaking
    // onto the context. Case-insensitive substring match.
    private static readonly string[] sr_credentialNameFragments =
    [
        "ForwardedJwt",
        "RawToken",
        "BearerToken",
        "Bearer",
        "Jwt",
        "Authorization",
    ];

    [Fact]
    public void IRequestContext_HasNoForwardedJwtTypedProperty()
    {
        var offending = typeof(IRequestContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => IsForwardedJwtType(p.PropertyType))
            .Select(p => p.Name)
            .ToList();

        offending.Should().BeEmpty(
            "the forwarded JWT must live in IForwardedJwtAccessor, never as a "
            + "property of IRequestContext (which the enricher projects).");
    }

    [Fact]
    public void IAuthContext_HasNoForwardedJwtTypedProperty()
    {
        var offending = typeof(IAuthContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => IsForwardedJwtType(p.PropertyType))
            .Select(p => p.Name)
            .ToList();

        offending.Should().BeEmpty();
    }

    [Fact]
    public void ContextInterfaces_HaveNoCredentialNamedProperty()
    {
        // Defends the complementary axis: even a string-typed property named
        // like a credential (e.g. "RawToken") would be a regression. Pins both
        // the context interface and its base.
        var offending = typeof(IRequestContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(IAuthContext).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => sr_credentialNameFragments.Any(frag =>
                p.Name.Contains(frag, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .Distinct()
            .ToList();

        offending.Should().BeEmpty(
            "no IRequestContext / IAuthContext property may be named like a "
            + "forwarded credential — the moment one is added, this fails.");
    }

    [Fact]
    public void Enrich_WithPopulatedHolderInScope_RecordsNoCredentialFieldOrBytes()
    {
        // Projection-behavior proof (defense-in-depth): even with a fully
        // populated IForwardedJwtAccessor registered in the SAME request scope
        // the enricher reaches via httpContext.RequestServices, the enricher
        // emits nothing credential-shaped — it has no path to the holder.
        const string known_jwt = "eyJhbGciOiJSUzI1NiJ9.PAYLOAD_SENTINEL.SIG_SENTINEL";

        var holder = new MutableForwardedJwtAccessor();
        holder.Capture(known_jwt);

        var stub = new StubRequestContext { IsAuthenticated = true, Subject = "sub-1" };
        var services = new ServiceCollection();
        services.AddSingleton<IRequestContext>(stub);
        services.AddSingleton<IForwardedJwtAccessor>(holder);
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var diag = new RecordingDiagnosticContext();

        D2RequestContextEnricher.Enrich(diag, http);

        // No recorded field name resembles a credential.
        diag.Recorded.Keys.Should().NotContain(k =>
            sr_credentialNameFragments.Any(frag =>
                k.Contains(frag, StringComparison.OrdinalIgnoreCase)));

        // And the raw bytes appear in no recorded VALUE.
        var renderedValues = string.Join(
            "\n", diag.Recorded.Values.Select(v => v?.ToString() ?? string.Empty));
        renderedValues.Should().NotContain("PAYLOAD_SENTINEL");
        renderedValues.Should().NotContain("SIG_SENTINEL");
        renderedValues.Should().NotContain(known_jwt);
    }

    private static bool IsForwardedJwtType(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        return underlying == typeof(ForwardedJwt);
    }

    /// <summary>
    /// Recording <see cref="IDiagnosticContext"/> — captures every <c>Set</c>
    /// call so the test can introspect the projected field set + values.
    /// </summary>
    private sealed class RecordingDiagnosticContext : IDiagnosticContext
    {
        public Dictionary<string, object?> Recorded { get; } = new(StringComparer.Ordinal);

        public void Set(string propertyName, object? value, bool destructureObjects = false)
            => Recorded[propertyName] = value;

        public void SetException(Exception? exception)
        {
            // Not exercised by these tests.
        }
    }

    /// <summary>
    /// Minimal <see cref="IRequestContext"/> stub — only the two fields these
    /// tests touch are set; everything else defaults to null. Mirrors the
    /// existing enricher-test stub shape.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Local",
        Justification = "Stub mirrors the full IRequestContext surface; "
            + "not every test sets every prop.")]
    private sealed class StubRequestContext : IRequestContext
    {
        public string? ClientIp { get; init; }

        public string? TraceId { get; init; }

        public string? RequestId { get; init; }

        public string? RequestPath { get; init; }

        public string? HttpMethod { get; init; }

        public DateTimeOffset? RequestStartedAt { get; init; }

        public string? IdempotencyKey { get; init; }

        public string? EdgeNodeId { get; init; }

        public string? LocaleIetfBcp47Tag { get; init; }

        public string? TimezoneIanaName { get; init; }

        public string? CurrencyIso4217Code { get; init; }

        public string? OrgPlanTier { get; init; }

        public string? FeatureFlagsCsv { get; init; }

        public string? SessionFingerprint { get; init; }

        public string? CurrentFingerprint { get; init; }

        public int? RiskScore { get; init; }

        public string? WhoIsHashId { get; init; }

        public string? AdminLocationHashId { get; init; }

        public string? City { get; init; }

        public string? SubdivisionIso31662Code { get; init; }

        public string? CountryIso31661Alpha2Code { get; init; }

        public string? PostalCode { get; init; }

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public string? Geohash { get; init; }

        public bool? IsVpn { get; init; }

        public bool? IsProxy { get; init; }

        public bool? IsTor { get; init; }

        public bool? IsHosting { get; init; }

        public int? Asn { get; init; }

        public string? AsnName { get; init; }

        public string? AsnType { get; init; }

        public bool? IsAuthenticated { get; init; }

        public IReadOnlyList<string> Audience { get; init; } = [];

        public Guid? SessionId { get; init; }

        public DateTimeOffset? TokenIssuedAt { get; init; }

        public DateTimeOffset? TokenExpiresAt { get; init; }

        public IReadOnlyList<ActorEntry> ActorChain { get; init; } = [];

        public string? AuthMethod { get; init; }

        public DateTimeOffset? LastStepUpAt { get; init; }

        public string? Subject { get; init; }

        public Guid? UserId { get; init; }

        public string? Username { get; init; }

        public string? RequestedByClientId { get; init; }

        public string? ImmediateCallerClientId { get; init; }

        public string? OriginatingClientId { get; init; }

        public bool? IsServiceIdentity { get; init; }

        public Guid? OrgId { get; init; }

        public string? OrgName { get; init; }

        public OrgType? OrgType { get; init; }

        public Role? OrgRole { get; init; }

        public bool? IsImpersonating { get; init; }

        public ImpersonationKind? ImpersonationKind { get; init; }

        public Guid? ImpersonatedBy { get; init; }

        public Guid? ImpersonationSessionId { get; init; }

        public Guid? ImpersonatorOrgId { get; init; }

        public string? ImpersonatorOrgName { get; init; }

        public OrgType? ImpersonatorOrgType { get; init; }

        public Role? ImpersonatorOrgRole { get; init; }

        public IReadOnlySet<string> Scopes { get; init; } = new HashSet<string>();

        public RequestOrigin Origin { get; init; } = RequestOrigin.Unestablished;

        public string? ImmediateCaller { get; init; }

        public IReadOnlyList<CallPathEntry> CallPath { get; init; } = [];
    }
}
