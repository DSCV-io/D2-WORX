// -----------------------------------------------------------------------
// <copyright file="RequestOriginEdgeAppBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http;

using D2.Shared.Auth.Http.Middleware;
using Microsoft.AspNetCore.Builder;

/// <summary>
/// <see cref="IApplicationBuilder"/> extensions for inserting the Edge-inbound
/// establishment middleware into a request pipeline.
/// </summary>
public static class RequestOriginEdgeAppBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Inserts <see cref="RequestOriginEdgeInboundMiddleware"/> into the pipeline.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Pipeline-position invariant</strong>: call this AFTER
        /// <see cref="AuthAppBuilderExtensions.UseD2Auth(IApplicationBuilder)"/> so the
        /// scoped request-context slot is already populated by the auth middleware before
        /// this middleware establishes the Edge origin and starts the call-path. The
        /// intended order is:
        /// </para>
        /// <code>
        /// app.UseRouting();
        /// app.UseD2Auth();
        /// app.UseD2RequestOriginEdge();
        /// </code>
        /// </remarks>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is <see langword="null"/>.
        /// </exception>
        public IApplicationBuilder UseD2RequestOriginEdge()
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseMiddleware<RequestOriginEdgeInboundMiddleware>();
        }
    }
}
