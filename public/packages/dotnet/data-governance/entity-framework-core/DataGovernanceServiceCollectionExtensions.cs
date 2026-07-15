// -----------------------------------------------------------------------
// <copyright file="DataGovernanceServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System;
using DcsvIo.D2.DataGovernance.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// DI registration extension for the GDPR anonymization engine, its configuration options,
/// and the deny-by-default startup model validator.
/// </summary>
public static class DataGovernanceServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the anonymization engine, binds <see cref="AnonymizationEngineOptions"/>
        /// from the <c>DATA_GOVERNANCE</c> configuration section, and installs the deny-by-default
        /// startup model validator.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>What is registered:</strong>
        /// <list type="bullet">
        ///   <item>
        ///     <see cref="IAnonymizationEngine"/> → <c>AnonymizationEngine</c> (scoped —
        ///     matches the scoped <see cref="Microsoft.EntityFrameworkCore.DbContext"/> it
        ///     depends on).
        ///   </item>
        ///   <item>
        ///     <see cref="AnonymizationEngineOptions"/> bound from the
        ///     <see cref="AnonymizationEngineOptions.SECTION_NAME"/> configuration section.
        ///   </item>
        ///   <item>
        ///     <see cref="AnonymizationModelValidator"/> as a singleton
        ///     <see cref="IHostedService"/>.
        ///   </item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>DbContext requirement:</strong> the engine and validator resolve
        /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/> (the non-generic base type) from
        /// a created scope. The host must register its concrete context as the non-generic
        /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>, either via the standard
        /// <c>AddDbContext&lt;T&gt;()</c> overload (which registers both <c>T</c> and
        /// <c>DbContext</c> by default) or with an explicit
        /// <c>services.AddScoped&lt;DbContext&gt;(sp =&gt; sp.GetRequiredService&lt;T&gt;())</c>.
        /// </para>
        /// <para>
        /// <strong>Idempotent:</strong> double-calling this extension does not register
        /// duplicate engine or validator instances. Repeated option binding is also safe
        /// (last-writer wins for <c>Configure</c>; the section-binding applies once).
        /// </para>
        /// <para>
        /// <strong>Opt-out:</strong> set <c>DATA_GOVERNANCE__SKIPMODELVALIDATION=true</c>
        /// (or <see cref="AnonymizationEngineOptions.SkipModelValidation"/> =
        /// <see langword="true"/> via the configure-callback overload) to disable the startup
        /// guard. Intended only for
        /// test hosts with intentionally incomplete models.
        /// </para>
        /// </remarks>
        /// <param name="configuration">
        /// The application configuration. Used to bind the <c>DATA_GOVERNANCE</c> section.
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is
        /// <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddD2DataGovernance(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<AnonymizationEngineOptions>()
                .Configure(opts =>
                    configuration
                        .GetSection(AnonymizationEngineOptions.SECTION_NAME)
                        .Bind(opts));

            services.TryAddScoped<IAnonymizationEngine, AnonymizationEngine>();

            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, AnonymizationModelValidator>());

            return services;
        }

        /// <summary>
        /// Registers the anonymization engine, binds <see cref="AnonymizationEngineOptions"/>
        /// from the <c>DATA_GOVERNANCE</c> configuration section, applies a caller-supplied
        /// configuration callback, and installs the deny-by-default startup model validator.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <paramref name="configure"/> callback is applied via
        /// <c>PostConfigure</c> so it runs after the section binding and wins over any
        /// configuration-source value for the same property. Use this overload to override
        /// individual options (e.g. set
        /// <see cref="AnonymizationEngineOptions.SkipModelValidation"/> =
        /// <see langword="true"/> in a test host without changing the configuration source).
        /// </para>
        /// </remarks>
        /// <param name="configuration">
        /// The application configuration. Used to bind the <c>DATA_GOVERNANCE</c> section.
        /// </param>
        /// <param name="configure">
        /// A callback applied after section binding. The callback's settings win over any
        /// configuration-source value for the same property.
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/>, <paramref name="configuration"/>, or
        /// <paramref name="configure"/> is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddD2DataGovernance(
            IConfiguration configuration,
            Action<AnonymizationEngineOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(configure);

            services.AddD2DataGovernance(configuration);
            services.PostConfigure(configure);

            return services;
        }
    }
}
