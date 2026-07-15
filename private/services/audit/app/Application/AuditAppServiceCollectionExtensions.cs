// -----------------------------------------------------------------------
// <copyright file="AuditAppServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Audit.App.Application;

using DcsvIo.D2.Private.Audit.App.Application.Handlers.Queries.PingAudit;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for the Audit App layer: NIE PingAudit handler.
/// </summary>
public static class AuditAppServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Audit App-layer services (handlers).
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2AuditApp()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(services);

            services.AddTransient<IPingAuditHandler, PingAuditHandler>();

            return services;
        }
    }
}
