using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Add user secrets.
builder.Configuration.AddUserSecrets<Program>();

// Define all params to pass to containers.
var dbUsername = builder.AddParameter("db-username", true);
var dbPassword = builder.AddParameter("db-password", true);
var dbaEmail = builder.AddParameter("dba-email", true);
var dbaPassword = builder.AddParameter("dba-password", true);
var cachePassword = builder.AddParameter("cache-password", true);
var mqUsername = builder.AddParameter("mq-username", true);
var mqPassword = builder.AddParameter("mq-password", true);
var kcUsername = builder.AddParameter("kc-username", true);
var kcPassword = builder.AddParameter("kc-password", true);
var otelUser = builder.AddParameter("otel-username", true);
var otelPassword = builder.AddParameter("otel-password", true);
var s3Username = builder.AddParameter("s3-username", true);
var s3Password = builder.AddParameter("s3-password", true);

/******************************************
 ************* Object Storage *************
 ******************************************/

// MinIO - S3 Compatible Object Storage.
var minio = builder.AddContainer("d2-minio", "minio/minio", "RELEASE.2025-09-07T16-13-09Z")
    .WithIconName("ScanObject")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "minio-api", isProxied: false)
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "minio-console")
    .WithEnvironment("MINIO_ROOT_USER", s3Username)
    .WithEnvironment("MINIO_ROOT_PASSWORD", s3Password)
    .WithEnvironment("MINIO_BROWSER", "on")
    .WithVolume("d2-minio-data", "/data")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithExternalHttpEndpoints();

// MinIO Client - Used to initialize buckets.
var minioInit = builder.AddContainer("d2-minio-init", "minio/mc", "RELEASE.2025-08-13T08-35-41Z")
    .WithIconName("StarArrowRightStart")
    .WaitFor(minio)
    .WithEnvironment("MINIO_ROOT_USER", s3Username)
    .WithEnvironment("MINIO_ROOT_PASSWORD", s3Password)
    .WithEnvironment("MINIO_PROMETHEUS_AUTH_TYPE", "public")
    .WithVolume("d2-minio-tokens", "/minio-token")
    .WithEntrypoint("/bin/sh")
    .WithArgs(
        "-c",
        "mc alias set myminio http://d2-minio:9000 $MINIO_ROOT_USER $MINIO_ROOT_PASSWORD && " +
        "mc mb --ignore-existing myminio/loki-logs && " +
        "mc mb --ignore-existing myminio/tempo-traces && " +
        "mc mb --ignore-existing myminio/mimir-blocks && " +
        "mc mb --ignore-existing myminio/mimir-ruler && " +
        "mc mb --ignore-existing myminio/minio-uploads && " +
        "mc admin prometheus generate myminio > /minio-token/prometheus-config.yaml && " +
        "echo 'MinIO buckets and Prometheus token initialized successfully'"
    )
    .WithLifetime(ContainerLifetime.Session);

/******************************************
 ************** Observability *************
 ******************************************/

// Loki - Log Aggregation.
var loki = builder.AddContainer("d2-loki", "grafana/loki", "3.5.5")
    .WithIconName("DocumentText")
    .WithHttpEndpoint(port: 3100, targetPort: 3100, name: "loki-http", isProxied: false)
    .WithHttpEndpoint(port: 9095, targetPort: 9095, name: "loki-grpc", isProxied: false)
    .WithBindMount("../../observability/loki/config", "/etc/loki", isReadOnly: true)
    .WithVolume("d2-loki-data", "/loki")
    .WithArgs("-config.file=/etc/loki/loki.yaml", "-config.expand-env=true")
    .WithEnvironment("MINIO_ROOT_USER", s3Username)
    .WithEnvironment("MINIO_ROOT_PASSWORD", s3Password)
    .WaitForCompletion(minioInit)
    .WithLifetime(ContainerLifetime.Persistent);

// Tempo - Distributed Tracing.
var tempo = builder.AddContainer("d2-tempo", "grafana/tempo", "2.8.2")
    .WithIconName("Timeline")
    .WithHttpEndpoint(port: 3200, targetPort: 3200, name: "tempo-http", isProxied: false)
    .WithHttpEndpoint(port: 9096, targetPort: 9096, name: "tempo-grpc", isProxied: false)
    .WithBindMount("../../observability/tempo/config", "/etc/tempo", isReadOnly: true)
    .WithVolume("d2-tempo-data", "/var/tempo")
    .WithArgs("-config.file=/etc/tempo/tempo.yaml", "-config.expand-env=true")
    .WithEnvironment("MINIO_ROOT_USER", s3Username)
    .WithEnvironment("MINIO_ROOT_PASSWORD", s3Password)
    .WaitForCompletion(minioInit)
    .WithLifetime(ContainerLifetime.Persistent);

