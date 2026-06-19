// -----------------------------------------------------------------------
// <copyright file="KeyCustodianDbContext.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Persistence.Postgres;

/// <summary>
/// The concrete PostgreSQL <see cref="DbContext"/> backing
/// <see cref="IKeyCustodianDbContext"/>. Maps the three flat persistence records
/// (<see cref="KeyRecord"/> / <see cref="KeyAuditRecord"/> /
/// <see cref="LeafIssuanceAuditRecord"/>) to the <c>keycustodian_db</c>
/// relational schema.
/// </summary>
/// <remarks>
/// The relational model — snake_case columns, the <c>Instant</c> ↔
/// <c>TIMESTAMPTZ</c> mapping (via <c>AddD2NodaTime</c>), the <c>xmin</c>
/// optimistic-concurrency token, and the append-only audit foreign key — lives in
/// the two <see cref="IEntityTypeConfiguration{TEntity}"/> classes in this folder.
/// The App layer depends only on the <see cref="IKeyCustodianDbContext"/> seam;
/// this is the only project that knows about Npgsql.
/// </remarks>
public sealed class KeyCustodianDbContext : DbContext, IKeyCustodianDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyCustodianDbContext"/> class.
    /// </summary>
    /// <param name="options">The configured context options (Npgsql provider).</param>
    public KeyCustodianDbContext(DbContextOptions<KeyCustodianDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public DbSet<KeyRecord> Keys => Set<KeyRecord>();

    /// <inheritdoc/>
    public DbSet<KeyAuditRecord> Audit => Set<KeyAuditRecord>();

    /// <inheritdoc/>
    public DbSet<LeafIssuanceAuditRecord> LeafIssuanceAudit => Set<LeafIssuanceAuditRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new KeyRecordConfiguration());
        modelBuilder.ApplyConfiguration(new KeyAuditRecordConfiguration());
        modelBuilder.ApplyConfiguration(new LeafIssuanceAuditRecordConfiguration());
    }
}
