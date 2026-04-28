// -----------------------------------------------------------------------
// <copyright file="20251123035938_RefDataInit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#nullable disable

namespace D2.Geo.Infra.Repository.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class RefDataInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    iso_4217_alpha_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    iso_4217_numeric_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    official_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    decimal_places = table.Column<int>(type: "integer", nullable: false),
                    symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.iso_4217_alpha_code);
                });

            migrationBuilder.CreateTable(
                name: "geopolitical_entities",
                columns: table => new
                {
                    short_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geopolitical_entities", x => x.short_code);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    iso_639_1_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    endonym = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.iso_639_1_code);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    iso_3166_1_alpha_2_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    iso_3166_1_alpha_3_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    iso_3166_1_numeric_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    official_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    phone_number_prefix = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    phone_number_format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sovereign_iso_3166_1_alpha_2_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    primary_currency_iso_4217_alpha_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    primary_locale_ietf_bcp_47_tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.iso_3166_1_alpha_2_code);
                    table.ForeignKey(
                        name: "FK_countries_countries_sovereign_iso_3166_1_alpha_2_code",
                        column: x => x.sovereign_iso_3166_1_alpha_2_code,
                        principalTable: "countries",
                        principalColumn: "iso_3166_1_alpha_2_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_countries_currencies_primary_currency_iso_4217_alpha_code",
                        column: x => x.primary_currency_iso_4217_alpha_code,
                        principalTable: "currencies",
                        principalColumn: "iso_4217_alpha_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "country_currencies",
                columns: table => new
                {
                    country_iso_3166_1_alpha_2_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    currency_iso_4217_alpha_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_country_currencies", x => new { x.country_iso_3166_1_alpha_2_code, x.currency_iso_4217_alpha_code });
                    table.ForeignKey(
                        name: "FK_country_currencies_countries_country_iso_3166_1_alpha_2_code",
                        column: x => x.country_iso_3166_1_alpha_2_code,
                        principalTable: "countries",
                        principalColumn: "iso_3166_1_alpha_2_code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_country_currencies_currencies_currency_iso_4217_alpha_code",
                        column: x => x.currency_iso_4217_alpha_code,
                        principalTable: "currencies",
                        principalColumn: "iso_4217_alpha_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "country_geopolitical_entities",
                columns: table => new
                {
                    country_iso_3166_1_alpha_2_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    geopolitical_entity_short_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_country_geopolitical_entities", x => new { x.country_iso_3166_1_alpha_2_code, x.geopolitical_entity_short_code });
                    table.ForeignKey(
                        name: "FK_country_geopolitical_entities_countries_country_iso_3166_1_~",
                        column: x => x.country_iso_3166_1_alpha_2_code,
                        principalTable: "countries",
                        principalColumn: "iso_3166_1_alpha_2_code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_country_geopolitical_entities_geopolitical_entities_geopoli~",
                        column: x => x.geopolitical_entity_short_code,
                        principalTable: "geopolitical_entities",
                        principalColumn: "short_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "locales",
                columns: table => new
                {
                    ietf_bcp_47_tag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    endonym = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    language_iso_639_1_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    country_iso_3166_1_alpha_2_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locales", x => x.ietf_bcp_47_tag);
                    table.ForeignKey(
                        name: "FK_locales_countries_country_iso_3166_1_alpha_2_code",
                        column: x => x.country_iso_3166_1_alpha_2_code,
                        principalTable: "countries",
                        principalColumn: "iso_3166_1_alpha_2_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_locales_languages_language_iso_639_1_code",
                        column: x => x.language_iso_639_1_code,
                        principalTable: "languages",
                        principalColumn: "iso_639_1_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subdivisions",
                columns: table => new
                {
                    iso_3166_2_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    short_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    official_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    country_iso_3166_1_alpha_2_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subdivisions", x => x.iso_3166_2_code);
                    table.ForeignKey(
                        name: "FK_subdivisions_countries_country_iso_3166_1_alpha_2_code",
                        column: x => x.country_iso_3166_1_alpha_2_code,
                        principalTable: "countries",
                        principalColumn: "iso_3166_1_alpha_2_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_countries_iso_3166_1_alpha_3_code",
                table: "countries",
                column: "iso_3166_1_alpha_3_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_countries_iso_3166_1_numeric_code",
                table: "countries",
                column: "iso_3166_1_numeric_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_countries_primary_currency_iso_4217_alpha_code",
                table: "countries",
                column: "primary_currency_iso_4217_alpha_code");

            migrationBuilder.CreateIndex(
                name: "IX_countries_primary_locale_ietf_bcp_47_tag",
                table: "countries",
                column: "primary_locale_ietf_bcp_47_tag");

            migrationBuilder.CreateIndex(
                name: "IX_countries_sovereign_iso_3166_1_alpha_2_code",
                table: "countries",
                column: "sovereign_iso_3166_1_alpha_2_code");

            migrationBuilder.CreateIndex(
                name: "IX_country_currencies_currency_iso_4217_alpha_code",
                table: "country_currencies",
                column: "currency_iso_4217_alpha_code");

            migrationBuilder.CreateIndex(
                name: "IX_country_geopolitical_entities_geopolitical_entity_short_code",
                table: "country_geopolitical_entities",
                column: "geopolitical_entity_short_code");

            migrationBuilder.CreateIndex(
                name: "IX_currencies_iso_4217_numeric_code",
                table: "currencies",
                column: "iso_4217_numeric_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locales_country_iso_3166_1_alpha_2_code",
                table: "locales",
                column: "country_iso_3166_1_alpha_2_code");

            migrationBuilder.CreateIndex(
                name: "IX_locales_language_iso_639_1_code",
                table: "locales",
                column: "language_iso_639_1_code");

            migrationBuilder.CreateIndex(
                name: "IX_subdivisions_country_iso_3166_1_alpha_2_code",
                table: "subdivisions",
                column: "country_iso_3166_1_alpha_2_code");

            migrationBuilder.AddForeignKey(
                name: "FK_countries_locales_primary_locale_ietf_bcp_47_tag",
                table: "countries",
                column: "primary_locale_ietf_bcp_47_tag",
                principalTable: "locales",
                principalColumn: "ietf_bcp_47_tag",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_countries_currencies_primary_currency_iso_4217_alpha_code",
                table: "countries");

            migrationBuilder.DropForeignKey(
                name: "FK_countries_locales_primary_locale_ietf_bcp_47_tag",
                table: "countries");

            migrationBuilder.DropTable(
                name: "country_currencies");

            migrationBuilder.DropTable(
                name: "country_geopolitical_entities");

            migrationBuilder.DropTable(
                name: "subdivisions");

            migrationBuilder.DropTable(
                name: "geopolitical_entities");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "locales");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "languages");
        }
    }
}
