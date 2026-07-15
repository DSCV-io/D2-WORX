using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "key_record",
                columns: table => new
                {
                    kid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    key_domain = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    key_type = table.Column<string>(type: "text", nullable: false),
                    key_material_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    public_key_material = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    activated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    retiring_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    compromised_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    compromise_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_key_record", x => x.kid);
                });

            migrationBuilder.CreateTable(
                name: "key_audit_record",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    resulting_status = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    detail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_key_audit_record", x => x.id);
                    table.ForeignKey(
                        name: "FK_key_audit_record_key_record_kid",
                        column: x => x.kid,
                        principalTable: "key_record",
                        principalColumn: "kid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_key_audit_record_kid_occurred_at",
                table: "key_audit_record",
                columns: new[] { "kid", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_key_record_key_domain_status",
                table: "key_record",
                columns: new[] { "key_domain", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "key_audit_record");

            migrationBuilder.DropTable(
                name: "key_record");
        }
    }
}
