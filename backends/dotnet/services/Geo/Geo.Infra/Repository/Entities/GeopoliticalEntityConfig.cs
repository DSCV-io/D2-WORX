// -----------------------------------------------------------------------
// <copyright file="GeopoliticalEntityConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="GeopoliticalEntity"/> entity.
/// </summary>
public class GeopoliticalEntityConfig : IEntityTypeConfiguration<GeopoliticalEntity>
{
    /// <summary>
    /// Configures the GeopoliticalEntity entity.
    /// </summary>
    ///
    /// <param name="builder">
    /// The entity type builder for GeopoliticalEntity.
    /// </param>
    public void Configure(EntityTypeBuilder<GeopoliticalEntity> builder)
    {
        builder.ToTable("geopolitical_entities");

        // Short Code (Primary Key).
        builder.HasKey(x => x.ShortCode);
        builder.Property(x => x.ShortCode)
            .HasColumnName("short_code")
            .HasMaxLength(64)
            .IsRequired();

        // Name (Property).
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        // Type (Property - Enum stored as string).
        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        // Countries (Navigation Collection - Many-to-many).
        // Relationship already configured from Country side.
    }
}
