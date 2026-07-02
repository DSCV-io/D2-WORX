using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2.Edge.KeyCustodian.Infra.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class OnePendingIndexAndActiveExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_key_record_one_pending_per_domain",
                table: "key_record",
                column: "key_domain",
                unique: true,
                filter: "status = 'Pending'");

            // Invariant: at most ONE Active key per domain. EF's fluent API cannot
            // model a partial DEFERRABLE EXCLUSION constraint, so it is added here in
            // raw SQL (the only path — a partial UNIQUE index cannot be DEFERRABLE,
            // and the RotateKey Active->Retiring + Pending->Active swap needs the
            // check deferred to COMMIT so the transient two-Active state inside one
            // transaction is tolerated). The constraint is invisible to the EF model
            // + snapshot, so it never round-trips into a scaffolded migration.
            // btree_gist supplies the "=" gist operator class for the text
            // key_domain column; it is a trusted extension (installable by any role
            // with CREATE on the database) and is shared, so it is created
            // idempotently and never dropped on Down. The 'Active' literal is the
            // persisted KeyStatus.Active string name (pinned by
            // KeyCustodianPersistedEnumStabilityTests).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                "ALTER TABLE key_record ADD CONSTRAINT ux_key_record_one_active_per_domain "
                + "EXCLUDE USING gist (key_domain WITH =) WHERE (status = 'Active') "
                + "DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE key_record DROP CONSTRAINT ux_key_record_one_active_per_domain;");

            migrationBuilder.DropIndex(
                name: "ux_key_record_one_pending_per_domain",
                table: "key_record");
        }
    }
}
