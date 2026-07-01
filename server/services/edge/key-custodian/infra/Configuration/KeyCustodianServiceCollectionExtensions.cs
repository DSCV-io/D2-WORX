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
using D2.Shared.Auth.Abstractions;
using D2.Shared.Context.Abstractions;
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
        /// <remarks>
        /// <b>Host prerequisite:</b> the host MUST bind
        /// <see cref="D2WorkloadIdentityOptions"/> (its own workload
        /// <c>ServiceId</c>) — the module's establishment-boundary registration owns
        /// the bind. This method does not re-bind it; it registers a fail-loud
        /// presence gate (an unset <c>ServiceId</c> fails <c>ValidateOnStart</c>)
        /// because the CA-seeding and key-rotation System workers establish their
        /// <c>RequestOrigin.System</c> request context from that self-id.
        /// </remarks>
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

            // Signing-domain authority policy — bound + fail-loud validated. The
            // validator REFUSES to boot a dangerous configuration (it rejects VALUES,
            // not just shape): no workload may be granted an in-process-only domain
            // (jwks-signing), no key may be empty, no value may name a non-catalog
            // domain. An EMPTY policy is legitimately fine (deny-all). This converts
            // the jwks-signing-Edge-only control from operator-discipline
            // to "the host won't start misconfigured" (defense-in-depth alongside the
            // structural authority-rule deny).
            services.AddOptions<SigningDomainAuthorityOptions>()
                .Bind(configuration.GetSection(SigningDomainAuthorityOptions.SECTION))
                .Validate(
                    static o => o.Validate() is null,
                    "KEYCUSTODIAN_SIGNING_AUTHORITY is misconfigured. See the host log "
                    + "for the specific invariant violated (in-process-only-domain grant, "
                    + "empty workload key, or non-catalog signing domain).")
                .ValidateOnStart();

            // Host workload identity — presence + validity gate (fail-loud). The host OWNS
            // the bind (its establishment-boundary registration binds
            // D2WorkloadIdentityOptions.ServiceId); this module does NOT re-bind it — it
            // only VALIDATES presence, because the System workers below (CaSeedingService /
            // KeyRotationService) establish their System request context from that self-id.
            // An unset ServiceId is a host misconfiguration: rather than silently seeding +
            // rotating under an empty self-id (fail-late, message-stripped), the host refuses
            // to start. The validator composes with the host's own bind/validation.
            services.AddOptions<D2WorkloadIdentityOptions>()
                .Validate(
                    static o => !o.ServiceId.Falsey(),
                    "D2WorkloadIdentityOptions.ServiceId is unset. The KeyCustodian System "
                    + "workers (CA seeding + key rotation) establish their System request "
                    + "context from the host's workload id; the host must bind it before "
                    + "AddD2KeyCustodian (its establishment-boundary registration does).")
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

            // --- Request context: scoped resolver for System-worker scopes -------
            // IRequestContext is normally a HOST responsibility (Edge's
            // AddD2AuthGrpc()/AddD2AuthHttp() register a throwing-by-default scoped
            // resolver keyed off the inbound HttpContext). A System-worker scope
            // created via IServiceScopeFactory.CreateAsyncScope() (CaSeedingService /
            // KeyRotationService) has no HttpContext, so that throwing resolver is the
            // WRONG one here — the module registers its own plain scoped resolver
            // (TryAdd: a host-registered resolver, if present, wins) so the workers can
            // establish + resolve a System request context on their own scope.
            services.TryAddScoped<MutableRequestContext>();
            services.TryAddScoped<IRequestContext>(
                sp => sp.GetRequiredService<MutableRequestContext>());

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
