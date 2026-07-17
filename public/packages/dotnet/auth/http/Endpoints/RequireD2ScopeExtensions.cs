// -----------------------------------------------------------------------
// <copyright file="RequireD2ScopeExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Http.Endpoints;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Builder;

/// <summary>
/// Fluent <see cref="IEndpointConventionBuilder"/> extensions for declaring
/// the scope requirements (or harmless-endpoint opt-in) of an endpoint. Read
/// by the auth middleware via the matched endpoint's metadata collection.
/// </summary>
/// <remarks>
/// <para>
/// Idiomatic Minimal-API style:
/// <code>
/// app.MapGet("/files/{id}", H).RequireAnyScope("files.read");
/// app.MapGet("/files/{id}/lock", H).RequireAllScopes("files.read", "files.write");
/// app.MapGet("/healthz", () => "ok").MarkAsD2HarmlessEndpoint();
/// </code>
/// Controller-action endpoints attach the same metadata via the auto
/// <c>WithMetadata</c> pickup performed by ASP.NET routing — fluent
/// extensions cover both surfaces.
/// </para>
/// <para>
/// <strong>Intentional asymmetry with the gRPC fluent extensions</strong>:
/// this class is generic on <see cref="IEndpointConventionBuilder"/> (any
/// HTTP builder is valid) while the gRPC sibling
/// (<c>RequireD2GrpcScopeExtensions</c> in <c>DcsvIo.D2.Auth.Grpc</c>) is
/// constrained to <c>GrpcServiceEndpointConventionBuilder</c>.
/// The asymmetry is by design: <see cref="EndpointScopeMetadata"/> is
/// consumed by <c>JwtAuthMiddleware</c>, which runs on every HTTP endpoint
/// regardless of how it was registered — so any <see cref="IEndpointConventionBuilder"/>
/// is a valid receiver. The gRPC fluent is constrained because
/// <c>MethodScopeMetadata</c> on a non-gRPC endpoint would not be enforced
/// by any middleware (silent under-enforcement).
/// </para>
/// </remarks>
public static class RequireD2ScopeExtensions
{
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Declares that the endpoint requires the caller's
        /// <c>IRequestContext.Scopes</c> set to overlap with <b>at least one</b>
        /// of the listed scopes (<see cref="ScopeMatch.Any"/>). Defense-in-depth
        /// at the transport boundary — <c>BaseHandler.ScopeRequirement</c> still
        /// re-checks per-handler.
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
        public TBuilder RequireAnyScope(
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
            var metadata = EndpointScopeMetadata.ForScopes(all, ScopeMatch.Any);
            builder.Add(b => b.Metadata.Add(metadata));
            return builder;
        }

        /// <summary>
        /// Declares that the endpoint requires the caller's
        /// <c>IRequestContext.Scopes</c> set to contain <b>every</b> listed
        /// scope (<see cref="ScopeMatch.All"/>). Defense-in-depth at the
        /// transport boundary — <c>BaseHandler.ScopeRequirement</c> still
        /// re-checks per-handler.
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
        public TBuilder RequireAllScopes(
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
            var metadata = EndpointScopeMetadata.ForScopes(all, ScopeMatch.All);
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
        public TBuilder MarkAsD2HarmlessEndpoint()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Add(b => b.Metadata.Add(EndpointScopeMetadata.HarmlessEndpoint));
            return builder;
        }
    }
}
