// -----------------------------------------------------------------------
// <copyright file="EndpointScopeMetadata.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.Endpoints;

using System.Collections.Frozen;
using System.Collections.Generic;

/// <summary>
/// Endpoint metadata that declares the scope requirements (or
/// harmless-endpoint opt-in) for a single endpoint. Attached via the fluent
/// extensions <c>RequireD2Scope</c> / <c>MarkAsD2HarmlessEndpoint</c>;
/// consumed by the auth middleware during request dispatch.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Harmless-endpoint opt-in is explicit</strong>. The absence of any
/// <see cref="EndpointScopeMetadata"/> on an endpoint is the deny-by-default
/// state: the middleware runs the full validator + liveness pipeline and
/// passes any authenticated caller (empty <see cref="RequiredScopes"/> set
/// matches every authenticated request). Endpoints that need to bypass auth
/// entirely MUST opt in explicitly via <see cref="HarmlessEndpoint"/> + the
/// <c>MarkAsD2HarmlessEndpoint</c> builder extension. The codebase
/// deliberately does NOT recognize the BCL <c>[AllowAnonymous]</c> attribute
/// — its semantic is tied to the BCL <c>AuthenticationMiddleware</c> chain
/// we bypass.
/// </para>
/// <para>
/// <strong>Scope-set semantics</strong> are "any-of": a request passes the
/// middleware's scope check when its <c>IRequestContext.Scopes</c> contains
/// AT LEAST ONE entry from <see cref="RequiredScopes"/>. This mirrors
/// <c>IAuthContextExtensions.HasAnyScope</c> and
/// <c>BaseHandler.RequiredScopes</c>.
/// </para>
/// </remarks>
public sealed record EndpointScopeMetadata
{
    /// <summary>
    /// Singleton instance representing a HARMLESS ENDPOINT — the auth
    /// middleware / interceptor SKIPS the entire JWT validation pipeline for
    /// matching calls. Attached at use-time via the
    /// <c>MarkAsD2HarmlessEndpoint()</c> builder extension or the
    /// <c>[D2HarmlessEndpoint]</c> attribute. Legitimate use cases ONLY:
    /// k8s probes, intra-cluster health/info endpoints returning
    /// closed-enumeration constants only, OIDC discovery (Edge-only). Any other
    /// data exposure via this surface is a security bug.
    /// </summary>
    public static readonly EndpointScopeMetadata HarmlessEndpoint = new(
        requiredScopes: FrozenSet<string>.Empty,
        isHarmlessEndpoint: true);

    private EndpointScopeMetadata(IReadOnlySet<string> requiredScopes, bool isHarmlessEndpoint)
    {
        RequiredScopes = requiredScopes;
        IsHarmlessEndpoint = isHarmlessEndpoint;
    }

    /// <summary>
    /// Gets the scope set the caller must overlap with at least one entry
    /// from. Empty when <see cref="IsHarmlessEndpoint"/> is
    /// <see langword="true"/>; also empty when the endpoint declares no
    /// specific scopes (any authenticated caller passes).
    /// </summary>
    public IReadOnlySet<string> RequiredScopes { get; }

    /// <summary>
    /// Gets a value indicating whether this endpoint is a HARMLESS endpoint —
    /// the middleware / interceptor SKIPS the validator + liveness + scope
    /// pipeline entirely. <see langword="true"/> ONLY for the legitimate use
    /// cases of probes / OIDC discovery / harmless intra-cluster info
    /// endpoints; <see langword="false"/> for every authenticated path.
    /// </summary>
    public bool IsHarmlessEndpoint { get; }

    /// <summary>
    /// Creates a metadata instance declaring the given scope set as required.
    /// Duplicates are deduped; comparison is ordinal.
    /// </summary>
    /// <param name="scopes">The scopes the endpoint accepts (any-of).</param>
    /// <returns>A new metadata instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="scopes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="scopes"/> is empty (use
    /// <see cref="HarmlessEndpoint"/> for harmless endpoints — explicit opt-in
    /// is required, never implicit via empty scope set).
    /// </exception>
    public static EndpointScopeMetadata ForScopes(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var frozen = scopes.ToFrozenSet(StringComparer.Ordinal);
        if (frozen.Count == 0)
        {
            throw new ArgumentException(
                "Required scopes set must contain at least one entry. "
                    + "For harmless endpoints, use MarkAsD2HarmlessEndpoint() / "
                    + "EndpointScopeMetadata.HarmlessEndpoint instead.",
                nameof(scopes));
        }

        return new EndpointScopeMetadata(frozen, isHarmlessEndpoint: false);
    }
}
