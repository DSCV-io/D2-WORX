// -----------------------------------------------------------------------
// <copyright file="ConnectionStringHelper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Configuration;

using D2.Shared.Utilities.Extensions;

/// <summary>
/// Resolves infrastructure connection strings from standard env vars
/// (<c>REDIS_URL</c>, <c>*_DATABASE_URL</c>, <c>RABBITMQ_URL</c>).
/// </summary>
/// <remarks>
/// <para>
/// Env vars use standard URI formats (understood by Node.js clients and prod
/// platforms). .NET clients need a different wire format (StackExchange for
/// Redis, ADO.NET for Npgsql). The <c>Parse*Uri</c> methods handle the
/// conversion and pass through values already in .NET format.
/// </para>
/// </remarks>
public static class ConnectionStringHelper
{
    /// <summary>
    /// Converts a <c>redis://:password@host:port</c> URI to StackExchange.Redis
    /// format (<c>host:port,password=password</c>). Passes through values that
    /// do not start with <c>redis://</c>.
    /// </summary>
    /// <param name="value">A Redis connection string or URI.</param>
    /// <returns>A StackExchange.Redis-compatible connection string.</returns>
    public static string ParseRedisUri(string value)
    {
        if (!value.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var hostPort = $"{uri.Host}:{(uri.Port > 0 ? uri.Port : 6379)}";

        // URI userinfo: redis://:password@host or redis://user:password@host
        var password = Uri.UnescapeDataString(
            uri.UserInfo.Contains(':')
                ? uri.UserInfo[(uri.UserInfo.IndexOf(':') + 1)..]
                : uri.UserInfo);

        return password.Falsey()
            ? hostPort
            : $"{hostPort},password={password}";
    }

    /// <summary>
    /// Converts a <c>postgresql://user:pass@host:port/db</c> URI to Npgsql
    /// ADO.NET format (<c>Host=…;Port=…;Username=…;Password=…;Database=…</c>).
    /// Passes through values that do not start with <c>postgresql://</c> or
    /// <c>postgres://</c>.
    /// </summary>
    /// <param name="value">A PostgreSQL connection string or URI.</param>
    /// <returns>An Npgsql-compatible ADO.NET connection string.</returns>
    public static string ParsePostgresUri(string value)
    {
        if (!value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var database = uri.AbsolutePath.TrimStart('/');

        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            return string.Join(
                ";",
                $"Host={uri.Host}",
                $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
                $"Database={database}");
        }

        var parts = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(parts[0]);
        var password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

        return string.Join(
            ";",
            $"Host={uri.Host}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Username={username}",
            $"Password={password}",
            $"Database={database}");
    }

    /// <summary>
    /// Gets a Redis connection string from <c>REDIS_URL</c>, parsed to
    /// StackExchange format.
    /// </summary>
    /// <returns>A StackExchange.Redis-compatible connection string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>REDIS_URL</c> is not set.
    /// </exception>
    public static string GetRedis()
    {
        var value = Environment.GetEnvironmentVariable("REDIS_URL");
        return value.Truthy()
            ? ParseRedisUri(value!)
            : throw new InvalidOperationException(
                "REDIS_URL is not set. Check your .env.local file.");
    }

    /// <summary>
    /// Gets a PostgreSQL connection string from the specified env var, parsed
    /// to ADO.NET format.
    /// </summary>
    /// <param name="envVar">The env var name (e.g. <c>GEO_DATABASE_URL</c>).</param>
    /// <returns>An Npgsql-compatible ADO.NET connection string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the env var is not set.
    /// </exception>
    public static string GetPostgres(string envVar)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        return value.Truthy()
            ? ParsePostgresUri(value!)
            : throw new InvalidOperationException(
                $"{envVar} is not set. Check your .env.local file.");
    }

    /// <summary>
    /// Gets a RabbitMQ connection string from <c>RABBITMQ_URL</c>.
    /// AMQP URIs are natively supported by the .NET RabbitMQ client — no
    /// conversion needed.
    /// </summary>
    /// <returns>A RabbitMQ connection string or AMQP URI.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>RABBITMQ_URL</c> is not set.
    /// </exception>
    public static string GetRabbitMq()
    {
        var value = Environment.GetEnvironmentVariable("RABBITMQ_URL");
        return value.Truthy()
            ? value!
            : throw new InvalidOperationException(
                "RABBITMQ_URL is not set. Check your .env.local file.");
    }
}
