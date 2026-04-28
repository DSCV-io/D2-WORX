// -----------------------------------------------------------------------
// <copyright file="20251124095406_SeedLocales.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace D2.Geo.Infra.Repository.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;

    /// <inheritdoc />
    public partial class SeedLocales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "locales",
                columns: new[] { "ietf_bcp_47_tag", "country_iso_3166_1_alpha_2_code", "endonym", "language_iso_639_1_code", "name" },
                values: new object[,]
                {
                    { "de-AT", "AT", "Deutsch (Österreich)", "de", "German (Austria)" },
                    { "de-BE", "BE", "Deutsch (Belgien)", "de", "German (Belgium)" },
                    { "de-CH", "CH", "Deutsch (Schweiz)", "de", "German (Switzerland)" },
                    { "de-DE", "DE", "Deutsch (Deutschland)", "de", "German (Germany)" },
                    { "de-LI", "LI", "Deutsch (Liechtenstein)", "de", "German (Liechtenstein)" },
                    { "de-LU", "LU", "Deutsch (Luxemburg)", "de", "German (Luxembourg)" },
                    { "en-AG", "AG", "English (Antigua and Barbuda)", "en", "English (Antigua and Barbuda)" },
                    { "en-AI", "AI", "English (Anguilla)", "en", "English (Anguilla)" },
                    { "en-AS", "AS", "English (American Samoa)", "en", "English (American Samoa)" },
                    { "en-AU", "AU", "English (Australia)", "en", "English (Australia)" },
                    { "en-BB", "BB", "English (Barbados)", "en", "English (Barbados)" },
                    { "en-BM", "BM", "English (Bermuda)", "en", "English (Bermuda)" },
                    { "en-BS", "BS", "English (Bahamas)", "en", "English (Bahamas)" },
                    { "en-BW", "BW", "English (Botswana)", "en", "English (Botswana)" },
                    { "en-BZ", "BZ", "English (Belize)", "en", "English (Belize)" },
                    { "en-CA", "CA", "English (Canada)", "en", "English (Canada)" },
                    { "en-CC", "CC", "English (Cocos (Keeling) Islands)", "en", "English (Cocos (Keeling) Islands)" },
                    { "en-CK", "CK", "English (Cook Islands)", "en", "English (Cook Islands)" },
                    { "en-CX", "CX", "English (Christmas Island)", "en", "English (Christmas Island)" },
                    { "en-DM", "DM", "English (Dominica)", "en", "English (Dominica)" },
                    { "en-FJ", "FJ", "English (Fiji)", "en", "English (Fiji)" },
                    { "en-FK", "FK", "English (Falkland Islands)", "en", "English (Falkland Islands)" },
                    { "en-FM", "FM", "English (Micronesia)", "en", "English (Micronesia)" },
                    { "en-GB", "GB", "English (United Kingdom)", "en", "English (United Kingdom)" },
                    { "en-GD", "GD", "English (Grenada)", "en", "English (Grenada)" },
                    { "en-GG", "GG", "English (Guernsey)", "en", "English (Guernsey)" },
                    { "en-GH", "GH", "English (Ghana)", "en", "English (Ghana)" },
                    { "en-GI", "GI", "English (Gibraltar)", "en", "English (Gibraltar)" },
                    { "en-GM", "GM", "English (Gambia)", "en", "English (Gambia)" },
                    { "en-GU", "GU", "English (Guam)", "en", "English (Guam)" },
                    { "en-GY", "GY", "English (Guyana)", "en", "English (Guyana)" },
                    { "en-IE", "IE", "English (Ireland)", "en", "English (Ireland)" },
                    { "en-IM", "IM", "English (Isle of Man)", "en", "English (Isle of Man)" },
                    { "en-IO", "IO", "English (British Indian Ocean Territory)", "en", "English (British Indian Ocean Territory)" },
                    { "en-JE", "JE", "English (Jersey)", "en", "English (Jersey)" },
                    { "en-JM", "JM", "English (Jamaica)", "en", "English (Jamaica)" },
                    { "en-KI", "KI", "English (Kiribati)", "en", "English (Kiribati)" },
                    { "en-KN", "KN", "English (Saint Kitts and Nevis)", "en", "English (Saint Kitts and Nevis)" },
                    { "en-KY", "KY", "English (Cayman Islands)", "en", "English (Cayman Islands)" },
                    { "en-LC", "LC", "English (Saint Lucia)", "en", "English (Saint Lucia)" },
                    { "en-LR", "LR", "English (Liberia)", "en", "English (Liberia)" },
                    { "en-LS", "LS", "English (Lesotho)", "en", "English (Lesotho)" },
                    { "en-MH", "MH", "English (Marshall Islands)", "en", "English (Marshall Islands)" },
                    { "en-MP", "MP", "English (Northern Mariana Islands)", "en", "English (Northern Mariana Islands)" },
                    { "en-MS", "MS", "English (Montserrat)", "en", "English (Montserrat)" },
                    { "en-MU", "MU", "English (Mauritius)", "en", "English (Mauritius)" },
                    { "en-MW", "MW", "English (Malawi)", "en", "English (Malawi)" },
                    { "en-NA", "NA", "English (Namibia)", "en", "English (Namibia)" },
                    { "en-NF", "NF", "English (Norfolk Island)", "en", "English (Norfolk Island)" },
                    { "en-NG", "NG", "English (Nigeria)", "en", "English (Nigeria)" },
                    { "en-NR", "NR", "English (Nauru)", "en", "English (Nauru)" },
                    { "en-NU", "NU", "English (Niue)", "en", "English (Niue)" },
                    { "en-NZ", "NZ", "English (New Zealand)", "en", "English (New Zealand)" },
                    { "en-PG", "PG", "English (Papua New Guinea)", "en", "English (Papua New Guinea)" },
                    { "en-PH", "PH", "English (Philippines)", "en", "English (Philippines)" },
                    { "en-PN", "PN", "English (Pitcairn Islands)", "en", "English (Pitcairn Islands)" },
                    { "en-PW", "PW", "English (Palau)", "en", "English (Palau)" },
                    { "en-SB", "SB", "English (Solomon Islands)", "en", "English (Solomon Islands)" },
                    { "en-SC", "SC", "English (Seychelles)", "en", "English (Seychelles)" },
                    { "en-SG", "SG", "English (Singapore)", "en", "English (Singapore)" },
                    { "en-SH", "SH", "English (Saint Helena)", "en", "English (Saint Helena)" },
                    { "en-SL", "SL", "English (Sierra Leone)", "en", "English (Sierra Leone)" },
                    { "en-SS", "SS", "English (South Sudan)", "en", "English (South Sudan)" },
                    { "en-SZ", "SZ", "English (Eswatini)", "en", "English (Eswatini)" },
                    { "en-TC", "TC", "English (Turks and Caicos Islands)", "en", "English (Turks and Caicos Islands)" },
                    { "en-TK", "TK", "English (Tokelau)", "en", "English (Tokelau)" },
                    { "en-TT", "TT", "English (Trinidad and Tobago)", "en", "English (Trinidad and Tobago)" },
                    { "en-TV", "TV", "English (Tuvalu)", "en", "English (Tuvalu)" },
                    { "en-UG", "UG", "English (Uganda)", "en", "English (Uganda)" },
                    { "en-UM", "UM", "English (U.S. Outlying Islands)", "en", "English (U.S. Outlying Islands)" },
                    { "en-US", "US", "English (United States)", "en", "English (United States)" },
                    { "en-VC", "VC", "English (Saint Vincent and the Grenadines)", "en", "English (Saint Vincent and the Grenadines)" },
                    { "en-VG", "VG", "English (British Virgin Islands)", "en", "English (British Virgin Islands)" },
                    { "en-VI", "VI", "English (U.S. Virgin Islands)", "en", "English (U.S. Virgin Islands)" },
                    { "en-ZA", "ZA", "English (South Africa)", "en", "English (South Africa)" },
                    { "en-ZM", "ZM", "English (Zambia)", "en", "English (Zambia)" },
                    { "en-ZW", "ZW", "English (Zimbabwe)", "en", "English (Zimbabwe)" },
                    { "es-AR", "AR", "Español (Argentina)", "es", "Spanish (Argentina)" },
                    { "es-BO", "BO", "Español (Bolivia)", "es", "Spanish (Bolivia)" },
                    { "es-CL", "CL", "Español (Chile)", "es", "Spanish (Chile)" },
                    { "es-CO", "CO", "Español (Colombia)", "es", "Spanish (Colombia)" },
                    { "es-CR", "CR", "Español (Costa Rica)", "es", "Spanish (Costa Rica)" },
                    { "es-CU", "CU", "Español (Cuba)", "es", "Spanish (Cuba)" },
                    { "es-DO", "DO", "Español (República Dominicana)", "es", "Spanish (Dominican Republic)" },
                    { "es-EC", "EC", "Español (Ecuador)", "es", "Spanish (Ecuador)" },
                    { "es-ES", "ES", "Español (España)", "es", "Spanish (Spain)" },
                    { "es-GQ", "GQ", "Español (Guinea Ecuatorial)", "es", "Spanish (Equatorial Guinea)" },
                    { "es-GT", "GT", "Español (Guatemala)", "es", "Spanish (Guatemala)" },
                    { "es-HN", "HN", "Español (Honduras)", "es", "Spanish (Honduras)" },
                    { "es-MX", "MX", "Español (México)", "es", "Spanish (Mexico)" },
                    { "es-NI", "NI", "Español (Nicaragua)", "es", "Spanish (Nicaragua)" },
                    { "es-PA", "PA", "Español (Panamá)", "es", "Spanish (Panama)" },
                    { "es-PE", "PE", "Español (Perú)", "es", "Spanish (Peru)" },
                    { "es-PR", "PR", "Español (Puerto Rico)", "es", "Spanish (Puerto Rico)" },
                    { "es-PY", "PY", "Español (Paraguay)", "es", "Spanish (Paraguay)" },
                    { "es-SV", "SV", "Español (El Salvador)", "es", "Spanish (El Salvador)" },
                    { "es-US", "US", "Español (Estados Unidos)", "es", "Spanish (United States)" },
                    { "es-UY", "UY", "Español (Uruguay)", "es", "Spanish (Uruguay)" },
                    { "es-VE", "VE", "Español (Venezuela)", "es", "Spanish (Venezuela)" },
                    { "fr-BE", "BE", "Français (Belgique)", "fr", "French (Belgium)" },
                    { "fr-BF", "BF", "Français (Burkina Faso)", "fr", "French (Burkina Faso)" },
                    { "fr-BI", "BI", "Français (Burundi)", "fr", "French (Burundi)" },
                    { "fr-BJ", "BJ", "Français (Bénin)", "fr", "French (Benin)" },
                    { "fr-BL", "BL", "Français (Saint-Barthélemy)", "fr", "French (Saint Barthélemy)" },
                    { "fr-CA", "CA", "Français (Canada)", "fr", "French (Canada)" },
                    { "fr-CD", "CD", "Français (RD Congo)", "fr", "French (DR Congo)" },
                    { "fr-CF", "CF", "Français (République centrafricaine)", "fr", "French (Central African Republic)" },
                    { "fr-CG", "CG", "Français (Congo)", "fr", "French (Congo)" },
                    { "fr-CH", "CH", "Français (Suisse)", "fr", "French (Switzerland)" },
                    { "fr-CI", "CI", "Français (Côte d'Ivoire)", "fr", "French (Côte d'Ivoire)" },
                    { "fr-CM", "CM", "Français (Cameroun)", "fr", "French (Cameroon)" },
                    { "fr-DJ", "DJ", "Français (Djibouti)", "fr", "French (Djibouti)" },
                    { "fr-FR", "FR", "Français (France)", "fr", "French (France)" },
                    { "fr-GA", "GA", "Français (Gabon)", "fr", "French (Gabon)" },
                    { "fr-GF", "GF", "Français (Guyane française)", "fr", "French (French Guiana)" },
                    { "fr-GN", "GN", "Français (Guinée)", "fr", "French (Guinea)" },
                    { "fr-GP", "GP", "Français (Guadeloupe)", "fr", "French (Guadeloupe)" },
                    { "fr-HT", "HT", "Français (Haïti)", "fr", "French (Haiti)" },
                    { "fr-LU", "LU", "Français (Luxembourg)", "fr", "French (Luxembourg)" },
                    { "fr-MC", "MC", "Français (Monaco)", "fr", "French (Monaco)" },
                    { "fr-MF", "MF", "Français (Saint-Martin)", "fr", "French (Saint Martin)" },
                    { "fr-ML", "ML", "Français (Mali)", "fr", "French (Mali)" },
                    { "fr-MQ", "MQ", "Français (Martinique)", "fr", "French (Martinique)" },
                    { "fr-NC", "NC", "Français (Nouvelle-Calédonie)", "fr", "French (New Caledonia)" },
                    { "fr-NE", "NE", "Français (Niger)", "fr", "French (Niger)" },
                    { "fr-PF", "PF", "Français (Polynésie française)", "fr", "French (French Polynesia)" },
                    { "fr-PM", "PM", "Français (Saint-Pierre-et-Miquelon)", "fr", "French (Saint Pierre and Miquelon)" },
                    { "fr-RE", "RE", "Français (La Réunion)", "fr", "French (Réunion)" },
                    { "fr-SN", "SN", "Français (Sénégal)", "fr", "French (Senegal)" },
                    { "fr-TD", "TD", "Français (Tchad)", "fr", "French (Chad)" },
                    { "fr-TG", "TG", "Français (Togo)", "fr", "French (Togo)" },
                    { "fr-WF", "WF", "Français (Wallis-et-Futuna)", "fr", "French (Wallis and Futuna)" },
                    { "fr-YT", "YT", "Français (Mayotte)", "fr", "French (Mayotte)" },
                    { "it-CH", "CH", "Italiano (Svizzera)", "it", "Italian (Switzerland)" },
                    { "it-IT", "IT", "Italiano (Italia)", "it", "Italian (Italy)" },
                    { "it-SM", "SM", "Italiano (San Marino)", "it", "Italian (San Marino)" },
                    { "it-VA", "VA", "Italiano (Città del Vaticano)", "it", "Italian (Vatican City)" },
                    { "ja-JP", "JP", "日本語 (日本)", "ja", "Japanese (Japan)" },
                });

            migrationBuilder.UpdateData(
                table: "reference_data_version",
                keyColumn: "id",
                keyValue: 0,
                columns: new[] { "updated_at", "version" },
                values: new object[] { new DateTime(2025, 11, 24, 9, 52, 0, 0, DateTimeKind.Utc), "1.1.0" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "de-AT");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "de-BE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "de-CH");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "de-DE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "de-LI");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "de-LU");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-AG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-AI");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-AS");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-AU");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-BB");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-BM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-BS");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-BW");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-BZ");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-CA");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-CC");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-CK");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-CX");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-DM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-FJ");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-FK");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-FM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-GB");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-GD");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-GG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-GH");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-GI");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-GM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-GU");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-GY");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-IE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-IM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-IO");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-JE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-JM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-KI");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-KN");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-KY");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-LC");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-LR");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-LS");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-MH");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-MP");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-MS");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-MU");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-MW");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-NA");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-NF");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-NG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-NR");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-NU");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-NZ");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-PG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-PH");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-PN");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-PW");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-SB");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-SC");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-SG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-SH");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-SL");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-SS");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-SZ");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-TC");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-TK");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-TT");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-TV");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-UG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-UM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-US");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-VC");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-VG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-VI");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-ZA");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-ZM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "en-ZW");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-AR");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-BO");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-CL");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-CO");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-CR");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-CU");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-DO");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-EC");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-ES");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-GQ");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-GT");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-HN");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-MX");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-NI");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-PA");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-PE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-PR");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-PY");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-SV");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-US");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-UY");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "es-VE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-BE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-BF");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-BI");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-BJ");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-BL");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-CA");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-CD");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-CF");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-CG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-CH");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-CI");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-CM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-DJ");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-FR");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-GA");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-GF");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-GN");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-GP");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-HT");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-LU");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-MC");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-MF");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-ML");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-MQ");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-NC");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-NE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-PF");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-PM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-RE");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-SN");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-TD");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-TG");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-WF");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "fr-YT");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "it-CH");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "it-IT");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "it-SM");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "it-VA");

            migrationBuilder.DeleteData(
                table: "locales",
                keyColumn: "ietf_bcp_47_tag",
                keyValue: "ja-JP");

            migrationBuilder.UpdateData(
                table: "reference_data_version",
                keyColumn: "id",
                keyValue: 0,
                columns: new[] { "updated_at", "version" },
                values: new object[] { new DateTime(2025, 11, 24, 8, 48, 0, 0, DateTimeKind.Utc), "1.0.0" });
        }
    }
}
