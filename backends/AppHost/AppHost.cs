using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Add user secrets.
builder.Configuration.AddUserSecrets<Program>();

// Define all params to pass to containers.
var dbUsername = builder.AddParameter("db-username");
var dbPassword = builder.AddParameter("db-password", true);
var dbaEmail = builder.AddParameter("dba-email");
var dbaPassword = builder.AddParameter("dba-password", true);
var cachePassword = builder.AddParameter("cache-password", true);
var mqUsername = builder.AddParameter("mq-username");
var mqPassword = builder.AddParameter("mq-password", true);
var kcUsername = builder.AddParameter("kc-username");
var kcPassword = builder.AddParameter("kc-password", true);
var otelUser = builder.AddParameter("otel-username");
var otelPassword = builder.AddParameter("otel-password", true);

/******************************************
 ************** Observability *************
 ******************************************/

// Loki (Log Aggregation) - Internal only
var loki = builder.AddContainer("d2-loki", "grafana/loki", "3.5.5")
    .WithIconName("DocumentText")
    .WithHttpEndpoint(port: 3100, targetPort: 3100, name: "loki-http", isProxied: false)
    .WithHttpEndpoint(port: 9095, targetPort: 9095, name: "loki-grpc", isProxied: false)
    .WithBindMount("../../observability/loki/config", "/etc/loki", isReadOnly: true)
    .WithVolume("d2-loki-data", "/loki")
    .WithArgs("-config.file=/etc/loki/loki.yaml")
    .WithLifetime(ContainerLifetime.Persistent);

// Tempo (Distributed Tracing) - Internal only
var tempo = builder.AddContainer("d2-tempo", "grafana/tempo", "2.8.2")
    .WithIconName("Timeline")
    .WithHttpEndpoint(port: 3200, targetPort: 3200, name: "tempo-http", isProxied: false)
    .WithHttpEndpoint(port: 9096, targetPort: 9096, name: "tempo-grpc", isProxied: false)
    .WithHttpEndpoint(port: 4317, targetPort: 4317, name: "tempo-otlp-grpc", isProxied: false)
    .WithHttpEndpoint(port: 4318, targetPort: 4318, name: "tempo-otlp-http", isProxied: false)
    .WithBindMount("../../observability/tempo/config", "/etc/tempo", isReadOnly: true)
    .WithVolume("d2-tempo-data", "/var/tempo")
    .WithArgs("-config.file=/etc/tempo/tempo.yaml")
    .WithLifetime(ContainerLifetime.Persistent);

// Mimir (Metrics) - Internal only
var mimir = builder.AddContainer("d2-mimir", "grafana/mimir", "2.17.1")
    .WithIconName("TopSpeed")
    .WithHttpEndpoint(port: 9009, targetPort: 9009, name: "mimir-http", isProxied: false)
    .WithHttpEndpoint(port: 9097, targetPort: 9097, name: "mimir-grpc", isProxied: false)
    .WithBindMount("../../observability/mimir/config", "/etc/mimir", isReadOnly: true)
    .WithVolume("d2-mimir-data", "/var/mimir")
    .WithArgs("-config.file=/etc/mimir/mimir.yaml")
    .WithLifetime(ContainerLifetime.Persistent);

// Prometheus (Metrics Scraping) - Internal only
var prometheus = builder.AddContainer("d2-prometheus", "prom/prometheus", "v3.6.0")
    .WithIconName("Fireplace")
    .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "prometheus-http", isProxied: false)
    .WithBindMount("../../observability/prometheus/config", "/etc/prometheus", isReadOnly: true)
    .WithVolume("d2-prometheus-data", "/prometheus")
    .WithArgs(
        "--config.file=/etc/prometheus/prometheus.yaml",
        "--web.config.file=/etc/prometheus/web.yaml",
        "--storage.tsdb.path=/prometheus",
        "--storage.tsdb.retention.time=15d",
        "--web.console.libraries=/etc/prometheus/console_libraries",
        "--web.console.templates=/etc/prometheus/consoles",
        "--web.enable-lifecycle",
        "--enable-feature=exemplar-storage"
    )
    .WithLifetime(ContainerLifetime.Persistent);

// Grafana (Visualization) - REQUIRES LOGIN
var grafana = builder.AddContainer("d2-grafana", "grafana/grafana", "12.2.0")
    .WithIconName("ChartMultiple")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "grafana")
    .WithBindMount("../../observability/grafana/provisioning", "/etc/grafana/provisioning", isReadOnly: true)
    .WithVolume("d2-grafana-data", "/var/grafana")
    // Security - Require authentication.
    .WithEnvironment("GF_SECURITY_ADMIN_USER", otelUser)
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", otelPassword)
    .WithEnvironment("GF_AUTH_ANONYMOUS_ENABLED", "false")
    .WithEnvironment("GF_AUTH_BASIC_ENABLED", "true")
    .WithEnvironment("GF_USERS_ALLOW_SIGN_UP", "false")
    .WithEnvironment("GF_USERS_ALLOW_ORG_CREATE", "false")
    .WithEnvironment("GF_SNAPSHOTS_EXTERNAL_ENABLED", "false")
    // Security - Defaults for DEV ONLY - should be true, strict and true for PROD.
    .WithEnvironment("GF_SECURITY_COOKIE_SECURE", "false")
    .WithEnvironment("GF_SECURITY_COOKIE_SAMESITE", "lax")
    .WithEnvironment("GF_SECURITY_STRICT_TRANSPORT_SECURITY", "false")
    // Features.
    .WithEnvironment("GF_FEATURE_TOGGLES_ENABLE", "traceqlEditor")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithExternalHttpEndpoints()
    // Wait for dependencies so that provisioning works.
    .WaitFor(tempo)
    .WaitFor(loki)
    .WaitFor(mimir)
    .WaitFor(prometheus);

