// -----------------------------------------------------------------------
// <copyright file="RequireD2GrpcScopeExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Endpoints;

using D2.Shared.Auth.Abstractions;
using D2.Shared.Utilities.Extensions;
using Microsoft.AspNetCore.Builder;

/// <summary>
/// Fluent <see cref="GrpcServiceEndpointConventionBuilder"/> extensions for
/// declaring the scope requirements (or harmless-endpoint opt-in) of a gRPC
/// method on the builder returned by <c>MapGrpcService&lt;T&gt;()</c>. Read
/// by the auth interceptor via the matched endpoint's metadata collection.
/// </summary>
/// <remarks>
/// <para>
/// <strong>gRPC-builder-only constraint</strong>: these extension methods are
/// constrained to <see cref="GrpcServiceEndpointConventionBuilder"/> — the
/// concrete type returned by <c>MapGrpcService&lt;T&gt;()</c>. Calling them
/// on an HTTP builder (e.g. <c>MapGet(...)</c>) is a compile error, preventing
/// the cross-transport misuse footgun where <see cref="MethodScopeMetadata"/>
/// is attached to an HTTP endpoint whose middleware never enforces it.
/// </para>
/// <para>
/// Idiomatic gRPC fluent style:
/// </para>
/// <code>
/// app.MapGrpcService&lt;FilesService&gt;().RequireAnyScope("files.read");
/// app.MapGrpcService&lt;FilesService&gt;().RequireAllScopes("files.read", "files.write");
/// app.MapGrpcService&lt;HealthProbeService&gt;().MarkAsD2HarmlessEndpoint();
/// </code>
/// <para>
/// The attribute path (<see cref="D2RequireAnyScopeAttribute"/> /
/// <see cref="D2RequireAllScopesAttribute"/> /
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
    extension(GrpcServiceEndpointConventionBuilder builder)
    {
        /// <summary>
        /// Declares that the gRPC method (or every method on the service this
        /// builder represents) requires the caller's <c>IRequestContext.Scopes</c>
        /// set to overlap with <b>at least one</b> of the listed scopes
        /// (<see cref="ScopeMatch.Any"/>). Defense-in-depth at the transport
        /// boundary — <c>BaseHandler.ScopeRequirement</c> still re-checks per-handler.
        /// </summary>
        /// <param name="scope">The first required scope (any-of).</param>
        /// <param name="additionalScopes">Additional scopes (any-of).</param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/>, <paramref name="scope"/>, or
        /// <paramref name="additionalScopes"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="scope"/> is empty / whitespace, or any
        /// entry in <paramref name="additionalScopes"/> is empty / whitespace.
        /// </exception>
        public GrpcServiceEndpointConventionBuilder RequireAnyScope(
            string scope,
            params string[] additionalScopes)
        {
            ArgumentNullException.ThrowIfNull(builder);
            scope.ThrowIfFalsey();
            ArgumentNullException.ThrowIfNull(additionalScopes);

            for (var i = 0; i < additionalScopes.Length; i++)
                additionalScopes[i].ThrowIfFalsey($"{nameof(additionalScopes)}[{i}]");

            var all = new string[additionalScopes.Length + 1];
            all[0] = scope;
            additionalScopes.CopyTo(all, 1);
            var metadata = MethodScopeMetadata.ForScopes(all, ScopeMatch.Any);
            builder.Add(b => b.Metadata.Add(metadata));
            return builder;
        }

        /// <summary>
        /// Declares that the gRPC method (or every method on the service this
        /// builder represents) requires the caller's <c>IRequestContext.Scopes</c>
        /// set to contain <b>every</b> listed scope (<see cref="ScopeMatch.All"/>).
        /// Defense-in-depth at the transport boundary — <c>BaseHandler.ScopeRequirement</c>
        /// still re-checks per-handler.
        /// </summary>
        /// <param name="scope">The first required scope (all-of).</param>
        /// <param name="additionalScopes">Additional scopes (all-of).</param>
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/>, <paramref name="scope"/>, or
        /// <paramref name="additionalScopes"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="scope"/> is empty / whitespace, or any
        /// entry in <paramref name="additionalScopes"/> is empty / whitespace.
        /// </exception>
        public GrpcServiceEndpointConventionBuilder RequireAllScopes(
            string scope,
            params string[] additionalScopes)
        {
            ArgumentNullException.ThrowIfNull(builder);
            scope.ThrowIfFalsey();
            ArgumentNullException.ThrowIfNull(additionalScopes);

            for (var i = 0; i < additionalScopes.Length; i++)
                additionalScopes[i].ThrowIfFalsey($"{nameof(additionalScopes)}[{i}]");

            var all = new string[additionalScopes.Length + 1];
            all[0] = scope;
            additionalScopes.CopyTo(all, 1);
            var metadata = MethodScopeMetadata.ForScopes(all, ScopeMatch.All);
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
        /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        public GrpcServiceEndpointConventionBuilder MarkAsD2HarmlessEndpoint()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Add(b => b.Metadata.Add(MethodScopeMetadata.HarmlessEndpoint));
            return builder;
        }
    }
}
