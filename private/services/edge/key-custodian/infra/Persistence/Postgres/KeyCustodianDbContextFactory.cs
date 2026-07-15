// -----------------------------------------------------------------------
// <copyright file="KeyCustodianDbContextFactory.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres;

using DcsvIo.D2.EntityFrameworkCore.Postgres;

/// <summary>
/// Design-time <see cref="KeyCustodianDbContext"/> factory used by
/// <c>dotnet ef migrations add</c>. Required because KeyCustodian is a
/// module-within-host with no <c>Sdk.Web</c> startup project for the EF tooling
/// to discover.
/// </summary>
/// <remarks>
/// Reads the design-time connection string from <c>KEYCUSTODIAN_DATABASE_URL</c>
/// and applies the canonical D2 Npgsql defaults (NodaTime + CommandTimeout +
/// MigrationsAssembly) via the shared
/// <see cref="DesignTimeDbContextFactoryBase{TContext}"/>. The migrations live in
/// this Infra assembly.
/// </remarks>
public sealed class KeyCustodianDbContextFactory
    : DesignTimeDbContextFactoryBase<KeyCustodianDbContext>
{
    /// <inheritdoc/>
    protected override string ConnectionStringEnvVar => "KEYCUSTODIAN_DATABASE_URL";

    /// <inheritdoc/>
    protected override string MigrationsAssemblyName =>
        typeof(KeyCustodianDbContextFactory).Assembly.GetName().Name!;

    /// <inheritdoc/>
    protected override KeyCustodianDbContext CreateContext(
        DbContextOptions<KeyCustodianDbContext> options) => new(options);
}
