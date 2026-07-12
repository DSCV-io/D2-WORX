// -----------------------------------------------------------------------
// <copyright file="MethodScopeMetadataResolver.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Endpoints;

using D2.Shared.Auth.Abstractions;
using global::Grpc.Core;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Shared resolution of gRPC method scope / harmless metadata and the
/// per-call <see cref="HttpContext"/> used by server interceptors.
/// </summary>
/// <remarks>
/// Single last-wins attribute walk + fluent-over-attribute precedence.
/// Used by both <c>JwtAuthInterceptor</c> and
/// <c>RequestOriginUnestablishedDenyInterceptor</c> so the two paths cannot
/// drift.
/// </remarks>
internal static class MethodScopeMetadataResolver
{
    private const string _HTTP_CONTEXT_USER_STATE_KEY = "__HttpContext__";

    /// <summary>
    /// Resolves the per-call ASP.NET <see cref="HttpContext"/> for a gRPC
    /// server call (canonical feature cast, with legacy UserState fallback).
    /// </summary>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>The http context, or <see langword="null"/> when unavailable.</returns>
    public static HttpContext? TryResolveHttpContext(ServerCallContext context)
    {
        // gRPC-on-AspNetCore: GetHttpContext() casts to IServerCallContextFeature
        // (production HttpContextServerCallContext). Current Grpc.AspNetCore.Server
        // no longer populates UserState["__HttpContext__"]; try/catch covers
        // hand-rolled test ServerCallContext subtypes that lack the feature.
        try
        {
            return context.GetHttpContext();
        }
        catch (InvalidOperationException)
        {
            if (context.UserState.TryGetValue(_HTTP_CONTEXT_USER_STATE_KEY, out var raw)
                && raw is HttpContext httpContext)
            {
                return httpContext;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves fluent or attribute-declared <see cref="MethodScopeMetadata"/>
    /// for the matched gRPC method (fluent wins; else last-wins attributes).
    /// </summary>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>
    /// The method metadata, or <see langword="null"/> when none is declared
    /// (deny-by-default / any-authenticated caller for JWT; non-harmless product
    /// path for origin deny).
    /// </returns>
    public static MethodScopeMetadata? TryResolve(ServerCallContext context)
    {
        var httpContext = TryResolveHttpContext(context);
        var endpoint = httpContext?.GetEndpoint();

        if (endpoint is null)
            return null;

        // Fluent path takes precedence over attribute path (deterministic
        // precedence: fluent > attribute > no-metadata).
        var fluent = endpoint.Metadata.GetMetadata<MethodScopeMetadata>();

        if (fluent is not null)
            return fluent;

        return ResolveFromAttributes(endpoint);
    }

    private static MethodScopeMetadata? ResolveFromAttributes(Endpoint endpoint)
    {
        // Walk the full metadata collection once, tracking the last index at
        // which each of the three attribute types appears. The one with the
        // highest index is the effective declaration (last-declared-wins, which
        // ASP.NET metadata ordering turns into method-level-over-class-level).
        var lastHarmlessIdx = -1;
        var lastAnyIdx = -1;
        var lastAllIdx = -1;
        D2RequireAnyScopeAttribute? lastAnyAttr = null;
        D2RequireAllScopesAttribute? lastAllAttr = null;

        var index = 0;

        foreach (var item in endpoint.Metadata)
        {
            if (item is D2HarmlessEndpointAttribute)
            {
                lastHarmlessIdx = index;
            }
            else if (item is D2RequireAnyScopeAttribute anyAttr)
            {
                lastAnyIdx = index;
                lastAnyAttr = anyAttr;
            }
            else if (item is D2RequireAllScopesAttribute allAttr)
            {
                lastAllIdx = index;
                lastAllAttr = allAttr;
            }

            index++;
        }

        if (lastHarmlessIdx < 0 && lastAnyIdx < 0 && lastAllIdx < 0)
            return null;

        var maxIdx = Math.Max(lastHarmlessIdx, Math.Max(lastAnyIdx, lastAllIdx));

        if (maxIdx == lastHarmlessIdx)
            return MethodScopeMetadata.HarmlessEndpoint;

        if (maxIdx == lastAllIdx)
            return MethodScopeMetadata.ForScopes(lastAllAttr!.Scopes, ScopeMatch.All);

        return MethodScopeMetadata.ForScopes(lastAnyAttr!.Scopes, ScopeMatch.Any);
    }
}
