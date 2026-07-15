// -----------------------------------------------------------------------
// <copyright file="DesignTimeDbContextFactoryBase.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EntityFrameworkCore.Postgres;

using System;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Abstract base for EF Core design-time <see cref="DbContext"/> factories in
/// module-within-host services that have no <c>Sdk.Web</c> startup project for
/// <c>dotnet ef</c> to discover.
/// </summary>
/// <remarks>
/// <para>
/// Subclasses supply the design-time connection-string environment variable name
/// (via <see cref="ConnectionStringEnvVar"/>) and a delegate that constructs the
/// concrete <see cref="DbContext"/> given its <see cref="DbContextOptions{TContext}"/>
/// (via <see cref="CreateContext"/>). The base reads the env var, applies the canonical
/// <see cref="NpgsqlContextDefaults.ApplyD2NpgsqlDefaults"/> settings (NodaTime +
/// CommandTimeout + MigrationsAssembly), and wires it into the context.
/// </para>
/// <para>
/// Usage — subclass in the consuming <c>infra/</c> project:
/// <code>
/// public sealed class MyDbContextFactory
///     : DesignTimeDbContextFactoryBase&lt;MyDbContext&gt;
/// {
///     protected override string ConnectionStringEnvVar => "MY_DATABASE_URL";
///     protected override string MigrationsAssembly =>
///         typeof(MyDbContextFactory).Assembly.GetName().Name!;
///     protected override MyDbContext CreateContext(DbContextOptions&lt;MyDbContext&gt; opts)
///         => new(opts);
/// }
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TContext">The <see cref="DbContext"/> type to construct.</typeparam>
public abstract class DesignTimeDbContextFactoryBase<TContext>
    : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Gets the environment variable name holding the design-time connection string
    /// (e.g. <c>"MYSERVICE_DATABASE_URL"</c>).
    /// </summary>
    protected abstract string ConnectionStringEnvVar { get; }

    /// <summary>
    /// Gets the migrations-assembly name to pass to
    /// <see cref="NpgsqlContextDefaults.ApplyD2NpgsqlDefaults"/>.
    /// Typically <c>typeof(MyDbContextFactory).Assembly.GetName().Name!</c>.
    /// </summary>
    protected abstract string MigrationsAssemblyName { get; }

    /// <summary>
    /// Gets the command timeout in seconds to apply. Defaults to <c>30</c>.
    /// </summary>
    protected virtual int CommandTimeoutSeconds => 30;

    /// <inheritdoc/>
    public TContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (connectionString.Falsey())
        {
            throw new InvalidOperationException(
                $"Design-time DbContext factory requires environment variable "
                + $"'{ConnectionStringEnvVar}' — set it before running 'dotnet ef'.");
        }

        var builder = new DbContextOptionsBuilder<TContext>();
        builder.ApplyD2NpgsqlDefaults(
            connectionString!,
            CommandTimeoutSeconds,
            MigrationsAssemblyName);

        return CreateContext(builder.Options);
    }

    /// <summary>
    /// Constructs the concrete <see cref="DbContext"/> from the configured options.
    /// </summary>
    /// <param name="options">The configured context options.</param>
    /// <returns>A new <typeparamref name="TContext"/> instance.</returns>
    protected abstract TContext CreateContext(DbContextOptions<TContext> options);
}
