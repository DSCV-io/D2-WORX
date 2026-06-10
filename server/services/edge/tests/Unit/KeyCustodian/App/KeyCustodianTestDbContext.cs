// -----------------------------------------------------------------------
// <copyright file="KeyCustodianTestDbContext.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System;
using D2.Edge.KeyCustodian.App.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Test-owned <see cref="IKeyCustodianDbContext"/> over the provider-free
/// InMemory database. Minimal model — <see cref="KeyRecord"/> keyed on
/// <c>Kid</c>, <see cref="KeyAuditRecord"/> on an identity <c>Id</c>. Relational
/// specifics (the <c>xmin</c> concurrency token, <c>Instant</c> value
/// converters, real SQL translation) are deliberately absent; Infra +
/// Testcontainers own those. The InMemory provider stores CLR objects
/// directly, so <c>Instant</c> persists without a converter here.
/// </summary>
public sealed class KeyCustodianTestDbContext : DbContext, IKeyCustodianDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyCustodianTestDbContext"/> class.
    /// </summary>
    /// <param name="options">The DbContext options (InMemory provider).</param>
    public KeyCustodianTestDbContext(DbContextOptions<KeyCustodianTestDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public DbSet<KeyRecord> Keys => Set<KeyRecord>();

    /// <inheritdoc/>
    public DbSet<KeyAuditRecord> Audit => Set<KeyAuditRecord>();

    /// <summary>
    /// Builds a fresh context backed by a uniquely-named InMemory database so each
    /// test is isolated.
    /// </summary>
    /// <returns>A new, empty <see cref="KeyCustodianTestDbContext"/>.</returns>
    public static KeyCustodianTestDbContext CreateEmpty()
    {
        var options = new DbContextOptionsBuilder<KeyCustodianTestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .EnableServiceProviderCaching(false)
            .Options;
        return new KeyCustodianTestDbContext(options);
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KeyRecord>().HasKey(k => k.Kid);
        modelBuilder.Entity<KeyAuditRecord>().HasKey(a => a.Id);
    }
}
