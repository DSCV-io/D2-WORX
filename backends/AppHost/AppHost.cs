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

// PostgreSQL (database).
var db = builder.AddPostgres("d2-postgres", dbUsername, dbPassword)
    .WithImageTag("18.0-trixie")
    .WithPgAdmin()
    .WithDataVolume("d2-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

// Redis (cache).
var cache = builder.AddRedis("d2-redis", null, cachePassword)
    .WithImageTag("8.2.1-bookworm")
    .WithDataVolume("d2-redis-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithRedisInsight();

// RabbitMQ (message broker).
var broker = builder.AddRabbitMQ("d2-rabbitmq", mqUsername, mqPassword)
    .WithImageTag("4.1.4-management")
    .WithDataVolume("d2-rabbitmq-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithManagementPlugin();

// Auth service.
var authService = builder.AddProject<Projects.Auth_API>("auth")
    .WithReference(db)
    .WaitFor(db)
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(broker)
    .WaitFor(broker);

// REST API gateway.
var rest = builder.AddProject<Projects.REST>("REST")
    // Services that the REST API depends on.
    .WaitFor(authService)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
