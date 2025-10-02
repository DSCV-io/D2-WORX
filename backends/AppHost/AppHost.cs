using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Add user secrets.
builder.Configuration.AddUserSecrets<Program>();

// Define all params to pass to containers.
var dbUsername = builder.AddParameter("db-username");
var dbPassword = builder.AddParameter("db-password", true);
var cachePassword = builder.AddParameter("cache-password", true);
var mqUsername = builder.AddParameter("mq-username");
var mqPassword = builder.AddParameter("mq-password", true);
var kcUsername = builder.AddParameter("kc-username");
var kcPassword = builder.AddParameter("kc-password", true);

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
var keycloak = builder.AddKeycloak("d2-keycloak", null, kcUsername, kcPassword)
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

// Auth service.
var authService = builder.AddProject<Projects.Auth_API>("d2-auth")
    .WithIconName("PersonAccounts")
    .DefaultInfraRefs(db, cache, broker, keycloak);

// REST API gateway.
var rest = builder.AddProject<Projects.REST>("d2-rest")
    .WithIconName("Globe")
    // Services that the REST API depends on.
    .WaitFor(authService)
    .WithHttpHealthCheck("/health");

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
}
