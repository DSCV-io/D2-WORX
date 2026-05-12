// -----------------------------------------------------------------------
// <copyright file="EndpointScopeMetadata.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.Endpoints;

using System.Collections.Frozen;
using System.Collections.Generic;

/// <summary>
/// Endpoint metadata that declares the scope requirements (or anonymous opt-in)
/// for a single endpoint. Attached via the fluent extensions
/// <c>RequireD2Scope</c> / <c>AllowD2Anonymous</c>; consumed by the auth
/// middleware during request dispatch.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Anonymous opt-in is explicit</strong>. The absence of any
/// <see cref="EndpointScopeMetadata"/> on an endpoint is the deny-by-default
/// state: the middleware runs the full validator + liveness pipeline and
/// passes any authenticated caller (empty <see cref="RequiredScopes"/> set
/// matches every authenticated request). Endpoints that need anonymous
/// access MUST opt in explicitly via <see cref="Anonymous"/> + the
/// <c>AllowD2Anonymous</c> builder extension. The codebase deliberately
/// does NOT recognize the BCL <c>[AllowAnonymous]</c> attribute — its
/// semantic is tied to the BCL <c>AuthenticationMiddleware</c> chain we
/// bypass.
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
    /// Singleton instance representing "this endpoint accepts anonymous
    /// requests" — middleware short-circuits to <c>next</c> without invoking
    /// the validator or liveness check when this metadata is attached.
    /// </summary>
    public static readonly EndpointScopeMetadata Anonymous = new(
        requiredScopes: FrozenSet<string>.Empty,
        isAnonymous: true);

    private EndpointScopeMetadata(IReadOnlySet<string> requiredScopes, bool isAnonymous)
    {
        RequiredScopes = requiredScopes;
        IsAnonymous = isAnonymous;
    }

    /// <summary>
    /// Gets the scope set the caller must overlap with at least one entry
    /// from. Empty when <see cref="IsAnonymous"/> is <see langword="true"/>;
    /// also empty when the endpoint declares no specific scopes (any
    /// authenticated caller passes).
    /// </summary>
    public IReadOnlySet<string> RequiredScopes { get; }

    /// <summary>
    /// Gets a value indicating whether this endpoint is anonymously accessible
    /// — middleware skips the validator + liveness pipeline entirely.
    /// </summary>
    public bool IsAnonymous { get; }

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
    /// <see cref="Anonymous"/> for anonymous endpoints — explicit opt-in is
    /// required, never implicit via empty scope set).
    /// </exception>
    public static EndpointScopeMetadata ForScopes(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var frozen = scopes.ToFrozenSet(StringComparer.Ordinal);
        if (frozen.Count == 0)
        {
            throw new ArgumentException(
                "Required scopes set must contain at least one entry. "
                    + "For anonymous endpoints, use AllowD2Anonymous() / "
                    + "EndpointScopeMetadata.Anonymous instead.",
                nameof(scopes));
        }

        return new EndpointScopeMetadata(frozen, isAnonymous: false);
    }
}
