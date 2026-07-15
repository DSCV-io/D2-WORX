// -----------------------------------------------------------------------
// <copyright file="DesignTimeDbContextFactoryBaseTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SharedEntityFrameworkCore;

using System;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Unit tests for <see cref="DesignTimeDbContextFactoryBase{TContext}"/>:
/// env-var-absent throws <see cref="InvalidOperationException"/>;
/// env-var-present returns a concrete <c>TContext</c> instance.
/// Uses a local probe subclass and a dedicated environment-variable key that is
/// cleaned up after each test.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DesignTimeDbContextFactoryBaseTests
{
    // Env var key used only by this test class — unlikely to collide with system vars.
    private const string _ENV_VAR = "D2_TEST_DTFACTORY_CONNSTR_1A2B3C4D";

    // Connection string used when the env var is set (no real DB is opened —
    // CreateDbContext only builds options; it does not open a connection).
    private const string _CONN_STR = "Host=localhost;Database=dt_probe;Username=u;Password=p";

    // =========================================================================
    // Env-var absent → InvalidOperationException
    // =========================================================================

    [Fact]
    public void CreateDbContext_EnvVarAbsent_ThrowsInvalidOperationException()
    {
        Environment.SetEnvironmentVariable(_ENV_VAR, null);

        var factory = new ProbeDbContextFactory();

        var act = () => factory.CreateDbContext([]);

        act.Should().ThrowExactly<InvalidOperationException>(
            "CreateDbContext must throw when the required connection-string env var is absent");
    }

    // =========================================================================
    // Env-var present → concrete TContext returned
    // =========================================================================

    [Fact]
    public void CreateDbContext_EnvVarPresent_ReturnsConcreteContext()
    {
        Environment.SetEnvironmentVariable(_ENV_VAR, _CONN_STR);
        try
        {
            var factory = new ProbeDbContextFactory();

            using var ctx = factory.CreateDbContext([]);

            ctx.Should().NotBeNull(
                "a valid connection string must produce a concrete context");
            ctx.Should().BeOfType<ProbeDbContext>(
                "the factory must return exactly the TContext subclass it was parameterized with");
        }
        finally
        {
            Environment.SetEnvironmentVariable(_ENV_VAR, null);
        }
    }

    // =========================================================================
    // Probe types — self-contained; no migration assembly required
    // =========================================================================

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options)
        : DbContext(options);

    private sealed class ProbeDbContextFactory
        : DesignTimeDbContextFactoryBase<ProbeDbContext>
    {
        protected override string ConnectionStringEnvVar => _ENV_VAR;

        protected override string MigrationsAssemblyName =>
            typeof(ProbeDbContextFactory).Assembly.GetName().Name!;

        protected override ProbeDbContext CreateContext(DbContextOptions<ProbeDbContext> options)
            => new(options);
    }
}
