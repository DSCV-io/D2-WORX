// -----------------------------------------------------------------------
// <copyright file="GeoDbContext.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository;

using D2.Geo.Domain.Entities;
using D2.Geo.Infra.Repository.Entities;
using D2.Geo.Infra.Repository.Seeding;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents the database context for geographic data.
/// </summary>
[MustDisposeResource(false)]
public class GeoDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeoDbContext"/> class.
    /// </summary>
    ///
    /// <param name="options">
    /// The options to be used by a <see cref="DbContext"/>.
    /// </param>
    [MustDisposeResource(false)]
    public GeoDbContext(DbContextOptions<GeoDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the Countries DbSet.
    /// </summary>
    public DbSet<Country> Countries { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Subdivisions DbSet.
    /// </summary>
    public DbSet<Subdivision> Subdivisions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the GeopoliticalEntities DbSet.
    /// </summary>
    public DbSet<GeopoliticalEntity> GeopoliticalEntities { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Currencies DbSet.
    /// </summary>
    public DbSet<Currency> Currencies { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Languages DbSet.
    /// </summary>
    public DbSet<Language> Languages { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Locales DbSet.
    /// </summary>
    public DbSet<Locale> Locales { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Timezones DbSet.
    /// </summary>
    public DbSet<Timezone> Timezones { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ReferenceDataVersions DbSet.
    /// </summary>
    public DbSet<ReferenceDataVersion> ReferenceDataVersions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Locations DbSet.
    /// </summary>
    public DbSet<Location> Locations { get; set; } = null!;

    /// <summary>
    /// Gets or sets the WhoIs DbSet.
    /// </summary>
    public DbSet<WhoIs> WhoIsRecords { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Contacts DbSet.
    /// </summary>
    public DbSet<Contact> Contacts { get; set; } = null!;

    /// <summary>
    /// Configures the model that was discovered by convention from the entity types.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The builder being used to construct the model for this context.
    /// </param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GeoDbContext).Assembly);

        // Seeding.
        modelBuilder.SeedReferenceDataVersion();
        modelBuilder.SeedLanguages();
        modelBuilder.SeedCurrencies();
        modelBuilder.SeedCountries();
        modelBuilder.SeedCountryCurrencies();
        modelBuilder.SeedGeopoliticalEntities();
        modelBuilder.SeedCountryGeopoliticalEntities();
        modelBuilder.SeedLocales();
        modelBuilder.SeedTimezones();
        modelBuilder.SeedSubdivisions();
    }
}
