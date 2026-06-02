// -----------------------------------------------------------------------
// <copyright file="GovDbContext.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.DataGovernance;

using System.ComponentModel.DataAnnotations;
using D2.Shared.DataGovernance.Abstractions;
using D2.Shared.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Synthetic host <c>DbContext</c> for anonymization engine integration tests.
/// Contains a mix of Tier-A and Tier-B user-owned entities, an org-owned entity,
/// and an exempt ledger to cover all anonymization paths and isolation scenarios.
/// Schema is created via <c>EnsureCreatedAsync</c> — no migrations.
/// </summary>
internal sealed class GovDbContext : DbContext
{
    internal GovDbContext(DbContextOptions<GovDbContext> options)
        : base(options)
    {
    }

    public DbSet<TierAUser> TierAUsers => Set<TierAUser>();

    public DbSet<TierBUser> TierBUsers => Set<TierBUser>();

    public DbSet<OrgRecord> OrgRecords => Set<OrgRecord>();

    public DbSet<ExemptLedger> ExemptLedgers => Set<ExemptLedger>();

    /// <summary>
    /// Builds and returns a <see cref="GovDbContext"/> for the given connection string.
    /// </summary>
    /// <param name="connectionString">
    /// Npgsql connection string for the Testcontainers PostgreSQL instance.
    /// </param>
    internal static GovDbContext Build(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new GovDbContext(options);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyAnonymizationConventions();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TierAUser>(e =>
        {
            e.HasKey(x => x.Id);

            // Table-split OwnsOne (no ToJson) — decorated via fluent
            e.OwnsOne(x => x.Address, nav =>
            {
                nav.Anonymize(a => a.Line1, "[deleted]");
                nav.AnonymizeNull(a => a.Line2);
            });

            // Complex property — decorated via fluent
            e.ComplexProperty(x => x.Name, cp =>
            {
                cp.Anonymize(n => n.First, "[deleted]");
                cp.AnonymizeNull(n => n.Last);
            });
        });

        modelBuilder.Entity<TierBUser>(e =>
        {
            e.HasKey(x => x.Id);

            // Map RowVersion to xmin (PostgreSQL system column for optimistic concurrency).
            e.Property(x => x.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<OrgRecord>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ExemptLedger>(e =>
        {
            e.HasKey(x => x.Id);
        });
    }

    // =========================================================================
    // Entity definitions (nested; test-infrastructure-only types)
    // =========================================================================

    /// <summary>
    /// Tier-A user entity: all anonymizable columns are constant/null/empty.
    /// A single ExecuteUpdateAsync covers the whole row.
    /// </summary>
    internal sealed class TierAUser : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        /// <summary>Gets or sets the display name. Decorated with a constant.</summary>
        [Anonymizable("Deleted")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the bio. Decorated with SetNull.</summary>
        [Anonymizable(AnonymizeKind.SetNull)]
        public string? Bio { get; set; }

        /// <summary>Gets or sets notes. Decorated with SetEmpty.</summary>
        [Anonymizable(AnonymizeKind.SetEmpty)]
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status string. Decorated with SetEmpty to prove converter-backed
        /// field.
        /// </summary>
        [Anonymizable(AnonymizeKind.SetEmpty)]
        public string Status { get; set; } = "active";

        /// <summary>
        /// Gets or sets the creation timestamp. Not decorated — must remain unchanged after
        /// sweep.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Gets or sets the owned address. Decorated via fluent in GovDbContext.</summary>
        public TierAAddress? Address { get; set; }

        /// <summary>Gets or sets the complex name. Decorated via fluent in GovDbContext.</summary>
        public TierAName Name { get; set; } = new();

        /// <inheritdoc />
        public bool IsAnonymized { get; set; }
    }

    /// <summary>Owned address VO for TierAUser — table-split (shares owner's table).</summary>
    internal sealed class TierAAddress
    {
        /// <summary>Gets or sets the first address line.</summary>
        public string Line1 { get; set; } = string.Empty;

        /// <summary>Gets or sets the optional second address line.</summary>
        public string? Line2 { get; set; }
    }

    /// <summary>Complex name for TierAUser.</summary>
    internal sealed class TierAName
    {
        /// <summary>Gets or sets the first name.</summary>
        public string First { get; set; } = string.Empty;

        /// <summary>Gets or sets the optional last name.</summary>
        public string? Last { get; set; }
    }

    /// <summary>
    /// Tier-B user entity: has a Template field, which forces materialize-mutate-SaveChanges.
    /// Also carries a concurrency token to exercise DbUpdateConcurrencyException retry.
    /// </summary>
    internal sealed class TierBUser : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        /// <summary>Gets or sets the email. Template field — forces Tier B.</summary>
        [Anonymizable(template: "deletedUser{UserId}@deleted.user.dcsv.io")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name. Constant field — both constant and template fire in
        /// Tier B.
        /// </summary>
        [Anonymizable("Deleted")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the concurrency token mapped to xmin.</summary>
        [Timestamp]
        public uint RowVersion { get; set; }

        /// <inheritdoc />
        public bool IsAnonymized { get; set; }
    }

    /// <summary>Org-owned entity: proves org sweep and user/org isolation.</summary>
    internal sealed class OrgRecord : IOrgOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? OrgId { get; set; }

        /// <summary>Gets or sets the org name. Anonymized with a constant.</summary>
        [Anonymizable("Deleted")]
        public string OrgName { get; set; } = string.Empty;

        /// <inheritdoc />
        public bool IsAnonymized { get; set; }
    }

    /// <summary>
    /// Exempt ledger: implements IExemptFromAnonymization — must be skipped entirely.
    /// Decorated fields must never be touched by the engine.
    /// </summary>
    internal sealed class ExemptLedger :
        IUserOwned,
        IExemptFromAnonymization,
        IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        /// <summary>
        /// Gets or sets the owner note. Decorated but must never be touched (entity is exempt).
        /// </summary>
        [Anonymizable("Deleted")]
        public string OwnerNote { get; set; } = string.Empty;

        /// <summary>Gets or sets the ledger amount.</summary>
        public decimal Amount { get; set; }

        /// <inheritdoc />
        public bool IsAnonymized { get; set; }
    }
}
