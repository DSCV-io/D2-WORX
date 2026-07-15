using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddX509CaCertificateAndLeafIssuanceAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ca_certificate",
                table: "key_record",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "leaf_issuance_audit_record",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workload_service_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issuing_ca_kid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    leaf_not_after = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaf_issuance_audit_record", x => x.id);
                    table.ForeignKey(
                        name: "FK_leaf_issuance_audit_record_key_record_issuing_ca_kid",
                        column: x => x.issuing_ca_kid,
                        principalTable: "key_record",
                        principalColumn: "kid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_leaf_issuance_audit_record_issuing_ca_kid",
                table: "leaf_issuance_audit_record",
                column: "issuing_ca_kid");

            migrationBuilder.CreateIndex(
                name: "ix_leaf_issuance_audit_record_workload_service_id_issued_at",
                table: "leaf_issuance_audit_record",
                columns: new[] { "workload_service_id", "issued_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leaf_issuance_audit_record");

            migrationBuilder.DropColumn(
                name: "ca_certificate",
                table: "key_record");
        }
    }
}
