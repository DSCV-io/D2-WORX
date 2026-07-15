// -----------------------------------------------------------------------
// <copyright file="ContactsHostDbContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Contacts;

using DcsvIo.D2.Contacts.EntityFrameworkCore;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using DcsvIo.D2.Location.EntityFrameworkCore;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Time.EfCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Synthetic host <c>DbContext</c> for Contacts VO end-to-end integration tests.
/// Holds four host entities covering all live-DB scenarios:
/// <list type="bullet">
///   <item><see cref="ContactHost"/> — round-trip + erasure (Tier B).</item>
///   <item><see cref="GeoOnlyHost"/> — Coordinates-only Tier-A coercion proof.</item>
///   <item><see cref="Invoice"/> — partial anonymization + same-VO-twice (Tier A).</item>
///   <item><see cref="UniqueContactRow"/> — unique-template per-row resolution.</item>
/// </list>
/// Schema created via <c>EnsureCreatedAsync</c> — no migrations.
/// </summary>
internal sealed class ContactsHostDbContext : DbContext
{
    internal ContactsHostDbContext(DbContextOptions<ContactsHostDbContext> options)
        : base(options)
    {
    }

    public DbSet<ContactHost> ContactHosts => Set<ContactHost>();

    public DbSet<GeoOnlyHost> GeoOnlyHosts => Set<GeoOnlyHost>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<UniqueContactRow> UniqueContactRows => Set<UniqueContactRow>();

    /// <summary>
    /// Builds and returns a <see cref="ContactsHostDbContext"/> for the given connection
    /// string.
    /// </summary>
    /// <param name="connectionString">
    /// Npgsql connection string for the Testcontainers PostgreSQL instance.
    /// </param>
    internal static ContactsHostDbContext Build(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ContactsHostDbContext>()
            .UseNpgsql(connectionString, o => o.AddD2NodaTime())
            .Options;
        return new ContactsHostDbContext(options);
    }

    /// <summary>
    /// Creates the ContactsHostDbContext tables in the shared Postgres database using
    /// <c>CREATE TABLE IF NOT EXISTS</c> DDL statements generated from the EF model.
    /// Must be called AFTER the database has been created (e.g. by
    /// <c>GovDbContext.EnsureCreatedAsync</c>), because this method does not create the
    /// database itself.
    /// </summary>
    /// <param name="connectionString">
    /// Npgsql connection string for the Testcontainers PostgreSQL instance.
    /// </param>
    internal static async Task EnsureSchemaAsync(string connectionString)
    {
        await using var ctx = Build(connectionString);

        // Generate the full create script from the EF model, then transform each
        // CREATE TABLE / CREATE INDEX / CREATE UNIQUE INDEX into the IF NOT EXISTS
        // forms so repeated calls are idempotent. The EF Npgsql script emits these
        // tokens verbatim without extra whitespace, so literal replacements are safe.
        var script = ctx.Database.GenerateCreateScript();
        var transformed = script
            .Replace(
                "CREATE UNIQUE INDEX ",
                "CREATE UNIQUE INDEX IF NOT EXISTS ",
                System.StringComparison.Ordinal)
            .Replace(
                "CREATE INDEX ",
                "CREATE INDEX IF NOT EXISTS ",
                System.StringComparison.Ordinal)
            .Replace(
                "CREATE TABLE ",
                "CREATE TABLE IF NOT EXISTS ",
                System.StringComparison.Ordinal);

        await ctx.Database.ExecuteSqlRawAsync(transformed);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyAnonymizationConventions();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // =========================================================================
        // ContactHost
        // =========================================================================
        modelBuilder.Entity<ContactHost>(b =>
        {
            b.HasKey(x => x.Id);

            b.ComplexProperty(x => x.Name, cp => cp.MapPersonal());
            b.ComplexProperty(x => x.Affixes, cp => cp.MapNameAffixes());
            b.ComplexProperty(x => x.Demo, cp => cp.MapDemographics());
            b.ComplexProperty(x => x.Work, cp => cp.MapProfessional());
            b.ComplexProperty(x => x.Street, cp => cp.MapStreetAddress());
            b.ComplexProperty(x => x.Admin, cp => cp.MapAdminLocation());
            b.ComplexProperty(x => x.Geo, cp => cp.MapCoordinates());

            b.MapEmailAddress(x => x.Email)
             .Anonymize("deletedUser{UserId}@deleted.user.dcsv.io");
            b.MapPhoneNumber(x => x.Phone)
             .Anonymize("10000000000");
        });

        // =========================================================================
        // GeoOnlyHost
        // =========================================================================
        modelBuilder.Entity<GeoOnlyHost>(b =>
        {
            b.HasKey(x => x.Id);
            b.ComplexProperty(x => x.Geo, cp => cp.MapCoordinates());
        });

        // =========================================================================
        // Invoice
        // =========================================================================
        modelBuilder.Entity<Invoice>(b =>
        {
            b.HasKey(x => x.Id);

            b.ComplexProperty(x => x.BillingAddress, cp => cp.MapAdminLocation());
            b.ComplexProperty(x => x.ShippingAddress, cp => cp.MapAdminLocation());
            b.ComplexProperty(x => x.CustomerName, cp => cp.MapPersonal());
        });

        // =========================================================================
        // UniqueContactRow
        // =========================================================================
        modelBuilder.Entity<UniqueContactRow>(b =>
        {
            b.HasKey(x => x.Id);

            b.MapEmailAddress(x => x.Email)
             .Unique("deletedUser{UserId}@deleted.user.dcsv.io");
        });
    }