/******************************************
 ************* Infrastructure *************
 ******************************************/

// PostgreSQL (database).
var db = builder.AddPostgres("d2-postgres", dbUsername, dbPassword)
    .WithIconName("DatabaseStack")
    .WithImageTag("18.0-trixie")
    .WithDataVolume("d2-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(x =>
    {
        x.WithIconName("DatabasePerson");
        x.WithContainerName("d2-pgadmin4");
        x.WithImageTag("9.8.0");
        x.WithLifetime(ContainerLifetime.Persistent);
        x.WithEnvironment("PGADMIN_DEFAULT_EMAIL", dbaEmail);
        x.WithEnvironment("PGADMIN_DEFAULT_PASSWORD", dbaPassword);
        x.WithEnvironment("PGADMIN_CONFIG_MASTER_PASSWORD_REQUIRED", "True");
        x.WithEnvironment("PGADMIN_CONFIG_ENHANCED_COOKIE_PROTECTION", "True");
    });

// Redis (cache).
var cache = builder.AddRedis("d2-redis", null, cachePassword)
    .WithIconName("Memory")
    .WithImageTag("8.2.1-bookworm")
    .WithDataVolume("d2-redis-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithRedisInsight(x =>
    {
        x.WithIconName("BookSearch");
        x.WithContainerName("d2-redisinsight");
        x.WithImageTag("2.70.1");
        x.WithDataVolume("d2-redisinsight-data");
        x.WithLifetime(ContainerLifetime.Persistent);
    });

// RabbitMQ (message broker).
var broker = builder.AddRabbitMQ("d2-rabbitmq", mqUsername, mqPassword)
    .WithIconName("Mailbox")
    .WithImageTag("4.1.4-management")
    .WithDataVolume("d2-rabbitmq-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithManagementPlugin();

// Create the keycloak database.
const string kc_pg_db_name = "keycloak";
db.AddDatabase(kc_pg_db_name);

// Add keycloak.
var keycloak = builder.AddKeycloak("d2-keycloak", 8080, kcUsername, kcPassword)
    .WithIconName("LockClosedKey")
    .WaitFor(db)
    .WithImageTag("26.4.0")
    .WithDataVolume("d2-keycloak-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL_DATABASE", kc_pg_db_name)
    .WithEnvironment("KC_DB_URL_HOST", "d2-postgres")
    .WithEnvironment("KC_DB_URL_PORT", "5432")
    .WithEnvironment("KC_DB_USERNAME", dbUsername)
    .WithEnvironment("KC_DB_PASSWORD", dbPassword);

/******************************************
 **************** Services ****************
 ******************************************/

// Auth service.
var authService = builder.AddProject<Projects.Auth_API>("d2-auth")
    .WithIconName("PersonAccounts")
    .DefaultInfraRefs(db, cache, broker, keycloak)
    .WithOtelRefs();

// REST API gateway.
var rest = builder.AddProject<Projects.REST>("d2-rest")
    .WithIconName("Globe")
    // Services that the REST API depends on.
    .WaitFor(authService)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithOtelRefs();

// SvelteKit frontend.
var svelte = builder.AddViteApp("sveltekit",
        workingDirectory: "../../frontends/sveltekit",
        packageManager: "pnpm")
    .WaitFor(rest)
    .WithPnpmPackageInstallation()
    .WithArgs("--host", "0.0.0.0", "--port", "5173")
    .WithIconName("DesktopCursor")
    .WithExternalHttpEndpoints();

builder.Build().Run();

/// <summary>
/// Extends the ResourceBuilder.
/// </summary>
internal static class ServiceExtensions
{
    /// <summary>
    /// Adds references and wait conditions for the default infrastructure services.
    /// </summary>
    /// <param name="builder">The resource builder for the resource.</param>
    /// <param name="db">The resource builder for the PostgreSQL database.</param>
    /// <param name="cache">The resource builder for the Redis cache.</param>
    /// <param name="broker">The resource builder for the RabbitMQ message broker.</param>
    /// <param name="keycloak">The resource builder for keycloak.</param>
    /// <typeparam name="TProject">A type that represents the project reference.</typeparam>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TProject> DefaultInfraRefs<TProject>(
            this IResourceBuilder<TProject> builder,
            IResourceBuilder<PostgresServerResource> db,
            IResourceBuilder<RedisResource> cache,
            IResourceBuilder<RabbitMQServerResource> broker,
            IResourceBuilder<KeycloakResource> keycloak)
        where TProject : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        builder.WithReference(db);
        builder.WaitFor(db);
        builder.WithReference(cache);
        builder.WaitFor(cache);
        builder.WithReference(broker);
        builder.WaitFor(broker);
        builder.WithReference(keycloak);
        builder.WaitFor(keycloak);
        return builder;
    }

    /// <summary>
    /// Adds observability environment variables for tempo and loki.
    /// </summary>
    /// <param name="builder">The resource builder for the resource.</param>
    /// <typeparam name="TProject">A type that represents the project reference.</typeparam>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TProject> WithOtelRefs<TProject>(
        this IResourceBuilder<TProject> builder)
        where TProject : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        builder.WithEnvironment("OTEL_SERVICE_NAME", builder.Resource.Name);
        builder.WithEnvironment("TEMPO_URI", "http://localhost:4318/v1/traces");
        builder.WithEnvironment("LOKI_URI", "http://localhost:3100");
        return builder;
    }
}
