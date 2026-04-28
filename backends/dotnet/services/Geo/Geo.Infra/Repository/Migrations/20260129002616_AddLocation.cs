// -----------------------------------------------------------------------
// <copyright file="20260129002616_AddLocation.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#nullable disable

namespace D2.Geo.Infra.Repository.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    hash_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    address_line_1 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    address_line_2 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    address_line_3 = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    city = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    subdivision_iso_3166_2_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    country_iso_3166_1_alpha_2_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.hash_id);
                    table.ForeignKey(
                        name: "FK_locations_countries_country_iso_3166_1_alpha_2_code",
                        column: x => x.country_iso_3166_1_alpha_2_code,
                        principalTable: "countries",
                        principalColumn: "iso_3166_1_alpha_2_code");
                    table.ForeignKey(
                        name: "FK_locations_subdivisions_subdivision_iso_3166_2_code",
                        column: x => x.subdivision_iso_3166_2_code,
                        principalTable: "subdivisions",
                        principalColumn: "iso_3166_2_code");
                });

            migrationBuilder.CreateIndex(
                name: "IX_locations_country_iso_3166_1_alpha_2_code",
                table: "locations",
                column: "country_iso_3166_1_alpha_2_code");

            migrationBuilder.CreateIndex(
                name: "IX_locations_subdivision_iso_3166_2_code",
                table: "locations",
                column: "subdivision_iso_3166_2_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "locations");
        }
    }
}
