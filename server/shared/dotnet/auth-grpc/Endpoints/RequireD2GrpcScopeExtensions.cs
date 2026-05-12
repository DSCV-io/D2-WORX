// -----------------------------------------------------------------------
// <copyright file="RequireD2GrpcScopeExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Endpoints;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// Fluent <see cref="IEndpointConventionBuilder"/> extensions for declaring
/// the scope requirements (or harmless-endpoint opt-in) of a gRPC method on
/// the builder returned by <c>MapGrpcService&lt;T&gt;()</c>. Read by the auth
/// interceptor via the matched endpoint's metadata collection.
/// </summary>
/// <remarks>
/// <para>
/// Idiomatic gRPC fluent style:
/// </para>
/// <code>
/// app.MapGrpcService&lt;FilesService&gt;().RequireD2Scope("files.read");
/// app.MapGrpcService&lt;HealthProbeService&gt;().MarkAsD2HarmlessEndpoint();
/// </code>
/// <para>
/// The attribute path (<see cref="D2RequireScopeAttribute"/> /
/// <see cref="D2HarmlessEndpointAttribute"/>) is the recommended primary
/// surface for gRPC services because gRPC service implementations are
/// concrete classes overriding generated <c>*ServiceBase</c> types; declaring
/// scope requirements at the method declaration is the most ergonomic. The
/// fluent path covers cases where attribute attachment is unwanted: tests
/// that need to inject metadata without modifying production code,
/// conditional registration based on feature flags, endpoint-builder
/// composition pipelines.
/// </para>
/// <para>
/// A distinct extension class (different namespace from
/// <c>D2.Shared.Auth.Http.Endpoints.RequireD2ScopeExtensions</c>) is
/// used so HTTP and gRPC fluent builders don't collide on extension-method
/// resolution for callers that consume both transport bindings.
/// </para>
/// </remarks>
public static class RequireD2GrpcScopeExtensions
{
    /// <summary>
    /// Declares that the gRPC method (or every method on the service this
    /// builder represents) requires the caller's <c>IRequestContext.Scopes</c>
    /// set to overlap with at least one of the listed scopes. Defense-in-depth
    /// at the transport boundary — <c>BaseHandler.RequiredScopes</c> still
    /// re-checks per-handler.
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
        var metadata = MethodScopeMetadata.ForScopes(all);
        builder.Add(b => b.Metadata.Add(metadata));
        return builder;
    }

    /// <summary>
    /// Marks the endpoint (or every method on the gRPC service this builder
    /// represents) as a HARMLESS endpoint — the auth middleware / interceptor
    /// SKIPS the entire JWT validation pipeline for matching calls. This is a
    /// SECURITY-CRITICAL annotation — misuse causes sensitive data to be returned
    /// without any authentication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Legitimate use cases ONLY</strong>: k8s / Docker liveness +
    /// readiness probes; intra-cluster service-to-service health or info
    /// endpoints returning closed-enumeration constants only; OIDC discovery
    /// endpoints (Edge-only). Every other case — anything that returns user
    /// data, organization data, session-derived state, or any field an operator
    /// would consider sensitive — is a security bug if it reaches this surface.
    /// Declare an anon-scope-required endpoint instead.
    /// </para>
    /// <para>
    /// Does NOT recognize the BCL <c>[AllowAnonymous]</c> attribute (that
    /// semantic ties to the BCL <c>AuthorizationMiddleware</c> chain we
    /// deliberately bypass). The <c>MarkAs</c> verb signals an explicit
    /// declarative annotation — the friction at the call site is intentional.
    /// </para>
    /// </remarks>
    /// <typeparam name="TBuilder">Endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static TBuilder MarkAsD2HarmlessEndpoint<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(b => b.Metadata.Add(MethodScopeMetadata.HarmlessEndpoint));
        return builder;
    }
}
