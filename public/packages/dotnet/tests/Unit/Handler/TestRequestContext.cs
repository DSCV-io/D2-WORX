// -----------------------------------------------------------------------
// <copyright file="TestRequestContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Collections.Generic;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;

/// <summary>
/// Hand-rolled <see cref="IRequestContext"/> for tests. All properties are
/// settable via init / object initializer; defaults match the production
/// MutableRequestContext (empty actor chain, empty audience, empty scopes).
/// </summary>
internal sealed class TestRequestContext : IRequestContext
{
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

    public string? ClientIp { get; init; }

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

    public RequestOrigin Origin { get; init; } = RequestOrigin.Unestablished;

    public string? ImmediateCaller { get; init; }

    public IReadOnlyList<CallPathEntry> CallPath { get; init; } = [];
}
