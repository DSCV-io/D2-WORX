// -----------------------------------------------------------------------
// <copyright file="SubdivisionConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="Subdivision"/> entity.
/// </summary>
public class SubdivisionConfig : IEntityTypeConfiguration<Subdivision>
{
    /// <summary>
    /// Configures the Subdivision entity.
    /// </summary>
    ///
    /// <param name="builder">
    /// The entity type builder for Subdivision.
    /// </param>
    public void Configure(EntityTypeBuilder<Subdivision> builder)
    {
        builder.ToTable("subdivisions");

        // ISO 3166-2 Code (Primary Key).
        builder.HasKey(x => x.ISO31662Code);
        builder.Property(x => x.ISO31662Code)
            .HasColumnName("iso_3166_2_code")
            .HasMaxLength(6)
            .IsRequired();

        // Short Code (Property).
        builder.Property(x => x.ShortCode)
            .HasColumnName("short_code")
            .HasMaxLength(3)
            .IsRequired();

        // Display Name (Property).
        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(64)
            .IsRequired();

        // Official Name (Property).
        builder.Property(x => x.OfficialName)
            .HasColumnName("official_name")
            .HasMaxLength(128)
            .IsRequired();

        // Country ISO 3166-1 Alpha-2 Code (Foreign Key, Required).
        builder.Property(x => x.CountryISO31661Alpha2Code)
            .HasColumnName("country_iso_3166_1_alpha_2_code")
            .HasMaxLength(2)
            .IsRequired();

        // Country (Navigation Property - Many-to-one).
        // Note: Relationship already configured from Country side.
    }
}
