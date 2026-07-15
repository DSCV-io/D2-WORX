// -----------------------------------------------------------------------
// <copyright file="D2RequestContextEnricher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging.Internal;

using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

/// <summary>
/// Enriches the Serilog request-completion log line with the
/// PII-reviewed LOG-OK projection of <see cref="IRequestContext"/> when the
/// per-request DI scope has one registered.
/// </summary>
/// <remarks>
/// <para>
/// Emits up to 42 fields when populated, organized into the following
/// clusters:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Tracing</b> (3): <c>TraceId</c>, <c>RequestId</c>,
///     <c>RequestPath</c>.
///   </description></item>
///   <item><description>
///     <b>Auth/Identity</b> (8): <c>IsAuthenticated</c>, <c>Subject</c>,
///     <c>UserId</c>, <c>Username</c>, <c>RequestedByClientId</c>,
///     <c>ImmediateCallerClientId</c>, <c>OriginatingClientId</c>,
///     <c>IsServiceIdentity</c>.
///   </description></item>
///   <item><description>
///     <b>Auth/Token+Trust</b> (5): <c>Audience</c>, <c>SessionId</c>,
///     <c>TokenIssuedAt</c>, <c>TokenExpiresAt</c>, <c>ActorChain</c>
///     (destructured).
///   </description></item>
///   <item><description>
///     <b>Auth/Org</b> (4): <c>OrgId</c>, <c>OrgName</c>, <c>OrgType</c>,
///     <c>OrgRole</c>.
///   </description></item>
///   <item><description>
///     <b>Auth/Impersonation</b> (8): <c>IsImpersonating</c>,
///     <c>ImpersonationKind</c>, <c>ImpersonatedBy</c>,
///     <c>ImpersonationSessionId</c>, <c>ImpersonatorOrgId</c>,
///     <c>ImpersonatorOrgName</c>, <c>ImpersonatorOrgType</c>,
///     <c>ImpersonatorOrgRole</c>.
///   </description></item>
///   <item><description>
///     <b>Scopes</b> (1): <c>Scopes</c> (destructured).
///   </description></item>
///   <item><description>
///     <b>Trust/Risk</b> (1): <c>RiskScore</c>.
///   </description></item>
///   <item><description>
///     <b>Fingerprints</b> (2): <c>SessionFingerprint</c>,
///     <c>CurrentFingerprint</c>.
///   </description></item>
///   <item><description>
///     <b>WhoIs/Geo</b> (3): <c>WhoIsHashId</c>,
///     <c>AdminLocationHashId</c>, <c>CountryIso31661Alpha2Code</c>.
///   </description></item>
///   <item><description>
///     <b>WhoIs/Network-Privacy</b> (4): <c>IsVpn</c>, <c>IsProxy</c>,
///     <c>IsTor</c>, <c>IsHosting</c>.
///   </description></item>
///   <item><description>
///     <b>WhoIs/ASN</b> (3): <c>Asn</c>, <c>AsnName</c>, <c>AsnType</c>.
///   </description></item>
/// </list>
/// <para>
/// See the per-lib README's "LOG-OK fields" section for the full per-field
/// table including types and rationale.
/// </para>
/// <para>
/// PII fields are NEVER emitted: <c>ClientIp</c>, <c>City</c>,
/// <c>SubdivisionIso31662Code</c>, <c>PostalCode</c>, <c>Latitude</c>,
/// <c>Longitude</c>, <c>Geohash</c>. Sub-country geographic precision
/// escalates only via <c>WhoIsHashId</c> lookups against the WhoIs store.
/// </para>
/// <para>
/// Each per-field <c>Set</c> call is gated by a null check on the field
/// value; null fields are not emitted at all (vs emitting a null structured
/// property). For the three collection fields (<c>Audience</c>,
/// <c>ActorChain</c>, <c>Scopes</c>), an empty-collection gate via
/// <c>Truthy()</c> applies — empty collections are not emitted to avoid
/// polluting logs with empty arrays on every request.
/// </para>
/// <para>
/// At pre-Edge-filler deployments this means the enricher is a structural
/// no-op — every value is null on every request and zero properties get
/// added to the log line.
/// </para>
/// <para>
/// <b>Precedence note</b>: behavior of the three Tracing-axis fields
/// differs by HOW the upstream value reaches the log event.
/// <see cref="IRequestContext.TraceId"/> reaches the diagnostic context via
/// the same <see cref="IDiagnosticContext"/> mechanism the request-logging
/// middleware uses, so this enricher (running LAST in the
/// <c>EnrichDiagnosticContext</c> callback) WINS via last-writer-wins on
/// the diag-ctx dictionary. <see cref="IRequestContext.RequestId"/> and
/// <see cref="IRequestContext.RequestPath"/> are pre-bound by Serilog's
/// own <c>ForContext</c> mechanism on the HTTP path BEFORE the callback
/// runs; once Serilog has populated those properties on the
/// <c>LogEvent</c>, subsequent <c>IDiagnosticContext.Set</c> calls are
/// silently dropped by Serilog's <c>AddPropertyIfAbsent</c> semantics.
/// The enricher emits all three for spec-contract completeness — on
/// non-HTTP transports without Serilog's pre-binding, every emission
/// surfaces. See the per-lib README's "Precedence note" section for the
/// full operator-facing description.
/// </para>
/// <para>
/// Degrades to a no-op when <see cref="IRequestContext"/> is not registered
/// in the per-request DI scope (the early-deployment reality where most
/// services don't yet have it wired). Never throws.
/// </para>
/// </remarks>
internal static class D2RequestContextEnricher
{
    /// <summary>
    /// Resolves <see cref="IRequestContext"/> from the current
    /// <see cref="HttpContext.RequestServices"/> scope and emits the LOG-OK
    /// fields onto <paramref name="diagnosticContext"/>. No-ops when the
    /// context is unregistered or every field is null.
    /// </summary>
    /// <param name="diagnosticContext">
    /// Serilog request-logging diagnostic context — receives one
    /// <see cref="IDiagnosticContext.Set(string, object?, bool)"/> call per
    /// non-null LOG-OK field.
    /// </param>
    /// <param name="httpContext">
    /// Current request's <see cref="HttpContext"/>. Must be non-null —
    /// guarded by the <c>UseSerilogRequestLogging</c> middleware that calls
    /// us, so the null path is defensive only.
    /// </param>
    internal static void Enrich(
        IDiagnosticContext diagnosticContext,
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(diagnosticContext);
        ArgumentNullException.ThrowIfNull(httpContext);

        var ctx = httpContext.RequestServices.GetService<IRequestContext>();
        if (ctx is null)
            return;

        // Tracing (3 fields) — TraceId / RequestPath OVERRIDE the
        // request-logging middleware's locally-set values when populated;
        // see the precedence note in the type-level remarks above.
        if (ctx.TraceId is { } traceId)
            diagnosticContext.Set(nameof(IRequestContext.TraceId), traceId);

        if (ctx.RequestId is { } requestId)
            diagnosticContext.Set(nameof(IRequestContext.RequestId), requestId);

        if (ctx.RequestPath is { } requestPath)
            diagnosticContext.Set(nameof(IRequestContext.RequestPath), requestPath);

        // Auth/Identity (8 fields).
        if (ctx.IsAuthenticated is { } isAuthenticated)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.IsAuthenticated),
                isAuthenticated);
        }

        if (ctx.Subject is { } subject)
            diagnosticContext.Set(nameof(IRequestContext.Subject), subject);

        if (ctx.UserId is { } userId)
            diagnosticContext.Set(nameof(IRequestContext.UserId), userId);

        if (ctx.Username is { } username)
            diagnosticContext.Set(nameof(IRequestContext.Username), username);

        if (ctx.RequestedByClientId is { } requestedByClientId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.RequestedByClientId),
                requestedByClientId);
        }

        if (ctx.ImmediateCallerClientId is { } immediateCallerClientId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImmediateCallerClientId),
                immediateCallerClientId);
        }

        if (ctx.OriginatingClientId is { } originatingClientId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.OriginatingClientId),
                originatingClientId);
        }

        if (ctx.IsServiceIdentity is { } isServiceIdentity)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.IsServiceIdentity),
                isServiceIdentity);
        }

        // Auth/Token+Trust (5 fields). Audience + ActorChain are non-null
        // collections per spec; gate on Truthy() to suppress empty arrays.
        if (ctx.Audience.Truthy())
        {
            diagnosticContext.Set(
                nameof(IRequestContext.Audience),
                ctx.Audience,
                destructureObjects: true);
        }

        if (ctx.SessionId is { } sessionId)
            diagnosticContext.Set(nameof(IRequestContext.SessionId), sessionId);

        if (ctx.TokenIssuedAt is { } tokenIssuedAt)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.TokenIssuedAt),
                tokenIssuedAt);
        }

        if (ctx.TokenExpiresAt is { } tokenExpiresAt)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.TokenExpiresAt),
                tokenExpiresAt);
        }

        if (ctx.ActorChain.Truthy())
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ActorChain),
                ctx.ActorChain,
                destructureObjects: true);
        }

        // Auth/Org (4 fields).
        if (ctx.OrgId is { } orgId)
            diagnosticContext.Set(nameof(IRequestContext.OrgId), orgId);

        if (ctx.OrgName is { } orgName)
            diagnosticContext.Set(nameof(IRequestContext.OrgName), orgName);

        if (ctx.OrgType is { } orgType)
            diagnosticContext.Set(nameof(IRequestContext.OrgType), orgType);

        if (ctx.OrgRole is { } orgRole)
            diagnosticContext.Set(nameof(IRequestContext.OrgRole), orgRole);

        // Auth/Impersonation (8 fields).
        if (ctx.IsImpersonating is { } isImpersonating)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.IsImpersonating),
                isImpersonating);
        }

        if (ctx.ImpersonationKind is { } impersonationKind)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonationKind),
                impersonationKind);
        }

        if (ctx.ImpersonatedBy is { } impersonatedBy)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatedBy),
                impersonatedBy);
        }

        if (ctx.ImpersonationSessionId is { } impersonationSessionId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonationSessionId),
                impersonationSessionId);
        }

        if (ctx.ImpersonatorOrgId is { } impersonatorOrgId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatorOrgId),
                impersonatorOrgId);
        }

        if (ctx.ImpersonatorOrgName is { } impersonatorOrgName)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatorOrgName),
                impersonatorOrgName);
        }

        if (ctx.ImpersonatorOrgType is { } impersonatorOrgType)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatorOrgType),
                impersonatorOrgType);
        }

        if (ctx.ImpersonatorOrgRole is { } impersonatorOrgRole)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.ImpersonatorOrgRole),
                impersonatorOrgRole);
        }

        // Scopes (1 field). Non-null set per spec; Truthy() gate suppresses
        // empty sets so we don't emit "Scopes":[] on every unauthenticated
        // request.
        if (ctx.Scopes.Truthy())
        {
            diagnosticContext.Set(
                nameof(IRequestContext.Scopes),
                ctx.Scopes,
                destructureObjects: true);
        }

        // Trust/Risk (1 field).
        if (ctx.RiskScore is { } riskScore)
            diagnosticContext.Set(nameof(IRequestContext.RiskScore), riskScore);

        // Fingerprints (2 fields).
        if (ctx.SessionFingerprint is { } sessionFingerprint)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.SessionFingerprint),
                sessionFingerprint);
        }

        if (ctx.CurrentFingerprint is { } currentFingerprint)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.CurrentFingerprint),
                currentFingerprint);
        }

        // WhoIs/Geo (3 fields).
        if (ctx.WhoIsHashId is { } whoIsHashId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.WhoIsHashId),
                whoIsHashId);
        }

        if (ctx.AdminLocationHashId is { } adminLocationHashId)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.AdminLocationHashId),
                adminLocationHashId);
        }

        if (ctx.CountryIso31661Alpha2Code is { } countryIso31661Alpha2Code)
        {
            diagnosticContext.Set(
                nameof(IRequestContext.CountryIso31661Alpha2Code),
                countryIso31661Alpha2Code);
        }

        // WhoIs/Network-Privacy (4 fields).
        if (ctx.IsVpn is { } isVpn)
            diagnosticContext.Set(nameof(IRequestContext.IsVpn), isVpn);

        if (ctx.IsProxy is { } isProxy)
            diagnosticContext.Set(nameof(IRequestContext.IsProxy), isProxy);

        if (ctx.IsTor is { } isTor)
            diagnosticContext.Set(nameof(IRequestContext.IsTor), isTor);

        if (ctx.IsHosting is { } isHosting)
            diagnosticContext.Set(nameof(IRequestContext.IsHosting), isHosting);

        // WhoIs/ASN (3 fields).
        if (ctx.Asn is { } asn)
            diagnosticContext.Set(nameof(IRequestContext.Asn), asn);

        if (ctx.AsnName is { } asnName)
            diagnosticContext.Set(nameof(IRequestContext.AsnName), asnName);

        if (ctx.AsnType is { } asnType)
            diagnosticContext.Set(nameof(IRequestContext.AsnType), asnType);
    }
}
