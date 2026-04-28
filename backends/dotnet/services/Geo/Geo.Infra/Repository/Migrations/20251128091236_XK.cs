// -----------------------------------------------------------------------
// <copyright file="20251128091236_XK.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace D2.Geo.Infra.Repository.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class XK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "iso_3166_1_alpha_2_code", "display_name", "iso_3166_1_alpha_3_code", "iso_3166_1_numeric_code", "official_name", "phone_number_prefix", "primary_currency_iso_4217_alpha_code", "primary_locale_ietf_bcp_47_tag", "sovereign_iso_3166_1_alpha_2_code" },
                values: new object[] { "XK", "Kosovo", "XKX", "383", "Republic of Kosovo", "383", "EUR", null, null });

            migrationBuilder.InsertData(
                table: "country_geopolitical_entities",
                columns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                values: new object[,]
                {
                    { "XK", "BALK" },
                    { "XK", "EU" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "XK", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "XK", "EU" });

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "XK");
        }
    }
}
