// -----------------------------------------------------------------------
// <copyright file="ReferenceDataVersionConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the ReferenceDataVersion entity.
/// </summary>
public class ReferenceDataVersionConfig : IEntityTypeConfiguration<ReferenceDataVersion>
{
    /// <summary>
    /// Configures the ReferenceDataVersion entity.
    /// </summary>
    ///
    /// <param name="builder">
    /// The entity type builder for ReferenceDataVersion.
    /// </param>
    public void Configure(EntityTypeBuilder<ReferenceDataVersion> builder)
    {
        builder.ToTable("reference_data_version");

        // Id (Primary Key).
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever()
            .IsRequired();

        // Version (Property).
        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasMaxLength(32)
            .IsRequired();

        // UpdatedAt (Property).
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
