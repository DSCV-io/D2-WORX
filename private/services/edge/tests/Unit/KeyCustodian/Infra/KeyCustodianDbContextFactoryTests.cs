// -----------------------------------------------------------------------
// <copyright file="KeyCustodianDbContextFactoryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Infra;

using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tests for <see cref="KeyCustodianDbContextFactory"/>: the design-time env-var
/// name, migrations-assembly name, and context-construction path are all pinned so
/// a mistyped constant is caught before the next <c>dotnet ef migrations add</c>
/// rather than silently generating migrations in the wrong assembly.
/// </summary>
public sealed class KeyCustodianDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_MissingEnvVar_ErrorMentionsKeyCustodianDatabaseUrl()
    {
        // If the wrong env-var name were embedded, the error would name a different
        // variable. This binds the constant through the production execution path.
        var original = Environment.GetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL", null);

            var factory = new KeyCustodianDbContextFactory();
            var act = () => ((IDesignTimeDbContextFactory<KeyCustodianDbContext>)factory)
                .CreateDbContext([]);

            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("*KEYCUSTODIAN_DATABASE_URL*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL", original);
        }
    }

    [Fact]
    public void CreateDbContext_WithValidConnectionString_ReturnsNonNullContext()
    {
        // Exercises the full CreateDbContext path (env-var read → options build →
        // CreateContext delegate). Uses a fake non-connecting string — no live DB
        // required (context is built but not opened).
        const string fake = "Host=localhost;Port=1;Database=d2-keycustodian;Username=u;Password=p";
        var original = Environment.GetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL", fake);

            var factory = new KeyCustodianDbContextFactory();
            using var context = ((IDesignTimeDbContextFactory<KeyCustodianDbContext>)factory)
                .CreateDbContext([]);

            context.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL", original);
        }
    }

    [Fact]
    public void CreateDbContext_WithValidConnectionString_MigrationsAssemblyIsKeyCustodianInfra()
    {
        // The migrations-assembly name is baked into the DbContextOptions by
        // MigrationsAssembly(...). A wrong name would silently generate migrations
        // in another assembly. Retrieve via the service-provider on the built context.
        const string fake = "Host=localhost;Port=1;Database=d2-keycustodian;Username=u;Password=p";
        var original = Environment.GetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL", fake);

            var factory = new KeyCustodianDbContextFactory();
            using var context = ((IDesignTimeDbContextFactory<KeyCustodianDbContext>)factory)
                .CreateDbContext([]);

            var migrationsAssembly = context.GetInfrastructure()
                .GetRequiredService<IMigrationsAssembly>();

            migrationsAssembly.Assembly.GetName().Name
                .Should().Be("DcsvIo.D2.Private.Edge.KeyCustodian.Infra");
        }
        finally
        {
            Environment.SetEnvironmentVariable("KEYCUSTODIAN_DATABASE_URL", original);
        }
    }
}
