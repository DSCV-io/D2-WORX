// -----------------------------------------------------------------------
// <copyright file="SecurityHeadersApplicationBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.AspNetCore.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// ASP.NET Core middleware extension for installing the D² opinionated
/// security-header set per OWASP Secure Headers Project guidance.
/// </summary>
public static class SecurityHeadersApplicationBuilderExtensions
{
    /// <param name="app">The ASP.NET Core application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Installs the security-headers middleware. Each response carries
        /// the OWASP-aligned default header set:
        /// <c>X-Content-Type-Options</c>, <c>X-Frame-Options</c>,
        /// <c>Referrer-Policy</c>, <c>X-Permitted-Cross-Domain-Policies</c>,
        /// <c>Cross-Origin-Resource-Policy</c>,
        /// <c>Cross-Origin-Opener-Policy</c>, and (HTTPS-only)
        /// <c>Strict-Transport-Security</c>. Each header carries the default
        /// literal documented on
        /// <see cref="D2SecurityHeadersOptions"/>; the
        /// <paramref name="configure"/> callback overrides per-header on the
        /// <see cref="D2SecurityHeadersOptions"/> instance bound from
        /// <c>IOptions&lt;D2SecurityHeadersOptions&gt;</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The middleware writes via
        /// <c>HttpResponse.OnStarting</c> so the headers ship with the first
        /// response byte regardless of downstream middleware that may already
        /// have begun the response body.
        /// </para>
        /// <para>
        /// Per-header override semantic:
        /// <c>null</c> override → the OWASP default value is written;
        /// empty / whitespace override → the header is suppressed
        /// (not written);
        /// non-empty override → the override literal is written.
        /// </para>
        /// <para>
        /// HSTS preload submission is intentionally NOT included by default
        /// (preload is a one-way door — once the apex domain is in the
        /// browser-built-in preload list, removal is slow and incomplete).
        /// Each service that wants preload submission opts in by setting
        /// <see cref="D2SecurityHeadersOptions.StrictTransportSecurity"/>
        /// to a value that includes <c>preload</c>.
        /// </para>
        /// <para>
        /// Calling this method multiple times on the same
        /// <see cref="IApplicationBuilder"/> registers the middleware
        /// multiple times; ASP.NET Core invokes them sequentially. The
        /// duplicate writes are idempotent (each overwrites the prior
        /// value) so the behavior is benign — but per-pipeline registration
        /// SHOULD happen exactly once at composition root.
        /// </para>
        /// </remarks>
        /// <param name="configure">
        /// Optional <see cref="D2SecurityHeadersOptions"/> customizer.
        /// Mutates the options instance currently held in DI; if no options
        /// were registered prior, an ad-hoc instance is created and
        /// embedded directly into the middleware registration.
        /// </param>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public IApplicationBuilder UseD2SecurityHeaders(
            Action<D2SecurityHeadersOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            var optionsAccessor = app.ApplicationServices
                .GetService<IOptions<D2SecurityHeadersOptions>>();

            if (optionsAccessor is null)
            {
                // No DI registration — build a snapshot, apply configure,
                // and embed directly. Primary path is when callers have
                // not pre-registered options via builder.Services.
                var snapshot = new D2SecurityHeadersOptions();
                configure?.Invoke(snapshot);
                return app.UseMiddleware<SecurityHeadersMiddleware>(
                    Options.Create(snapshot));
            }

            // DI-registered: configure mutates the resolved instance so
            // subsequent IOptions<...>.Value reads see the override.
            configure?.Invoke(optionsAccessor.Value);
            return app.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}
