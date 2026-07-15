// -----------------------------------------------------------------------
// <copyright file="KeyAuditRecordConfiguration.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core relational mapping for the append-only <see cref="KeyAuditRecord"/> row
/// in the <c>key_audit_record</c> table.
/// </summary>
/// <remarks>
/// <para>
/// One row is inserted per lifecycle transition; rows are never updated or
/// deleted. The <c>Restrict</c> delete behavior on the foreign key to
/// <c>key_record.kid</c> makes the audit trail tamper-evident — a key row that
/// carries audit history cannot be deleted out from under it.
/// </para>
/// <para>
/// The primary key is a database-generated identity (<c>bigint</c>); the
/// <c>(kid, occurred_at)</c> index serves the per-key chronological history query.
/// </para>
/// </remarks>
public sealed class KeyAuditRecordConfiguration : IEntityTypeConfiguration<KeyAuditRecord>
{
    /// <summary>Max length of a <c>kid</c> value (matches the key-record key).</summary>
    private const int _KID_MAX_LENGTH = 64;

    /// <summary>Max length of the optional non-PII breadcrumb detail.</summary>
    private const int _DETAIL_MAX_LENGTH = 512;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KeyAuditRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("key_audit_record");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.Kid)
            .HasColumnName("kid")
            .HasMaxLength(_KID_MAX_LENGTH);

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasConversion<string>();

        builder.Property(a => a.ResultingStatus)
            .HasColumnName("resulting_status")
            .HasConversion<string>();

        builder.Property(a => a.OccurredAt)
            .HasColumnName("occurred_at");

        builder.Property(a => a.Detail)
            .HasColumnName("detail")
            .HasMaxLength(_DETAIL_MAX_LENGTH);

        // Append-only audit: deleting a key row that has audit history is
        // forbidden (Restrict) so the trail can never be orphaned or silently
        // dropped. The FK is by the kid string (the key-record PK).
        builder.HasOne<KeyRecord>()
            .WithMany()
            .HasForeignKey(a => a.Kid)
            .HasPrincipalKey(k => k.Kid)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.Kid, a.OccurredAt })
            .HasDatabaseName("ix_key_audit_record_kid_occurred_at");
    }
}
