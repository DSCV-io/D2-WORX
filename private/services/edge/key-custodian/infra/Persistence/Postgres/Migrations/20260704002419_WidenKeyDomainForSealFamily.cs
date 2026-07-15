using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class WidenKeyDomainForSealFamily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "key_domain",
                table: "key_record",
                type: "character varying(69)",
                maxLength: 69,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "key_domain",
                table: "key_record",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(69)",
                oldMaxLength: 69);
        }
    }
}
