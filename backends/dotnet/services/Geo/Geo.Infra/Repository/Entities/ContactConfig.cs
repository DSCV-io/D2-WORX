// -----------------------------------------------------------------------
// <copyright file="ContactConfig.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Entities;

using System.Collections.Immutable;
using System.Text.Json;
using D2.Geo.Domain.Entities;
using D2.Geo.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for the <see cref="Contact"/> entity.
/// </summary>
public class ContactConfig : IEntityTypeConfiguration<Contact>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");

        // Primary key (GUID v7).
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .IsRequired();

        // General properties.
        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Metadata properties.
        builder.Property(c => c.ContextKey)
            .HasColumnName("context_key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.RelatedEntityId)
            .HasColumnName("related_entity_id")
            .IsRequired();

        builder.Property(c => c.IETFBCP47Tag)
            .HasColumnName("ietf_bcp47_tag")
            .HasMaxLength(35)
            .HasDefaultValue("en-US")
            .IsRequired();

        // FK relationship to Locale entity (like Location → Country).
        builder.HasOne(c => c.Locale)
            .WithMany()
            .HasForeignKey(c => c.IETFBCP47Tag)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.IANAIdentifier)
            .HasColumnName("iana_identifier")
            .HasMaxLength(64)
            .HasDefaultValue("America/New_York")
            .IsRequired();

        // FK relationship to Timezone entity (mirrors Locale FK above).
        builder.HasOne(c => c.Timezone)
            .WithMany()
            .HasForeignKey(c => c.IANAIdentifier)
            .OnDelete(DeleteBehavior.Restrict);

        // ContactMethods (stored as JSONB via value converter).
        // Using value converter instead of OwnsOne because nested types contain
        // ImmutableHashSet<string> which EF Core can't model, but System.Text.Json handles fine.
        builder.Property(c => c.ContactMethods)
            .HasColumnName("contact_methods")
            .HasColumnType("jsonb")
            .HasConversion(
                cm => cm != null ? JsonSerializer.Serialize(cm, (JsonSerializerOptions?)null) : null,
                json => json != null ? JsonSerializer.Deserialize<ContactMethods>(json, (JsonSerializerOptions?)null) : null);

        // Personal details (owned, flattened as columns).
        builder.OwnsOne(c => c.PersonalDetails, p =>
        {
            p.Property(x => x.Title)
                .HasColumnName("personal_title")
                .HasConversion<string?>()
                .HasMaxLength(20);

            p.Property(x => x.FirstName)
                .HasColumnName("personal_first_name")
                .HasMaxLength(255);

            p.Property(x => x.PreferredName)
                .HasColumnName("personal_preferred_name")
                .HasMaxLength(255);

            p.Property(x => x.MiddleName)
                .HasColumnName("personal_middle_name")
                .HasMaxLength(255);

            p.Property(x => x.LastName)
                .HasColumnName("personal_last_name")
                .HasMaxLength(255);

            p.Property(x => x.GenerationalSuffix)
                .HasColumnName("personal_generational_suffix")
                .HasConversion<string?>()
                .HasMaxLength(10);

            // ProfessionalCredentials stored as text[] array.
            // Using value converter because ImmutableList has no parameterless constructor.
            p.Property(x => x.ProfessionalCredentials)
                .HasColumnName("personal_professional_credentials")
                .HasColumnType("text[]")
                .HasConversion(
                    list => list.ToArray(),
                    arr => arr.ToImmutableList(),
                    new ValueComparer<ImmutableList<string>>(
                        (a, b) => a != null && b != null && a.SequenceEqual(b),
                        c => c.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
                        c => ImmutableList.CreateRange(c)));

            p.Property(x => x.DateOfBirth)
                .HasColumnName("personal_date_of_birth");

            p.Property(x => x.BiologicalSex)
                .HasColumnName("personal_biological_sex")
                .HasConversion<string?>()
                .HasMaxLength(20);
        });

        // Professional details (owned, flattened as columns).
        builder.OwnsOne(c => c.ProfessionalDetails, pr =>
        {
            pr.Property(x => x.CompanyName)
                .HasColumnName("professional_company_name")
                .HasMaxLength(255);

            pr.Property(x => x.JobTitle)
                .HasColumnName("professional_job_title")
                .HasMaxLength(255);

            pr.Property(x => x.Department)
                .HasColumnName("professional_department")
                .HasMaxLength(255);

            pr.Property(x => x.CompanyWebsite)
                .HasColumnName("professional_company_website")
                .HasConversion(
                    uri => uri != null ? uri.ToString() : null,
                    str => str != null ? new Uri(str) : null)
                .HasMaxLength(2048);
        });

        // Foreign key to Location.
        builder.Property(c => c.LocationHashId)
            .HasColumnName("location_hash_id")
            .HasMaxLength(64);

        builder.HasOne(c => c.Location)
            .WithMany()
            .HasForeignKey(c => c.LocationHashId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes.
        builder.HasIndex(c => new { c.ContextKey, c.RelatedEntityId })
            .HasDatabaseName("ix_contacts_context_key_related_entity_id")
            .IsUnique();

        // Note: GIN index on contact_methods JSONB should be added via raw SQL migration
        // since EF Core doesn't support indexing owned JSON types directly.
        // SQL: CREATE INDEX ix_contacts_contact_methods ON contacts USING gin (contact_methods);
    }
}
