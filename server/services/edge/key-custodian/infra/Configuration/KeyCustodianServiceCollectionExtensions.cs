// -----------------------------------------------------------------------
// <copyright file="KeyCustodianServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Configuration;

using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.Infra.Messaging.RabbitMq;
using D2.Edge.KeyCustodian.Infra.Observability;
using D2.Edge.KeyCustodian.Infra.Persistence.Postgres;
using D2.Edge.KeyCustodian.Infra.Scheduling.Hosted;
using D2.Edge.KeyCustodian.Infra.Vault.File;
using D2.Shared.EntityFrameworkCore.Postgres;
using D2.Shared.Handler.Repo.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using IClock = D2.Shared.Time.IClock;
using SystemClock = D2.Shared.Time.SystemClock;

/// <summary>
/// The KeyCustodian module composition seam — registers the whole infra adapter
/// layer (persistence, vault, messaging, scheduling, health) and chains the App
/// layer.
/// </summary>
public static class KeyCustodianServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the entire KeyCustodian module: bound + start-validated
        /// options, the PostgreSQL <c>DbContext</c> (no retry strategy), the keyed
        /// root crypto, the file-backed root-key provider, the RabbitMQ announcer,
        /// the startup migrator and the rotation scheduler (in that order), the
        /// readiness health checks, and the chained App layer.
        /// </summary>
        /// <param name="configuration">The configuration root to bind options from.</param>
        /// <param name="connectionString">
        /// The <c>keycustodian_db</c> connection string (from
        /// <c>KEYCUSTODIAN_DATABASE_URL</c>). Used by the DbContext and the
        /// rotation/migration advisory-lock connections.
        /// </param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        public IServiceCollection AddD2KeyCustodian(
            IConfiguration configuration, string connectionString)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            connectionString.ThrowIfFalsey();

            // --- Options: bind + validate-on-start (both POCOs) ------------------
            services.AddOptions<KeyCustodianOptions>()
                .Bind(configuration.GetSection(KeyCustodianOptions.SECTION))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<KeyCustodianInfraOptions>()
                .Bind(configuration.GetSection(KeyCustodianInfraOptions.SECTION))
                .Configure(o => o.ConnectionString = connectionString)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // --- Persistence: plain scoped DbContext, shared Npgsql defaults -----
            // No EnableRetryOnFailure (an execution-strategy reconnect would drop a
            // session advisory lock mid-critical-section).
            var commandTimeout = configuration
                .GetSection(KeyCustodianInfraOptions.SECTION)
                .GetValue(
                    "DbCommandTimeoutSeconds",
                    KeyCustodianInfraOptions.DEFAULT_DB_COMMAND_TIMEOUT_SECONDS);

            var migrationsAssembly =
                typeof(KeyCustodianDbContext).Assembly.GetName().Name!;

            services.AddDbContext<KeyCustodianDbContext>(opts =>
                opts.ApplyD2NpgsqlDefaults(
                    connectionString, commandTimeout, migrationsAssembly));

            services.AddScoped<IKeyCustodianDbContext>(
                sp => sp.GetRequiredService<KeyCustodianDbContext>());

            // IDbExceptionClassifier (PostgreSQL) for BaseRepoHandler.
            services.AddD2Postgres();

            // IClock — the handlers + rotation math need it. TryAdd so a host that
            // already registered a clock (or a test's TestClock) wins.
            services.TryAddSingleton<IClock, SystemClock>();

            // --- Vault: file-backed root key provider + keyed root crypto --------
            services.AddSingleton<IRootKeyProvider, FileRootKeyProvider>();

            // File-backed CA provider — loads + chain-validates the dev CA chain
            // from the same directory; consumed by the startup seeder.
            services.AddSingleton<ICaProvider, FileCaProvider>();

            services.AddD2EncryptionFor(
                KeyCustodianRootKey.ROOT_SERVICE_KEY,
                sp => sp.GetRequiredService<IRootKeyProvider>().GetRootKeyring());

            services.AddD2EncryptionStartupCheck();

            // --- Messaging: RabbitMQ rotation announcer --------------------------
            services.AddSingleton<IKeyRotationAnnouncer, RabbitMqKeyRotationAnnouncer>();

            // --- Hosted services: migrator → seeder → rotation (order matters) ---
            // Same-host StartAsync ordering is registration order, pinned by a
            // composition-order test. Migration must apply before anything reads the
            // schema; the CA must be seeded before the first rotation tick classifies
            // the CA domains; rotation runs last.
            services.AddHostedService(sp => new AdvisoryLockMigrator<KeyCustodianDbContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                connectionString,
                AdvisoryLocks.KeycustodianDb.MIGRATOR,
                sp.GetRequiredService<ILogger<AdvisoryLockMigrator<KeyCustodianDbContext>>>()));

            services.AddHostedService<CaSeedingService>();

            services.AddHostedService<KeyRotationService>();

            // --- Health: DB connectivity (production path) + KC readiness --------
            // The module registers its readiness checks; the host registers the
            // liveness "self" check. DB connectivity uses the framework
            // AddDbContextCheck (production path) so the probe runs the real
            // CanConnectAsync, not a hand-rolled query.
            services.AddHealthChecks()
                .AddDbContextCheck<KeyCustodianDbContext>(
                    name: _DB_HEALTH_CHECK_NAME,
                    tags: [KeyCustodianHealthTags.READY])
                .AddCheck<KeyCustodianHealthCheck>(
                    name: _READINESS_HEALTH_CHECK_NAME,
                    tags: [KeyCustodianHealthTags.READY]);

            // --- App layer -------------------------------------------------------
            services.AddD2KeyCustodianApp();

            return services;
        }
    }

    /// <summary>Health-check name for the database connectivity probe.</summary>
    private const string _DB_HEALTH_CHECK_NAME = "keycustodian-db";

    /// <summary>Health-check name for the KeyCustodian readiness probe.</summary>
    private const string _READINESS_HEALTH_CHECK_NAME = "keycustodian";
}
