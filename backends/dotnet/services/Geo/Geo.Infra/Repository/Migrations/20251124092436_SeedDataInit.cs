// -----------------------------------------------------------------------
// <copyright file="20251124092436_SeedDataInit.cs" company="DCSV">
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
    public partial class SeedDataInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "phone_number_format",
                table: "countries");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "reference_data_version",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "iso_3166_1_alpha_2_code", "display_name", "iso_3166_1_alpha_3_code", "iso_3166_1_numeric_code", "official_name", "phone_number_prefix", "primary_currency_iso_4217_alpha_code", "primary_locale_ietf_bcp_47_tag", "sovereign_iso_3166_1_alpha_2_code" },
                values: new object[,]
                {
                    { "AE", "United Arab Emirates", "ARE", "784", "United Arab Emirates", "971", null, null, null },
                    { "AF", "Afghanistan", "AFG", "004", "Islamic Republic of Afghanistan", "93", null, null, null },
                    { "AG", "Antigua and Barbuda", "ATG", "028", "Antigua and Barbuda", "1", null, null, null },
                    { "AL", "Albania", "ALB", "008", "Republic of Albania", "355", null, null, null },
                    { "AM", "Armenia", "ARM", "051", "Republic of Armenia", "374", null, null, null },
                    { "AO", "Angola", "AGO", "024", "Republic of Angola", "244", null, null, null },
                    { "AQ", "Antarctica", "ATA", "010", "Antarctica", "672", null, null, null },
                    { "AR", "Argentina", "ARG", "032", "Argentine Republic", "54", null, null, null },
                    { "AU", "Australia", "AUS", "036", "Commonwealth of Australia", "61", null, null, null },
                    { "AZ", "Azerbaijan", "AZE", "031", "Republic of Azerbaijan", "994", null, null, null },
                    { "BA", "Bosnia and Herzegovina", "BIH", "070", "Bosnia and Herzegovina", "387", null, null, null },
                    { "BB", "Barbados", "BRB", "052", "Barbados", "1", null, null, null },
                    { "BD", "Bangladesh", "BGD", "050", "People's Republic of Bangladesh", "880", null, null, null },
                    { "BF", "Burkina Faso", "BFA", "854", "Burkina Faso", "226", null, null, null },
                    { "BG", "Bulgaria", "BGR", "100", "Republic of Bulgaria", "359", null, null, null },
                    { "BH", "Bahrain", "BHR", "048", "Kingdom of Bahrain", "973", null, null, null },
                    { "BI", "Burundi", "BDI", "108", "Republic of Burundi", "257", null, null, null },
                    { "BJ", "Benin", "BEN", "204", "Republic of Benin", "229", null, null, null },
                    { "BN", "Brunei", "BRN", "096", "Brunei Darussalam", "673", null, null, null },
                    { "BO", "Bolivia", "BOL", "068", "Plurinational State of Bolivia", "591", null, null, null },
                    { "BR", "Brazil", "BRA", "076", "Federative Republic of Brazil", "55", null, null, null },
                    { "BS", "Bahamas", "BHS", "044", "Commonwealth of The Bahamas", "1", null, null, null },
                    { "BT", "Bhutan", "BTN", "064", "Kingdom of Bhutan", "975", null, null, null },
                    { "BW", "Botswana", "BWA", "072", "Republic of Botswana", "267", null, null, null },
                    { "BY", "Belarus", "BLR", "112", "Republic of Belarus", "375", null, null, null },
                    { "BZ", "Belize", "BLZ", "084", "Belize", "501", null, null, null },
                    { "CD", "DR Congo", "COD", "180", "Democratic Republic of the Congo", "243", null, null, null },
                    { "CF", "Central African Republic", "CAF", "140", "Central African Republic", "236", null, null, null },
                    { "CG", "Congo", "COG", "178", "Republic of the Congo", "242", null, null, null },
                    { "CH", "Switzerland", "CHE", "756", "Swiss Confederation", "41", null, null, null },
                    { "CI", "Côte d'Ivoire", "CIV", "384", "Republic of Côte d'Ivoire", "225", null, null, null },
                    { "CL", "Chile", "CHL", "152", "Republic of Chile", "56", null, null, null },
                    { "CM", "Cameroon", "CMR", "120", "Republic of Cameroon", "237", null, null, null },
                    { "CN", "China", "CHN", "156", "People's Republic of China", "86", null, null, null },
                    { "CO", "Colombia", "COL", "170", "Republic of Colombia", "57", null, null, null },
                    { "CR", "Costa Rica", "CRI", "188", "Republic of Costa Rica", "506", null, null, null },
                    { "CU", "Cuba", "CUB", "192", "Republic of Cuba", "53", null, null, null },
                    { "CV", "Cabo Verde", "CPV", "132", "Republic of Cabo Verde", "238", null, null, null },
                    { "CZ", "Czechia", "CZE", "203", "Czech Republic", "420", null, null, null },
                    { "DJ", "Djibouti", "DJI", "262", "Republic of Djibouti", "253", null, null, null },
                    { "DK", "Denmark", "DNK", "208", "Kingdom of Denmark", "45", null, null, null },
                    { "DM", "Dominica", "DMA", "212", "Commonwealth of Dominica", "1", null, null, null },
                    { "DO", "Dominican Republic", "DOM", "214", "Dominican Republic", "1", null, null, null },
                    { "DZ", "Algeria", "DZA", "012", "People's Democratic Republic of Algeria", "213", null, null, null },
                    { "EG", "Egypt", "EGY", "818", "Arab Republic of Egypt", "20", null, null, null },
                    { "EH", "Western Sahara", "ESH", "732", "Western Sahara", "212", null, null, null },
                    { "ER", "Eritrea", "ERI", "232", "State of Eritrea", "291", null, null, null },
                    { "ET", "Ethiopia", "ETH", "231", "Federal Democratic Republic of Ethiopia", "251", null, null, null },
                    { "FJ", "Fiji", "FJI", "242", "Republic of Fiji", "679", null, null, null },
                    { "GA", "Gabon", "GAB", "266", "Gabonese Republic", "241", null, null, null },
                    { "GD", "Grenada", "GRD", "308", "Grenada", "1", null, null, null },
                    { "GE", "Georgia", "GEO", "268", "Georgia", "995", null, null, null },
                    { "GH", "Ghana", "GHA", "288", "Republic of Ghana", "233", null, null, null },
                    { "GM", "Gambia", "GMB", "270", "Republic of The Gambia", "220", null, null, null },
                    { "GN", "Guinea", "GIN", "324", "Republic of Guinea", "224", null, null, null },
                    { "GQ", "Equatorial Guinea", "GNQ", "226", "Republic of Equatorial Guinea", "240", null, null, null },
                    { "GT", "Guatemala", "GTM", "320", "Republic of Guatemala", "502", null, null, null },
                    { "GW", "Guinea-Bissau", "GNB", "624", "Republic of Guinea-Bissau", "245", null, null, null },
                    { "GY", "Guyana", "GUY", "328", "Co-operative Republic of Guyana", "592", null, null, null },
                    { "HN", "Honduras", "HND", "340", "Republic of Honduras", "504", null, null, null },
                    { "HT", "Haiti", "HTI", "332", "Republic of Haiti", "509", null, null, null },
                    { "HU", "Hungary", "HUN", "348", "Hungary", "36", null, null, null },
                    { "ID", "Indonesia", "IDN", "360", "Republic of Indonesia", "62", null, null, null },
                    { "IL", "Israel", "ISR", "376", "State of Israel", "972", null, null, null },
                    { "IN", "India", "IND", "356", "Republic of India", "91", null, null, null },
                    { "IQ", "Iraq", "IRQ", "368", "Republic of Iraq", "964", null, null, null },
                    { "IR", "Iran", "IRN", "364", "Islamic Republic of Iran", "98", null, null, null },
                    { "IS", "Iceland", "ISL", "352", "Iceland", "354", null, null, null },
                    { "JM", "Jamaica", "JAM", "388", "Jamaica", "1", null, null, null },
                    { "JO", "Jordan", "JOR", "400", "Hashemite Kingdom of Jordan", "962", null, null, null },
                    { "KE", "Kenya", "KEN", "404", "Republic of Kenya", "254", null, null, null },
                    { "KG", "Kyrgyzstan", "KGZ", "417", "Kyrgyz Republic", "996", null, null, null },
                    { "KH", "Cambodia", "KHM", "116", "Kingdom of Cambodia", "855", null, null, null },
                    { "KI", "Kiribati", "KIR", "296", "Republic of Kiribati", "686", null, null, null },
                    { "KM", "Comoros", "COM", "174", "Union of the Comoros", "269", null, null, null },
                    { "KN", "Saint Kitts and Nevis", "KNA", "659", "Federation of Saint Christopher and Nevis", "1", null, null, null },
                    { "KP", "North Korea", "PRK", "408", "Democratic People's Republic of Korea", "850", null, null, null },
                    { "KR", "South Korea", "KOR", "410", "Republic of Korea", "82", null, null, null },
                    { "KW", "Kuwait", "KWT", "414", "State of Kuwait", "965", null, null, null },
                    { "KZ", "Kazakhstan", "KAZ", "398", "Republic of Kazakhstan", "7", null, null, null },
                    { "LA", "Laos", "LAO", "418", "Lao People's Democratic Republic", "856", null, null, null },
                    { "LB", "Lebanon", "LBN", "422", "Lebanese Republic", "961", null, null, null },
                    { "LC", "Saint Lucia", "LCA", "662", "Saint Lucia", "1", null, null, null },
                    { "LI", "Liechtenstein", "LIE", "438", "Principality of Liechtenstein", "423", null, null, null },
                    { "LK", "Sri Lanka", "LKA", "144", "Democratic Socialist Republic of Sri Lanka", "94", null, null, null },
                    { "LR", "Liberia", "LBR", "430", "Republic of Liberia", "231", null, null, null },
                    { "LS", "Lesotho", "LSO", "426", "Kingdom of Lesotho", "266", null, null, null },
                    { "LY", "Libya", "LBY", "434", "State of Libya", "218", null, null, null },
                    { "MA", "Morocco", "MAR", "504", "Kingdom of Morocco", "212", null, null, null },
                    { "MD", "Moldova", "MDA", "498", "Republic of Moldova", "373", null, null, null },
                    { "MG", "Madagascar", "MDG", "450", "Republic of Madagascar", "261", null, null, null },
                    { "MK", "North Macedonia", "MKD", "807", "Republic of North Macedonia", "389", null, null, null },
                    { "ML", "Mali", "MLI", "466", "Republic of Mali", "223", null, null, null },
                    { "MM", "Myanmar", "MMR", "104", "Republic of the Union of Myanmar", "95", null, null, null },
                    { "MN", "Mongolia", "MNG", "496", "Mongolia", "976", null, null, null },
                    { "MR", "Mauritania", "MRT", "478", "Islamic Republic of Mauritania", "222", null, null, null },
                    { "MU", "Mauritius", "MUS", "480", "Republic of Mauritius", "230", null, null, null },
                    { "MV", "Maldives", "MDV", "462", "Republic of Maldives", "960", null, null, null },
                    { "MW", "Malawi", "MWI", "454", "Republic of Malawi", "265", null, null, null },
                    { "MX", "Mexico", "MEX", "484", "United Mexican States", "52", null, null, null },
                    { "MY", "Malaysia", "MYS", "458", "Malaysia", "60", null, null, null },
                    { "MZ", "Mozambique", "MOZ", "508", "Republic of Mozambique", "258", null, null, null },
                    { "NA", "Namibia", "NAM", "516", "Republic of Namibia", "264", null, null, null },
                    { "NE", "Niger", "NER", "562", "Republic of the Niger", "227", null, null, null },
                    { "NG", "Nigeria", "NGA", "566", "Federal Republic of Nigeria", "234", null, null, null },
                    { "NI", "Nicaragua", "NIC", "558", "Republic of Nicaragua", "505", null, null, null },
                    { "NO", "Norway", "NOR", "578", "Kingdom of Norway", "47", null, null, null },
                    { "NP", "Nepal", "NPL", "524", "Federal Democratic Republic of Nepal", "977", null, null, null },
                    { "NR", "Nauru", "NRU", "520", "Republic of Nauru", "674", null, null, null },
                    { "NZ", "New Zealand", "NZL", "554", "New Zealand", "64", null, null, null },
                    { "OM", "Oman", "OMN", "512", "Sultanate of Oman", "968", null, null, null },
                    { "PE", "Peru", "PER", "604", "Republic of Peru", "51", null, null, null },
                    { "PG", "Papua New Guinea", "PNG", "598", "Independent State of Papua New Guinea", "675", null, null, null },
                    { "PH", "Philippines", "PHL", "608", "Republic of the Philippines", "63", null, null, null },
                    { "PK", "Pakistan", "PAK", "586", "Islamic Republic of Pakistan", "92", null, null, null },
                    { "PL", "Poland", "POL", "616", "Republic of Poland", "48", null, null, null },
                    { "PS", "Palestine", "PSE", "275", "State of Palestine", "970", null, null, null },
                    { "PY", "Paraguay", "PRY", "600", "Republic of Paraguay", "595", null, null, null },
                    { "QA", "Qatar", "QAT", "634", "State of Qatar", "974", null, null, null },
                    { "RO", "Romania", "ROU", "642", "Romania", "40", null, null, null },
                    { "RS", "Serbia", "SRB", "688", "Republic of Serbia", "381", null, null, null },
                    { "RU", "Russia", "RUS", "643", "Russian Federation", "7", null, null, null },
                    { "RW", "Rwanda", "RWA", "646", "Republic of Rwanda", "250", null, null, null },
                    { "SA", "Saudi Arabia", "SAU", "682", "Kingdom of Saudi Arabia", "966", null, null, null },
                    { "SB", "Solomon Islands", "SLB", "090", "Solomon Islands", "677", null, null, null },
                    { "SC", "Seychelles", "SYC", "690", "Republic of Seychelles", "248", null, null, null },
                    { "SD", "Sudan", "SDN", "729", "Republic of the Sudan", "249", null, null, null },
                    { "SE", "Sweden", "SWE", "752", "Kingdom of Sweden", "46", null, null, null },
                    { "SG", "Singapore", "SGP", "702", "Republic of Singapore", "65", null, null, null },
                    { "SL", "Sierra Leone", "SLE", "694", "Republic of Sierra Leone", "232", null, null, null },
                    { "SN", "Senegal", "SEN", "686", "Republic of Senegal", "221", null, null, null },
                    { "SO", "Somalia", "SOM", "706", "Federal Republic of Somalia", "252", null, null, null },
                    { "SR", "Suriname", "SUR", "740", "Republic of Suriname", "597", null, null, null },
                    { "SS", "South Sudan", "SSD", "728", "Republic of South Sudan", "211", null, null, null },
                    { "ST", "São Tomé and Príncipe", "STP", "678", "Democratic Republic of São Tomé and Príncipe", "239", null, null, null },
                    { "SY", "Syria", "SYR", "760", "Syrian Arab Republic", "963", null, null, null },
                    { "SZ", "Eswatini", "SWZ", "748", "Kingdom of Eswatini", "268", null, null, null },
                    { "TD", "Chad", "TCD", "148", "Republic of Chad", "235", null, null, null },
                    { "TG", "Togo", "TGO", "768", "Togolese Republic", "228", null, null, null },
                    { "TH", "Thailand", "THA", "764", "Kingdom of Thailand", "66", null, null, null },
                    { "TJ", "Tajikistan", "TJK", "762", "Republic of Tajikistan", "992", null, null, null },
                    { "TM", "Turkmenistan", "TKM", "795", "Turkmenistan", "993", null, null, null },
                    { "TN", "Tunisia", "TUN", "788", "Republic of Tunisia", "216", null, null, null },
                    { "TO", "Tonga", "TON", "776", "Kingdom of Tonga", "676", null, null, null },
                    { "TR", "Türkiye", "TUR", "792", "Republic of Türkiye", "90", null, null, null },
                    { "TT", "Trinidad and Tobago", "TTO", "780", "Republic of Trinidad and Tobago", "1", null, null, null },
                    { "TV", "Tuvalu", "TUV", "798", "Tuvalu", "688", null, null, null },
                    { "TW", "Taiwan", "TWN", "158", "Republic of China", "886", null, null, null },
                    { "TZ", "Tanzania", "TZA", "834", "United Republic of Tanzania", "255", null, null, null },
                    { "UA", "Ukraine", "UKR", "804", "Ukraine", "380", null, null, null },
                    { "UG", "Uganda", "UGA", "800", "Republic of Uganda", "256", null, null, null },
                    { "UY", "Uruguay", "URY", "858", "Oriental Republic of Uruguay", "598", null, null, null },
                    { "UZ", "Uzbekistan", "UZB", "860", "Republic of Uzbekistan", "998", null, null, null },
                    { "VC", "Saint Vincent and the Grenadines", "VCT", "670", "Saint Vincent and the Grenadines", "1", null, null, null },
                    { "VE", "Venezuela", "VEN", "862", "Bolivarian Republic of Venezuela", "58", null, null, null },
                    { "VN", "Vietnam", "VNM", "704", "Socialist Republic of Vietnam", "84", null, null, null },
                    { "VU", "Vanuatu", "VUT", "548", "Republic of Vanuatu", "678", null, null, null },
                    { "WS", "Samoa", "WSM", "882", "Independent State of Samoa", "685", null, null, null },
                    { "YE", "Yemen", "YEM", "887", "Republic of Yemen", "967", null, null, null },
                    { "ZA", "South Africa", "ZAF", "710", "Republic of South Africa", "27", null, null, null },
                    { "ZM", "Zambia", "ZMB", "894", "Republic of Zambia", "260", null, null, null },
                });

            migrationBuilder.InsertData(
                table: "currencies",
                columns: new[] { "iso_4217_alpha_code", "decimal_places", "display_name", "iso_4217_numeric_code", "official_name", "symbol" },
                values: new object[,]
                {
                    { "CAD", 2, "Canadian Dollar", "124", "Canadian Dollar", "$" },
                    { "EUR", 2, "Euro", "978", "Euro", "€" },
                    { "GBP", 2, "British Pound", "826", "Pound Sterling", "£" },
                    { "JPY", 0, "Yen", "392", "Japanese Yen", "¥" },
                    { "USD", 2, "US Dollar", "840", "United States Dollar", "$" },
                });

            migrationBuilder.InsertData(
                table: "geopolitical_entities",
                columns: new[] { "short_code", "name", "type" },
                values: new object[,]
                {
                    { "AF", "Africa", "Continent" },
                    { "AFTA", "ASEAN Free Trade Area", "FreeTradeAgreement" },
                    { "AL", "Arab League", "GovernanceAndCooperationAgreement" },
                    { "AN", "Antarctica", "Continent" },
                    { "ANZUS", "Australia, New Zealand, United States Security Treaty", "MilitaryAlliance" },
                    { "ARAB", "Arabian Peninsula", "SubContinent" },
                    { "AS", "Asia", "Continent" },
                    { "ASEAN", "Association of Southeast Asian Nations", "GovernanceAndCooperationAgreement" },
                    { "AU", "African Union", "GovernanceAndCooperationAgreement" },
                    { "AUKUS", "Australia-United Kingdom-United States", "MilitaryAlliance" },
                    { "BALK", "Balkans", "GeopoliticalRegion" },
                    { "BALT", "Baltic States", "GeopoliticalRegion" },
                    { "BENE", "Benelux", "GeopoliticalRegion" },
                    { "BRICS", "BRICS", "GovernanceAndCooperationAgreement" },
                    { "CAM", "Central America", "SubContinent" },
                    { "CARIB", "Caribbean", "GeopoliticalRegion" },
                    { "CARICOM", "Caribbean Community", "GovernanceAndCooperationAgreement" },
                    { "CAS", "Central Asia", "SubContinent" },
                    { "CEMAC", "Central African Economic and Monetary Community", "MonetaryUnion" },
                    { "COE", "Council of Europe", "GovernanceAndCooperationAgreement" },
                    { "CPTPP", "Comprehensive and Progressive Agreement for Trans-Pacific Partnership", "FreeTradeAgreement" },
                    { "CSTO", "Collective Security Treaty Organization", "MilitaryAlliance" },
                    { "CW", "Commonwealth of Nations", "GovernanceAndCooperationAgreement" },
                    { "EAEU", "Eurasian Economic Union", "EconomicUnion" },
                    { "EAS", "East Asia", "SubContinent" },
                    { "ECCU", "Eastern Caribbean Currency Union", "MonetaryUnion" },
                    { "EEA", "European Economic Area", "CommonMarket" },
                    { "EU", "Europe", "Continent" },
                    { "EUCU", "European Union Customs Union", "CustomsUnion" },
                    { "EUR", "European Union", "PoliticalUnion" },
                    { "EZ", "Eurozone", "MonetaryUnion" },
                    { "FVEY", "Five Eyes", "MilitaryAlliance" },
                    { "G20", "Group of Twenty", "GovernanceAndCooperationAgreement" },
                    { "G7", "Group of Seven", "GovernanceAndCooperationAgreement" },
                    { "GCC", "Gulf Cooperation Council", "GovernanceAndCooperationAgreement" },
                    { "INDS", "Indian Subcontinent", "SubContinent" },
                    { "LATAM", "Latin America", "GeopoliticalRegion" },
                    { "MENA", "Middle East and North Africa", "GeopoliticalRegion" },
                    { "MERCOSUR", "Southern Common Market (Mercosur)", "CommonMarket" },
                    { "NA", "North America", "Continent" },
                    { "NATO", "North Atlantic Treaty Organization", "MilitaryAlliance" },
                    { "NC", "Nordic Council", "GovernanceAndCooperationAgreement" },
                    { "NORD", "Nordic Countries", "GeopoliticalRegion" },
                    { "OC", "Oceania", "Continent" },
                    { "OECD", "Organisation for Economic Co-operation and Development", "GovernanceAndCooperationAgreement" },
                    { "OIF", "Organisation internationale de la Francophonie", "GovernanceAndCooperationAgreement" },
                    { "OPEC", "Organization of the Petroleum Exporting Countries", "GovernanceAndCooperationAgreement" },
                    { "QUAD", "Quadrilateral Security Dialogue", "MilitaryAlliance" },
                    { "RCEP", "Regional Comprehensive Economic Partnership", "FreeTradeAgreement" },
                    { "SA", "South America", "Continent" },
                    { "SAARC", "South Asian Association for Regional Cooperation", "GovernanceAndCooperationAgreement" },
                    { "SACU", "Southern African Customs Union", "CustomsUnion" },
                    { "SAHEL", "Sahel", "GeopoliticalRegion" },
                    { "SCAN", "Scandinavia", "SubContinent" },
                    { "SEA", "Southeast Asia", "SubContinent" },
                    { "SSA", "Sub-Saharan Africa", "GeopoliticalRegion" },
                    { "UN", "United Nations", "GovernanceAndCooperationAgreement" },
                    { "USMCA", "United States-Mexico-Canada Agreement", "FreeTradeAgreement" },
                    { "WAEMU", "West African Economic and Monetary Union", "MonetaryUnion" },
                });

            migrationBuilder.InsertData(
                table: "languages",
                columns: new[] { "iso_639_1_code", "endonym", "name" },
                values: new object[,]
                {
                    { "de", "Deutsch", "German" },
                    { "en", "English", "English" },
                    { "es", "Español", "Spanish" },
                    { "fr", "Français", "French" },
                    { "it", "Italiano", "Italian" },
                    { "ja", "日本語", "Japanese" },
                });

            migrationBuilder.InsertData(
                table: "reference_data_version",
                columns: new[] { "id", "updated_at", "version" },
                values: new object[] { 0, new DateTime(2025, 11, 24, 8, 48, 0, 0, DateTimeKind.Utc), "1.0.0" });

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "iso_3166_1_alpha_2_code", "display_name", "iso_3166_1_alpha_3_code", "iso_3166_1_numeric_code", "official_name", "phone_number_prefix", "primary_currency_iso_4217_alpha_code", "primary_locale_ietf_bcp_47_tag", "sovereign_iso_3166_1_alpha_2_code" },
                values: new object[,]
                {
                    { "AD", "Andorra", "AND", "020", "Principality of Andorra", "376", "EUR", null, null },
                    { "AT", "Austria", "AUT", "040", "Republic of Austria", "43", "EUR", null, null },
                    { "BE", "Belgium", "BEL", "056", "Kingdom of Belgium", "32", "EUR", null, null },
                    { "BV", "Bouvet Island", "BVT", "074", "Bouvet Island", "47", null, null, "NO" },
                    { "CA", "Canada", "CAN", "124", "Canada", "1", "CAD", null, null },
                    { "CC", "Cocos (Keeling) Islands", "CCK", "166", "Territory of Cocos (Keeling) Islands", "61", null, null, "AU" },
                    { "CK", "Cook Islands", "COK", "184", "Cook Islands", "682", null, null, "NZ" },
                    { "CX", "Christmas Island", "CXR", "162", "Territory of Christmas Island", "61", null, null, "AU" },
                    { "CY", "Cyprus", "CYP", "196", "Republic of Cyprus", "357", "EUR", null, null },
                    { "DE", "Germany", "DEU", "276", "Federal Republic of Germany", "49", "EUR", null, null },
                    { "EC", "Ecuador", "ECU", "218", "Republic of Ecuador", "593", "USD", null, null },
                    { "EE", "Estonia", "EST", "233", "Republic of Estonia", "372", "EUR", null, null },
                    { "ES", "Spain", "ESP", "724", "Kingdom of Spain", "34", "EUR", null, null },
                    { "FI", "Finland", "FIN", "246", "Republic of Finland", "358", "EUR", null, null },
                    { "FM", "Micronesia", "FSM", "583", "Federated States of Micronesia", "691", "USD", null, null },
                    { "FO", "Faroe Islands", "FRO", "234", "Faroe Islands", "298", null, null, "DK" },
                    { "FR", "France", "FRA", "250", "French Republic", "33", "EUR", null, null },
                    { "GB", "United Kingdom", "GBR", "826", "United Kingdom of Great Britain and Northern Ireland", "44", "GBP", null, null },
                    { "GL", "Greenland", "GRL", "304", "Greenland", "299", null, null, "DK" },
                    { "GR", "Greece", "GRC", "300", "Hellenic Republic", "30", "EUR", null, null },
                    { "HK", "Hong Kong", "HKG", "344", "Hong Kong Special Administrative Region of China", "852", null, null, "CN" },
                    { "HM", "Heard Island and McDonald Islands", "HMD", "334", "Heard Island and McDonald Islands", "61", null, null, "AU" },
                    { "HR", "Croatia", "HRV", "191", "Republic of Croatia", "385", "EUR", null, null },
                    { "IE", "Ireland", "IRL", "372", "Ireland", "353", "EUR", null, null },
                    { "IT", "Italy", "ITA", "380", "Italian Republic", "39", "EUR", null, null },
                    { "JP", "Japan", "JPN", "392", "Japan", "81", "JPY", null, null },
                    { "LT", "Lithuania", "LTU", "440", "Republic of Lithuania", "370", "EUR", null, null },
                    { "LU", "Luxembourg", "LUX", "442", "Grand Duchy of Luxembourg", "352", "EUR", null, null },
                    { "LV", "Latvia", "LVA", "428", "Republic of Latvia", "371", "EUR", null, null },
                    { "MC", "Monaco", "MCO", "492", "Principality of Monaco", "377", "EUR", null, null },
                    { "ME", "Montenegro", "MNE", "499", "Montenegro", "382", "EUR", null, null },
                    { "MH", "Marshall Islands", "MHL", "584", "Republic of the Marshall Islands", "692", "USD", null, null },
                    { "MO", "Macao", "MAC", "446", "Macao Special Administrative Region of China", "853", null, null, "CN" },
                    { "MT", "Malta", "MLT", "470", "Republic of Malta", "356", "EUR", null, null },
                    { "NF", "Norfolk Island", "NFK", "574", "Territory of Norfolk Island", "672", null, null, "AU" },
                    { "NL", "Netherlands", "NLD", "528", "Kingdom of the Netherlands", "31", "EUR", null, null },
                    { "NU", "Niue", "NIU", "570", "Niue", "683", null, null, "NZ" },
                    { "PA", "Panama", "PAN", "591", "Republic of Panama", "507", "USD", null, null },
                    { "PT", "Portugal", "PRT", "620", "Portuguese Republic", "351", "EUR", null, null },
                    { "PW", "Palau", "PLW", "585", "Republic of Palau", "680", "USD", null, null },
                    { "SI", "Slovenia", "SVN", "705", "Republic of Slovenia", "386", "EUR", null, null },
                    { "SJ", "Svalbard and Jan Mayen", "SJM", "744", "Svalbard and Jan Mayen", "47", null, null, "NO" },
                    { "SK", "Slovakia", "SVK", "703", "Slovak Republic", "421", "EUR", null, null },
                    { "SM", "San Marino", "SMR", "674", "Republic of San Marino", "378", "EUR", null, null },
                    { "SV", "El Salvador", "SLV", "222", "Republic of El Salvador", "503", "USD", null, null },
                    { "TK", "Tokelau", "TKL", "772", "Tokelau", "690", null, null, "NZ" },
                    { "TL", "Timor-Leste", "TLS", "626", "Democratic Republic of Timor-Leste", "670", "USD", null, null },
                    { "US", "United States", "USA", "840", "United States of America", "1", "USD", null, null },
                    { "VA", "Vatican City", "VAT", "336", "Holy See", "39", "EUR", null, null },
                    { "ZW", "Zimbabwe", "ZWE", "716", "Republic of Zimbabwe", "263", "USD", null, null },
                });

            migrationBuilder.InsertData(
                table: "country_currencies",
                columns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                values: new object[,]
                {
                    { "AL", "EUR" },
                    { "BA", "EUR" },
                    { "BB", "USD" },
                    { "BG", "EUR" },
                    { "BS", "USD" },
                    { "BZ", "USD" },
                    { "CH", "EUR" },
                    { "CR", "USD" },
                    { "CZ", "EUR" },
                    { "DK", "EUR" },
                    { "GT", "USD" },
                    { "HN", "USD" },
                    { "HU", "EUR" },
                    { "JM", "USD" },
                    { "LI", "EUR" },
                    { "MA", "EUR" },
                    { "MK", "EUR" },
                    { "MX", "USD" },
                    { "NI", "USD" },
                    { "PL", "EUR" },
                    { "RO", "EUR" },
                    { "RS", "EUR" },
                    { "SE", "EUR" },
                    { "TR", "EUR" },
                });

            migrationBuilder.InsertData(
                table: "country_geopolitical_entities",
                columns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                values: new object[,]
                {
                    { "AE", "AL" },
                    { "AE", "ARAB" },
                    { "AE", "AS" },
                    { "AE", "BRICS" },
                    { "AE", "GCC" },
                    { "AE", "MENA" },
                    { "AE", "OPEC" },
                    { "AE", "UN" },
                    { "AF", "AS" },
                    { "AF", "SAARC" },
                    { "AF", "UN" },
                    { "AG", "CARIB" },
                    { "AG", "CARICOM" },
                    { "AG", "CW" },
                    { "AG", "ECCU" },
                    { "AG", "NA" },
                    { "AG", "UN" },
                    { "AL", "BALK" },
                    { "AL", "COE" },
                    { "AL", "EU" },
                    { "AL", "NATO" },
                    { "AL", "OIF" },
                    { "AL", "UN" },
                    { "AM", "AS" },
                    { "AM", "COE" },
                    { "AM", "CSTO" },
                    { "AM", "EAEU" },
                    { "AM", "OIF" },
                    { "AM", "UN" },
                    { "AO", "AF" },
                    { "AO", "AU" },
                    { "AO", "OPEC" },
                    { "AO", "SSA" },
                    { "AO", "UN" },
                    { "AQ", "AN" },
                    { "AR", "G20" },
                    { "AR", "LATAM" },
                    { "AR", "MERCOSUR" },
                    { "AR", "SA" },
                    { "AR", "UN" },
                    { "AU", "ANZUS" },
                    { "AU", "AUKUS" },
                    { "AU", "CPTPP" },
                    { "AU", "CW" },
                    { "AU", "FVEY" },
                    { "AU", "G20" },
                    { "AU", "OC" },
                    { "AU", "OECD" },
                    { "AU", "QUAD" },
                    { "AU", "RCEP" },
                    { "AU", "UN" },
                    { "AZ", "AS" },
                    { "AZ", "COE" },
                    { "AZ", "UN" },
                    { "BA", "BALK" },
                    { "BA", "COE" },
                    { "BA", "EU" },
                    { "BA", "UN" },
                    { "BB", "CARIB" },
                    { "BB", "CARICOM" },
                    { "BB", "CW" },
                    { "BB", "NA" },
                    { "BB", "UN" },
                    { "BD", "AS" },
                    { "BD", "CW" },
                    { "BD", "INDS" },
                    { "BD", "SAARC" },
                    { "BD", "UN" },
                    { "BF", "AF" },
                    { "BF", "AU" },
                    { "BF", "OIF" },
                    { "BF", "SAHEL" },
                    { "BF", "SSA" },
                    { "BF", "UN" },
                    { "BF", "WAEMU" },
                    { "BG", "BALK" },
                    { "BG", "COE" },
                    { "BG", "EEA" },
                    { "BG", "EU" },
                    { "BG", "EUCU" },
                    { "BG", "EUR" },
                    { "BG", "NATO" },
                    { "BG", "OIF" },
                    { "BG", "UN" },
                    { "BH", "AL" },
                    { "BH", "ARAB" },
                    { "BH", "AS" },
                    { "BH", "GCC" },
                    { "BH", "MENA" },
                    { "BH", "UN" },
                    { "BI", "AF" },
                    { "BI", "AU" },
                    { "BI", "OIF" },
                    { "BI", "SSA" },
                    { "BI", "UN" },
                    { "BJ", "AF" },
                    { "BJ", "AU" },
                    { "BJ", "OIF" },
                    { "BJ", "SSA" },
                    { "BJ", "UN" },
                    { "BJ", "WAEMU" },
                    { "BN", "AFTA" },
                    { "BN", "AS" },
                    { "BN", "ASEAN" },
                    { "BN", "CPTPP" },
                    { "BN", "CW" },
                    { "BN", "RCEP" },
                    { "BN", "SEA" },
                    { "BN", "UN" },
                    { "BO", "LATAM" },
                    { "BO", "MERCOSUR" },
                    { "BO", "SA" },
                    { "BO", "UN" },
                    { "BR", "BRICS" },
                    { "BR", "G20" },
                    { "BR", "LATAM" },
                    { "BR", "MERCOSUR" },
                    { "BR", "SA" },
                    { "BR", "UN" },
                    { "BS", "CARIB" },
                    { "BS", "CARICOM" },
                    { "BS", "CW" },
                    { "BS", "NA" },
                    { "BS", "UN" },
                    { "BT", "AS" },
                    { "BT", "INDS" },
                    { "BT", "SAARC" },
                    { "BT", "UN" },
                    { "BW", "AF" },
                    { "BW", "AU" },
                    { "BW", "CW" },
                    { "BW", "SACU" },
                    { "BW", "SSA" },
                    { "BW", "UN" },
                    { "BY", "CSTO" },
                    { "BY", "EAEU" },
                    { "BY", "EU" },
                    { "BY", "UN" },
                    { "BZ", "CAM" },
                    { "BZ", "CARICOM" },
                    { "BZ", "CW" },
                    { "BZ", "LATAM" },
                    { "BZ", "NA" },
                    { "BZ", "UN" },
                    { "CD", "AF" },
                    { "CD", "AU" },
                    { "CD", "OIF" },
                    { "CD", "SSA" },
                    { "CD", "UN" },
                    { "CF", "AF" },
                    { "CF", "AU" },
                    { "CF", "CEMAC" },
                    { "CF", "OIF" },
                    { "CF", "SSA" },
                    { "CF", "UN" },
                    { "CG", "AF" },
                    { "CG", "AU" },
                    { "CG", "CEMAC" },
                    { "CG", "OIF" },
                    { "CG", "OPEC" },
                    { "CG", "SSA" },
                    { "CG", "UN" },
                    { "CH", "COE" },
                    { "CH", "EU" },
                    { "CH", "OECD" },
                    { "CH", "OIF" },
                    { "CH", "UN" },
                    { "CI", "AF" },
                    { "CI", "AU" },
                    { "CI", "OIF" },
                    { "CI", "SSA" },
                    { "CI", "UN" },
                    { "CI", "WAEMU" },
                    { "CL", "CPTPP" },
                    { "CL", "LATAM" },
                    { "CL", "MERCOSUR" },
                    { "CL", "OECD" },
                    { "CL", "SA" },
                    { "CL", "UN" },
                    { "CM", "AF" },
                    { "CM", "AU" },
                    { "CM", "CEMAC" },
                    { "CM", "CW" },
                    { "CM", "OIF" },
                    { "CM", "SSA" },
                    { "CM", "UN" },
                    { "CN", "AS" },
                    { "CN", "BRICS" },
                    { "CN", "EAS" },
                    { "CN", "G20" },
                    { "CN", "RCEP" },
                    { "CN", "UN" },
                    { "CO", "LATAM" },
                    { "CO", "MERCOSUR" },
                    { "CO", "OECD" },
                    { "CO", "SA" },
                    { "CO", "UN" },
                    { "CR", "CAM" },
                    { "CR", "LATAM" },
                    { "CR", "NA" },
                    { "CR", "OECD" },
                    { "CR", "UN" },
                    { "CU", "CARIB" },
                    { "CU", "LATAM" },
                    { "CU", "NA" },
                    { "CU", "UN" },
                    { "CV", "AF" },
                    { "CV", "AU" },
                    { "CV", "OIF" },
                    { "CV", "SSA" },
                    { "CV", "UN" },
                    { "CZ", "COE" },
                    { "CZ", "EEA" },
                    { "CZ", "EU" },
                    { "CZ", "EUCU" },
                    { "CZ", "EUR" },
                    { "CZ", "NATO" },
                    { "CZ", "OECD" },
                    { "CZ", "UN" },
                    { "DJ", "AF" },
                    { "DJ", "AL" },
                    { "DJ", "AU" },
                    { "DJ", "OIF" },
                    { "DJ", "SSA" },
                    { "DJ", "UN" },
                    { "DK", "COE" },
                    { "DK", "EEA" },
                    { "DK", "EU" },
                    { "DK", "EUCU" },
                    { "DK", "EUR" },
                    { "DK", "NATO" },
                    { "DK", "NC" },
                    { "DK", "NORD" },
                    { "DK", "OECD" },
                    { "DK", "SCAN" },
                    { "DK", "UN" },
                    { "DM", "CARIB" },
                    { "DM", "CARICOM" },
                    { "DM", "CW" },
                    { "DM", "ECCU" },
                    { "DM", "NA" },
                    { "DM", "OIF" },
                    { "DM", "UN" },
                    { "DO", "CARIB" },
                    { "DO", "LATAM" },
                    { "DO", "NA" },
                    { "DO", "UN" },
                    { "DZ", "AF" },
                    { "DZ", "AL" },
                    { "DZ", "AU" },
                    { "DZ", "MENA" },
                    { "DZ", "OPEC" },
                    { "DZ", "UN" },
                    { "EG", "AF" },
                    { "EG", "AL" },
                    { "EG", "AU" },
                    { "EG", "BRICS" },
                    { "EG", "MENA" },
                    { "EG", "OIF" },
                    { "EG", "UN" },
                    { "EH", "AF" },
                    { "EH", "AU" },
                    { "EH", "MENA" },
                    { "ER", "AF" },
                    { "ER", "AU" },
                    { "ER", "SAHEL" },
                    { "ER", "SSA" },
                    { "ER", "UN" },
                    { "ET", "AF" },
                    { "ET", "AU" },
                    { "ET", "BRICS" },
                    { "ET", "SSA" },
                    { "ET", "UN" },
                    { "FJ", "CW" },
                    { "FJ", "OC" },
                    { "FJ", "UN" },
                    { "GA", "AF" },
                    { "GA", "AU" },
                    { "GA", "CEMAC" },
                    { "GA", "OIF" },
                    { "GA", "OPEC" },
                    { "GA", "SSA" },
                    { "GA", "UN" },
                    { "GD", "CARIB" },
                    { "GD", "CARICOM" },
                    { "GD", "CW" },
                    { "GD", "ECCU" },
                    { "GD", "NA" },
                    { "GD", "UN" },
                    { "GE", "AS" },
                    { "GE", "COE" },
                    { "GE", "UN" },
                    { "GH", "AF" },
                    { "GH", "AU" },
                    { "GH", "CW" },
                    { "GH", "SSA" },
                    { "GH", "UN" },
                    { "GM", "AF" },
                    { "GM", "AU" },
                    { "GM", "CW" },
                    { "GM", "SSA" },
                    { "GM", "UN" },
                    { "GN", "AF" },
                    { "GN", "AU" },
                    { "GN", "OIF" },
                    { "GN", "SSA" },
                    { "GN", "UN" },
                    { "GQ", "AF" },
                    { "GQ", "AU" },
                    { "GQ", "CEMAC" },
                    { "GQ", "OIF" },
                    { "GQ", "OPEC" },
                    { "GQ", "SSA" },
                    { "GQ", "UN" },
                    { "GT", "CAM" },
                    { "GT", "LATAM" },
                    { "GT", "NA" },
                    { "GT", "UN" },
                    { "GW", "AF" },
                    { "GW", "AU" },
                    { "GW", "OIF" },
                    { "GW", "SSA" },
                    { "GW", "UN" },
                    { "GW", "WAEMU" },
                    { "GY", "CARICOM" },
                    { "GY", "CW" },
                    { "GY", "LATAM" },
                    { "GY", "MERCOSUR" },
                    { "GY", "SA" },
                    { "GY", "UN" },
                    { "HN", "CAM" },
                    { "HN", "LATAM" },
                    { "HN", "NA" },
                    { "HN", "UN" },
                    { "HT", "CARIB" },
                    { "HT", "CARICOM" },
                    { "HT", "LATAM" },
                    { "HT", "NA" },
                    { "HT", "OIF" },
                    { "HT", "UN" },
                    { "HU", "COE" },
                    { "HU", "EEA" },
                    { "HU", "EU" },
                    { "HU", "EUCU" },
                    { "HU", "EUR" },
                    { "HU", "NATO" },
                    { "HU", "OECD" },
                    { "HU", "UN" },
                    { "ID", "AFTA" },
                    { "ID", "AS" },
                    { "ID", "ASEAN" },
                    { "ID", "G20" },
                    { "ID", "RCEP" },
                    { "ID", "SEA" },
                    { "ID", "UN" },
                    { "IL", "AS" },
                    { "IL", "MENA" },
                    { "IL", "OECD" },
                    { "IL", "UN" },
                    { "IN", "AS" },
                    { "IN", "BRICS" },
                    { "IN", "CW" },
                    { "IN", "G20" },
                    { "IN", "INDS" },
                    { "IN", "QUAD" },
                    { "IN", "SAARC" },
                    { "IN", "UN" },
                    { "IQ", "AL" },
                    { "IQ", "AS" },
                    { "IQ", "MENA" },
                    { "IQ", "OPEC" },
                    { "IQ", "UN" },
                    { "IR", "AS" },
                    { "IR", "BRICS" },
                    { "IR", "MENA" },
                    { "IR", "OPEC" },
                    { "IR", "UN" },
                    { "IS", "COE" },
                    { "IS", "EEA" },
                    { "IS", "EU" },
                    { "IS", "NATO" },
                    { "IS", "NC" },
                    { "IS", "NORD" },
                    { "IS", "OECD" },
                    { "IS", "UN" },
                    { "JM", "CARIB" },
                    { "JM", "CARICOM" },
                    { "JM", "CW" },
                    { "JM", "NA" },
                    { "JM", "UN" },
                    { "JO", "AL" },
                    { "JO", "AS" },
                    { "JO", "MENA" },
                    { "JO", "UN" },
                    { "KE", "AF" },
                    { "KE", "AU" },
                    { "KE", "CW" },
                    { "KE", "SSA" },
                    { "KE", "UN" },
                    { "KG", "AS" },
                    { "KG", "CAS" },
                    { "KG", "CSTO" },
                    { "KG", "EAEU" },
                    { "KG", "UN" },
                    { "KH", "AFTA" },
                    { "KH", "AS" },
                    { "KH", "ASEAN" },
                    { "KH", "RCEP" },
                    { "KH", "SEA" },
                    { "KH", "UN" },
                    { "KI", "CW" },
                    { "KI", "OC" },
                    { "KI", "UN" },
                    { "KM", "AF" },
                    { "KM", "AL" },
                    { "KM", "AU" },
                    { "KM", "OIF" },
                    { "KM", "SSA" },
                    { "KM", "UN" },
                    { "KN", "CARIB" },
                    { "KN", "CARICOM" },
                    { "KN", "CW" },
                    { "KN", "ECCU" },
                    { "KN", "NA" },
                    { "KN", "UN" },
                    { "KP", "AS" },
                    { "KP", "EAS" },
                    { "KP", "UN" },
                    { "KR", "AS" },
                    { "KR", "EAS" },
                    { "KR", "G20" },
                    { "KR", "OECD" },
                    { "KR", "RCEP" },
                    { "KR", "UN" },
                    { "KW", "AL" },
                    { "KW", "ARAB" },
                    { "KW", "AS" },
                    { "KW", "GCC" },
                    { "KW", "MENA" },
                    { "KW", "OPEC" },
                    { "KW", "UN" },
                    { "KZ", "AS" },
                    { "KZ", "CAS" },
                    { "KZ", "CSTO" },
                    { "KZ", "EAEU" },
                    { "KZ", "UN" },
                    { "LA", "AFTA" },
                    { "LA", "AS" },
                    { "LA", "ASEAN" },
                    { "LA", "RCEP" },
                    { "LA", "SEA" },
                    { "LA", "UN" },
                    { "LB", "AL" },
                    { "LB", "AS" },
                    { "LB", "MENA" },
                    { "LB", "OIF" },
                    { "LB", "UN" },
                    { "LC", "CARIB" },
                    { "LC", "CARICOM" },
                    { "LC", "CW" },
                    { "LC", "ECCU" },
                    { "LC", "NA" },
                    { "LC", "UN" },
                    { "LI", "COE" },
                    { "LI", "EEA" },
                    { "LI", "EU" },
                    { "LI", "UN" },
                    { "LK", "AS" },
                    { "LK", "CW" },
                    { "LK", "INDS" },
                    { "LK", "SAARC" },
                    { "LK", "UN" },
                    { "LR", "AF" },
                    { "LR", "AU" },
                    { "LR", "SSA" },
                    { "LR", "UN" },
                    { "LS", "AF" },
                    { "LS", "AU" },
                    { "LS", "CW" },
                    { "LS", "SACU" },
                    { "LS", "SSA" },
                    { "LS", "UN" },
                    { "LY", "AF" },
                    { "LY", "AL" },
                    { "LY", "AU" },
                    { "LY", "MENA" },
                    { "LY", "OPEC" },
                    { "LY", "UN" },
                    { "MA", "AF" },
                    { "MA", "AL" },
                    { "MA", "AU" },
                    { "MA", "MENA" },
                    { "MA", "OIF" },
                    { "MA", "UN" },
                    { "MD", "COE" },
                    { "MD", "EU" },
                    { "MD", "UN" },
                    { "MG", "AF" },
                    { "MG", "AU" },
                    { "MG", "OIF" },
                    { "MG", "SSA" },
                    { "MG", "UN" },
                    { "MK", "BALK" },
                    { "MK", "COE" },
                    { "MK", "EU" },
                    { "MK", "NATO" },
                    { "MK", "OIF" },
                    { "MK", "UN" },
                    { "ML", "AF" },
                    { "ML", "AU" },
                    { "ML", "OIF" },
                    { "ML", "SAHEL" },
                    { "ML", "SSA" },
                    { "ML", "UN" },
                    { "ML", "WAEMU" },
                    { "MM", "AFTA" },
                    { "MM", "AS" },
                    { "MM", "ASEAN" },
                    { "MM", "RCEP" },
                    { "MM", "SEA" },
                    { "MM", "UN" },
                    { "MN", "AS" },
                    { "MN", "EAS" },
                    { "MN", "UN" },
                    { "MR", "AF" },
                    { "MR", "AL" },
                    { "MR", "AU" },
                    { "MR", "OIF" },
                    { "MR", "SAHEL" },
                    { "MR", "SSA" },
                    { "MR", "UN" },
                    { "MU", "AF" },
                    { "MU", "AU" },
                    { "MU", "CW" },
                    { "MU", "OIF" },
                    { "MU", "SSA" },
                    { "MU", "UN" },
                    { "MV", "AS" },
                    { "MV", "CW" },
                    { "MV", "INDS" },
                    { "MV", "SAARC" },
                    { "MV", "UN" },
                    { "MW", "AF" },
                    { "MW", "AU" },
                    { "MW", "CW" },
                    { "MW", "SSA" },
                    { "MW", "UN" },
                    { "MX", "CPTPP" },
                    { "MX", "G20" },
                    { "MX", "LATAM" },
                    { "MX", "NA" },
                    { "MX", "OECD" },
                    { "MX", "UN" },
                    { "MX", "USMCA" },
                    { "MY", "AFTA" },
                    { "MY", "AS" },
                    { "MY", "ASEAN" },
                    { "MY", "CPTPP" },
                    { "MY", "CW" },
                    { "MY", "RCEP" },
                    { "MY", "SEA" },
                    { "MY", "UN" },
                    { "MZ", "AF" },
                    { "MZ", "AU" },
                    { "MZ", "CW" },
                    { "MZ", "SSA" },
                    { "MZ", "UN" },
                    { "NA", "AF" },
                    { "NA", "AU" },
                    { "NA", "CW" },
                    { "NA", "SACU" },
                    { "NA", "SSA" },
                    { "NA", "UN" },
                    { "NE", "AF" },
                    { "NE", "AU" },
                    { "NE", "OIF" },
                    { "NE", "SAHEL" },
                    { "NE", "SSA" },
                    { "NE", "UN" },
                    { "NE", "WAEMU" },
                    { "NG", "AF" },
                    { "NG", "AU" },
                    { "NG", "CW" },
                    { "NG", "OPEC" },
                    { "NG", "SSA" },
                    { "NG", "UN" },
                    { "NI", "CAM" },
                    { "NI", "LATAM" },
                    { "NI", "NA" },
                    { "NI", "UN" },
                    { "NO", "COE" },
                    { "NO", "EEA" },
                    { "NO", "EU" },
                    { "NO", "NATO" },
                    { "NO", "NC" },
                    { "NO", "NORD" },
                    { "NO", "OECD" },
                    { "NO", "SCAN" },
                    { "NO", "UN" },
                    { "NP", "AS" },
                    { "NP", "INDS" },
                    { "NP", "SAARC" },
                    { "NP", "UN" },
                    { "NR", "CW" },
                    { "NR", "OC" },
                    { "NR", "UN" },
                    { "NZ", "ANZUS" },
                    { "NZ", "CPTPP" },
                    { "NZ", "CW" },
                    { "NZ", "FVEY" },
                    { "NZ", "OC" },
                    { "NZ", "OECD" },
                    { "NZ", "RCEP" },
                    { "NZ", "UN" },
                    { "OM", "AL" },
                    { "OM", "ARAB" },
                    { "OM", "AS" },
                    { "OM", "GCC" },
                    { "OM", "MENA" },
                    { "OM", "UN" },
                    { "PE", "CPTPP" },
                    { "PE", "LATAM" },
                    { "PE", "MERCOSUR" },
                    { "PE", "SA" },
                    { "PE", "UN" },
                    { "PG", "CW" },
                    { "PG", "OC" },
                    { "PG", "UN" },
                    { "PH", "AFTA" },
                    { "PH", "AS" },
                    { "PH", "ASEAN" },
                    { "PH", "RCEP" },
                    { "PH", "SEA" },
                    { "PH", "UN" },
                    { "PK", "AS" },
                    { "PK", "CW" },
                    { "PK", "INDS" },
                    { "PK", "SAARC" },
                    { "PK", "UN" },
                    { "PL", "COE" },
                    { "PL", "EEA" },
                    { "PL", "EU" },
                    { "PL", "EUCU" },
                    { "PL", "EUR" },
                    { "PL", "NATO" },
                    { "PL", "OECD" },
                    { "PL", "UN" },
                    { "PS", "AL" },
                    { "PS", "AS" },
                    { "PS", "MENA" },
                    { "PY", "LATAM" },
                    { "PY", "MERCOSUR" },
                    { "PY", "SA" },
                    { "PY", "UN" },
                    { "QA", "AL" },
                    { "QA", "ARAB" },
                    { "QA", "AS" },
                    { "QA", "GCC" },
                    { "QA", "MENA" },
                    { "QA", "UN" },
                    { "RO", "BALK" },
                    { "RO", "COE" },
                    { "RO", "EEA" },
                    { "RO", "EU" },
                    { "RO", "EUCU" },
                    { "RO", "EUR" },
                    { "RO", "NATO" },
                    { "RO", "OIF" },
                    { "RO", "UN" },
                    { "RS", "BALK" },
                    { "RS", "COE" },
                    { "RS", "EU" },
                    { "RS", "UN" },
                    { "RU", "AS" },
                    { "RU", "BRICS" },
                    { "RU", "CSTO" },
                    { "RU", "EAEU" },
                    { "RU", "G20" },
                    { "RU", "UN" },
                    { "RW", "AF" },
                    { "RW", "AU" },
                    { "RW", "CW" },
                    { "RW", "OIF" },
                    { "RW", "SSA" },
                    { "RW", "UN" },
                    { "SA", "AL" },
                    { "SA", "ARAB" },
                    { "SA", "AS" },
                    { "SA", "BRICS" },
                    { "SA", "G20" },
                    { "SA", "GCC" },
                    { "SA", "MENA" },
                    { "SA", "OPEC" },
                    { "SA", "UN" },
                    { "SB", "CW" },
                    { "SB", "OC" },
                    { "SB", "UN" },
                    { "SC", "AF" },
                    { "SC", "AU" },
                    { "SC", "CW" },
                    { "SC", "OIF" },
                    { "SC", "SSA" },
                    { "SC", "UN" },
                    { "SD", "AF" },
                    { "SD", "AL" },
                    { "SD", "AU" },
                    { "SD", "SAHEL" },
                    { "SD", "UN" },
                    { "SE", "COE" },
                    { "SE", "EEA" },
                    { "SE", "EU" },
                    { "SE", "EUCU" },
                    { "SE", "EUR" },
                    { "SE", "NATO" },
                    { "SE", "NC" },
                    { "SE", "NORD" },
                    { "SE", "OECD" },
                    { "SE", "SCAN" },
                    { "SE", "UN" },
                    { "SG", "AFTA" },
                    { "SG", "AS" },
                    { "SG", "ASEAN" },
                    { "SG", "CPTPP" },
                    { "SG", "CW" },
                    { "SG", "RCEP" },
                    { "SG", "SEA" },
                    { "SG", "UN" },
                    { "SL", "AF" },
                    { "SL", "AU" },
                    { "SL", "CW" },
                    { "SL", "SSA" },
                    { "SL", "UN" },
                    { "SN", "AF" },
                    { "SN", "AU" },
                    { "SN", "OIF" },
                    { "SN", "SAHEL" },
                    { "SN", "SSA" },
                    { "SN", "UN" },
                    { "SN", "WAEMU" },
                    { "SO", "AF" },
                    { "SO", "AL" },
                    { "SO", "AU" },
                    { "SO", "SSA" },
                    { "SO", "UN" },
                    { "SR", "CARICOM" },
                    { "SR", "LATAM" },
                    { "SR", "MERCOSUR" },
                    { "SR", "SA" },
                    { "SR", "UN" },
                    { "SS", "AF" },
                    { "SS", "AU" },
                    { "SS", "SSA" },
                    { "SS", "UN" },
                    { "ST", "AF" },
                    { "ST", "AU" },
                    { "ST", "OIF" },
                    { "ST", "SSA" },
                    { "ST", "UN" },
                    { "SY", "AL" },
                    { "SY", "AS" },
                    { "SY", "MENA" },
                    { "SY", "UN" },
                    { "SZ", "AF" },
                    { "SZ", "AU" },
                    { "SZ", "CW" },
                    { "SZ", "SACU" },
                    { "SZ", "SSA" },
                    { "SZ", "UN" },
                    { "TD", "AF" },
                    { "TD", "AU" },
                    { "TD", "CEMAC" },
                    { "TD", "OIF" },
                    { "TD", "SAHEL" },
                    { "TD", "SSA" },
                    { "TD", "UN" },
                    { "TG", "AF" },
                    { "TG", "AU" },
                    { "TG", "CW" },
                    { "TG", "OIF" },
                    { "TG", "SSA" },
                    { "TG", "UN" },
                    { "TG", "WAEMU" },
                    { "TH", "AFTA" },
                    { "TH", "AS" },
                    { "TH", "ASEAN" },
                    { "TH", "RCEP" },
                    { "TH", "SEA" },
                    { "TH", "UN" },
                    { "TJ", "AS" },
                    { "TJ", "CAS" },
                    { "TJ", "CSTO" },
                    { "TJ", "UN" },
                    { "TM", "AS" },
                    { "TM", "CAS" },
                    { "TM", "UN" },
                    { "TN", "AF" },
                    { "TN", "AL" },
                    { "TN", "AU" },
                    { "TN", "MENA" },
                    { "TN", "OIF" },
                    { "TN", "UN" },
                    { "TO", "CW" },
                    { "TO", "OC" },
                    { "TO", "UN" },
                    { "TR", "AS" },
                    { "TR", "COE" },
                    { "TR", "EUCU" },
                    { "TR", "G20" },
                    { "TR", "MENA" },
                    { "TR", "NATO" },
                    { "TR", "OECD" },
                    { "TR", "UN" },
                    { "TT", "CARIB" },
                    { "TT", "CARICOM" },
                    { "TT", "CW" },
                    { "TT", "NA" },
                    { "TT", "UN" },
                    { "TV", "CW" },
                    { "TV", "OC" },
                    { "TV", "UN" },
                    { "TW", "AS" },
                    { "TW", "EAS" },
                    { "TZ", "AF" },
                    { "TZ", "AU" },
                    { "TZ", "CW" },
                    { "TZ", "SSA" },
                    { "TZ", "UN" },
                    { "UA", "COE" },
                    { "UA", "EU" },
                    { "UA", "UN" },
                    { "UG", "AF" },
                    { "UG", "AU" },
                    { "UG", "CW" },
                    { "UG", "SSA" },
                    { "UG", "UN" },
                    { "UY", "LATAM" },
                    { "UY", "MERCOSUR" },
                    { "UY", "SA" },
                    { "UY", "UN" },
                    { "UZ", "AS" },
                    { "UZ", "CAS" },
                    { "UZ", "UN" },
                    { "VC", "CARIB" },
                    { "VC", "CARICOM" },
                    { "VC", "CW" },
                    { "VC", "ECCU" },
                    { "VC", "NA" },
                    { "VC", "UN" },
                    { "VE", "LATAM" },
                    { "VE", "OPEC" },
                    { "VE", "SA" },
                    { "VE", "UN" },
                    { "VN", "AFTA" },
                    { "VN", "AS" },
                    { "VN", "ASEAN" },
                    { "VN", "CPTPP" },
                    { "VN", "OIF" },
                    { "VN", "RCEP" },
                    { "VN", "SEA" },
                    { "VN", "UN" },
                    { "VU", "CW" },
                    { "VU", "OC" },
                    { "VU", "OIF" },
                    { "VU", "UN" },
                    { "WS", "CW" },
                    { "WS", "OC" },
                    { "WS", "UN" },
                    { "YE", "AL" },
                    { "YE", "ARAB" },
                    { "YE", "AS" },
                    { "YE", "MENA" },
                    { "YE", "UN" },
                    { "ZA", "AF" },
                    { "ZA", "AU" },
                    { "ZA", "BRICS" },
                    { "ZA", "CW" },
                    { "ZA", "G20" },
                    { "ZA", "SACU" },
                    { "ZA", "SSA" },
                    { "ZA", "UN" },
                    { "ZM", "AF" },
                    { "ZM", "AU" },
                    { "ZM", "CW" },
                    { "ZM", "SSA" },
                    { "ZM", "UN" },
                });

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "iso_3166_1_alpha_2_code", "display_name", "iso_3166_1_alpha_3_code", "iso_3166_1_numeric_code", "official_name", "phone_number_prefix", "primary_currency_iso_4217_alpha_code", "primary_locale_ietf_bcp_47_tag", "sovereign_iso_3166_1_alpha_2_code" },
                values: new object[,]
                {
                    { "AI", "Anguilla", "AIA", "660", "Anguilla", "1", null, null, "GB" },
                    { "AS", "American Samoa", "ASM", "016", "American Samoa", "1", "USD", null, "US" },
                    { "AW", "Aruba", "ABW", "533", "Aruba", "297", null, null, "NL" },
                    { "AX", "Åland Islands", "ALA", "248", "Åland Islands", "358", "EUR", null, "FI" },
                    { "BL", "Saint Barthélemy", "BLM", "652", "Collectivity of Saint Barthélemy", "590", "EUR", null, "FR" },
                    { "BM", "Bermuda", "BMU", "060", "Bermuda", "1", null, null, "GB" },
                    { "BQ", "Bonaire, Sint Eustatius and Saba", "BES", "535", "Bonaire, Sint Eustatius and Saba", "599", "USD", null, "NL" },
                    { "CW", "Curaçao", "CUW", "531", "Country of Curaçao", "599", null, null, "NL" },
                    { "FK", "Falkland Islands", "FLK", "238", "Falkland Islands", "500", null, null, "GB" },
                    { "GF", "French Guiana", "GUF", "254", "French Guiana", "594", "EUR", null, "FR" },
                    { "GG", "Guernsey", "GGY", "831", "Bailiwick of Guernsey", "44", "GBP", null, "GB" },
                    { "GI", "Gibraltar", "GIB", "292", "Gibraltar", "350", null, null, "GB" },
                    { "GP", "Guadeloupe", "GLP", "312", "Guadeloupe", "590", "EUR", null, "FR" },
                    { "GS", "South Georgia and the South Sandwich Islands", "SGS", "239", "South Georgia and the South Sandwich Islands", "44", null, null, "GB" },
                    { "GU", "Guam", "GUM", "316", "Guam", "1", "USD", null, "US" },
                    { "IM", "Isle of Man", "IMN", "833", "Isle of Man", "44", "GBP", null, "GB" },
                    { "IO", "British Indian Ocean Territory", "IOT", "086", "British Indian Ocean Territory", "246", "USD", null, "GB" },
                    { "JE", "Jersey", "JEY", "832", "Bailiwick of Jersey", "44", "GBP", null, "GB" },
                    { "KY", "Cayman Islands", "CYM", "136", "Cayman Islands", "1", null, null, "GB" },
                    { "MF", "Saint Martin", "MAF", "663", "Collectivity of Saint Martin", "590", "EUR", null, "FR" },
                    { "MP", "Northern Mariana Islands", "MNP", "580", "Commonwealth of the Northern Mariana Islands", "1", "USD", null, "US" },
                    { "MQ", "Martinique", "MTQ", "474", "Martinique", "596", "EUR", null, "FR" },
                    { "MS", "Montserrat", "MSR", "500", "Montserrat", "1", null, null, "GB" },
                    { "NC", "New Caledonia", "NCL", "540", "New Caledonia", "687", null, null, "FR" },
                    { "PF", "French Polynesia", "PYF", "258", "French Polynesia", "689", null, null, "FR" },
                    { "PM", "Saint Pierre and Miquelon", "SPM", "666", "Saint Pierre and Miquelon", "508", "EUR", null, "FR" },
                    { "PN", "Pitcairn Islands", "PCN", "612", "Pitcairn, Henderson, Ducie and Oeno Islands", "64", null, null, "GB" },
                    { "PR", "Puerto Rico", "PRI", "630", "Commonwealth of Puerto Rico", "1", "USD", null, "US" },
                    { "RE", "Réunion", "REU", "638", "Réunion", "262", "EUR", null, "FR" },
                    { "SH", "Saint Helena, Ascension and Tristan da Cunha", "SHN", "654", "Saint Helena, Ascension and Tristan da Cunha", "290", null, null, "GB" },
                    { "SX", "Sint Maarten", "SXM", "534", "Sint Maarten", "1", null, null, "NL" },
                    { "TC", "Turks and Caicos Islands", "TCA", "796", "Turks and Caicos Islands", "1", "USD", null, "GB" },
                    { "TF", "French Southern Territories", "ATF", "260", "French Southern and Antarctic Lands", "33", "EUR", null, "FR" },
                    { "UM", "United States Minor Outlying Islands", "UMI", "581", "United States Minor Outlying Islands", "1", "USD", null, "US" },
                    { "VG", "British Virgin Islands", "VGB", "092", "Virgin Islands", "1", "USD", null, "GB" },
                    { "VI", "U.S. Virgin Islands", "VIR", "850", "Virgin Islands of the United States", "1", "USD", null, "US" },
                    { "WF", "Wallis and Futuna", "WLF", "876", "Territory of the Wallis and Futuna Islands", "681", null, null, "FR" },
                    { "YT", "Mayotte", "MYT", "175", "Department of Mayotte", "262", "EUR", null, "FR" },
                });

            migrationBuilder.InsertData(
                table: "country_currencies",
                columns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                values: new object[,]
                {
                    { "AD", "EUR" },
                    { "AT", "EUR" },
                    { "BE", "EUR" },
                    { "CA", "CAD" },
                    { "CA", "USD" },
                    { "CY", "EUR" },
                    { "DE", "EUR" },
                    { "EC", "USD" },
                    { "EE", "EUR" },
                    { "ES", "EUR" },
                    { "FI", "EUR" },
                    { "FM", "USD" },
                    { "FR", "EUR" },
                    { "GB", "GBP" },
                    { "GR", "EUR" },
                    { "HR", "EUR" },
                    { "IE", "EUR" },
                    { "IE", "GBP" },
                    { "IT", "EUR" },
                    { "JP", "JPY" },
                    { "LT", "EUR" },
                    { "LU", "EUR" },
                    { "LV", "EUR" },
                    { "MC", "EUR" },
                    { "ME", "EUR" },
                    { "MH", "USD" },
                    { "MT", "EUR" },
                    { "NL", "EUR" },
                    { "PA", "USD" },
                    { "PT", "EUR" },
                    { "PW", "USD" },
                    { "SI", "EUR" },
                    { "SK", "EUR" },
                    { "SM", "EUR" },
                    { "SV", "USD" },
                    { "TL", "USD" },
                    { "US", "USD" },
                    { "VA", "EUR" },
                    { "ZW", "USD" },
                });

            migrationBuilder.InsertData(
                table: "country_geopolitical_entities",
                columns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                values: new object[,]
                {
                    { "AD", "COE" },
                    { "AD", "EU" },
                    { "AD", "EUCU" },
                    { "AD", "EZ" },
                    { "AD", "OIF" },
                    { "AD", "UN" },
                    { "AT", "COE" },
                    { "AT", "EEA" },
                    { "AT", "EU" },
                    { "AT", "EUCU" },
                    { "AT", "EUR" },
                    { "AT", "EZ" },
                    { "AT", "OECD" },
                    { "AT", "UN" },
                    { "BE", "BENE" },
                    { "BE", "COE" },
                    { "BE", "EEA" },
                    { "BE", "EU" },
                    { "BE", "EUCU" },
                    { "BE", "EUR" },
                    { "BE", "EZ" },
                    { "BE", "NATO" },
                    { "BE", "OECD" },
                    { "BE", "OIF" },
                    { "BE", "UN" },
                    { "CA", "CPTPP" },
                    { "CA", "CW" },
                    { "CA", "FVEY" },
                    { "CA", "G20" },
                    { "CA", "G7" },
                    { "CA", "NA" },
                    { "CA", "NATO" },
                    { "CA", "OECD" },
                    { "CA", "OIF" },
                    { "CA", "UN" },
                    { "CA", "USMCA" },
                    { "CC", "OC" },
                    { "CK", "OC" },
                    { "CX", "OC" },
                    { "CY", "AS" },
                    { "CY", "COE" },
                    { "CY", "CW" },
                    { "CY", "EEA" },
                    { "CY", "EUCU" },
                    { "CY", "EUR" },
                    { "CY", "EZ" },
                    { "CY", "UN" },
                    { "DE", "COE" },
                    { "DE", "EEA" },
                    { "DE", "EU" },
                    { "DE", "EUCU" },
                    { "DE", "EUR" },
                    { "DE", "EZ" },
                    { "DE", "G20" },
                    { "DE", "G7" },
                    { "DE", "NATO" },
                    { "DE", "OECD" },
                    { "DE", "UN" },
                    { "EC", "LATAM" },
                    { "EC", "MERCOSUR" },
                    { "EC", "SA" },
                    { "EC", "UN" },
                    { "EE", "BALT" },
                    { "EE", "COE" },
                    { "EE", "EEA" },
                    { "EE", "EU" },
                    { "EE", "EUCU" },
                    { "EE", "EUR" },
                    { "EE", "EZ" },
                    { "EE", "NATO" },
                    { "EE", "OECD" },
                    { "EE", "UN" },
                    { "ES", "COE" },
                    { "ES", "EEA" },
                    { "ES", "EU" },
                    { "ES", "EUCU" },
                    { "ES", "EUR" },
                    { "ES", "EZ" },
                    { "ES", "NATO" },
                    { "ES", "OECD" },
                    { "ES", "UN" },
                    { "FI", "COE" },
                    { "FI", "EEA" },
                    { "FI", "EU" },
                    { "FI", "EUCU" },
                    { "FI", "EUR" },
                    { "FI", "EZ" },
                    { "FI", "NATO" },
                    { "FI", "NC" },
                    { "FI", "NORD" },
                    { "FI", "OECD" },
                    { "FI", "UN" },
                    { "FM", "OC" },
                    { "FM", "UN" },
                    { "FO", "EU" },
                    { "FO", "NC" },
                    { "FO", "NORD" },
                    { "FR", "COE" },
                    { "FR", "EEA" },
                    { "FR", "EU" },
                    { "FR", "EUCU" },
                    { "FR", "EUR" },
                    { "FR", "EZ" },
                    { "FR", "G20" },
                    { "FR", "G7" },
                    { "FR", "NATO" },
                    { "FR", "OECD" },
                    { "FR", "OIF" },
                    { "FR", "UN" },
                    { "GB", "AUKUS" },
                    { "GB", "COE" },
                    { "GB", "CW" },
                    { "GB", "EU" },
                    { "GB", "FVEY" },
                    { "GB", "G20" },
                    { "GB", "G7" },
                    { "GB", "NATO" },
                    { "GB", "OECD" },
                    { "GB", "UN" },
                    { "GL", "NA" },
                    { "GL", "NC" },
                    { "GL", "NORD" },
                    { "GR", "COE" },
                    { "GR", "EEA" },
                    { "GR", "EU" },
                    { "GR", "EUCU" },
                    { "GR", "EUR" },
                    { "GR", "EZ" },
                    { "GR", "NATO" },
                    { "GR", "OECD" },
                    { "GR", "OIF" },
                    { "GR", "UN" },
                    { "HK", "AS" },
                    { "HK", "EAS" },
                    { "HM", "OC" },
                    { "HR", "BALK" },
                    { "HR", "COE" },
                    { "HR", "EEA" },
                    { "HR", "EU" },
                    { "HR", "EUCU" },
                    { "HR", "EUR" },
                    { "HR", "EZ" },
                    { "HR", "NATO" },
                    { "HR", "UN" },
                    { "IE", "COE" },
                    { "IE", "EEA" },
                    { "IE", "EU" },
                    { "IE", "EUCU" },
                    { "IE", "EUR" },
                    { "IE", "EZ" },
                    { "IE", "OECD" },
                    { "IE", "UN" },
                    { "IT", "COE" },
                    { "IT", "EEA" },
                    { "IT", "EU" },
                    { "IT", "EUCU" },
                    { "IT", "EUR" },
                    { "IT", "EZ" },
                    { "IT", "G20" },
                    { "IT", "G7" },
                    { "IT", "NATO" },
                    { "IT", "OECD" },
                    { "IT", "UN" },
                    { "JP", "AS" },
                    { "JP", "CPTPP" },
                    { "JP", "EAS" },
                    { "JP", "G20" },
                    { "JP", "G7" },
                    { "JP", "OECD" },
                    { "JP", "QUAD" },
                    { "JP", "RCEP" },
                    { "JP", "UN" },
                    { "LT", "BALT" },
                    { "LT", "COE" },
                    { "LT", "EEA" },
                    { "LT", "EU" },
                    { "LT", "EUCU" },
                    { "LT", "EUR" },
                    { "LT", "EZ" },
                    { "LT", "NATO" },
                    { "LT", "OECD" },
                    { "LT", "UN" },
                    { "LU", "BENE" },
                    { "LU", "COE" },
                    { "LU", "EEA" },
                    { "LU", "EU" },
                    { "LU", "EUCU" },
                    { "LU", "EUR" },
                    { "LU", "EZ" },
                    { "LU", "NATO" },
                    { "LU", "OECD" },
                    { "LU", "OIF" },
                    { "LU", "UN" },
                    { "LV", "BALT" },
                    { "LV", "COE" },
                    { "LV", "EEA" },
                    { "LV", "EU" },
                    { "LV", "EUCU" },
                    { "LV", "EUR" },
                    { "LV", "EZ" },
                    { "LV", "NATO" },
                    { "LV", "OECD" },
                    { "LV", "UN" },
                    { "MC", "COE" },
                    { "MC", "EU" },
                    { "MC", "EUCU" },
                    { "MC", "EZ" },
                    { "MC", "OIF" },
                    { "MC", "UN" },
                    { "ME", "BALK" },
                    { "ME", "COE" },
                    { "ME", "EU" },
                    { "ME", "EZ" },
                    { "ME", "NATO" },
                    { "ME", "OIF" },
                    { "ME", "UN" },
                    { "MH", "OC" },
                    { "MH", "UN" },
                    { "MO", "AS" },
                    { "MO", "EAS" },
                    { "MT", "COE" },
                    { "MT", "CW" },
                    { "MT", "EEA" },
                    { "MT", "EU" },
                    { "MT", "EUCU" },
                    { "MT", "EUR" },
                    { "MT", "EZ" },
                    { "MT", "UN" },
                    { "NF", "OC" },
                    { "NL", "BENE" },
                    { "NL", "COE" },
                    { "NL", "EEA" },
                    { "NL", "EU" },
                    { "NL", "EUCU" },
                    { "NL", "EUR" },
                    { "NL", "EZ" },
                    { "NL", "NATO" },
                    { "NL", "OECD" },
                    { "NL", "UN" },
                    { "NU", "OC" },
                    { "PA", "CAM" },
                    { "PA", "LATAM" },
                    { "PA", "NA" },
                    { "PA", "UN" },
                    { "PT", "COE" },
                    { "PT", "EEA" },
                    { "PT", "EU" },
                    { "PT", "EUCU" },
                    { "PT", "EUR" },
                    { "PT", "EZ" },
                    { "PT", "NATO" },
                    { "PT", "OECD" },
                    { "PT", "UN" },
                    { "PW", "OC" },
                    { "PW", "UN" },
                    { "SI", "BALK" },
                    { "SI", "COE" },
                    { "SI", "EEA" },
                    { "SI", "EU" },
                    { "SI", "EUCU" },
                    { "SI", "EUR" },
                    { "SI", "EZ" },
                    { "SI", "NATO" },
                    { "SI", "OECD" },
                    { "SI", "UN" },
                    { "SJ", "EU" },
                    { "SK", "COE" },
                    { "SK", "EEA" },
                    { "SK", "EU" },
                    { "SK", "EUCU" },
                    { "SK", "EUR" },
                    { "SK", "EZ" },
                    { "SK", "NATO" },
                    { "SK", "OECD" },
                    { "SK", "UN" },
                    { "SM", "COE" },
                    { "SM", "EU" },
                    { "SM", "EUCU" },
                    { "SM", "EZ" },
                    { "SM", "UN" },
                    { "SV", "CAM" },
                    { "SV", "LATAM" },
                    { "SV", "NA" },
                    { "SV", "UN" },
                    { "TK", "OC" },
                    { "TL", "AS" },
                    { "TL", "SEA" },
                    { "TL", "UN" },
                    { "US", "ANZUS" },
                    { "US", "AUKUS" },
                    { "US", "FVEY" },
                    { "US", "G20" },
                    { "US", "G7" },
                    { "US", "NA" },
                    { "US", "NATO" },
                    { "US", "OECD" },
                    { "US", "QUAD" },
                    { "US", "UN" },
                    { "US", "USMCA" },
                    { "VA", "EU" },
                    { "VA", "EZ" },
                    { "ZW", "AF" },
                    { "ZW", "AU" },
                    { "ZW", "SSA" },
                    { "ZW", "UN" },
                });

            migrationBuilder.InsertData(
                table: "country_currencies",
                columns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                values: new object[,]
                {
                    { "AS", "USD" },
                    { "AX", "EUR" },
                    { "BL", "EUR" },
                    { "BM", "USD" },
                    { "BQ", "USD" },
                    { "GF", "EUR" },
                    { "GG", "GBP" },
                    { "GP", "EUR" },
                    { "GU", "USD" },
                    { "IM", "GBP" },
                    { "IO", "USD" },
                    { "JE", "GBP" },
                    { "KY", "USD" },
                    { "MF", "EUR" },
                    { "MP", "USD" },
                    { "MQ", "EUR" },
                    { "PM", "EUR" },
                    { "PR", "USD" },
                    { "RE", "EUR" },
                    { "TC", "USD" },
                    { "TF", "EUR" },
                    { "UM", "USD" },
                    { "VG", "USD" },
                    { "VI", "USD" },
                    { "YT", "EUR" },
                });

            migrationBuilder.InsertData(
                table: "country_geopolitical_entities",
                columns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                values: new object[,]
                {
                    { "AI", "CARIB" },
                    { "AI", "ECCU" },
                    { "AI", "NA" },
                    { "AS", "OC" },
                    { "AW", "CARIB" },
                    { "AW", "NA" },
                    { "AX", "EU" },
                    { "AX", "NC" },
                    { "AX", "NORD" },
                    { "BL", "CARIB" },
                    { "BL", "NA" },
                    { "BM", "NA" },
                    { "BQ", "CARIB" },
                    { "BQ", "NA" },
                    { "CW", "CARIB" },
                    { "CW", "NA" },
                    { "FK", "SA" },
                    { "GF", "LATAM" },
                    { "GF", "SA" },
                    { "GG", "EU" },
                    { "GI", "EU" },
                    { "GP", "CARIB" },
                    { "GP", "NA" },
                    { "GS", "SA" },
                    { "GU", "OC" },
                    { "IM", "EU" },
                    { "JE", "EU" },
                    { "KY", "CARIB" },
                    { "KY", "NA" },
                    { "MF", "CARIB" },
                    { "MF", "NA" },
                    { "MP", "OC" },
                    { "MQ", "CARIB" },
                    { "MQ", "NA" },
                    { "MS", "CARIB" },
                    { "MS", "CARICOM" },
                    { "MS", "ECCU" },
                    { "MS", "NA" },
                    { "NC", "OC" },
                    { "PF", "OC" },
                    { "PM", "NA" },
                    { "PN", "OC" },
                    { "PR", "CARIB" },
                    { "PR", "LATAM" },
                    { "PR", "NA" },
                    { "RE", "AF" },
                    { "SX", "CARIB" },
                    { "SX", "NA" },
                    { "TC", "CARIB" },
                    { "TC", "NA" },
                    { "UM", "OC" },
                    { "VG", "CARIB" },
                    { "VG", "NA" },
                    { "VI", "CARIB" },
                    { "VI", "NA" },
                    { "WF", "OC" },
                    { "YT", "AF" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BV");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SH");

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "AD", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "AL", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "AS", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "AT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "AX", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BA", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BB", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BG", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BL", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BM", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BQ", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BS", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "BZ", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "CA", "CAD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "CA", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "CH", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "CR", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "CY", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "CZ", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "DE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "DK", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "EC", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "EE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "ES", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "FI", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "FM", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "FR", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "GB", "GBP" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "GF", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "GG", "GBP" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "GP", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "GR", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "GT", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "GU", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "HN", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "HR", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "HU", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "IE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "IE", "GBP" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "IM", "GBP" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "IO", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "IT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "JE", "GBP" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "JM", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "JP", "JPY" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "KY", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "LI", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "LT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "LU", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "LV", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MA", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MC", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "ME", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MF", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MH", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MK", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MP", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MQ", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "MX", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "NI", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "NL", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "PA", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "PL", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "PM", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "PR", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "PT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "PW", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "RE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "RO", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "RS", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "SE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "SI", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "SK", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "SM", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "SV", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "TC", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "TF", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "TL", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "TR", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "UM", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "US", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "VA", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "VG", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "VI", "USD" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "YT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_currencies",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "currency_iso_4217_alpha_code" },
                keyValues: new object[] { "ZW", "USD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AD", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AD", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AD", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AD", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AD", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AD", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AE", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AE", "ARAB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AE", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AE", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AE", "GCC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AE", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AE", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AF", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AF", "SAARC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AF", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AG", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AG", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AG", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AG", "ECCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AG", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AI", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AI", "ECCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AI", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AL", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AL", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AL", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AL", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AL", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AL", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AM", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AM", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AM", "CSTO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AM", "EAEU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AM", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AO", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AO", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AO", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AO", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AQ", "AN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AR", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AR", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AR", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AR", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AS", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AT", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AT", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AT", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AT", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AT", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AT", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "ANZUS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "AUKUS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "FVEY" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "QUAD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AU", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AW", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AW", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AX", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AX", "NC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AX", "NORD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AZ", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AZ", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "AZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BA", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BA", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BA", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BB", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BB", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BB", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BB", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BB", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BD", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BD", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BD", "INDS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BD", "SAARC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BD", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "BENE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BF", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BF", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BF", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BF", "SAHEL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BF", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BF", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BF", "WAEMU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BH", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BH", "ARAB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BH", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BH", "GCC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BH", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BH", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BI", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BI", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BI", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BI", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BI", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BJ", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BJ", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BJ", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BJ", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BJ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BJ", "WAEMU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BL", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BL", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BM", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BN", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BN", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BN", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BN", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BN", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BN", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BN", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BO", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BO", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BO", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BQ", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BQ", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BR", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BR", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BR", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BR", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BR", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BS", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BS", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BS", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BS", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BS", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BT", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BT", "INDS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BT", "SAARC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BW", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BW", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BW", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BW", "SACU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BW", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BW", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BY", "CSTO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BY", "EAEU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BY", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BY", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BZ", "CAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BZ", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BZ", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BZ", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BZ", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "BZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "FVEY" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "G7" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CA", "USMCA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CC", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CD", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CD", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CD", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CD", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CD", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CF", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CF", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CF", "CEMAC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CF", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CF", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CF", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CG", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CG", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CG", "CEMAC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CG", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CG", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CG", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CH", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CH", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CH", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CH", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CH", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CI", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CI", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CI", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CI", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CI", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CI", "WAEMU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CK", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CL", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CL", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CL", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CL", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CL", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CL", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CM", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CM", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CM", "CEMAC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CM", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CM", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CM", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CN", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CN", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CN", "EAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CN", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CN", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CO", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CO", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CO", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CO", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CR", "CAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CR", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CR", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CR", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CU", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CU", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CU", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CU", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CV", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CV", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CV", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CV", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CV", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CW", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CW", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CX", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CY", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CY", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CY", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CY", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CY", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CY", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CY", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CY", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CZ", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CZ", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CZ", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CZ", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CZ", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CZ", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CZ", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "CZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "G7" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DJ", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DJ", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DJ", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DJ", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DJ", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DJ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "NC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "NORD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "SCAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DK", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DM", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DM", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DM", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DM", "ECCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DM", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DM", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DO", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DO", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DO", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DZ", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DZ", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DZ", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DZ", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DZ", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "DZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EC", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EC", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EC", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EC", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "BALT" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EG", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EG", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EG", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EG", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EG", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EG", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EH", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EH", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "EH", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ER", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ER", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ER", "SAHEL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ER", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ER", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ES", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ET", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ET", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ET", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ET", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ET", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "NC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "NORD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FI", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FJ", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FJ", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FJ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FK", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FM", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FO", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FO", "NC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FO", "NORD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "G7" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "FR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GA", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GA", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GA", "CEMAC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GA", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GA", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GA", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "AUKUS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "FVEY" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "G7" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GB", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GD", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GD", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GD", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GD", "ECCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GD", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GD", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GE", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GE", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GF", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GF", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GG", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GH", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GH", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GH", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GH", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GH", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GI", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GL", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GL", "NC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GL", "NORD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GM", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GM", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GM", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GM", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GN", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GN", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GN", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GN", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GP", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GP", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GQ", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GQ", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GQ", "CEMAC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GQ", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GQ", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GQ", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GQ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GS", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GT", "CAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GT", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GT", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GU", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GW", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GW", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GW", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GW", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GW", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GW", "WAEMU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GY", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GY", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GY", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GY", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GY", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "GY", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HK", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HK", "EAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HM", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HN", "CAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HN", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HN", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HT", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HT", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HT", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HT", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HT", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HU", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HU", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HU", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HU", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HU", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HU", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HU", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "HU", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ID", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ID", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ID", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ID", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ID", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ID", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ID", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IE", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IE", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IE", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IE", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IE", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IE", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IL", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IL", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IL", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IL", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IM", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IN", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IN", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IN", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IN", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IN", "INDS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IN", "QUAD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IN", "SAARC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IQ", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IQ", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IQ", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IQ", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IQ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IR", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IR", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IR", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IR", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IS", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IS", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IS", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IS", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IS", "NC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IS", "NORD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IS", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IS", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "G7" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "IT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JE", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JM", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JM", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JM", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JM", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JO", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JO", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JO", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "EAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "G7" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "QUAD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "JP", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KE", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KE", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KE", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KE", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KG", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KG", "CAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KG", "CSTO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KG", "EAEU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KH", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KH", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KH", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KH", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KH", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KH", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KI", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KI", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KI", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KM", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KM", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KM", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KM", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KM", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KN", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KN", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KN", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KN", "ECCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KN", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KP", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KP", "EAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KP", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KR", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KR", "EAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KR", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KR", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KR", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KW", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KW", "ARAB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KW", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KW", "GCC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KW", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KW", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KW", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KY", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KY", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KZ", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KZ", "CAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KZ", "CSTO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KZ", "EAEU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "KZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LA", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LA", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LA", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LA", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LA", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LB", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LB", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LB", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LB", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LB", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LC", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LC", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LC", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LC", "ECCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LC", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LC", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LI", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LI", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LI", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LI", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LK", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LK", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LK", "INDS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LK", "SAARC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LK", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LR", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LR", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LR", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LS", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LS", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LS", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LS", "SACU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LS", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LS", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "BALT" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "BENE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LU", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "BALT" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LV", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LY", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LY", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LY", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LY", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LY", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "LY", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MA", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MA", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MA", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MA", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MA", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MC", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MC", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MC", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MC", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MC", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MC", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MD", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MD", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MD", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ME", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ME", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ME", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ME", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ME", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ME", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ME", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MF", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MF", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MG", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MG", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MG", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MG", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MH", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MH", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MK", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MK", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MK", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MK", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MK", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MK", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ML", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ML", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ML", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ML", "SAHEL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ML", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ML", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ML", "WAEMU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MM", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MM", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MM", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MM", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MM", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MN", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MN", "EAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MO", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MO", "EAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MP", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MQ", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MQ", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MR", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MR", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MR", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MR", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MR", "SAHEL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MR", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MS", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MS", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MS", "ECCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MS", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MT", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MT", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MT", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MT", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MT", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MT", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MU", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MU", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MU", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MU", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MU", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MU", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MV", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MV", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MV", "INDS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MV", "SAARC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MV", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MW", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MW", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MW", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MW", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MW", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MX", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MX", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MX", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MX", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MX", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MX", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MX", "USMCA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MY", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MY", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MY", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MY", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MY", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MY", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MY", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MY", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MZ", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MZ", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MZ", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MZ", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "MZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NA", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NA", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NA", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NA", "SACU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NA", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NC", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NE", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NE", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NE", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NE", "SAHEL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NE", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NE", "WAEMU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NF", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NG", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NG", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NG", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NG", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NG", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NI", "CAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NI", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NI", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NI", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "BENE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NL", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "NC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "NORD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "SCAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NP", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NP", "INDS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NP", "SAARC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NP", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NR", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NR", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NU", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NZ", "ANZUS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NZ", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NZ", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NZ", "FVEY" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NZ", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NZ", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NZ", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "NZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "OM", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "OM", "ARAB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "OM", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "OM", "GCC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "OM", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "OM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PA", "CAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PA", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PA", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PE", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PE", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PE", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PE", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PF", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PG", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PG", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PH", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PH", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PH", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PH", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PH", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PH", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PK", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PK", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PK", "INDS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PK", "SAARC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PK", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PL", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PL", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PL", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PL", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PL", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PL", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PL", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PL", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PM", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PN", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PR", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PR", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PR", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PS", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PS", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PS", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PW", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PW", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PY", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PY", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PY", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "PY", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "QA", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "QA", "ARAB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "QA", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "QA", "GCC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "QA", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "QA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RE", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RS", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RS", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RS", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RS", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RU", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RU", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RU", "CSTO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RU", "EAEU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RU", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RU", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RW", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RW", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RW", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RW", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RW", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "RW", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "ARAB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "GCC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SB", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SB", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SB", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SC", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SC", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SC", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SC", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SC", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SC", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SD", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SD", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SD", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SD", "SAHEL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SD", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "NC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "NORD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "SCAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SG", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SG", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SG", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SG", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SG", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SG", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SG", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "BALK" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SI", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SJ", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "EEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "EUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SK", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SL", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SL", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SL", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SL", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SL", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SM", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SM", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SM", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SM", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SN", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SN", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SN", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SN", "SAHEL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SN", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SN", "WAEMU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SO", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SO", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SO", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SO", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SR", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SR", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SR", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SR", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SS", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SS", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SS", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SS", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ST", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ST", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ST", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ST", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ST", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SV", "CAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SV", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SV", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SV", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SX", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SX", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SY", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SY", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SY", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SY", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SZ", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SZ", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SZ", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SZ", "SACU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SZ", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "SZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TC", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TC", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TD", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TD", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TD", "CEMAC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TD", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TD", "SAHEL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TD", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TD", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TG", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TG", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TG", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TG", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TG", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TG", "WAEMU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TH", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TH", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TH", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TH", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TH", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TH", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TJ", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TJ", "CAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TJ", "CSTO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TJ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TK", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TL", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TL", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TL", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TM", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TM", "CAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TN", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TN", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TN", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TN", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TN", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TO", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TO", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TO", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TR", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TR", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TR", "EUCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TR", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TR", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TR", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TR", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TR", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TT", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TT", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TT", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TT", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TT", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TV", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TV", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TV", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TW", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TW", "EAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TZ", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TZ", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TZ", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TZ", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "TZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UA", "COE" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UA", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UG", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UG", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UG", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UG", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UG", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UM", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "ANZUS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "AUKUS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "FVEY" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "G7" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "NATO" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "OECD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "QUAD" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "US", "USMCA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UY", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UY", "MERCOSUR" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UY", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UY", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UZ", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UZ", "CAS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "UZ", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VA", "EU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VA", "EZ" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VC", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VC", "CARICOM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VC", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VC", "ECCU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VC", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VC", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VE", "LATAM" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VE", "OPEC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VE", "SA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VG", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VG", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VI", "CARIB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VI", "NA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VN", "AFTA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VN", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VN", "ASEAN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VN", "CPTPP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VN", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VN", "RCEP" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VN", "SEA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VN", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VU", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VU", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VU", "OIF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "VU", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "WF", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "WS", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "WS", "OC" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "WS", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "YE", "AL" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "YE", "ARAB" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "YE", "AS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "YE", "MENA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "YE", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "YT", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZA", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZA", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZA", "BRICS" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZA", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZA", "G20" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZA", "SACU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZA", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZA", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZM", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZM", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZM", "CW" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZM", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZM", "UN" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZW", "AF" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZW", "AU" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZW", "SSA" });

            migrationBuilder.DeleteData(
                table: "country_geopolitical_entities",
                keyColumns: new[] { "country_iso_3166_1_alpha_2_code", "geopolitical_entity_short_code" },
                keyValues: new object[] { "ZW", "UN" });

            migrationBuilder.DeleteData(
                table: "languages",
                keyColumn: "iso_639_1_code",
                keyValue: "de");

            migrationBuilder.DeleteData(
                table: "languages",
                keyColumn: "iso_639_1_code",
                keyValue: "en");

            migrationBuilder.DeleteData(
                table: "languages",
                keyColumn: "iso_639_1_code",
                keyValue: "es");

            migrationBuilder.DeleteData(
                table: "languages",
                keyColumn: "iso_639_1_code",
                keyValue: "fr");

            migrationBuilder.DeleteData(
                table: "languages",
                keyColumn: "iso_639_1_code",
                keyValue: "it");

            migrationBuilder.DeleteData(
                table: "languages",
                keyColumn: "iso_639_1_code",
                keyValue: "ja");

            migrationBuilder.DeleteData(
                table: "reference_data_version",
                keyColumn: "id",
                keyValue: 0);

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AD");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AQ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AX");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BB");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BD");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BH");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BJ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BQ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "BZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CC");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CD");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CH");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CV");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CX");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DJ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "EC");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "EE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "EG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "EH");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ER");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ES");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ET");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FJ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GD");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GH");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GP");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GQ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "HU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ID");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IQ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "IT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "JP");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KH");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KP");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "KZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LB");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LC");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LV");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "LY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MC");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MD");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ME");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MH");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ML");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MP");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MQ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MV");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MX");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "MZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NC");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NP");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "OM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PH");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "PY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "QA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "RE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "RO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "RS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "RU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "RW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SB");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SC");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SD");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SJ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ST");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SV");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SX");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "SZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TC");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TD");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TH");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TJ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TV");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TW");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "TZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UY");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "UZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VC");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VG");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "VU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "WF");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "WS");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "YE");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "YT");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZA");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZM");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "ZW");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "AF");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "AFTA");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "AL");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "AN");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "ANZUS");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "ARAB");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "AS");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "ASEAN");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "AU");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "AUKUS");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "BALK");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "BALT");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "BENE");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "BRICS");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "CAM");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "CARIB");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "CARICOM");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "CAS");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "CEMAC");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "COE");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "CPTPP");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "CSTO");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "CW");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "EAEU");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "EAS");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "ECCU");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "EEA");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "EU");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "EUCU");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "EUR");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "EZ");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "FVEY");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "G20");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "G7");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "GCC");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "INDS");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "LATAM");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "MENA");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "MERCOSUR");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "NA");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "NATO");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "NC");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "NORD");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "OC");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "OECD");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "OIF");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "OPEC");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "QUAD");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "RCEP");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "SA");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "SAARC");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "SACU");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "SAHEL");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "SCAN");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "SEA");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "SSA");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "UN");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "USMCA");

            migrationBuilder.DeleteData(
                table: "geopolitical_entities",
                keyColumn: "short_code",
                keyValue: "WAEMU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "AU");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "CN");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "DK");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FI");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "FR");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "GB");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NL");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NO");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "NZ");

            migrationBuilder.DeleteData(
                table: "countries",
                keyColumn: "iso_3166_1_alpha_2_code",
                keyValue: "US");

            migrationBuilder.DeleteData(
                table: "currencies",
                keyColumn: "iso_4217_alpha_code",
                keyValue: "CAD");

            migrationBuilder.DeleteData(
                table: "currencies",
                keyColumn: "iso_4217_alpha_code",
                keyValue: "JPY");

            migrationBuilder.DeleteData(
                table: "currencies",
                keyColumn: "iso_4217_alpha_code",
                keyValue: "EUR");

            migrationBuilder.DeleteData(
                table: "currencies",
                keyColumn: "iso_4217_alpha_code",
                keyValue: "GBP");

            migrationBuilder.DeleteData(
                table: "currencies",
                keyColumn: "iso_4217_alpha_code",
                keyValue: "USD");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "reference_data_version",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "phone_number_format",
                table: "countries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: string.Empty);
        }
    }
}
