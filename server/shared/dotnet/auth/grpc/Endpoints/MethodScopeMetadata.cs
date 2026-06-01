// -----------------------------------------------------------------------
// <copyright file="MethodScopeMetadata.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Endpoints;

using System.Collections.Frozen;
using System.Collections.Generic;
using D2.Shared.Auth.Abstractions;

/// <summary>
/// gRPC endpoint metadata that declares the scope requirements (or
/// harmless-endpoint opt-in) for a single gRPC method. Attached via the
/// fluent extensions <c>RequireAnyScope</c> / <c>RequireAllScopes</c> /
/// <c>MarkAsD2HarmlessEndpoint</c>, OR derived from
/// <c>D2RequireAnyScopeAttribute</c> / <c>D2RequireAllScopesAttribute</c> /
/// <c>D2HarmlessEndpointAttribute</c> declarations on the service
/// implementation. Consumed by the <c>JwtAuthInterceptor</c> during inbound
/// RPC dispatch.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Harmless-endpoint opt-in is explicit</strong>. The absence of any
/// <see cref="MethodScopeMetadata"/> on a gRPC method is the deny-by-default
/// state: the interceptor runs the full validator + liveness pipeline and
/// passes any authenticated caller (empty <see cref="Scopes"/> set
/// matches every authenticated request). Methods that need to bypass auth
/// entirely MUST opt in explicitly via <see cref="HarmlessEndpoint"/> + the
/// <c>MarkAsD2HarmlessEndpoint</c> builder extension or
/// <c>D2HarmlessEndpointAttribute</c>.
/// </para>
/// <para>
/// <strong>Scope-set semantics</strong> are controlled by <see cref="Match"/>:
/// <see cref="ScopeMatch.Any"/> requires at least one overlap between the
/// caller's <c>IRequestContext.Scopes</c> and <see cref="Scopes"/>;
/// <see cref="ScopeMatch.All"/> requires the caller to hold every entry in
/// <see cref="Scopes"/>. The match mode is set explicitly at declaration time
/// via <c>RequireAnyScope</c> / <c>RequireAllScopes</c> or the corresponding
/// attribute.
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
    /// Singleton instance representing a HARMLESS ENDPOINT — the auth
    /// middleware / interceptor SKIPS the entire JWT validation pipeline for
    /// matching calls. Attached at use-time via the
    /// <c>MarkAsD2HarmlessEndpoint()</c> builder extension or the
    /// <c>[D2HarmlessEndpoint]</c> attribute. Legitimate use cases ONLY:
    /// k8s probes, intra-cluster health/info endpoints returning
    /// closed-enumeration constants only, OIDC discovery (Edge-only). Any other
    /// data exposure via this surface is a security bug.
    /// </summary>
    public static readonly MethodScopeMetadata HarmlessEndpoint = new(
        scopes: FrozenSet<string>.Empty,
        match: ScopeMatch.Any,
        isHarmlessEndpoint: true);

    private MethodScopeMetadata(
        IReadOnlySet<string> scopes,
        ScopeMatch match,
        bool isHarmlessEndpoint)
    {
        Scopes = scopes;
        Match = match;
        IsHarmlessEndpoint = isHarmlessEndpoint;
    }

    /// <summary>
    /// Gets the scope set the caller must satisfy. How this set is evaluated
    /// against the caller's granted scopes depends on <see cref="Match"/>.
    /// Empty when <see cref="IsHarmlessEndpoint"/> is <see langword="true"/>;
    /// also empty when the method declares no specific scopes (any
    /// authenticated caller passes).
    /// </summary>
    public IReadOnlySet<string> Scopes { get; }

    /// <summary>
    /// Gets the match mode that governs how <see cref="Scopes"/> is evaluated.
    /// <see cref="ScopeMatch.Any"/> = caller holds at least one entry;
    /// <see cref="ScopeMatch.All"/> = caller holds every entry.
    /// </summary>
    public ScopeMatch Match { get; }

    /// <summary>
    /// Gets a value indicating whether this endpoint is a HARMLESS endpoint —
    /// the middleware / interceptor SKIPS the validator + liveness + scope
    /// pipeline entirely. <see langword="true"/> ONLY for the legitimate use
    /// cases of probes / OIDC discovery / harmless intra-cluster info
    /// endpoints; <see langword="false"/> for every authenticated path.
    /// </summary>
    public bool IsHarmlessEndpoint { get; }

    /// <summary>
    /// Creates a metadata instance declaring the given scope set as required,
    /// using the specified <paramref name="match"/> mode.
    /// Duplicates are deduped; comparison is ordinal.
    /// </summary>
    /// <param name="scopes">The scopes the method requires.</param>
    /// <param name="match">
    /// Whether the caller must hold any one of the scopes
    /// (<see cref="ScopeMatch.Any"/>) or every scope
    /// (<see cref="ScopeMatch.All"/>).
    /// </param>
    /// <returns>A new metadata instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="scopes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="scopes"/> is empty (use
    /// <see cref="HarmlessEndpoint"/> for harmless endpoints — explicit opt-in
    /// is required, never implicit via empty scope set).
    /// </exception>
    public static MethodScopeMetadata ForScopes(IEnumerable<string> scopes, ScopeMatch match)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var frozen = scopes.ToFrozenSet(StringComparer.Ordinal);

        // Manual Count check required: ThrowIfFalsey has no custom-message overload
        // and the bespoke ArgumentException message names the HarmlessEndpoint alternative.
        if (frozen.Count == 0)
        {
            throw new ArgumentException(
                "Required scopes set must contain at least one entry. "
                    + "For harmless endpoints, use MarkAsD2HarmlessEndpoint() / "
                    + "[D2HarmlessEndpoint] / MethodScopeMetadata.HarmlessEndpoint instead.",
                nameof(scopes));
        }

        return new MethodScopeMetadata(frozen, match, isHarmlessEndpoint: false);
    }
}
