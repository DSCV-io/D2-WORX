// -----------------------------------------------------------------------
// <copyright file="MethodScopeMetadata.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Endpoints;

using System.Collections.Frozen;
using System.Collections.Generic;

/// <summary>
/// gRPC endpoint metadata that declares the scope requirements (or anonymous
/// opt-in) for a single gRPC method. Attached via the fluent extensions
/// <c>RequireD2Scope</c> / <c>AllowD2Anonymous</c>, OR derived from
/// <c>D2RequireScopeAttribute</c> / <c>D2AllowAnonymousAttribute</c>
/// declarations on the service implementation. Consumed by the
/// <c>JwtAuthInterceptor</c> during inbound RPC dispatch.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Anonymous opt-in is explicit</strong>. The absence of any
/// <see cref="MethodScopeMetadata"/> on a gRPC method is the deny-by-default
/// state: the interceptor runs the full validator + liveness pipeline and
/// passes any authenticated caller (empty <see cref="RequiredScopes"/> set
/// matches every authenticated request). Methods that need anonymous access
/// MUST opt in explicitly via <see cref="Anonymous"/> + the
/// <c>AllowD2Anonymous</c> builder extension or <c>D2AllowAnonymousAttribute</c>.
/// </para>
/// <para>
/// <strong>Scope-set semantics</strong> are "any-of": a request passes the
/// interceptor's scope check when its <c>IRequestContext.Scopes</c> contains
/// AT LEAST ONE entry from <see cref="RequiredScopes"/>. This mirrors
/// <c>IAuthContextExtensions.HasAnyScope</c> and
/// <c>BaseHandler.RequiredScopes</c> (and the HTTP middleware's
/// <c>EndpointScopeMetadata</c>).
/// </para>
/// <para>
/// <strong>Distinct from <c>EndpointScopeMetadata</c></strong> by intent: HTTP
/// and gRPC may grow independent per-transport options (path templates,
/// per-method options, etc.) without one binding accidentally enforcing the
/// other transport's metadata. The shape mirrors HTTP for code-search parity
/// but the type is namespace-distinct.
/// </para>
/// </remarks>
public sealed record MethodScopeMetadata
{
    /// <summary>
    /// Singleton instance representing "this gRPC method accepts anonymous
    /// requests" — the interceptor short-circuits to <c>continuation</c>
    /// without invoking the validator or liveness check when this metadata
    /// is attached.
    /// </summary>
    public static readonly MethodScopeMetadata Anonymous = new(
        requiredScopes: FrozenSet<string>.Empty,
        isAnonymous: true);

    private MethodScopeMetadata(IReadOnlySet<string> requiredScopes, bool isAnonymous)
    {
        RequiredScopes = requiredScopes;
        IsAnonymous = isAnonymous;
    }

    /// <summary>
    /// Gets the scope set the caller must overlap with at least one entry
    /// from. Empty when <see cref="IsAnonymous"/> is <see langword="true"/>;
    /// also empty when the method declares no specific scopes (any
    /// authenticated caller passes).
    /// </summary>
    public IReadOnlySet<string> RequiredScopes { get; }

    /// <summary>
    /// Gets a value indicating whether this gRPC method is anonymously
    /// accessible — the interceptor skips the validator + liveness pipeline
    /// entirely.
    /// </summary>
    public bool IsAnonymous { get; }

    /// <summary>
    /// Creates a metadata instance declaring the given scope set as required.
    /// Duplicates are deduped; comparison is ordinal.
    /// </summary>
    /// <param name="scopes">The scopes the method accepts (any-of).</param>
    /// <returns>A new metadata instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="scopes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="scopes"/> is empty (use
    /// <see cref="Anonymous"/> for anonymous methods — explicit opt-in is
    /// required, never implicit via empty scope set).
    /// </exception>
    public static MethodScopeMetadata ForScopes(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var frozen = scopes.ToFrozenSet(StringComparer.Ordinal);
        if (frozen.Count == 0)
        {
            throw new ArgumentException(
                "Required scopes set must contain at least one entry. "
                    + "For anonymous gRPC methods, use AllowD2Anonymous() / "
                    + "[D2AllowAnonymous] / MethodScopeMetadata.Anonymous instead.",
                nameof(scopes));
        }

        return new MethodScopeMetadata(frozen, isAnonymous: false);
    }
}
