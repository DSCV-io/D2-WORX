// -----------------------------------------------------------------------
// <copyright file="RequireD2ScopeExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.Endpoints;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// Fluent <see cref="IEndpointConventionBuilder"/> extensions for declaring
/// the scope requirements (or harmless-endpoint opt-in) of an endpoint. Read
/// by the auth middleware via the matched endpoint's metadata collection.
/// </summary>
/// <remarks>
/// Idiomatic Minimal-API style:
/// <code>
/// app.MapGet("/files/{id}", H).RequireD2Scope("files.read");
/// app.MapGet("/healthz", () => "ok").MarkAsD2HarmlessEndpoint();
/// </code>
/// Controller-action endpoints attach the same metadata via the auto
/// <c>WithMetadata</c> pickup performed by ASP.NET routing — fluent
/// extensions cover both surfaces.
/// </remarks>
public static class RequireD2ScopeExtensions
{
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Declares that the endpoint requires the caller's
        /// <c>IRequestContext.Scopes</c> set to overlap with at least one of the
        /// listed scopes. Defense-in-depth at the transport boundary —
        /// <c>BaseHandler.RequiredScopes</c> still re-checks per-handler.
        /// </summary>
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
        public TBuilder RequireD2Scope(
            string scope,
            params string[] additionalScopes)
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
