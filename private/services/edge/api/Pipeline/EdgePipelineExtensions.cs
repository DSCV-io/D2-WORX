// -----------------------------------------------------------------------
// <copyright file="EdgePipelineExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Api.Pipeline;

using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Auth.Http;
using DcsvIo.D2.Logging;
using DcsvIo.D2.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Edge HTTP pipeline — <see cref="UseD2EdgePipeline"/> (locked middleware order).
/// </summary>
/// <remarks>
/// Does <b>not</b> call <c>UseD2DefaultPipeline</c>. Composes individual
/// <c>UseD2*</c> calls so a rate-limit middleware slot stays a documented gap
/// without inventing ServiceDefaults hooks. Rate-limit body is documented in
/// <c>Pipeline/README.md</c> only — no middleware registration here.
/// </remarks>
public static class EdgePipelineExtensions
{
    /// <param name="app">The application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Installs the Edge middleware pipeline in locked order:
        /// SecurityHeaders → RequestLogging → Cors → Routing → InfrastructureBypass
        /// → (reserved rate-limit slot) → Authentication → Auth → RequestOriginEdge
        /// → Authorization.
        /// </summary>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public IApplicationBuilder UseD2EdgePipeline()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(app);

            var optionsAccessor = app.ApplicationServices
                .GetService<IOptions<D2ServiceDefaultsOptions>>();

            var options = optionsAccessor?.Value ?? new D2ServiceDefaultsOptions();

            app.UseD2SecurityHeaders(options.SecurityHeadersConfigure);
            app.UseD2RequestLogging();
            app.UseD2Cors();
            app.UseRouting();
            app.UseD2InfrastructureBypass(options.InfrastructureBypassConfigure);

            // RESERVED SLOT — rate-limit middleware body is not registered.
            // Documented in Pipeline/README.md only.
            if (options.SkipAuthAutoWiring is false)
            {
                app.UseAuthentication();
                app.UseD2Auth();

                // AFTER UseD2Auth — auth/http establishment law.
                app.UseD2RequestOriginEdge();
                app.UseAuthorization();
            }

            return app;
        }
    }
}
