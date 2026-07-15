// -----------------------------------------------------------------------
// <copyright file="AuthAppBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http;

using D2.Shared.Auth.Http.Middleware;
using Microsoft.AspNetCore.Builder;

/// <summary>
/// <see cref="IApplicationBuilder"/> extensions for inserting the auth
/// middleware into a request pipeline.
/// </summary>
public static class AuthAppBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Inserts <see cref="JwtAuthMiddleware"/> into the pipeline.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Pipeline-position invariant</strong>: call this AFTER
        /// <see cref="EndpointRoutingApplicationBuilderExtensions.UseRouting"/>
        /// (so the matched endpoint's metadata is available to the middleware)
        /// and BEFORE the endpoint dispatcher
        /// (<see cref="EndpointRoutingApplicationBuilderExtensions.UseEndpoints"/>
        /// or <c>app.MapXxx</c> calls). The intended order is:
        /// </para>
        /// <code>
        /// app.UseRouting();
        /// app.UseD2Auth();
        /// app.MapGet("/files/{id}", ...).RequireAnyScope("files.read");
        /// </code>
        /// </remarks>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is <see langword="null"/>.
        /// </exception>
        public IApplicationBuilder UseD2Auth()
        {
            ArgumentNullException.ThrowIfNull(app);
            return app.UseMiddleware<JwtAuthMiddleware>();
        }
    }
}
