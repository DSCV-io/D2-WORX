// -----------------------------------------------------------------------
// <copyright file="CurrencyConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="Currency"/> entity.
/// </summary>
public class CurrencyConfig : IEntityTypeConfiguration<Currency>
{
    /// <summary>
    /// Configures the Currency entity.
    /// </summary>
    ///
    /// <param name="builder">
    /// The entity type builder for Currency.
    /// </param>
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("currencies");

        // ISO 4217 Alpha Code (Primary Key).
        builder.HasKey(x => x.ISO4217AlphaCode);
        builder.Property(x => x.ISO4217AlphaCode)
            .HasColumnName("iso_4217_alpha_code")
            .HasMaxLength(3)
            .IsRequired();

        // ISO 4217 Numeric Code (Unique).
        builder.HasIndex(x => x.ISO4217NumericCode)
            .IsUnique();
        builder.Property(x => x.ISO4217NumericCode)
            .HasColumnName("iso_4217_numeric_code")
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

        // Decimal Places (Property).
        builder.Property(x => x.DecimalPlaces)
            .HasColumnName("decimal_places")
            .IsRequired();

        // Symbol (Property).
        builder.Property(x => x.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(16)
            .IsRequired();

        // Countries (Navigation Collection - Many-to-many).
        // Note: Relationship already configured from Country side.
    }
}
