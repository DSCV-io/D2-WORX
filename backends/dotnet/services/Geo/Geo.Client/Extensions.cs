// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client;

using D2.Geo.Client.CQRS.Handlers.C;
using D2.Geo.Client.CQRS.Handlers.Q;
using D2.Geo.Client.CQRS.Handlers.X;
using D2.Geo.Client.Interfaces.CQRS.Handlers.C;
using D2.Geo.Client.Interfaces.CQRS.Handlers.Q;
using D2.Geo.Client.Interfaces.CQRS.Handlers.X;
using D2.Geo.Client.Interfaces.Messaging.Handlers.Sub;
using D2.Geo.Client.Messaging.Consumers;
using D2.Geo.Client.Messaging.Handlers.Sub;
using D2.Services.Protos.Geo.V1;
using D2.Shared.InMemoryCache.Default;
using D2.Shared.Utilities.CircuitBreaker;
using D2.Shared.Utilities.Extensions;
using D2.Shared.Utilities.Singleflight;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for adding GeoRefDataService handlers.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// Adds GeoRefDataService handlers for consumer services.
    /// </summary>
    ///
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds GeoRefDataService consumer handlers including gRPC client.
        /// </summary>
        ///
        /// <param name="configuration">
        /// The configuration containing the Geo service URL.
        /// </param>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddGeoRefDataConsumer(IConfiguration configuration)
        {
            services.AddGrpcClient<GeoService.GeoServiceClient>(o =>
            {
                o.Address = new Uri(configuration["GeoService:Url"] ?? "http://geo-service");
            });

            services.AddTransient<IComplex.IGetHandler, Get>();
            services.AddTransient<ICommands.IReqUpdateHandler, ReqUpdate>();
            services.AddGeoRefDataShared();

            // Cross-process cache invalidation. Both BackgroundServices subscribe
            // to the matching events.geo.* fanout exchanges and evict the local
            // memory cache when the Geo service mutates data — keeps every
            // geo-client-equipped process consistent without per-service wiring.
            services.AddHostedService<UpdatedConsumerService>();
            services.AddHostedService<ContactEvictionConsumerService>();

            return services;
        }

        /// <summary>
        /// Adds GeoRefDataService provider handlers (for Geo service itself).
        /// </summary>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddGeoRefDataProvider()
        {
            services.AddTransient<ICommands.ISetInDistHandler, SetInDist>();
            services.AddGeoRefDataShared();

            return services;
        }

        /// <summary>
        /// Adds shared GeoRefDataService handlers used by both consumer and provider.
        /// </summary>
        private void AddGeoRefDataShared()
        {
            services.AddTransient<IQueries.IGetFromMemHandler, GetFromMem>();
            services.AddTransient<IQueries.IGetFromDistHandler, GetFromDist>();
            services.AddTransient<IQueries.IGetFromDiskHandler, GetFromDisk>();
            services.AddTransient<ICommands.ISetInMemHandler, SetInMem>();
            services.AddTransient<ICommands.ISetOnDiskHandler, SetOnDisk>();
            services.AddTransient<ISubs.IUpdatedHandler, Updated>();
            services.AddTransient<ISubs.IContactsEvictedHandler, ContactsEvicted>();
        }

        /// <summary>
        /// Adds WhoIs caching services with local memory cache and gRPC fallback.
        /// </summary>
        ///
        /// <param name="configuration">
        /// The configuration to read options from.
        /// </param>
        /// <param name="servicePrefix">
        /// Optional uppercase service prefix for layered configuration. When provided,
        /// shared defaults from <c>GEO_CLIENT</c> are bound first, then
        /// service-specific overrides from <c>{PREFIX}_GEO_CLIENT</c> are
        /// overlaid. For example, <c>"AUTH"</c> reads from <c>AUTH_GEO_CLIENT</c>.
        /// </param>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        /// <remarks>
        /// This method requires a gRPC client for <see cref="GeoService.GeoServiceClient"/>
        /// to be registered (e.g., via <see cref="AddGeoRefDataConsumer"/> or manually).
        /// </remarks>
        public IServiceCollection AddWhoIsCache(
            IConfiguration configuration,
            string? servicePrefix = null)
        {
            services.ConfigureGeoClientOptions(configuration, servicePrefix);
            services.AddDefaultMemoryCaching();
            services.AddGeoCircuitBreaker();
            services.AddSingleton<Singleflight>();
            services.AddTransient<IComplex.IFindWhoIsHandler, FindWhoIs>();

            return services;
        }

        /// <summary>
        /// Registers the Geo gRPC circuit breaker as a singleton.
        /// </summary>
        private void AddGeoCircuitBreaker()
        {
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<GeoClientOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<FindWhoIs>>();

                return new CircuitBreaker<FindWhoIsResponse>(
                    _ => false,
                    new CircuitBreakerOptions
                    {
                        FailureThreshold = opts.CircuitBreakerFailureThreshold,
                        CooldownDuration = opts.CircuitBreakerCooldownDuration,
                    },
                    (from, to) =>
                    {
                        if (to == CircuitState.Open)
                        {
                            LogCircuitBreakerOpened(
                                logger,
                                opts.CircuitBreakerFailureThreshold,
                                opts.CircuitBreakerCooldownDuration);
                        }
                        else if (to == CircuitState.Closed && from == CircuitState.HalfOpen)
                        {
                            LogCircuitBreakerClosed(logger);
                        }
                    });
            });
        }

        /// <summary>
        /// Adds contact handler services with local memory cache and gRPC calls.
        /// </summary>
        ///
        /// <param name="configuration">
        /// The configuration to read options from.
        /// </param>
        /// <param name="servicePrefix">
        /// Optional uppercase service prefix for layered configuration. When provided,
        /// shared defaults from <c>GEO_CLIENT</c> are bound first, then
        /// service-specific overrides from <c>{PREFIX}_GEO_CLIENT</c> are
        /// overlaid. For example, <c>"AUTH"</c> reads from <c>AUTH_GEO_CLIENT</c>.
        /// </param>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        /// <remarks>
        /// This method requires a gRPC client for <see cref="GeoService.GeoServiceClient"/>
        /// to be registered (e.g., via <see cref="AddGeoRefDataConsumer"/> or manually).
        /// </remarks>
        public IServiceCollection AddContactHandlers(
            IConfiguration configuration,
            string? servicePrefix = null)
        {
            services.ConfigureGeoClientOptions(configuration, servicePrefix);
            services.AddDefaultMemoryCaching();
            services.AddTransient<ICommands.ICreateContactsHandler, CreateContacts>();
            services.AddTransient<ICommands.IDeleteContactsByExtKeysHandler, DeleteContactsByExtKeys>();
            services.AddTransient<IQueries.IGetContactsByExtKeysHandler, GetContactsByExtKeys>();
            services.AddTransient<IComplex.IUpdateContactsByExtKeysHandler, UpdateContactsByExtKeys>();

            return services;
        }

        /// <summary>
        /// Binds <see cref="GeoClientOptions"/> using a layered approach: shared defaults
        /// from <c>GEO_CLIENT</c> first, then service-specific overrides from
        /// <c>{PREFIX}_GEO_CLIENT</c> on top.
        /// </summary>
        private void ConfigureGeoClientOptions(
            IConfiguration configuration,
            string? servicePrefix)
        {
            const string base_section_name = "GEO_CLIENT";

            // Always bind shared defaults.
            services.Configure<GeoClientOptions>(configuration.GetSection(base_section_name));

            // Overlay service-specific overrides if prefix provided.
            if (servicePrefix.Truthy())
            {
                services.Configure<GeoClientOptions>(
                    configuration.GetSection($"{servicePrefix}_{base_section_name}"));
            }
        }
    }

    /// <summary>
    /// Logs a warning when the Geo gRPC circuit breaker opens after consecutive failures.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Geo gRPC circuit breaker opened after {Threshold} consecutive failures. Will probe in {Cooldown}.")]
    private static partial void LogCircuitBreakerOpened(ILogger logger, int threshold, TimeSpan cooldown);

    /// <summary>
    /// Logs an informational message when the Geo gRPC circuit breaker closes after recovery.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Geo gRPC circuit breaker closed — service recovered.")]
    private static partial void LogCircuitBreakerClosed(ILogger logger);
}
