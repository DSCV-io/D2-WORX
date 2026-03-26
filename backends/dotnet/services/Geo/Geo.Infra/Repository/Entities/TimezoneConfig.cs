// -----------------------------------------------------------------------
// <copyright file="TimezoneConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="Timezone"/> entity.
/// </summary>
public class TimezoneConfig : IEntityTypeConfiguration<Timezone>
{
    /// <summary>
    /// Configures the Timezone entity.
    /// </summary>
    ///
    /// <param name="builder">
    /// The entity type builder for Timezone.
    /// </param>
    public void Configure(EntityTypeBuilder<Timezone> builder)
    {
        builder.ToTable("timezones");

        // IANA Identifier (Primary Key).
        builder.HasKey(x => x.IANAIdentifier);
        builder.Property(x => x.IANAIdentifier)
            .HasColumnName("iana_identifier")
            .HasMaxLength(64)
            .IsRequired();

        // Display Name (Property).
        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(128)
            .IsRequired();

        // UTC Offset STD (Property).
        builder.Property(x => x.UTCOffsetSTD)
            .HasColumnName("utc_offset_std")
            .HasMaxLength(7)
            .IsRequired();

        // UTC Offset DST (Property, nullable).
        builder.Property(x => x.UTCOffsetDST)
            .HasColumnName("utc_offset_dst")
            .HasMaxLength(7);

        // Abbreviation STD (Property).
        builder.Property(x => x.AbbreviationSTD)
            .HasColumnName("abbreviation_std")
            .HasMaxLength(10)
            .IsRequired();

        // Abbreviation DST (Property, nullable).
        builder.Property(x => x.AbbreviationDST)
            .HasColumnName("abbreviation_dst")
            .HasMaxLength(10);

        // Country ISO 3166-1 Alpha-2 Code (Foreign Key, Required).
        builder.Property(x => x.CountryISO31661Alpha2Code)
            .HasColumnName("country_iso_3166_1_alpha_2_code")
            .HasMaxLength(2)
            .IsRequired();

        // Country (Navigation Property - Many-to-one).
        // Note: Relationship configured from Country side via HasMany/WithOne.
    }
}
