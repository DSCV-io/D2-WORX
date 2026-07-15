// -----------------------------------------------------------------------
// <copyright file="KeyRecordConfiguration.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core relational mapping for the flat <see cref="KeyRecord"/> row in the
/// <c>key_record</c> table.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KeyRecord.Status"/> is an ORDINARY settable value column (stored as
/// the enum's string name), NOT a table-per-hierarchy discriminator — the flat
/// record's CLR type never changes, so a transition is a plain <c>UPDATE</c>.
/// </para>
/// <para>
/// <see cref="KeyRecord.Xmin"/> maps to PostgreSQL's <c>xmin</c> system column as
/// the optimistic-concurrency token (<see cref="PropertyBuilder.IsRowVersion"/> +
/// <c>xid</c> column type) — two concurrent transitions on the same row resolve to
/// exactly one winner; the loser's <c>SaveChangesAsync</c> throws
/// <c>DbUpdateConcurrencyException</c>.
/// </para>
/// <para>
/// <c>Instant</c> columns map to <c>TIMESTAMPTZ</c> automatically via
/// <c>AddD2NodaTime</c> (wired by the shared Npgsql-defaults applier), so no
/// per-column value converter is declared here.
/// </para>
/// </remarks>
public sealed class KeyRecordConfiguration : IEntityTypeConfiguration<KeyRecord>
{
    /// <summary>Max length of a <c>kid</c> value (the minted identifier).</summary>
    private const int _KID_MAX_LENGTH = 64;

    /// <summary>
    /// Max length of a key-domain wire value. The longest legal domain is a seal-family
    /// value: the <c>seal:</c> prefix (5 chars) + a maximum-length workload service id
    /// (64 chars) = 69. Catalog literals are all far shorter.
    /// </summary>
    private const int _KEY_DOMAIN_MAX_LENGTH = 69;

    /// <summary>Max length of an operator-supplied compromise reason.</summary>
    private const int _COMPROMISE_REASON_MAX_LENGTH = 512;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KeyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("key_record");

        builder.HasKey(k => k.Kid);

        builder.Property(k => k.Kid)
            .HasColumnName("kid")
            .HasMaxLength(_KID_MAX_LENGTH);

        builder.Property(k => k.KeyDomain)
            .HasColumnName("key_domain")
            .HasMaxLength(_KEY_DOMAIN_MAX_LENGTH);

        // Enum stored as its string name (stable, human-readable, migration-safe).
        builder.Property(k => k.KeyType)
            .HasColumnName("key_type")
            .HasConversion<string>();

        builder.Property(k => k.KeyMaterialEncrypted)
            .HasColumnName("key_material_encrypted");

        builder.Property(k => k.PublicKeyMaterial)
            .HasColumnName("public_key_material");

        builder.Property(k => k.CaCertificate)
            .HasColumnName("ca_certificate");

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at");

        // Settable VALUE column (NOT a TPH discriminator) — the flat record's CLR
        // type never changes; always derived from the aggregate's sealed state.
        builder.Property(k => k.Status)
            .HasColumnName("status")
            .HasConversion<string>();

        builder.Property(k => k.ActivatedAt)
            .HasColumnName("activated_at");

        builder.Property(k => k.RetiringAt)
            .HasColumnName("retiring_at");

        builder.Property(k => k.RetiredAt)
            .HasColumnName("retired_at");

        builder.Property(k => k.CompromisedAt)
            .HasColumnName("compromised_at");

        builder.Property(k => k.CompromiseReason)
            .HasColumnName("compromise_reason")
            .HasMaxLength(_COMPROMISE_REASON_MAX_LENGTH);

        // PostgreSQL xmin system column as the optimistic-concurrency token.
        builder.Property(k => k.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // The rotation hot path filters by (domain, status) — e.g. "the Active
        // key for jwks-signing". One composite index serves every domain-scoped
        // lifecycle query.
        builder.HasIndex(k => new { k.KeyDomain, k.Status })
            .HasDatabaseName("ix_key_record_key_domain_status");

        // Invariant: at most ONE Pending key per domain — the structural backstop
        // for the generate/rotate race window, so the guarantee no longer rests on
        // the rotation advisory lock alone. A partial UNIQUE index filtered to the
        // persisted 'Pending' status literal (the enum is stored by string name via
        // HasConversion<string>() — the literal MUST track KeyStatus.Pending's
        // persisted name, pinned by KeyCustodianPersistedEnumStabilityTests).
        //
        // Modeling it here (rather than in raw SQL) is deliberate: EF's command-
        // batch preparer uses the unique-index value dependency to emit the
        // releasing UPDATE before the acquiring INSERT within a single
        // SaveChangesAsync, so the CompromiseKey "retire the old Pending + insert a
        // fresh Pending" swap succeeds. A duplicate raises SQLSTATE 23505
        // (unique_violation), classified to a typed 409 by the shared repo pipeline.
        //
        // The companion "one Active per domain" invariant is enforced by a partial,
        // DEFERRABLE EXCLUSION constraint added in raw SQL by the
        // OnePendingIndexAndActiveExclusion migration — EF's fluent API cannot model
        // EXCLUDE, and the RotateKey Active->Retiring + Pending->Active swap needs
        // the check deferred to COMMIT (a non-deferrable partial index would reject
        // the transient two-Active state mid-transaction). That constraint is
        // intentionally invisible to the EF model (and to the model snapshot).
        builder.HasIndex(k => k.KeyDomain)
            .HasDatabaseName("ux_key_record_one_pending_per_domain")
            .IsUnique()
            .HasFilter("status = 'Pending'");
    }
}
