// -----------------------------------------------------------------------
// <copyright file="InfrastructureBypassApplicationBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.AspNetCore.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// ASP.NET Core middleware extension installing the infrastructure-path
/// bypass middleware — tags or short-circuits requests whose path matches
/// the canonical infrastructure-path list so heavy business middleware
/// downstream can no-op early on probe / metrics / well-known requests.
/// </summary>
public static class InfrastructureBypassApplicationBuilderExtensions
{
    /// <param name="app">The ASP.NET Core application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Installs the infrastructure-bypass middleware. For each incoming
        /// request: sets
        /// <see cref="D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY"/>
        /// to a boolean indicating whether
        /// <see cref="Microsoft.AspNetCore.Http.HttpRequest.Path"/> matches
        /// the configured infrastructure-path list (default
        /// <see cref="D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS"/>).
        /// In the default short-circuit mode
        /// (<see cref="D2InfrastructureBypassOptions.TagOnly"/> = <c>false</c>),
        /// matched requests bypass every middleware registered AFTER this
        /// one and are routed directly to the matched endpoint via the
        /// routing-resolved <see cref="Microsoft.AspNetCore.Http.Endpoint"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Pipeline placement: install AFTER <c>app.UseRouting()</c> (which
        /// resolves the matched endpoint without invoking it) and BEFORE
        /// the business middleware (rate-limit, idempotency, request
        /// enrichment, etc.). The middleware reads the routing-matched
        /// endpoint from
        /// <see cref="Microsoft.AspNetCore.Http.EndpointHttpContextExtensions.GetEndpoint"/>
        /// and invokes its
        /// <see cref="Microsoft.AspNetCore.Http.Endpoint.RequestDelegate"/>
        /// directly when short-circuiting; if no endpoint has been routed
        /// (caller put bypass before <c>UseRouting()</c>), the middleware
        /// falls through to the next delegate so the pipeline still
        /// completes correctly.
        /// </para>
        /// <para>
        /// Tag-only mode: services that need custom middleware to execute
        /// on infrastructure paths set
        /// <see cref="D2InfrastructureBypassOptions.TagOnly"/> to <c>true</c>;
        /// the middleware then ONLY tags the request and continues. Business
        /// middleware downstream can read the flag to opt-out per its own
        /// policy.
        /// </para>
        /// </remarks>
        /// <param name="configure">
        /// Optional <see cref="D2InfrastructureBypassOptions"/> customizer.
        /// </param>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public IApplicationBuilder UseD2InfrastructureBypass(
            Action<D2InfrastructureBypassOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            var optionsAccessor = app.ApplicationServices
                .GetService<IOptions<D2InfrastructureBypassOptions>>();

            if (optionsAccessor is null)
            {
                var snapshot = new D2InfrastructureBypassOptions();
                configure?.Invoke(snapshot);
                return app.UseMiddleware<InfrastructureBypassMiddleware>(
                    Options.Create(snapshot));
            }

            configure?.Invoke(optionsAccessor.Value);
            return app.UseMiddleware<InfrastructureBypassMiddleware>();
        }
    }
}
