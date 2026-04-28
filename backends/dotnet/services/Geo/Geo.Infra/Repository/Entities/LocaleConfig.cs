// -----------------------------------------------------------------------
// <copyright file="LocaleConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="Locale"/> entity.
/// </summary>
public class LocaleConfig : IEntityTypeConfiguration<Locale>
{
    /// <summary>
    /// Configures the Locale entity.
    /// </summary>
    ///
    /// <param name="builder">
    /// The entity type builder for Locale.
    /// </param>
    public void Configure(EntityTypeBuilder<Locale> builder)
    {
        builder.ToTable("locales");

        // IETF BCP 47 Tag (Primary Key).
        builder.HasKey(x => x.IETFBCP47Tag);
        builder.Property(x => x.IETFBCP47Tag)
            .HasColumnName("ietf_bcp_47_tag")
            .HasMaxLength(35)
            .IsRequired();

        // Name (Property).
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(128)
            .IsRequired();

        // Endonym (Property).
        builder.Property(x => x.Endonym)
            .HasColumnName("endonym")
            .HasMaxLength(128)
            .IsRequired();

        // Language ISO 639-1 Code (Foreign Key, Required).
        builder.Property(x => x.LanguageISO6391Code)
            .HasColumnName("language_iso_639_1_code")
            .HasMaxLength(2)
            .IsRequired();

        // Country ISO 3166-1 Alpha-2 Code (Foreign Key, Required).
        builder.Property(x => x.CountryISO31661Alpha2Code)
            .HasColumnName("country_iso_3166_1_alpha_2_code")
            .HasMaxLength(2)
            .IsRequired();

        // Language (Navigation Property - Many-to-one).
        builder.HasOne(x => x.Language)
            .WithMany()
            .HasForeignKey(x => x.LanguageISO6391Code)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Country (Navigation Property - Many-to-one).
        // Note: Relationship already configured from Country side.
    }
}