// Mimir - Metrics.
var mimir = builder.AddContainer("d2-mimir", "grafana/mimir", "2.17.1")
    .WithIconName("TopSpeed")
    .WithHttpEndpoint(port: 9009, targetPort: 9009, name: "mimir-http", isProxied: false)
    .WithHttpEndpoint(port: 9097, targetPort: 9097, name: "mimir-grpc", isProxied: false)
    .WithBindMount("../../observability/mimir/config", "/etc/mimir", isReadOnly: true)
    .WithVolume("d2-mimir-data", "/var/mimir")
    .WithArgs("-config.file=/etc/mimir/mimir.yaml", "-config.expand-env=true")
    .WithEnvironment("MINIO_ROOT_USER", s3Username)
    .WithEnvironment("MINIO_ROOT_PASSWORD", s3Password)
    .WaitForCompletion(minioInit)
    .WithLifetime(ContainerLifetime.Persistent);

// cAdvisor - Container Resource Monitoring.
var cAdvisor = builder.AddContainer("d2-cadvisor", "gcr.io/cadvisor/cadvisor", "v0.50.0")
    .WithIconName("ChartMultiple")
    .WithHttpEndpoint(port: 8081, targetPort: 8080, name: "cadvisor-http", isProxied: false)
    .WithBindMount("/", "/rootfs", isReadOnly: true)
    .WithBindMount("/var/run", "/var/run", isReadOnly: true)
    .WithBindMount("/sys", "/sys", isReadOnly: true)
    .WithBindMount("/var/lib/docker", "/var/lib/docker", isReadOnly: true)
    .WithArgs(
        "--housekeeping_interval=10s",
        "--docker_only=true"
    )
    .WithLifetime(ContainerLifetime.Persistent);

// Grafana Alloy - Unified Agent for Metrics, Logs and Traces.
var grafanaAlloy = builder.AddContainer("d2-grafana-alloy", "grafana/alloy", "v1.11.0")
    .WithIconName("Agents")
    .WithHttpEndpoint(port: 12345, targetPort: 12345, name: "alloy-http", isProxied: false)
    .WithHttpEndpoint(port: 4317, targetPort: 4317, name: "otlp-grpc", isProxied: false)
    .WithHttpEndpoint(port: 4318, targetPort: 4318, name: "otlp-http", isProxied: false)
    .WithEnvironment("ALLOY_DEPLOY_MODE", "docker")
    .WithEnvironment("MINIO_ROOT_USER", s3Username)
    .WithEnvironment("MINIO_ROOT_PASSWORD", s3Password)
    .WithBindMount("/proc", "/rootproc", isReadOnly: true)
    .WithBindMount("/sys", "/sys", isReadOnly: true)
    .WithBindMount("/", "/rootfs", isReadOnly: true)
    .WithBindMount("/var/lib/docker", "/var/lib/docker", isReadOnly: true)
    .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock", isReadOnly: true)
    .WithBindMount("../../observability/alloy/config", "/etc/alloy", isReadOnly: true)
    .WithVolume("d2-alloy-data", "/var/lib/alloy/data")
    .WithVolume("d2-minio-tokens", "/minio-token", isReadOnly: true)
    .WithArgs(
        "run",
        "/etc/alloy/config.alloy",
        "--server.http.listen-addr=0.0.0.0:12345",
        "--stability.level=generally-available"
    )
    .WaitFor(cAdvisor)
    .WaitFor(mimir)
    .WaitFor(loki)
    .WaitFor(tempo)
    .WaitForCompletion(minioInit)
    .WithLifetime(ContainerLifetime.Persistent);

// Grafana - Visualization.
var grafana = builder.AddContainer("d2-grafana", "grafana/grafana", "12.2.0")
    .WithIconName("ChartPerson")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "grafana")
    .WithBindMount("../../observability/grafana/provisioning", "/etc/grafana/provisioning",
        isReadOnly: true)
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
    .WaitFor(mimir);

/******************************************
 ************* Infrastructure *************
 ******************************************/

// PostgreSQL - Relational Database.
var db = builder.AddPostgres(
        "d2-postgres",
        dbUsername,
        dbPassword,
        5532) // Not using 5432 to avoid conflicts for local dev (when PG is installed locally).
    .WithIconName("DatabaseStack")
    .WithImageTag("18.0-trixie")
    .WithDataVolume("d2-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(x =>
    {
        x.WithHostPort(5533);
        x.WithIconName("DatabasePerson");
        x.WithContainerName("d2-pgadmin4");
        x.WithImageTag("9.8.0");
        x.WithLifetime(ContainerLifetime.Persistent);
        x.WithEnvironment("PGADMIN_DEFAULT_EMAIL", dbaEmail);
        x.WithEnvironment("PGADMIN_DEFAULT_PASSWORD", dbaPassword);
        x.WithEnvironment("PGADMIN_CONFIG_MASTER_PASSWORD_REQUIRED", "True");
        x.WithEnvironment("PGADMIN_CONFIG_ENHANCED_COOKIE_PROTECTION", "True");
    });

// Postgres Exporter - PostgreSQL Server Monitoring.
var postgresExporter = builder.AddContainer(
        "d2-postgres-exporter", "prometheuscommunity/postgres-exporter", "v0.18.1")
    .WithIconName("DatabasePlugConnected")
    .WithEnvironment(
        "DATA_SOURCE_NAME",
        $"postgresql://{dbUsername}:{dbPassword}@d2-postgres:5432/postgres?sslmode=disable")
    .WithHttpEndpoint(port: 9187, targetPort: 9187, name: "metrics", isProxied: false)
    .WaitFor(db)
    .WithLifetime(ContainerLifetime.Persistent);

