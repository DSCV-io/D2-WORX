// -----------------------------------------------------------------------
// <copyright file="20251124101805_SeedSubdivsInit.cs" company="DCSV">
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
    public partial class SeedSubdivsInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "reference_data_version",
                keyColumn: "id",
                keyValue: 0,
                columns: new[] { "updated_at", "version" },
                values: new object[] { new DateTime(2025, 11, 24, 10, 0, 0, 0, DateTimeKind.Utc), "1.2.0" });

            migrationBuilder.InsertData(
                table: "subdivisions",
                columns: new[] { "iso_3166_2_code", "country_iso_3166_1_alpha_2_code", "display_name", "official_name", "short_code" },
                values: new object[,]
                {
                    { "CA-AB", "CA", "Alberta", "Province of Alberta", "AB" },
                    { "CA-BC", "CA", "British Columbia", "Province of British Columbia", "BC" },
                    { "CA-MB", "CA", "Manitoba", "Province of Manitoba", "MB" },
                    { "CA-NB", "CA", "New Brunswick", "Province of New Brunswick", "NB" },
                    { "CA-NL", "CA", "Newfoundland and Labrador", "Province of Newfoundland and Labrador", "NL" },
                    { "CA-NS", "CA", "Nova Scotia", "Province of Nova Scotia", "NS" },
                    { "CA-NT", "CA", "Northwest Territories", "Northwest Territories", "NT" },
                    { "CA-NU", "CA", "Nunavut", "Nunavut", "NU" },
                    { "CA-ON", "CA", "Ontario", "Province of Ontario", "ON" },
                    { "CA-PE", "CA", "Prince Edward Island", "Province of Prince Edward Island", "PE" },
                    { "CA-QC", "CA", "Quebec", "Province of Quebec", "QC" },
                    { "CA-SK", "CA", "Saskatchewan", "Province of Saskatchewan", "SK" },
                    { "CA-YT", "CA", "Yukon", "Yukon", "YT" },
                    { "DE-BB", "DE", "Brandenburg", "Land Brandenburg", "BB" },
                    { "DE-BE", "DE", "Berlin", "Land Berlin", "BE" },
                    { "DE-BW", "DE", "Baden-Württemberg", "Land Baden-Württemberg", "BW" },
                    { "DE-BY", "DE", "Bavaria", "Freistaat Bayern", "BY" },
                    { "DE-HB", "DE", "Bremen", "Freie Hansestadt Bremen", "HB" },
                    { "DE-HE", "DE", "Hesse", "Land Hessen", "HE" },
                    { "DE-HH", "DE", "Hamburg", "Freie und Hansestadt Hamburg", "HH" },
                    { "DE-MV", "DE", "Mecklenburg-Vorpommern", "Land Mecklenburg-Vorpommern", "MV" },
                    { "DE-NI", "DE", "Lower Saxony", "Land Niedersachsen", "NI" },
                    { "DE-NW", "DE", "North Rhine-Westphalia", "Land Nordrhein-Westfalen", "NW" },
                    { "DE-RP", "DE", "Rhineland-Palatinate", "Land Rheinland-Pfalz", "RP" },
                    { "DE-SH", "DE", "Schleswig-Holstein", "Land Schleswig-Holstein", "SH" },
                    { "DE-SL", "DE", "Saarland", "Land Saarland", "SL" },
                    { "DE-SN", "DE", "Saxony", "Freistaat Sachsen", "SN" },
                    { "DE-ST", "DE", "Saxony-Anhalt", "Land Sachsen-Anhalt", "ST" },
                    { "DE-TH", "DE", "Thuringia", "Freistaat Thüringen", "TH" },
                    { "ES-AN", "ES", "Andalusia", "Comunidad Autónoma de Andalucía", "AN" },
                    { "ES-AR", "ES", "Aragon", "Comunidad Autónoma de Aragón", "AR" },
                    { "ES-AS", "ES", "Asturias", "Principado de Asturias", "AS" },
                    { "ES-CB", "ES", "Cantabria", "Comunidad Autónoma de Cantabria", "CB" },
                    { "ES-CE", "ES", "Ceuta", "Ciudad Autónoma de Ceuta", "CE" },
                    { "ES-CL", "ES", "Castile and León", "Comunidad Autónoma de Castilla y León", "CL" },
                    { "ES-CM", "ES", "Castilla-La Mancha", "Comunidad Autónoma de Castilla-La Mancha", "CM" },
                    { "ES-CN", "ES", "Canary Islands", "Comunidad Autónoma de Canarias", "CN" },
                    { "ES-CT", "ES", "Catalonia", "Comunidad Autónoma de Cataluña", "CT" },
                    { "ES-EX", "ES", "Extremadura", "Comunidad Autónoma de Extremadura", "EX" },
                    { "ES-GA", "ES", "Galicia", "Comunidad Autónoma de Galicia", "GA" },
                    { "ES-IB", "ES", "Balearic Islands", "Comunidad Autónoma de las Illes Balears", "IB" },
                    { "ES-MC", "ES", "Murcia", "Comunidad Autónoma de la Región de Murcia", "MC" },
                    { "ES-MD", "ES", "Madrid", "Comunidad de Madrid", "MD" },
                    { "ES-ML", "ES", "Melilla", "Ciudad Autónoma de Melilla", "ML" },
                    { "ES-NC", "ES", "Navarre", "Comunidad Foral de Navarra", "NC" },
                    { "ES-PV", "ES", "Basque Country", "Comunidad Autónoma del País Vasco", "PV" },
                    { "ES-RI", "ES", "La Rioja", "Comunidad Autónoma de La Rioja", "RI" },
                    { "ES-VC", "ES", "Valencia", "Comunidad Valenciana", "VC" },
                    { "FR-ARA", "FR", "Auvergne-Rhône-Alpes", "Région Auvergne-Rhône-Alpes", "ARA" },
                    { "FR-BFC", "FR", "Bourgogne-Franche-Comté", "Région Bourgogne-Franche-Comté", "BFC" },
                    { "FR-BRE", "FR", "Brittany", "Région Bretagne", "BRE" },
                    { "FR-COR", "FR", "Corsica", "Collectivité de Corse", "COR" },
                    { "FR-CVL", "FR", "Centre-Val de Loire", "Région Centre-Val de Loire", "CVL" },
                    { "FR-GES", "FR", "Grand Est", "Région Grand Est", "GES" },
                    { "FR-HDF", "FR", "Hauts-de-France", "Région Hauts-de-France", "HDF" },
                    { "FR-IDF", "FR", "Île-de-France", "Région Île-de-France", "IDF" },
                    { "FR-NAQ", "FR", "Nouvelle-Aquitaine", "Région Nouvelle-Aquitaine", "NAQ" },
                    { "FR-NOR", "FR", "Normandy", "Région Normandie", "NOR" },
                    { "FR-OCC", "FR", "Occitania", "Région Occitanie", "OCC" },
                    { "FR-PAC", "FR", "Provence-Alpes-Côte d'Azur", "Région Provence-Alpes-Côte d'Azur", "PAC" },
                    { "FR-PDL", "FR", "Pays de la Loire", "Région Pays de la Loire", "PDL" },
                    { "GB-ENG", "GB", "England", "England", "ENG" },
                    { "GB-NIR", "GB", "Northern Ireland", "Northern Ireland", "NIR" },
                    { "GB-SCT", "GB", "Scotland", "Scotland", "SCT" },
                    { "GB-WLS", "GB", "Wales", "Wales", "WLS" },
                    { "IT-21", "IT", "Piedmont", "Regione Piemonte", "21" },
                    { "IT-23", "IT", "Aosta Valley", "Regione Autonoma Valle d'Aosta", "23" },
                    { "IT-25", "IT", "Lombardy", "Regione Lombardia", "25" },
                    { "IT-32", "IT", "Trentino-South Tyrol", "Regione Autonoma Trentino-Alto Adige", "32" },
                    { "IT-34", "IT", "Veneto", "Regione del Veneto", "34" },
                    { "IT-36", "IT", "Friuli-Venezia Giulia", "Regione Autonoma Friuli-Venezia Giulia", "36" },
                    { "IT-42", "IT", "Liguria", "Regione Liguria", "42" },
                    { "IT-45", "IT", "Emilia-Romagna", "Regione Emilia-Romagna", "45" },
                    { "IT-52", "IT", "Tuscany", "Regione Toscana", "52" },
                    { "IT-55", "IT", "Umbria", "Regione Umbria", "55" },
                    { "IT-57", "IT", "Marche", "Regione Marche", "57" },
                    { "IT-62", "IT", "Lazio", "Regione Lazio", "62" },
                    { "IT-65", "IT", "Abruzzo", "Regione Abruzzo", "65" },
                    { "IT-67", "IT", "Molise", "Regione Molise", "67" },
                    { "IT-72", "IT", "Campania", "Regione Campania", "72" },
                    { "IT-75", "IT", "Apulia", "Regione Puglia", "75" },
                    { "IT-77", "IT", "Basilicata", "Regione Basilicata", "77" },
                    { "IT-78", "IT", "Calabria", "Regione Calabria", "78" },
                    { "IT-82", "IT", "Sicily", "Regione Siciliana", "82" },
                    { "IT-88", "IT", "Sardinia", "Regione Autonoma della Sardegna", "88" },
                    { "JP-01", "JP", "Hokkaido", "Hokkaidō", "01" },
                    { "JP-02", "JP", "Aomori", "Aomori-ken", "02" },
                    { "JP-03", "JP", "Iwate", "Iwate-ken", "03" },
                    { "JP-04", "JP", "Miyagi", "Miyagi-ken", "04" },
                    { "JP-05", "JP", "Akita", "Akita-ken", "05" },
                    { "JP-06", "JP", "Yamagata", "Yamagata-ken", "06" },
                    { "JP-07", "JP", "Fukushima", "Fukushima-ken", "07" },
                    { "JP-08", "JP", "Ibaraki", "Ibaraki-ken", "08" },
                    { "JP-09", "JP", "Tochigi", "Tochigi-ken", "09" },
                    { "JP-10", "JP", "Gunma", "Gunma-ken", "10" },
                    { "JP-11", "JP", "Saitama", "Saitama-ken", "11" },
                    { "JP-12", "JP", "Chiba", "Chiba-ken", "12" },
                    { "JP-13", "JP", "Tokyo", "Tōkyō-to", "13" },
                    { "JP-14", "JP", "Kanagawa", "Kanagawa-ken", "14" },
                    { "JP-15", "JP", "Niigata", "Niigata-ken", "15" },
                    { "JP-16", "JP", "Toyama", "Toyama-ken", "16" },
                    { "JP-17", "JP", "Ishikawa", "Ishikawa-ken", "17" },
                    { "JP-18", "JP", "Fukui", "Fukui-ken", "18" },
                    { "JP-19", "JP", "Yamanashi", "Yamanashi-ken", "19" },
                    { "JP-20", "JP", "Nagano", "Nagano-ken", "20" },
                    { "JP-21", "JP", "Gifu", "Gifu-ken", "21" },
                    { "JP-22", "JP", "Shizuoka", "Shizuoka-ken", "22" },
                    { "JP-23", "JP", "Aichi", "Aichi-ken", "23" },
                    { "JP-24", "JP", "Mie", "Mie-ken", "24" },
                    { "JP-25", "JP", "Shiga", "Shiga-ken", "25" },
                    { "JP-26", "JP", "Kyoto", "Kyōto-fu", "26" },
                    { "JP-27", "JP", "Osaka", "Ōsaka-fu", "27" },
                    { "JP-28", "JP", "Hyogo", "Hyōgo-ken", "28" },
                    { "JP-29", "JP", "Nara", "Nara-ken", "29" },
                    { "JP-30", "JP", "Wakayama", "Wakayama-ken", "30" },
                    { "JP-31", "JP", "Tottori", "Tottori-ken", "31" },
                    { "JP-32", "JP", "Shimane", "Shimane-ken", "32" },
                    { "JP-33", "JP", "Okayama", "Okayama-ken", "33" },
                    { "JP-34", "JP", "Hiroshima", "Hiroshima-ken", "34" },
                    { "JP-35", "JP", "Yamaguchi", "Yamaguchi-ken", "35" },
                    { "JP-36", "JP", "Tokushima", "Tokushima-ken", "36" },
                    { "JP-37", "JP", "Kagawa", "Kagawa-ken", "37" },
                    { "JP-38", "JP", "Ehime", "Ehime-ken", "38" },
                    { "JP-39", "JP", "Kochi", "Kōchi-ken", "39" },
                    { "JP-40", "JP", "Fukuoka", "Fukuoka-ken", "40" },
                    { "JP-41", "JP", "Saga", "Saga-ken", "41" },
                    { "JP-42", "JP", "Nagasaki", "Nagasaki-ken", "42" },
                    { "JP-43", "JP", "Kumamoto", "Kumamoto-ken", "43" },
                    { "JP-44", "JP", "Oita", "Ōita-ken", "44" },
                    { "JP-45", "JP", "Miyazaki", "Miyazaki-ken", "45" },
                    { "JP-46", "JP", "Kagoshima", "Kagoshima-ken", "46" },
                    { "JP-47", "JP", "Okinawa", "Okinawa-ken", "47" },
                    { "US-AK", "US", "Alaska", "State of Alaska", "AK" },
                    { "US-AL", "US", "Alabama", "State of Alabama", "AL" },
                    { "US-AR", "US", "Arkansas", "State of Arkansas", "AR" },
                    { "US-AZ", "US", "Arizona", "State of Arizona", "AZ" },
                    { "US-CA", "US", "California", "State of California", "CA" },
                    { "US-CO", "US", "Colorado", "State of Colorado", "CO" },
                    { "US-CT", "US", "Connecticut", "State of Connecticut", "CT" },
                    { "US-DC", "US", "District of Columbia", "District of Columbia", "DC" },
                    { "US-DE", "US", "Delaware", "State of Delaware", "DE" },
                    { "US-FL", "US", "Florida", "State of Florida", "FL" },
                    { "US-GA", "US", "Georgia", "State of Georgia", "GA" },
                    { "US-HI", "US", "Hawaii", "State of Hawaii", "HI" },
                    { "US-IA", "US", "Iowa", "State of Iowa", "IA" },
                    { "US-ID", "US", "Idaho", "State of Idaho", "ID" },
                    { "US-IL", "US", "Illinois", "State of Illinois", "IL" },
                    { "US-IN", "US", "Indiana", "State of Indiana", "IN" },
                    { "US-KS", "US", "Kansas", "State of Kansas", "KS" },
                    { "US-KY", "US", "Kentucky", "Commonwealth of Kentucky", "KY" },
                    { "US-LA", "US", "Louisiana", "State of Louisiana", "LA" },
                    { "US-MA", "US", "Massachusetts", "Commonwealth of Massachusetts", "MA" },
                    { "US-MD", "US", "Maryland", "State of Maryland", "MD" },
                    { "US-ME", "US", "Maine", "State of Maine", "ME" },
                    { "US-MI", "US", "Michigan", "State of Michigan", "MI" },
                    { "US-MN", "US", "Minnesota", "State of Minnesota", "MN" },
                    { "US-MO", "US", "Missouri", "State of Missouri", "MO" },
                    { "US-MS", "US", "Mississippi", "State of Mississippi", "MS" },
                    { "US-MT", "US", "Montana", "State of Montana", "MT" },
                    { "US-NC", "US", "North Carolina", "State of North Carolina", "NC" },
                    { "US-ND", "US", "North Dakota", "State of North Dakota", "ND" },
                    { "US-NE", "US", "Nebraska", "State of Nebraska", "NE" },
                    { "US-NH", "US", "New Hampshire", "State of New Hampshire", "NH" },
                    { "US-NJ", "US", "New Jersey", "State of New Jersey", "NJ" },
                    { "US-NM", "US", "New Mexico", "State of New Mexico", "NM" },
                    { "US-NV", "US", "Nevada", "State of Nevada", "NV" },
                    { "US-NY", "US", "New York", "State of New York", "NY" },
                    { "US-OH", "US", "Ohio", "State of Ohio", "OH" },
                    { "US-OK", "US", "Oklahoma", "State of Oklahoma", "OK" },
                    { "US-OR", "US", "Oregon", "State of Oregon", "OR" },
                    { "US-PA", "US", "Pennsylvania", "Commonwealth of Pennsylvania", "PA" },
                    { "US-RI", "US", "Rhode Island", "State of Rhode Island", "RI" },
                    { "US-SC", "US", "South Carolina", "State of South Carolina", "SC" },
                    { "US-SD", "US", "South Dakota", "State of South Dakota", "SD" },
                    { "US-TN", "US", "Tennessee", "State of Tennessee", "TN" },
                    { "US-TX", "US", "Texas", "State of Texas", "TX" },
                    { "US-UT", "US", "Utah", "State of Utah", "UT" },
                    { "US-VA", "US", "Virginia", "Commonwealth of Virginia", "VA" },
                    { "US-VT", "US", "Vermont", "State of Vermont", "VT" },
                    { "US-WA", "US", "Washington", "State of Washington", "WA" },
                    { "US-WI", "US", "Wisconsin", "State of Wisconsin", "WI" },
                    { "US-WV", "US", "West Virginia", "State of West Virginia", "WV" },
                    { "US-WY", "US", "Wyoming", "State of Wyoming", "WY" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-AB");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-BC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-MB");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-NB");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-NL");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-NS");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-NT");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-NU");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-ON");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-PE");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-QC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-SK");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "CA-YT");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-BB");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-BE");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-BW");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-BY");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-HB");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-HE");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-HH");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-MV");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-NI");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-NW");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-RP");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-SH");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-SL");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-SN");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-ST");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "DE-TH");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-AN");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-AR");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-AS");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-CB");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-CE");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-CL");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-CM");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-CN");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-CT");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-EX");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-GA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-IB");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-MC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-MD");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-ML");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-NC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-PV");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-RI");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "ES-VC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-ARA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-BFC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-BRE");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-COR");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-CVL");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-GES");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-HDF");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-IDF");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-NAQ");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-NOR");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-OCC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-PAC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "FR-PDL");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "GB-ENG");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "GB-NIR");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "GB-SCT");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "GB-WLS");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-21");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-23");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-25");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-32");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-34");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-36");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-42");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-45");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-52");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-55");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-57");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-62");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-65");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-67");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-72");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-75");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-77");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-78");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-82");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "IT-88");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-01");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-02");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-03");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-04");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-05");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-06");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-07");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-08");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-09");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-10");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-11");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-12");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-13");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-14");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-15");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-16");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-17");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-18");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-19");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-20");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-21");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-22");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-23");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-24");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-25");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-26");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-27");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-28");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-29");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-30");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-31");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-32");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-33");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-34");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-35");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-36");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-37");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-38");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-39");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-40");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-41");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-42");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-43");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-44");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-45");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-46");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "JP-47");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-AK");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-AL");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-AR");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-AZ");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-CA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-CO");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-CT");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-DC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-DE");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-FL");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-GA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-HI");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-IA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-ID");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-IL");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-IN");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-KS");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-KY");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-LA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-MA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-MD");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-ME");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-MI");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-MN");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-MO");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-MS");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-MT");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-NC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-ND");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-NE");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-NH");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-NJ");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-NM");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-NV");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-NY");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-OH");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-OK");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-OR");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-PA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-RI");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-SC");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-SD");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-TN");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-TX");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-UT");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-VA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-VT");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-WA");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-WI");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-WV");

            migrationBuilder.DeleteData(
                table: "subdivisions",
                keyColumn: "iso_3166_2_code",
                keyValue: "US-WY");

            migrationBuilder.UpdateData(
                table: "reference_data_version",
                keyColumn: "id",
                keyValue: 0,
                columns: new[] { "updated_at", "version" },
                values: new object[] { new DateTime(2025, 11, 24, 9, 52, 0, 0, DateTimeKind.Utc), "1.1.0" });
        }
    }
}