    // =========================================================================
    // Entity definitions (nested; test-infrastructure-only)
    // =========================================================================

    /// <summary>
    /// Host aggregate for round-trip, erasure, and Tier-B Coordinates coercion tests.
    /// Carries all multi-field VOs (Personal, NameAffixes, Demographics, Professional,
    /// StreetAddress, AdminLocation, Coordinates) plus nullable Email (Template → Tier B)
    /// and Phone (Constant).
    /// <c>AccountTier</c> is a non-PII field that must be retained after erasure.
    /// <para>
    /// <c>NameAffixes</c> and <c>Demographics</c> are all-nullable VOs: EF Core requires
    /// the host property to be non-nullable (required complex type with no required scalar).
    /// The host declares them as <c>= null!</c> and always seeds at least one non-null member.
    /// </para>
    /// </summary>
    internal sealed class ContactHost : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public Personal Name { get; set; } = null!;

        /// <summary>
        /// Gets or sets the name affixes. Required complex type (no required scalar —
        /// EF rejects an optional all-nullable complex prop at model build).
        /// </summary>
        public NameAffixes Affixes { get; set; } = null!;

        /// <summary>
        /// Gets or sets the demographics. Required complex type (no required scalar —
        /// EF rejects an optional all-nullable complex prop at model build).
        /// </summary>
        public Demographics Demo { get; set; } = null!;

        public Professional Work { get; set; } = null!;

        public StreetAddress Street { get; set; } = null!;

        public AdminLocation Admin { get; set; } = null!;

        public Coordinates Geo { get; set; } = null!;

        public EmailAddress? Email { get; set; }

        public PhoneNumber? Phone { get; set; }

        /// <summary>
        /// Gets or sets the account tier. Non-PII (undecorated) — must be retained on
        /// erasure to prove non-PII retention.
        /// </summary>
        public string AccountTier { get; set; } = string.Empty;

        /// <inheritdoc />
        public bool IsAnonymized { get; set; }
    }

    /// <summary>
    /// Minimal host for the Tier-A Coordinates-coercion proof.
    /// No Email/Phone template → classified Tier A → engine uses
    /// <c>ExecuteUpdateAsync</c>, which exercises the numeric-Constant coercion design.
    /// </summary>
    internal sealed class GeoOnlyHost : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public Coordinates Geo { get; set; } = null!;

        /// <inheritdoc />
        public bool IsAnonymized { get; set; }
    }

    /// <summary>
    /// Invoice host for partial-anonymization tests. Maps <c>AdminLocation</c> twice
    /// (billing + shipping) and a <c>Personal</c> customer name. Financial scalars
    /// are non-PII and must be retained after erasure.
    /// No Template rules → Tier A (<c>ExecuteUpdateAsync</c>).
    /// </summary>
    internal sealed class Invoice : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public AdminLocation BillingAddress { get; set; } = null!;

        public AdminLocation ShippingAddress { get; set; } = null!;

        public Personal CustomerName { get; set; } = null!;

        /// <summary>Gets or sets the invoice total. Non-PII — retained on erasure.</summary>
        public decimal TotalAmount { get; set; }

        /// <summary>Gets or sets the tax amount. Non-PII — retained on erasure.</summary>
        public decimal TaxAmount { get; set; }

        /// <summary>Gets or sets the invoice number. Non-PII — retained on erasure.</summary>
        public string InvoiceNumber { get; set; } = string.Empty;

        /// <inheritdoc />
        public bool IsAnonymized { get; set; }
    }

    /// <summary>
    /// Minimal entity for the unique-template per-row-resolution proof. The Email
    /// column carries a unique index and the <c>{UserId}</c> template tombstone so each
    /// erased row resolves to a per-row-distinct value, preventing unique-constraint
    /// violations when multiple rows are erased.
    /// </summary>
    internal sealed class UniqueContactRow : IUserOwned, IAnonymizationTrackable
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public EmailAddress? Email { get; set; }

        /// <inheritdoc />
        public bool IsAnonymized { get; set; }
    }
}
