// -----------------------------------------------------------------------
// <copyright file="20251128100056_PrimaryLocales.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#nullable disable

namespace D2.Geo.Infra.Repository.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class PrimaryLocales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-AG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-AI");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-AR");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-AS");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "de-AT");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-AU");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BB",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-BB");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-BF");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-BI");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BJ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-BJ");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BL",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-BL");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-BM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BO",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-BO");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-BS");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BW",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-BW");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BZ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-BZ");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-CA");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-CC");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CD",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-CD");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-CF");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-CG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "de-CH");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-CI");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CK",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-CK");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CL",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-CL");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-CM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CO",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-CO");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-CR");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-CU");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CX",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-CX");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "de-DE");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DJ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-DJ");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-DM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DO",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-DO");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "EC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-EC");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FJ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-FJ");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FK",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-FK");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-FM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-FR");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-GA");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GB",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-GB");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GD",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-GD");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-GF");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-GG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-GH");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-GI");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-GM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-GN");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GP",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-GP");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GQ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-GQ");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-GT");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-GU");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GY",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-GY");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-HN");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-HT");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-IE");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-IM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IO",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-IO");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "it-IT");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-JE");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-JM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JP",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "ja-JP");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-KI");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-KN");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KY",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-KY");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-LC");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "de-LI");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-LR");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-LS");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-MC");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-MF");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-MH");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ML",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-ML");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MP",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-MP");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MQ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-MQ");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-MS");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-MU");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MW",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-MW");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MX",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-MX");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-NA");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-NC");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-NE");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-NF");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-NG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-NI");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-NR");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-NU");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NZ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-NZ");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-PA");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-PE");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-PF");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-PG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-PH");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-PM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-PN");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-PR");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PW",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-PW");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PY",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-PY");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "RE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-RE");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SB",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-SB");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-SC");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-SG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-SH");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SL",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-SL");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "it-SM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-SN");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-SS");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SV",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-SV");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SZ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-SZ");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-TC");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TD",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-TD");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-TG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TK",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-TK");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-TT");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TV",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-TV");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-UG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-UM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "US",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-US");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UY",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-UY");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "it-VA");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-VC");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "es-VE");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-VG");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-VI");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "WF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-WF");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "YT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "fr-YT");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-ZA");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-ZM");

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZW",
                column: "primary_locale_ietf_bcp_47_tag",
                value: "en-ZW");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BB",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BJ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BL",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BO",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BW",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BZ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CD",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CK",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CL",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CO",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CX",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DJ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DO",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "EC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FJ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FK",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GB",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GD",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GP",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GQ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GY",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IO",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JP",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KY",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ML",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MP",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MQ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MW",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MX",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NU",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NZ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PR",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PW",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PY",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "RE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SB",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SH",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SL",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SN",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SS",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SV",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SZ",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TD",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TK",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TV",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "US",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UY",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VC",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VE",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VG",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VI",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "WF",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "YT",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZA",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZM",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);

            migrationBuilder.UpdateData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZW",
                column: "primary_locale_ietf_bcp_47_tag",
                value: null);
        }
    }
}
