// -----------------------------------------------------------------------
// <copyright file="LanguageConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="Language"/> entity.
/// </summary>
public class LanguageConfig : IEntityTypeConfiguration<Language>
{
    /// <summary>
    /// Configures the Language entity.
    /// </summary>
    ///
    /// <param name="builder">
    /// The entity type builder for Language.
    /// </param>
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("languages");

        // ISO 639-1 Code (Primary Key).
        builder.HasKey(x => x.ISO6391Code);
        builder.Property(x => x.ISO6391Code)
            .HasColumnName("iso_639_1_code")
            .HasMaxLength(2)
            .IsRequired();

        // Name (Property).
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(64)
            .IsRequired();

        // Endonym (Property).
        builder.Property(x => x.Endonym)
            .HasColumnName("endonym")
            .HasMaxLength(64)
            .IsRequired();
    }
}
