// -----------------------------------------------------------------------
// <copyright file="CountryGeopoliticalEntitySeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding country-geopolitical entity relationships.
/// </summary>
public static class CountryGeopoliticalEntitySeeding
{
    /// <summary>
    /// Seeds the country_geopolitical_entities join table.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the country_geopolitical_entities join table.
        /// </summary>
        public void SeedCountryGeopoliticalEntities()
        {
            var entries = new List<object>();

            // =================================================================
            // Continents
            // =================================================================

            // Africa (AF)
            entries.AddRange(CreateEntries("AF", [
                "DZ", "AO", "BJ", "BW", "BF", "BI", "CV", "CM", "CF", "TD", "KM", "CG",
                "CD", "CI", "DJ", "EG", "GQ", "ER", "SZ", "ET", "GA", "GM", "GH", "GN",
                "GW", "KE", "LS", "LR", "LY", "MG", "MW", "ML", "MR", "MU", "YT", "MA",
                "MZ", "NA", "NE", "NG", "RE", "RW", "ST", "SN", "SC", "SL", "SO", "ZA",
                "SS", "SD", "TZ", "TG", "TN", "UG", "EH", "ZM", "ZW"]));

            // Antarctica (AN)
            entries.AddRange(CreateEntries("AN", ["AQ"]));

            // Asia (AS)
            entries.AddRange(CreateEntries("AS", [
                "AF", "AM", "AZ", "BH", "BD", "BT", "BN", "KH", "CN", "CY", "GE", "HK",
                "IN", "ID", "IR", "IQ", "IL", "JP", "JO", "KZ", "KW", "KG", "LA", "LB",
                "MO", "MY", "MV", "MN", "MM", "NP", "KP", "OM", "PK", "PS", "PH", "QA",
                "RU", "SA", "SG", "KR", "LK", "SY", "TW", "TJ", "TH", "TL", "TR", "TM",
                "AE", "UZ", "VN", "YE"]));

            // Europe (EU)
            entries.AddRange(CreateEntries("EU", [
                "AL", "AD", "AT", "AX", "BY", "BE", "BA", "BG", "HR", "CZ", "DK", "EE",
                "FO", "FI", "FR", "DE", "GI", "GR", "GG", "HU", "IS", "IE", "IM", "IT",
                "JE", "LV", "LI", "LT", "LU", "MT", "MD", "MC", "ME", "NL", "MK", "NO",
                "PL", "PT", "RO", "SM", "RS", "SK", "SI", "ES", "SJ", "SE", "CH", "UA",
                "GB", "VA", "XK"]));

            // North America (NA)
            entries.AddRange(CreateEntries("NA", [
                "AI", "AG", "AW", "BS", "BB", "BZ", "BM", "BQ", "VG", "CA", "KY", "CR",
                "CU", "CW", "DM", "DO", "SV", "GL", "GD", "GP", "GT", "HT", "HN", "JM",
                "MQ", "MX", "MS", "NI", "PA", "PR", "BL", "KN", "LC", "MF", "PM", "VC",
                "SX", "TT", "TC", "US", "VI"]));

            // Oceania (OC)
            entries.AddRange(CreateEntries("OC", [
                "AS", "AU", "CX", "CC", "CK", "FJ", "PF", "GU", "HM", "KI", "MH", "FM",
                "NR", "NC", "NZ", "NU", "NF", "MP", "PW", "PG", "PN", "WS", "SB", "TK",
                "TO", "TV", "UM", "VU", "WF"]));

            // South America (SA)
            entries.AddRange(CreateEntries("SA", [
                "AR", "BO", "BR", "CL", "CO", "EC", "FK", "GF", "GY", "PY", "PE", "GS",
                "SR", "UY", "VE"]));

            // =================================================================
            // Subcontinents
            // =================================================================

            // Arabian Peninsula (ARAB)
            entries.AddRange(CreateEntries("ARAB", [
                "BH", "KW", "OM", "QA", "SA", "AE", "YE"]));

            // Central America (CAM)
            entries.AddRange(CreateEntries("CAM", [
                "BZ", "CR", "SV", "GT", "HN", "NI", "PA"]));

            // Central Asia (CAS)
            entries.AddRange(CreateEntries("CAS", ["KZ", "KG", "TJ", "TM", "UZ"]));

            // East Asia (EAS)
            entries.AddRange(CreateEntries("EAS", [
                "CN", "HK", "JP", "KP", "KR", "MO", "MN", "TW"]));

            // Indian Subcontinent (INDS)
            entries.AddRange(CreateEntries("INDS", [
                "BD", "BT", "IN", "MV", "NP", "PK", "LK"]));

            // Scandinavia (SCAN)
            entries.AddRange(CreateEntries("SCAN", ["DK", "NO", "SE"]));

            // Southeast Asia (SEA)
            entries.AddRange(CreateEntries("SEA", [
                "BN", "KH", "ID", "LA", "MY", "MM", "PH", "SG", "TH", "TL", "VN"]));

            // =================================================================
            // Geopolitical Regions
            // =================================================================

            // Balkans (BALK)
            entries.AddRange(CreateEntries("BALK", [
                "AL", "BA", "BG", "HR", "ME", "MK", "RO", "RS", "SI", "XK"]));

            // Baltic States (BALT)
            entries.AddRange(CreateEntries("BALT", ["EE", "LV", "LT"]));

            // Benelux (BENE)
            entries.AddRange(CreateEntries("BENE", ["BE", "LU", "NL"]));

            // Caribbean (CARIB)
            entries.AddRange(CreateEntries("CARIB", [
                "AI", "AG", "AW", "BS", "BB", "BQ", "VG", "KY", "CU", "CW", "DM", "DO",
                "GD", "GP", "HT", "JM", "MQ", "MS", "PR", "BL", "KN", "LC", "MF", "VC",
                "SX", "TT", "TC", "VI"]));

            // Latin America (LATAM)
            entries.AddRange(CreateEntries("LATAM", [
                "AR", "BZ", "BO", "BR", "CL", "CO", "CR", "CU", "DO", "EC", "SV", "GF",
                "GT", "GY", "HT", "HN", "MX", "NI", "PA", "PY", "PE", "PR", "SR", "UY",
                "VE"]));

            // Middle East and North Africa (MENA)
            entries.AddRange(CreateEntries("MENA", [
                "DZ", "BH", "EG", "IR", "IQ", "IL", "JO", "KW", "LB", "LY", "MA", "OM",
                "PS", "QA", "SA", "SY", "TN", "TR", "AE", "EH", "YE"]));

            // Nordic Countries (NORD)
            entries.AddRange(CreateEntries("NORD", [
                "AX", "DK", "FO", "FI", "GL", "IS", "NO", "SE"]));

            // Sahel (SAHEL)
            entries.AddRange(CreateEntries("SAHEL", [
                "BF", "TD", "ER", "ML", "MR", "NE", "SN", "SD"]));

            // Sub-Saharan Africa (SSA)
            entries.AddRange(CreateEntries("SSA", [
                "AO", "BJ", "BW", "BF", "BI", "CV", "CM", "CF", "TD", "KM", "CG", "CD",
                "CI", "DJ", "GQ", "ER", "SZ", "ET", "GA", "GM", "GH", "GN", "GW", "KE",
                "LS", "LR", "MG", "MW", "ML", "MR", "MU", "MZ", "NA", "NE", "NG", "RW",
                "ST", "SN", "SC", "SL", "SO", "ZA", "SS", "TZ", "TG", "UG", "ZM", "ZW"]));

            // =================================================================
            // Free Trade Agreements
            // =================================================================

            // ASEAN Free Trade Area (AFTA)
            entries.AddRange(CreateEntries("AFTA", [
                "BN", "KH", "ID", "LA", "MY", "MM", "PH", "SG", "TH", "VN"]));

            // CPTPP
            entries.AddRange(CreateEntries("CPTPP", [
                "AU", "BN", "CA", "CL", "JP", "MY", "MX", "NZ", "PE", "SG", "VN"]));

            // RCEP
            entries.AddRange(CreateEntries("RCEP", [
                "AU", "BN", "KH", "CN", "ID", "JP", "KR", "LA", "MY", "MM", "NZ", "PH",
                "SG", "TH", "VN"]));

            // USMCA
            entries.AddRange(CreateEntries("USMCA", ["CA", "MX", "US"]));

            // =================================================================
            // Customs Unions
            // =================================================================

            // EU Customs Union (EUCU) - EU members + Turkey, Andorra, Monaco, San Marino
            entries.AddRange(CreateEntries("EUCU", [
                "AD", "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE",
                "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "MC", "NL", "PL", "PT",
                "RO", "SK", "SI", "SM", "ES", "SE", "TR"]));

            // Southern African Customs Union (SACU)
            entries.AddRange(CreateEntries("SACU", ["BW", "SZ", "LS", "NA", "ZA"]));

            // =================================================================
            // Common Markets
            // =================================================================

            // European Economic Area (EEA) - EU + EFTA (except CH)
            entries.AddRange(CreateEntries("EEA", [
                "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR",
                "HU", "IS", "IE", "IT", "LV", "LI", "LT", "LU", "MT", "NL", "NO", "PL",
                "PT", "RO", "SK", "SI", "ES", "SE"]));

            // Mercosur (full + associate)
            entries.AddRange(CreateEntries("MERCOSUR", [
                "AR", "BO", "BR", "CL", "CO", "EC", "GY", "PY", "PE", "SR", "UY"]));

            // =================================================================
            // Economic Unions
            // =================================================================

            // Eurasian Economic Union (EAEU)
            entries.AddRange(CreateEntries("EAEU", ["AM", "BY", "KZ", "KG", "RU"]));

            // =================================================================
            // Monetary Unions
            // =================================================================

            // Eurozone (EZ)
            entries.AddRange(CreateEntries("EZ", [
                "AD", "AT", "BE", "HR", "CY", "EE", "FI", "FR", "DE", "GR", "IE", "IT",
                "LV", "LT", "LU", "MT", "MC", "ME", "NL", "PT", "SM", "SK", "SI", "ES",
                "VA"]));

            // Eastern Caribbean Currency Union (ECCU)
            entries.AddRange(CreateEntries("ECCU", [
                "AI", "AG", "DM", "GD", "MS", "KN", "LC", "VC"]));

            // West African Economic and Monetary Union (WAEMU)
            entries.AddRange(CreateEntries("WAEMU", [
                "BJ", "BF", "CI", "GW", "ML", "NE", "SN", "TG"]));

            // Central African Economic and Monetary Community (CEMAC)
            entries.AddRange(CreateEntries("CEMAC", [
                "CM", "CF", "TD", "CG", "GQ", "GA"]));

            // =================================================================
            // Political Unions
            // =================================================================

            // European Union (EUR)
            entries.AddRange(CreateEntries("EUR", [
                "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR",
                "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK",
                "SI", "ES", "SE"]));

            // =================================================================
            // Governance and Cooperation Agreements
            // =================================================================

            // African Union (AU)
            entries.AddRange(CreateEntries("AU", [
                "DZ", "AO", "BJ", "BW", "BF", "BI", "CV", "CM", "CF", "TD", "KM", "CG",
                "CD", "CI", "DJ", "EG", "GQ", "ER", "SZ", "ET", "GA", "GM", "GH", "GN",
                "GW", "KE", "LS", "LR", "LY", "MG", "MW", "ML", "MR", "MU", "MA", "MZ",
                "NA", "NE", "NG", "RW", "ST", "SN", "SC", "SL", "SO", "ZA", "SS", "SD",
                "TZ", "TG", "TN", "UG", "EH", "ZM", "ZW"]));

            // Arab League (AL)
            entries.AddRange(CreateEntries("AL", [
                "DZ", "BH", "KM", "DJ", "EG", "IQ", "JO", "KW", "LB", "LY", "MR", "MA",
                "OM", "PS", "QA", "SA", "SO", "SD", "SY", "TN", "AE", "YE"]));

            // Association of Southeast Asian Nations (ASEAN)
            entries.AddRange(CreateEntries("ASEAN", [
                "BN", "KH", "ID", "LA", "MY", "MM", "PH", "SG", "TH", "VN"]));

            // BRICS (expanded 2024)
            entries.AddRange(CreateEntries("BRICS", [
                "BR", "CN", "EG", "ET", "IN", "IR", "RU", "SA", "AE", "ZA"]));

            // Caribbean Community (CARICOM)
            entries.AddRange(CreateEntries("CARICOM", [
                "AG", "BS", "BB", "BZ", "DM", "GD", "GY", "HT", "JM", "MS", "KN", "LC",
                "SR", "TT", "VC"]));

            // Council of Europe (COE)
            entries.AddRange(CreateEntries("COE", [
                "AL", "AD", "AM", "AT", "AZ", "BE", "BA", "BG", "HR", "CY", "CZ", "DK",
                "EE", "FI", "FR", "GE", "DE", "GR", "HU", "IS", "IE", "IT", "LV", "LI",
                "LT", "LU", "MT", "MD", "MC", "ME", "NL", "MK", "NO", "PL", "PT", "RO",
                "SM", "RS", "SK", "SI", "ES", "SE", "CH", "TR", "UA", "GB"]));

            // Commonwealth of Nations (CW)
            entries.AddRange(CreateEntries("CW", [
                "AG", "AU", "BS", "BD", "BB", "BZ", "BW", "BN", "CM", "CA", "CY", "DM",
                "SZ", "FJ", "GM", "GH", "GD", "GY", "IN", "JM", "KE", "KI", "LS", "MW",
                "MY", "MV", "MT", "MU", "MZ", "NA", "NR", "NZ", "NG", "PK", "PG", "RW",
                "KN", "LC", "VC", "WS", "SC", "SL", "SG", "SB", "ZA", "LK", "TZ", "TG",
                "TO", "TT", "TV", "UG", "GB", "VU", "ZM"]));

            // Group of Seven (G7)
            entries.AddRange(CreateEntries("G7", [
                "CA", "FR", "DE", "IT", "JP", "GB", "US"]));

            // Group of Twenty (G20)
            entries.AddRange(CreateEntries("G20", [
                "AR", "AU", "BR", "CA", "CN", "FR", "DE", "IN", "ID", "IT", "JP", "KR",
                "MX", "RU", "SA", "ZA", "TR", "GB", "US"]));

            // Gulf Cooperation Council (GCC)
            entries.AddRange(CreateEntries("GCC", [
                "BH", "KW", "OM", "QA", "SA", "AE"]));

            // Nordic Council (NC)
            entries.AddRange(CreateEntries("NC", [
                "AX", "DK", "FO", "FI", "GL", "IS", "NO", "SE"]));

            // OECD
            entries.AddRange(CreateEntries("OECD", [
                "AU", "AT", "BE", "CA", "CL", "CO", "CR", "CZ", "DK", "EE", "FI", "FR",
                "DE", "GR", "HU", "IS", "IE", "IL", "IT", "JP", "KR", "LV", "LT", "LU",
                "MX", "NL", "NZ", "NO", "PL", "PT", "SK", "SI", "ES", "SE", "CH", "TR",
                "GB", "US"]));

            // Organisation internationale de la Francophonie (OIF) - major members
            entries.AddRange(CreateEntries("OIF", [
                "AL", "AD", "AM", "BE", "BJ", "BG", "BF", "BI", "CV", "CM", "CA", "CF",
                "TD", "KM", "CG", "CD", "CI", "DJ", "DM", "EG", "GQ", "FR", "GA", "GR",
                "GN", "GW", "HT", "LB", "LU", "MK", "MG", "ML", "MA", "MU", "MR", "MC",
                "ME", "NE", "RO", "RW", "ST", "SN", "SC", "CH", "TG", "TN", "VN", "VU"]));

            // Organization of the Petroleum Exporting Countries (OPEC)
            entries.AddRange(CreateEntries("OPEC", [
                "DZ", "AO", "CG", "GQ", "GA", "IR", "IQ", "KW", "LY", "NG", "SA", "AE",
                "VE"]));

            // South Asian Association for Regional Cooperation (SAARC)
            entries.AddRange(CreateEntries("SAARC", [
                "AF", "BD", "BT", "IN", "MV", "NP", "PK", "LK"]));

            // United Nations (UN) - all 193 member states
            entries.AddRange(CreateEntries("UN", [
                "AF", "AL", "DZ", "AD", "AO", "AG", "AR", "AM", "AU", "AT", "AZ", "BS",
                "BH", "BD", "BB", "BY", "BE", "BZ", "BJ", "BT", "BO", "BA", "BW", "BR",
                "BN", "BG", "BF", "BI", "CV", "KH", "CM", "CA", "CF", "TD", "CL", "CN",
                "CO", "KM", "CG", "CD", "CR", "CI", "HR", "CU", "CY", "CZ", "DK", "DJ",
                "DM", "DO", "EC", "EG", "SV", "GQ", "ER", "EE", "SZ", "ET", "FJ", "FI",
                "FR", "GA", "GM", "GE", "DE", "GH", "GR", "GD", "GT", "GN", "GW", "GY",
                "HT", "HN", "HU", "IS", "IN", "ID", "IR", "IQ", "IE", "IL", "IT", "JM",
                "JP", "JO", "KZ", "KE", "KI", "KP", "KR", "KW", "KG", "LA", "LV", "LB",
                "LS", "LR", "LY", "LI", "LT", "LU", "MG", "MW", "MY", "MV", "ML", "MT",
                "MH", "MR", "MU", "MX", "FM", "MD", "MC", "MN", "ME", "MA", "MZ", "MM",
                "NA", "NR", "NP", "NL", "NZ", "NI", "NE", "NG", "MK", "NO", "OM", "PK",
                "PW", "PA", "PG", "PY", "PE", "PH", "PL", "PT", "QA", "RO", "RU", "RW",
                "KN", "LC", "VC", "WS", "SM", "ST", "SA", "SN", "RS", "SC", "SL", "SG",
                "SK", "SI", "SB", "SO", "ZA", "SS", "ES", "LK", "SD", "SR", "SE", "CH",
                "SY", "TJ", "TZ", "TH", "TL", "TG", "TO", "TT", "TN", "TR", "TM", "TV",
                "UG", "UA", "AE", "GB", "US", "UY", "UZ", "VU", "VE", "VN", "YE", "ZM",
                "ZW"]));

            // =================================================================
            // Military Alliances
            // =================================================================

            // ANZUS
            entries.AddRange(CreateEntries("ANZUS", ["AU", "NZ", "US"]));

            // AUKUS
            entries.AddRange(CreateEntries("AUKUS", ["AU", "GB", "US"]));

            // Collective Security Treaty Organization (CSTO)
            entries.AddRange(CreateEntries("CSTO", [
                "AM", "BY", "KZ", "KG", "RU", "TJ"]));

            // Five Eyes (FVEY)
            entries.AddRange(CreateEntries("FVEY", ["AU", "CA", "NZ", "GB", "US"]));

            // NATO
            entries.AddRange(CreateEntries("NATO", [
                "AL", "BE", "BG", "CA", "HR", "CZ", "DK", "EE", "FI", "FR", "DE", "GR",
                "HU", "IS", "IT", "LV", "LT", "LU", "ME", "NL", "MK", "NO", "PL", "PT",
                "RO", "SK", "SI", "ES", "SE", "TR", "GB", "US"]));

            // Quadrilateral Security Dialogue (QUAD)
            entries.AddRange(CreateEntries("QUAD", ["AU", "IN", "JP", "US"]));

            modelBuilder.Entity("country_geopolitical_entities").HasData(entries.ToArray());
        }

        private static object[] CreateEntries(string geoPolCode, string[] countryCodes)
        {
            return countryCodes.Select(object (c) => new
            {
                country_iso_3166_1_alpha_2_code = c,
                geopolitical_entity_short_code = geoPolCode,
            }).ToArray();
        }
    }
}
