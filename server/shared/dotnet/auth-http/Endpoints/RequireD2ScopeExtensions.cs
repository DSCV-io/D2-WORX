// -----------------------------------------------------------------------
// <copyright file="RequireD2ScopeExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.Endpoints;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// Fluent <see cref="IEndpointConventionBuilder"/> extensions for declaring
/// the scope requirements (or anonymous opt-in) of an endpoint. Read by the
/// auth middleware via the matched endpoint's metadata collection.
/// </summary>
/// <remarks>
/// Idiomatic Minimal-API style:
/// <code>
/// app.MapGet("/files/{id}", H).RequireD2Scope("files.read");
/// app.MapPost("/auth/sign-in", H).AllowD2Anonymous();
/// </code>
/// Controller-action endpoints attach the same metadata via the auto
/// <c>WithMetadata</c> pickup performed by ASP.NET routing — fluent
/// extensions cover both surfaces.
/// </remarks>
public static class RequireD2ScopeExtensions
{
    /// <summary>
    /// Declares that the endpoint requires the caller's
    /// <c>IRequestContext.Scopes</c> set to overlap with at least one of the
    /// listed scopes. Defense-in-depth at the transport boundary —
    /// <c>BaseHandler.RequiredScopes</c> still re-checks per-handler.
    /// </summary>
    /// <typeparam name="TBuilder">Endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="scope">The first required scope (at-least-one).</param>
    /// <param name="additionalScopes">Additional scopes (at-least-one).</param>
    /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/>, <paramref name="scope"/>, or
    /// <paramref name="additionalScopes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="scope"/> is empty / whitespace, or any
    /// entry in <paramref name="additionalScopes"/> is empty / whitespace.
    /// </exception>
    public static TBuilder RequireD2Scope<TBuilder>(
        this TBuilder builder,
        string scope,
        params string[] additionalScopes)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(additionalScopes);

        for (var i = 0; i < additionalScopes.Length; i++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                additionalScopes[i],
                $"{nameof(additionalScopes)}[{i}]");
        }

        var all = new string[additionalScopes.Length + 1];
        all[0] = scope;
        additionalScopes.CopyTo(all, 1);
        var metadata = EndpointScopeMetadata.ForScopes(all);
        builder.Add(b => b.Metadata.Add(metadata));
        return builder;
    }

    /// <summary>
    /// Declares that the endpoint accepts anonymous requests — the auth
    /// middleware short-circuits the validator + liveness pipeline. Required
    /// for endpoints in the codegen anonymous family (sign-in, password
    /// reset, etc.). Does NOT recognize the BCL <c>[AllowAnonymous]</c>
    /// attribute (that semantic ties to the BCL <c>AuthorizationMiddleware</c>
    /// chain we deliberately bypass).
    /// </summary>
    /// <typeparam name="TBuilder">Endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static TBuilder AllowD2Anonymous<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(b => b.Metadata.Add(EndpointScopeMetadata.Anonymous));
        return builder;
    }
}
