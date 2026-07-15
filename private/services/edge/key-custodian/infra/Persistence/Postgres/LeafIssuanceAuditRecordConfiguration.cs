// -----------------------------------------------------------------------
// <copyright file="LeafIssuanceAuditRecordConfiguration.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Persistence.Postgres;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core relational mapping for the append-only
/// <see cref="LeafIssuanceAuditRecord"/> row in the
/// <c>leaf_issuance_audit_record</c> table.
/// </summary>
/// <remarks>
/// <para>
/// One row is inserted per workload leaf-certificate issuance; rows are never
/// updated or deleted. The <c>Restrict</c> delete behavior on the foreign key to
/// <c>key_record.kid</c> (the issuing intermediate CA) keeps the audit trail
/// tamper-evident — an issuing-CA key row that carries issuance history cannot be
/// deleted out from under it.
/// </para>
/// <para>
/// The primary key is a database-generated identity (<c>bigint</c>); the
/// <c>(workload_service_id, issued_at)</c> index serves the per-workload
/// chronological history query.
/// </para>
/// </remarks>
public sealed class LeafIssuanceAuditRecordConfiguration
    : IEntityTypeConfiguration<LeafIssuanceAuditRecord>
{
    /// <summary>Max length of a workload service identifier.</summary>
    private const int _WORKLOAD_SERVICE_ID_MAX_LENGTH = 64;

    /// <summary>Max length of a <c>kid</c> value (matches the key-record key).</summary>
    private const int _KID_MAX_LENGTH = 64;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LeafIssuanceAuditRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("leaf_issuance_audit_record");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.WorkloadServiceId)
            .HasColumnName("workload_service_id")
            .HasMaxLength(_WORKLOAD_SERVICE_ID_MAX_LENGTH);

        builder.Property(a => a.IssuingCaKid)
            .HasColumnName("issuing_ca_kid")
            .HasMaxLength(_KID_MAX_LENGTH);

        builder.Property(a => a.IssuedAt)
            .HasColumnName("issued_at");

        builder.Property(a => a.LeafNotAfter)
            .HasColumnName("leaf_not_after");

        // Append-only audit: deleting the issuing-CA key row that has issuance
        // history is forbidden (Restrict) so the trail can never be orphaned. The
        // FK is by the issuing-CA kid string (the key-record PK).
        builder.HasOne<KeyRecord>()
            .WithMany()
            .HasForeignKey(a => a.IssuingCaKid)
            .HasPrincipalKey(k => k.Kid)
            .OnDelete(DeleteBehavior.Restrict);

        // Explicit snake_case name so the EF-generated IX_ default is overridden to
        // match the project's ix_ convention (matches KeyRecordConfiguration pattern).
        builder.HasIndex(a => a.IssuingCaKid)
            .HasDatabaseName("ix_leaf_issuance_audit_record_issuing_ca_kid");

        builder.HasIndex(a => new { a.WorkloadServiceId, a.IssuedAt })
            .HasDatabaseName("ix_leaf_issuance_audit_record_workload_service_id_issued_at");
    }
}