// Redis - Cache.
var cache = builder.AddRedis("d2-redis", 6379, cachePassword)
    .WithIconName("Memory")
    .WithImageTag("8.2.1-bookworm")
    .WithDataVolume("d2-redis-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithRedisInsight(x =>
    {
        x.WithHostPort(5540);
        x.WithIconName("BookSearch");
        x.WithContainerName("d2-redisinsight");
        x.WithImageTag("2.70.1");
        x.WithDataVolume("d2-redisinsight-data");
        x.WithLifetime(ContainerLifetime.Persistent);
    });

// Redis Exporter - Redis Monitoring.
var redisExporter = builder.AddContainer(
        "d2-redis-exporter", "oliver006/redis_exporter", "v1.78.0")
    .WithIconName("ChartLine")
    .WithEnvironment("REDIS_ADDR", "d2-redis:6379")
    .WithEnvironment("REDIS_PASSWORD", cachePassword)
    .WithHttpEndpoint(port: 9121, targetPort: 9121, name: "metrics", isProxied: false)
    .WaitFor(cache)
    .WithLifetime(ContainerLifetime.Persistent);

// RabbitMQ - Message Broker.
var broker = builder.AddRabbitMQ("d2-rabbitmq", mqUsername, mqPassword, 15672)
    .WithIconName("Mailbox")
    .WithImageTag("4.1.4-management")
    .WithDataVolume("d2-rabbitmq-data")
    .WithHttpEndpoint(port: 15692, targetPort: 15692, name: "metrics", isProxied: false)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithManagementPlugin();

// Create the keycloak database.
const string kc_pg_db_name = "keycloak";
db.AddDatabase(kc_pg_db_name);

// Keycloak - Identity and Access Management.
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
    .WithEnvironment("KC_DB_PASSWORD", dbPassword)
    .WithEnvironment("KC_METRICS_ENABLED", "true")
    .WithEnvironment("KC_HTTP_METRICS_HISTOGRAMS_ENABLED", "true")
    .WithEnvironment("KC_HTTP_METRICS_SLOS", "5,10,25,50,250,500,1000,2500,5000,10000")
    .WithEnvironment("KC_FEATURES", "opentelemetry,user-event-metrics,organization")
    .WithEnvironment("KC_EVENT_METRICS_USER_ENABLED", "true")
    .WithEnvironment("KC_EVENT_METRICS_USER_EVENTS", "authreqid_to_token,client_delete,client_info,client_initiated_account_linking,client_login,client_register,client_update,code_to_token,custom_required_action,delete_account,execute_action_token,execute_actions,federated_identity_link,federated_identity_override_link,grant_consent,identity_provider_first_login,identity_provider_link_account,identity_provider_login,identity_provider_post_login,identity_provider_response,identity_provider_retrieve_token,impersonate,introspect_token,invalid_signature,invite_org,login,logout,oauth2_device_auth,oauth2_device_code_to_token,oauth2_device_verify_user_code,oauth2_extension_grant,permission_token,pushed_authorization_request,refresh_token,register,register_node,remove_credential,remove_federated_identity,reset_password,restart_authentication,revoke_grant,send_identity_provider_link,send_reset_password,send_verify_email,token_exchange,unregister_node,update_consent,update_credential,update_email,update_profile,user_disabled_by_permanent_lockout,user_disabled_by_temporary_lockout,user_info_request,verify_email,verify_profile")
    .WithEnvironment("KC_EVENT_METRICS_USER_TAGS", "realm,idp,clientId");

/******************************************
 **************** Services ****************
 ******************************************/

// Auth - Service.
var authService = builder.AddProject<Projects.Auth_API>("d2-auth")
    .WithIconName("PersonAccounts")
    .DefaultInfraRefs(db, cache, broker, keycloak)
    .WithOtelRefs();

// REST API - Gateway.
var restGateway = builder.AddProject<Projects.REST>("d2-rest")
    .WithIconName("Globe")
    // Services that the REST API depends on.
    .WaitFor(authService)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithOtelRefs();

// SvelteKit - Frontend.
var svelte = builder.AddViteApp("sveltekit",
        workingDirectory: "../../frontends/sveltekit",
        packageManager: "pnpm")
    .WaitFor(restGateway)
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
    /// Adds observability environment variables for traces and logs.
    /// </summary>
    /// <param name="builder">The resource builder for the resource.</param>
    /// <typeparam name="TProject">A type that represents the project reference.</typeparam>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TProject> WithOtelRefs<TProject>(
        this IResourceBuilder<TProject> builder)
        where TProject : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        builder.WithEnvironment("OTEL_SERVICE_NAME", builder.Resource.Name);
        builder.WithEnvironment("TRACES_URI", "http://localhost:4318/v1/traces");
        builder.WithEnvironment("LOGS_URI", "http://localhost:3100");
        return builder;
    }
}
