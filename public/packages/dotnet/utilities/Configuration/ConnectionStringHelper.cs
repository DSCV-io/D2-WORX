// -----------------------------------------------------------------------
// <copyright file="ConnectionStringHelper.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Configuration;

using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Resolves infrastructure connection strings from standard environment
/// variables (<c>REDIS_URL</c>, <c>*_DATABASE_URL</c>, <c>RABBITMQ_URL</c>),
/// converting URI-shaped values into the wire formats expected by the .NET
/// clients (<c>StackExchange.Redis</c>, <c>Npgsql</c>).
/// </summary>
/// <remarks>
/// Env vars use standard URI formats so the same value works for every
/// platform we deploy to (Node.js services, container orchestrators, .NET
/// services). The <c>Parse*Uri</c> methods convert into .NET-native shapes;
/// values that are already in .NET shape pass through untouched.
/// </remarks>
public static class ConnectionStringHelper
{
    /// <summary>
    /// Converts a <c>redis://[:password]@host[:port]</c> URI to the
    /// <c>StackExchange.Redis</c> form
    /// (<c>host:port[,password=...]</c>). Pass-through for values that do not
    /// start with <c>redis://</c>.
    /// </summary>
    /// <param name="value">A Redis connection string or URI.</param>
    /// <returns>A <c>StackExchange.Redis</c>-compatible connection string.</returns>
    public static string ParseRedisUri(string value)
    {
        if (!value.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var hostPort = $"{uri.Host}:{(uri.Port > 0 ? uri.Port : 6379)}";

        // userinfo is "redis://:password@host" or "redis://user:password@host";
        // we only need the password.
        var password = Uri.UnescapeDataString(
            uri.UserInfo.Contains(':')
                ? uri.UserInfo[(uri.UserInfo.IndexOf(':') + 1)..]
                : uri.UserInfo);

        return password.Falsey()
            ? hostPort
            : $"{hostPort},password={password}";
    }

    /// <summary>
    /// Converts a <c>postgres[ql]://user:pass@host[:port]/db</c> URI to an
    /// <c>Npgsql</c> ADO.NET connection string. Pass-through for values that
    /// do not start with <c>postgres://</c> or <c>postgresql://</c>.
    /// </summary>
    /// <param name="value">A PostgreSQL connection string or URI.</param>
    /// <returns>An <c>Npgsql</c>-compatible ADO.NET connection string.</returns>
    public static string ParsePostgresUri(string value)
    {
        if (!value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var database = uri.AbsolutePath.TrimStart('/');

        if (uri.UserInfo.Falsey())
        {
            return string.Join(
                ';',
                $"Host={uri.Host}",
                $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
                $"Database={database}");
        }

        var parts = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(parts[0]);
        var password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

        return string.Join(
            ';',
            $"Host={uri.Host}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Username={username}",
            $"Password={password}",
            $"Database={database}");
    }

    /// <summary>
    /// Reads <c>REDIS_URL</c> and returns it parsed for
    /// <c>StackExchange.Redis</c>.
    /// </summary>
    /// <returns>A <c>StackExchange.Redis</c>-compatible connection string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>REDIS_URL</c> is not set or is empty.
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
    /// Reads the named env var and returns it parsed for <c>Npgsql</c>.
    /// </summary>
    /// <param name="envVar">The env var name (e.g. <c>GEO_DATABASE_URL</c>).</param>
    /// <returns>An <c>Npgsql</c>-compatible ADO.NET connection string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the env var is not set or is empty.
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
    /// Reads <c>RABBITMQ_URL</c> and returns it as-is. AMQP URIs are natively
    /// supported by the .NET RabbitMQ client — no conversion required.
    /// </summary>
    /// <returns>A RabbitMQ connection string or AMQP URI.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>RABBITMQ_URL</c> is not set or is empty.
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
