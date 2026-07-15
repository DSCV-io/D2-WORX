// -----------------------------------------------------------------------
// <copyright file="CorsApplicationBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using Microsoft.AspNetCore.Builder;

/// <summary>
/// ASP.NET Core middleware extension applying the D² CORS policy registered
/// by <see cref="CorsServiceCollectionExtensions.AddD2Cors"/>.
/// </summary>
public static class CorsApplicationBuilderExtensions
{
    /// <param name="app">The ASP.NET Core application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Installs CORS middleware that applies the D² policy named
        /// <see cref="D2AspNetCoreConstants.DEFAULT_CORS_POLICY_NAME"/>
        /// (<c>"D2_DEFAULT"</c>). The policy MUST have been registered via
        /// <see cref="CorsServiceCollectionExtensions.AddD2Cors"/> on
        /// <c>builder.Services</c> earlier in the host build — calling
        /// <c>UseD2Cors</c> without a prior <c>AddD2Cors</c> raises an
        /// <see cref="System.InvalidOperationException"/> at first request
        /// (the underlying ASP.NET Core CORS middleware behavior).
        /// </summary>
        /// <returns>The same <paramref name="app"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="app"/> is null.
        /// </exception>
        public IApplicationBuilder UseD2Cors()
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseCors(D2AspNetCoreConstants.DEFAULT_CORS_POLICY_NAME);
        }
    }
}
