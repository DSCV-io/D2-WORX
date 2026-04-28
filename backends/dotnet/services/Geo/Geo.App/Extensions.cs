// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App;

using D2.Geo.App.Implementations.CQRS.Handlers.C;
using D2.Geo.App.Implementations.CQRS.Handlers.Q;
using D2.Geo.App.Implementations.CQRS.Handlers.X;
using D2.Geo.Client.Interfaces.CQRS.Handlers.X;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ICommands = D2.Geo.App.Interfaces.CQRS.Handlers.C.ICommands;
using IGeoComplex = D2.Geo.App.Interfaces.CQRS.Handlers.X.IComplex;
using IQueries = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries;

/// <summary>
/// Extension methods for adding Geo services to the application.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds Geo application handlers to the service collection.
    /// </summary>
    ///
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Geo application services to the service collection.
        /// </summary>
        ///
        /// <param name="configuration">
        /// The application configuration.
        /// </param>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddGeoApp(IConfiguration configuration)
        {
            services.Configure<GeoAppOptions>(configuration.GetSection("GEO_APP"));

            // Complex handlers.
            services.AddTransient<IComplex.IGetHandler, Get>();
            services.AddTransient<IGeoComplex.IFindWhoIsHandler, FindWhoIs>();
            services.AddTransient<IGeoComplex.IUpdateContactsByExtKeysHandler, UpdateContactsByExtKeys>();

            // Query handlers.
            services.AddTransient<IQueries.ICheckHealthHandler, CheckHealth>();
            services.AddTransient<IQueries.IGetLocationsByIdsHandler, GetLocationsByIds>();
            services.AddTransient<IQueries.IGetWhoIsByIdsHandler, GetWhoIsByIds>();
            services.AddTransient<IQueries.IGetContactsByIdsHandler, GetContactsByIds>();
            services.AddTransient<IQueries.IGetContactsByExtKeysHandler, GetContactsByExtKeys>();

            // Command handlers.
            services.AddTransient<ICommands.ICreateLocationsHandler, CreateLocations>();
            services.AddTransient<ICommands.ICreateWhoIsHandler, CreateWhoIs>();
            services.AddTransient<ICommands.ICreateContactsHandler, CreateContacts>();
            services.AddTransient<ICommands.IDeleteContactsHandler, DeleteContacts>();
            services.AddTransient<ICommands.IDeleteContactsByExtKeysHandler, DeleteContactsByExtKeys>();
            services.AddTransient<ICommands.ICleanupOrphanedLocationsHandler, CleanupOrphanedLocations>();
            services.AddTransient<ICommands.IPurgeStaleWhoIsHandler, PurgeStaleWhoIs>();

            return services;
        }
    }
}
