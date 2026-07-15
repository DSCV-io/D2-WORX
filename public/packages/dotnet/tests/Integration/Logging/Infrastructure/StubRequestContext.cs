// -----------------------------------------------------------------------
// <copyright file="StubRequestContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Logging.Infrastructure;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;

/// <summary>
/// Hand-rolled <see cref="IRequestContext"/> stub for integration tests of
/// the request-logging middleware + the <c>D2RequestContextEnricher</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every property defaults to its type's default (null for nullables) so each
/// test sets only what it needs via the object initializer.
/// </para>
/// <para>
/// Adding a property to <see cref="IRequestContext"/> requires updating this
/// stub — fail-to-compile surfaces forces a §3 PII review + an explicit add
/// to either <c>D2RequestContextEnricher</c> (LOG-OK) or
/// <c>RequestContextEnricherIntegrationTests</c> NOT-LOGGED contract. This
/// is a feature, not a bug.
/// </para>
/// </remarks>
internal sealed class StubRequestContext : IRequestContext
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

    // Infrastructure
    public string? EdgeNodeId { get; init; }

    // User Preferences
    public string? LocaleIetfBcp47Tag { get; init; }

    public string? TimezoneIanaName { get; init; }

    public string? CurrencyIso4217Code { get; init; }

    // Entitlements
    public string? OrgPlanTier { get; init; }

    public string? FeatureFlagsCsv { get; init; }

    // Fingerprints
    public string? SessionFingerprint { get; init; }

    public string? CurrentFingerprint { get; init; }

    public int? RiskScore { get; init; }

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
