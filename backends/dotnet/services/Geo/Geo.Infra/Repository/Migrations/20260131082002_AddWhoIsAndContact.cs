// -----------------------------------------------------------------------
// <copyright file="20260131082002_AddWhoIsAndContact.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#nullable disable

namespace D2.Geo.Infra.Repository.Migrations
{
    using System;
    using System.Collections.Immutable;
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class AddWhoIsAndContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_locations_countries_country_iso_3166_1_alpha_2_code",
                table: "locations");

            migrationBuilder.DropForeignKey(
                name: "FK_locations_subdivisions_subdivision_iso_3166_2_code",
                table: "locations");

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    context_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_methods = table.Column<string>(type: "jsonb", nullable: true),
                    personal_title = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    personal_first_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    personal_preferred_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    personal_middle_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    personal_last_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    personal_generational_suffix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    personal_professional_credentials = table.Column<ImmutableList<string>>(type: "character varying(50)[]", nullable: true),
                    personal_date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    personal_biological_sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    professional_company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    professional_job_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    professional_department = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    professional_company_website = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    location_hash_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_contacts_locations_location_hash_id",
                        column: x => x.location_hash_id,
                        principalTable: "locations",
                        principalColumn: "hash_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "who_is",
                columns: table => new
                {
                    hash_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    asn = table.Column<int>(type: "integer", nullable: true),
                    as_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    as_domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    as_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    carrier_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    mcc = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    mnc = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    as_changed = table.Column<DateOnly>(type: "date", nullable: true),
                    geo_changed = table.Column<DateOnly>(type: "date", nullable: true),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: true),
                    is_anycast = table.Column<bool>(type: "boolean", nullable: true),
                    is_hosting = table.Column<bool>(type: "boolean", nullable: true),
                    is_mobile = table.Column<bool>(type: "boolean", nullable: true),
                    is_satellite = table.Column<bool>(type: "boolean", nullable: true),
                    is_proxy = table.Column<bool>(type: "boolean", nullable: true),
                    is_relay = table.Column<bool>(type: "boolean", nullable: true),
                    is_tor = table.Column<bool>(type: "boolean", nullable: true),
                    is_vpn = table.Column<bool>(type: "boolean", nullable: true),
                    privacy_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    location_hash_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_who_is", x => x.hash_id);
                    table.ForeignKey(
                        name: "FK_who_is_locations_location_hash_id",
                        column: x => x.location_hash_id,
                        principalTable: "locations",
                        principalColumn: "hash_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contacts_context_key_related_entity_id",
                table: "contacts",
                columns: new[] { "context_key", "related_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_location_hash_id",
                table: "contacts",
                column: "location_hash_id");

            migrationBuilder.CreateIndex(
                name: "ix_who_is_fingerprint",
                table: "who_is",
                column: "fingerprint",
                filter: "fingerprint IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_who_is_ip_address",
                table: "who_is",
                column: "ip_address");

            migrationBuilder.CreateIndex(
                name: "IX_who_is_location_hash_id",
                table: "who_is",
                column: "location_hash_id");

            migrationBuilder.AddForeignKey(
                name: "FK_locations_countries_country_iso_3166_1_alpha_2_code",
                table: "locations",
                column: "country_iso_3166_1_alpha_2_code",
                principalTable: "countries",
                principalColumn: "iso_3166_1_alpha_2_code",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_locations_subdivisions_subdivision_iso_3166_2_code",
                table: "locations",
                column: "subdivision_iso_3166_2_code",
                principalTable: "subdivisions",
                principalColumn: "iso_3166_2_code",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_locations_countries_country_iso_3166_1_alpha_2_code",
                table: "locations");

            migrationBuilder.DropForeignKey(
                name: "FK_locations_subdivisions_subdivision_iso_3166_2_code",
                table: "locations");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropTable(
                name: "who_is");

            migrationBuilder.AddForeignKey(
                name: "FK_locations_countries_country_iso_3166_1_alpha_2_code",
                table: "locations",
                column: "country_iso_3166_1_alpha_2_code",
                principalTable: "countries",
                principalColumn: "iso_3166_1_alpha_2_code");

            migrationBuilder.AddForeignKey(
                name: "FK_locations_subdivisions_subdivision_iso_3166_2_code",
                table: "locations",
                column: "subdivision_iso_3166_2_code",
                principalTable: "subdivisions",
                principalColumn: "iso_3166_2_code");
        }
    }
}
