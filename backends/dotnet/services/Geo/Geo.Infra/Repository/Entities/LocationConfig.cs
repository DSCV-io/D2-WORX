// -----------------------------------------------------------------------
// <copyright file="LocationConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="Location"/> entity.
/// </summary>
public class LocationConfig : IEntityTypeConfiguration<Location>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        // Primary key (content-addressable hash).
        builder.HasKey(l => l.HashId);
        builder.Property(l => l.HashId)
            .HasColumnName("hash_id")
            .HasMaxLength(64)
            .IsRequired();

        // Coordinates (owned type).
        builder.OwnsOne(l => l.Coordinates, coords =>
        {
            coords.Property(c => c.Latitude).HasColumnName("latitude");
            coords.Property(c => c.Longitude).HasColumnName("longitude");
        });

        // Address (owned type).
        builder.OwnsOne(l => l.Address, addr =>
        {
            addr.Property(a => a.Line1).HasColumnName("address_line_1").HasMaxLength(255);
            addr.Property(a => a.Line2).HasColumnName("address_line_2").HasMaxLength(255);
            addr.Property(a => a.Line3).HasColumnName("address_line_3").HasMaxLength(255);
        });

        // Simple properties.
        builder.Property(l => l.City)
            .HasColumnName("city")
            .HasMaxLength(255);

        builder.Property(l => l.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(16);

        builder.Property(l => l.SubdivisionISO31662Code)
            .HasColumnName("subdivision_iso_3166_2_code")
            .HasMaxLength(6);

        builder.Property(l => l.CountryISO31661Alpha2Code)
            .HasColumnName("country_iso_3166_1_alpha_2_code")
            .HasMaxLength(2);

        // Optional foreign keys to reference data.
        builder.HasOne(l => l.Subdivision)
            .WithMany()
            .HasForeignKey(l => l.SubdivisionISO31662Code)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.Country)
            .WithMany()
            .HasForeignKey(l => l.CountryISO31661Alpha2Code)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
