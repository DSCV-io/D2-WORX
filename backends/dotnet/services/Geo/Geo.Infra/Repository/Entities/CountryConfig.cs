// -----------------------------------------------------------------------
// <copyright file="CountryConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="Country"/> entity.
/// </summary>
public class CountryConfig : IEntityTypeConfiguration<Country>
{
    /// <summary>
    /// Configures the Country entity.
    /// </summary>
    ///
    /// <param name="builder">
    /// The entity type builder for Country.
    /// </param>
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries");

        // ISO 3166-1 Alpha-2 Code (Primary Key).
        builder.HasKey(x => x.ISO31661Alpha2Code);
        builder.Property(x => x.ISO31661Alpha2Code)
            .HasColumnName("iso_3166_1_alpha_2_code")
            .HasMaxLength(2)
            .IsRequired();

        // ISO 3166-1 Alpha-3 Code (Unique).
        builder.HasIndex(x => x.ISO31661Alpha3Code)
            .IsUnique();
        builder.Property(x => x.ISO31661Alpha3Code)
            .HasColumnName("iso_3166_1_alpha_3_code")
            .HasMaxLength(3)
            .IsRequired();

        // ISO 3166-1 Numeric Code (Unique).
        builder.HasIndex(x => x.ISO31661NumericCode)
            .IsUnique();
        builder.Property(x => x.ISO31661NumericCode)
            .HasColumnName("iso_3166_1_numeric_code")
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

        // Phone Number Prefix (Property).
        builder.Property(x => x.PhoneNumberPrefix)
            .HasColumnName("phone_number_prefix")
            .HasMaxLength(3)
            .IsRequired();

        // Sovereign ISO 3166-1 Alpha-2 Code (Foreign Key, Optional + Navigation Collection).
        builder.Property(x => x.SovereignISO31661Alpha2Code)
            .HasColumnName("sovereign_iso_3166_1_alpha_2_code")
            .HasMaxLength(2)
            .IsRequired(false);
        builder.HasOne(x => x.SovereignCountry)
            .WithMany(x => x.Territories)
            .HasForeignKey(x => x.SovereignISO31661Alpha2Code)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Primary Currency (Foreign Key, Optional).
        builder.Property(x => x.PrimaryCurrencyISO4217AlphaCode)
            .HasColumnName("primary_currency_iso_4217_alpha_code")
            .HasMaxLength(3)
            .IsRequired(false);
        builder.HasOne(x => x.PrimaryCurrency)
            .WithMany()
            .HasForeignKey(x => x.PrimaryCurrencyISO4217AlphaCode)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Primary Locale (Foreign Key, Optional).
        builder.Property(x => x.PrimaryLocaleIETFBCP47Tag)
            .HasColumnName("primary_locale_ietf_bcp_47_tag")
            .HasMaxLength(64)
            .IsRequired(false);
        builder.HasOne(x => x.PrimaryLocale)
            .WithMany()
            .HasForeignKey(x => x.PrimaryLocaleIETFBCP47Tag)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Subdivisions (Navigation Collection - One-to-many).
        builder.HasMany(x => x.Subdivisions)
            .WithOne(x => x.Country)
            .HasForeignKey(x => x.CountryISO31661Alpha2Code)
            .OnDelete(DeleteBehavior.Restrict);

        // Currencies (Navigation Collection - Many-to-many).
        builder.HasMany(x => x.Currencies)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "country_currencies",
                j => j.HasOne<Currency>()
                    .WithMany()
                    .HasForeignKey("currency_iso_4217_alpha_code")
                    .IsRequired()
                    .HasPrincipalKey(nameof(Currency.ISO4217AlphaCode))
                    .OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Country>()
                    .WithMany()
                    .HasForeignKey("country_iso_3166_1_alpha_2_code")
                    .IsRequired()
                    .HasPrincipalKey(nameof(Country.ISO31661Alpha2Code))
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey("country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code");
                    j.Property<string>("country_iso_3166_1_alpha_2_code")
                        .HasMaxLength(2)
                        .IsRequired();
                    j.Property<string>("currency_iso_4217_alpha_code")
                        .HasMaxLength(3)
                        .IsRequired();
                });

        // Locales (Navigation Collection - One-to-many).
        builder.HasMany(x => x.Locales)
            .WithOne(x => x.Country)
            .HasForeignKey(x => x.CountryISO31661Alpha2Code)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Timezones (Navigation Collection - One-to-many).
        builder.HasMany(x => x.Timezones)
            .WithOne(x => x.Country)
            .HasForeignKey(x => x.CountryISO31661Alpha2Code)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Geopolitical Entities (Navigation Collection - Many-to-many).
        builder.HasMany(x => x.GeopoliticalEntities)
            .WithMany(x => x.Countries)
            .UsingEntity<Dictionary<string, object>>(
                "country_geopolitical_entities",
                j => j.HasOne<GeopoliticalEntity>()
                    .WithMany()
                    .HasForeignKey("geopolitical_entity_short_code")
                    .IsRequired()
                    .HasPrincipalKey(nameof(GeopoliticalEntity.ShortCode))
                    .OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Country>()
                    .WithMany()
                    .HasForeignKey("country_iso_3166_1_alpha_2_code")
                    .IsRequired()
                    .HasPrincipalKey(nameof(Country.ISO31661Alpha2Code))
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey("country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code");
                    j.Property<string>("country_iso_3166_1_alpha_2_code")
                        .HasMaxLength(2)
                        .IsRequired();
                    j.Property<string>("geopolitical_entity_short_code")
                        .HasMaxLength(64)
                        .IsRequired();
                });
    }
}
