// -----------------------------------------------------------------------
// <copyright file="SubdivisionSeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding subdivision data.
/// </summary>
public static class SubdivisionSeeding
{
    /// <summary>
    /// Seeds the Subdivision entity.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the Subdivision entity.
        /// </summary>
        public void SeedSubdivisions()
        {
            modelBuilder.Entity<Subdivision>().HasData([

                // =============================================================
                // United States (US) - 50 States + DC
                // =============================================================
                new Subdivision
                {
                    ISO31662Code = "US-AL",
                    ShortCode = "AL",
                    DisplayName = "Alabama",
                    OfficialName = "State of Alabama",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-AK",
                    ShortCode = "AK",
                    DisplayName = "Alaska",
                    OfficialName = "State of Alaska",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-AZ",
                    ShortCode = "AZ",
                    DisplayName = "Arizona",
                    OfficialName = "State of Arizona",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-AR",
                    ShortCode = "AR",
                    DisplayName = "Arkansas",
                    OfficialName = "State of Arkansas",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-CA",
                    ShortCode = "CA",
                    DisplayName = "California",
                    OfficialName = "State of California",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-CO",
                    ShortCode = "CO",
                    DisplayName = "Colorado",
                    OfficialName = "State of Colorado",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-CT",
                    ShortCode = "CT",
                    DisplayName = "Connecticut",
                    OfficialName = "State of Connecticut",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-DE",
                    ShortCode = "DE",
                    DisplayName = "Delaware",
                    OfficialName = "State of Delaware",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-DC",
                    ShortCode = "DC",
                    DisplayName = "District of Columbia",
                    OfficialName = "District of Columbia",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-FL",
                    ShortCode = "FL",
                    DisplayName = "Florida",
                    OfficialName = "State of Florida",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-GA",
                    ShortCode = "GA",
                    DisplayName = "Georgia",
                    OfficialName = "State of Georgia",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-HI",
                    ShortCode = "HI",
                    DisplayName = "Hawaii",
                    OfficialName = "State of Hawaii",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-ID",
                    ShortCode = "ID",
                    DisplayName = "Idaho",
                    OfficialName = "State of Idaho",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-IL",
                    ShortCode = "IL",
                    DisplayName = "Illinois",
                    OfficialName = "State of Illinois",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-IN",
                    ShortCode = "IN",
                    DisplayName = "Indiana",
                    OfficialName = "State of Indiana",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-IA",
                    ShortCode = "IA",
                    DisplayName = "Iowa",
                    OfficialName = "State of Iowa",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-KS",
                    ShortCode = "KS",
                    DisplayName = "Kansas",
                    OfficialName = "State of Kansas",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-KY",
                    ShortCode = "KY",
                    DisplayName = "Kentucky",
                    OfficialName = "Commonwealth of Kentucky",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-LA",
                    ShortCode = "LA",
                    DisplayName = "Louisiana",
                    OfficialName = "State of Louisiana",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-ME",
                    ShortCode = "ME",
                    DisplayName = "Maine",
                    OfficialName = "State of Maine",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-MD",
                    ShortCode = "MD",
                    DisplayName = "Maryland",
                    OfficialName = "State of Maryland",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-MA",
                    ShortCode = "MA",
                    DisplayName = "Massachusetts",
                    OfficialName = "Commonwealth of Massachusetts",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-MI",
                    ShortCode = "MI",
                    DisplayName = "Michigan",
                    OfficialName = "State of Michigan",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-MN",
                    ShortCode = "MN",
                    DisplayName = "Minnesota",
                    OfficialName = "State of Minnesota",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-MS",
                    ShortCode = "MS",
                    DisplayName = "Mississippi",
                    OfficialName = "State of Mississippi",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-MO",
                    ShortCode = "MO",
                    DisplayName = "Missouri",
                    OfficialName = "State of Missouri",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-MT",
                    ShortCode = "MT",
                    DisplayName = "Montana",
                    OfficialName = "State of Montana",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-NE",
                    ShortCode = "NE",
                    DisplayName = "Nebraska",
                    OfficialName = "State of Nebraska",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-NV",
                    ShortCode = "NV",
                    DisplayName = "Nevada",
                    OfficialName = "State of Nevada",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-NH",
                    ShortCode = "NH",
                    DisplayName = "New Hampshire",
                    OfficialName = "State of New Hampshire",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-NJ",
                    ShortCode = "NJ",
                    DisplayName = "New Jersey",
                    OfficialName = "State of New Jersey",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-NM",
                    ShortCode = "NM",
                    DisplayName = "New Mexico",
                    OfficialName = "State of New Mexico",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-NY",
                    ShortCode = "NY",
                    DisplayName = "New York",
                    OfficialName = "State of New York",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-NC",
                    ShortCode = "NC",
                    DisplayName = "North Carolina",
                    OfficialName = "State of North Carolina",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-ND",
                    ShortCode = "ND",
                    DisplayName = "North Dakota",
                    OfficialName = "State of North Dakota",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-OH",
                    ShortCode = "OH",
                    DisplayName = "Ohio",
                    OfficialName = "State of Ohio",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-OK",
                    ShortCode = "OK",
                    DisplayName = "Oklahoma",
                    OfficialName = "State of Oklahoma",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-OR",
                    ShortCode = "OR",
                    DisplayName = "Oregon",
                    OfficialName = "State of Oregon",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-PA",
                    ShortCode = "PA",
                    DisplayName = "Pennsylvania",
                    OfficialName = "Commonwealth of Pennsylvania",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-RI",
                    ShortCode = "RI",
                    DisplayName = "Rhode Island",
                    OfficialName = "State of Rhode Island",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-SC",
                    ShortCode = "SC",
                    DisplayName = "South Carolina",
                    OfficialName = "State of South Carolina",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-SD",
                    ShortCode = "SD",
                    DisplayName = "South Dakota",
                    OfficialName = "State of South Dakota",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-TN",
                    ShortCode = "TN",
                    DisplayName = "Tennessee",
                    OfficialName = "State of Tennessee",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-TX",
                    ShortCode = "TX",
                    DisplayName = "Texas",
                    OfficialName = "State of Texas",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-UT",
                    ShortCode = "UT",
                    DisplayName = "Utah",
                    OfficialName = "State of Utah",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-VT",
                    ShortCode = "VT",
                    DisplayName = "Vermont",
                    OfficialName = "State of Vermont",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-VA",
                    ShortCode = "VA",
                    DisplayName = "Virginia",
                    OfficialName = "Commonwealth of Virginia",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-WA",
                    ShortCode = "WA",
                    DisplayName = "Washington",
                    OfficialName = "State of Washington",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-WV",
                    ShortCode = "WV",
                    DisplayName = "West Virginia",
                    OfficialName = "State of West Virginia",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-WI",
                    ShortCode = "WI",
                    DisplayName = "Wisconsin",
                    OfficialName = "State of Wisconsin",
                    CountryISO31661Alpha2Code = "US",
                },
                new Subdivision
                {
                    ISO31662Code = "US-WY",
                    ShortCode = "WY",
                    DisplayName = "Wyoming",
                    OfficialName = "State of Wyoming",
                    CountryISO31661Alpha2Code = "US",
                },

                // =============================================================
                // Canada (CA) - 10 Provinces + 3 Territories
                // =============================================================
                new Subdivision
                {
                    ISO31662Code = "CA-AB",
                    ShortCode = "AB",
                    DisplayName = "Alberta",
                    OfficialName = "Province of Alberta",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-BC",
                    ShortCode = "BC",
                    DisplayName = "British Columbia",
                    OfficialName = "Province of British Columbia",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-MB",
                    ShortCode = "MB",
                    DisplayName = "Manitoba",
                    OfficialName = "Province of Manitoba",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-NB",
                    ShortCode = "NB",
                    DisplayName = "New Brunswick",
                    OfficialName = "Province of New Brunswick",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-NL",
                    ShortCode = "NL",
                    DisplayName = "Newfoundland and Labrador",
                    OfficialName = "Province of Newfoundland and Labrador",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-NS",
                    ShortCode = "NS",
                    DisplayName = "Nova Scotia",
                    OfficialName = "Province of Nova Scotia",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-ON",
                    ShortCode = "ON",
                    DisplayName = "Ontario",
                    OfficialName = "Province of Ontario",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-PE",
                    ShortCode = "PE",
                    DisplayName = "Prince Edward Island",
                    OfficialName = "Province of Prince Edward Island",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-QC",
                    ShortCode = "QC",
                    DisplayName = "Quebec",
                    OfficialName = "Province of Quebec",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-SK",
                    ShortCode = "SK",
                    DisplayName = "Saskatchewan",
                    OfficialName = "Province of Saskatchewan",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-NT",
                    ShortCode = "NT",
                    DisplayName = "Northwest Territories",
                    OfficialName = "Northwest Territories",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-NU",
                    ShortCode = "NU",
                    DisplayName = "Nunavut",
                    OfficialName = "Nunavut",
                    CountryISO31661Alpha2Code = "CA",
                },
                new Subdivision
                {
                    ISO31662Code = "CA-YT",
                    ShortCode = "YT",
                    DisplayName = "Yukon",
                    OfficialName = "Yukon",
                    CountryISO31661Alpha2Code = "CA",
                },

                // =============================================================
                // United Kingdom (GB) - 4 Countries
                // =============================================================
                new Subdivision
                {
                    ISO31662Code = "GB-ENG",
                    ShortCode = "ENG",
                    DisplayName = "England",
                    OfficialName = "England",
                    CountryISO31661Alpha2Code = "GB",
                },
                new Subdivision
                {
                    ISO31662Code = "GB-NIR",
                    ShortCode = "NIR",
                    DisplayName = "Northern Ireland",
                    OfficialName = "Northern Ireland",
                    CountryISO31661Alpha2Code = "GB",
                },
                new Subdivision
                {
                    ISO31662Code = "GB-SCT",
                    ShortCode = "SCT",
                    DisplayName = "Scotland",
                    OfficialName = "Scotland",
                    CountryISO31661Alpha2Code = "GB",
                },
                new Subdivision
                {
                    ISO31662Code = "GB-WLS",
                    ShortCode = "WLS",
                    DisplayName = "Wales",
                    OfficialName = "Wales",
                    CountryISO31661Alpha2Code = "GB",
                },

                // =============================================================
                // Germany (DE) - 16 Bundesländer
                // =============================================================
                new Subdivision
                {
                    ISO31662Code = "DE-BW",
                    ShortCode = "BW",
                    DisplayName = "Baden-Württemberg",
                    OfficialName = "Land Baden-Württemberg",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-BY",
                    ShortCode = "BY",
                    DisplayName = "Bavaria",
                    OfficialName = "Freistaat Bayern",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-BE",
                    ShortCode = "BE",
                    DisplayName = "Berlin",
                    OfficialName = "Land Berlin",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-BB",
                    ShortCode = "BB",
                    DisplayName = "Brandenburg",
                    OfficialName = "Land Brandenburg",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-HB",
                    ShortCode = "HB",
                    DisplayName = "Bremen",
                    OfficialName = "Freie Hansestadt Bremen",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-HH",
                    ShortCode = "HH",
                    DisplayName = "Hamburg",
                    OfficialName = "Freie und Hansestadt Hamburg",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-HE",
                    ShortCode = "HE",
                    DisplayName = "Hesse",
                    OfficialName = "Land Hessen",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-MV",
                    ShortCode = "MV",
                    DisplayName = "Mecklenburg-Vorpommern",
                    OfficialName = "Land Mecklenburg-Vorpommern",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-NI",
                    ShortCode = "NI",
                    DisplayName = "Lower Saxony",
                    OfficialName = "Land Niedersachsen",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-NW",
                    ShortCode = "NW",
                    DisplayName = "North Rhine-Westphalia",
                    OfficialName = "Land Nordrhein-Westfalen",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-RP",
                    ShortCode = "RP",
                    DisplayName = "Rhineland-Palatinate",
                    OfficialName = "Land Rheinland-Pfalz",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-SL",
                    ShortCode = "SL",
                    DisplayName = "Saarland",
                    OfficialName = "Land Saarland",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-SN",
                    ShortCode = "SN",
                    DisplayName = "Saxony",
                    OfficialName = "Freistaat Sachsen",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-ST",
                    ShortCode = "ST",
                    DisplayName = "Saxony-Anhalt",
                    OfficialName = "Land Sachsen-Anhalt",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-SH",
                    ShortCode = "SH",
                    DisplayName = "Schleswig-Holstein",
                    OfficialName = "Land Schleswig-Holstein",
                    CountryISO31661Alpha2Code = "DE",
                },
                new Subdivision
                {
                    ISO31662Code = "DE-TH",
                    ShortCode = "TH",
                    DisplayName = "Thuringia",
                    OfficialName = "Freistaat Thüringen",
                    CountryISO31661Alpha2Code = "DE",
                },

                // =============================================================
                // France (FR) - 13 Regions (Metropolitan)
                // =============================================================
                new Subdivision
                {
                    ISO31662Code = "FR-ARA",
                    ShortCode = "ARA",
                    DisplayName = "Auvergne-Rhône-Alpes",
                    OfficialName = "Région Auvergne-Rhône-Alpes",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-BFC",
                    ShortCode = "BFC",
                    DisplayName = "Bourgogne-Franche-Comté",
                    OfficialName = "Région Bourgogne-Franche-Comté",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-BRE",
                    ShortCode = "BRE",
                    DisplayName = "Brittany",
                    OfficialName = "Région Bretagne",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-CVL",
                    ShortCode = "CVL",
                    DisplayName = "Centre-Val de Loire",
                    OfficialName = "Région Centre-Val de Loire",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-COR",
                    ShortCode = "COR",
                    DisplayName = "Corsica",
                    OfficialName = "Collectivité de Corse",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-GES",
                    ShortCode = "GES",
                    DisplayName = "Grand Est",
                    OfficialName = "Région Grand Est",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-HDF",
                    ShortCode = "HDF",
                    DisplayName = "Hauts-de-France",
                    OfficialName = "Région Hauts-de-France",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-IDF",
                    ShortCode = "IDF",
                    DisplayName = "Île-de-France",
                    OfficialName = "Région Île-de-France",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-NOR",
                    ShortCode = "NOR",
                    DisplayName = "Normandy",
                    OfficialName = "Région Normandie",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-NAQ",
                    ShortCode = "NAQ",
                    DisplayName = "Nouvelle-Aquitaine",
                    OfficialName = "Région Nouvelle-Aquitaine",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-OCC",
                    ShortCode = "OCC",
                    DisplayName = "Occitania",
                    OfficialName = "Région Occitanie",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-PDL",
                    ShortCode = "PDL",
                    DisplayName = "Pays de la Loire",
                    OfficialName = "Région Pays de la Loire",
                    CountryISO31661Alpha2Code = "FR",
                },
                new Subdivision
                {
                    ISO31662Code = "FR-PAC",
                    ShortCode = "PAC",
                    DisplayName = "Provence-Alpes-Côte d'Azur",
                    OfficialName = "Région Provence-Alpes-Côte d'Azur",
                    CountryISO31661Alpha2Code = "FR",
                },

                // =============================================================
                // Japan (JP) - 47 Prefectures
                // =============================================================
                new Subdivision
                {
                    ISO31662Code = "JP-01",
                    ShortCode = "01",
                    DisplayName = "Hokkaido",
                    OfficialName = "Hokkaidō",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-02",
                    ShortCode = "02",
                    DisplayName = "Aomori",
                    OfficialName = "Aomori-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-03",
                    ShortCode = "03",
                    DisplayName = "Iwate",
                    OfficialName = "Iwate-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-04",
                    ShortCode = "04",
                    DisplayName = "Miyagi",
                    OfficialName = "Miyagi-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-05",
                    ShortCode = "05",
                    DisplayName = "Akita",
                    OfficialName = "Akita-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-06",
                    ShortCode = "06",
                    DisplayName = "Yamagata",
                    OfficialName = "Yamagata-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-07",
                    ShortCode = "07",
                    DisplayName = "Fukushima",
                    OfficialName = "Fukushima-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-08",
                    ShortCode = "08",
                    DisplayName = "Ibaraki",
                    OfficialName = "Ibaraki-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-09",
                    ShortCode = "09",
                    DisplayName = "Tochigi",
                    OfficialName = "Tochigi-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-10",
                    ShortCode = "10",
                    DisplayName = "Gunma",
                    OfficialName = "Gunma-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-11",
                    ShortCode = "11",
                    DisplayName = "Saitama",
                    OfficialName = "Saitama-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-12",
                    ShortCode = "12",
                    DisplayName = "Chiba",
                    OfficialName = "Chiba-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-13",
                    ShortCode = "13",
                    DisplayName = "Tokyo",
                    OfficialName = "Tōkyō-to",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-14",
                    ShortCode = "14",
                    DisplayName = "Kanagawa",
                    OfficialName = "Kanagawa-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-15",
                    ShortCode = "15",
                    DisplayName = "Niigata",
                    OfficialName = "Niigata-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-16",
                    ShortCode = "16",
                    DisplayName = "Toyama",
                    OfficialName = "Toyama-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-17",
                    ShortCode = "17",
                    DisplayName = "Ishikawa",
                    OfficialName = "Ishikawa-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-18",
                    ShortCode = "18",
                    DisplayName = "Fukui",
                    OfficialName = "Fukui-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-19",
                    ShortCode = "19",
                    DisplayName = "Yamanashi",
                    OfficialName = "Yamanashi-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-20",
                    ShortCode = "20",
                    DisplayName = "Nagano",
                    OfficialName = "Nagano-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-21",
                    ShortCode = "21",
                    DisplayName = "Gifu",
                    OfficialName = "Gifu-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-22",
                    ShortCode = "22",
                    DisplayName = "Shizuoka",
                    OfficialName = "Shizuoka-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-23",
                    ShortCode = "23",
                    DisplayName = "Aichi",
                    OfficialName = "Aichi-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-24",
                    ShortCode = "24",
                    DisplayName = "Mie",
                    OfficialName = "Mie-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-25",
                    ShortCode = "25",
                    DisplayName = "Shiga",
                    OfficialName = "Shiga-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-26",
                    ShortCode = "26",
                    DisplayName = "Kyoto",
                    OfficialName = "Kyōto-fu",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-27",
                    ShortCode = "27",
                    DisplayName = "Osaka",
                    OfficialName = "Ōsaka-fu",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-28",
                    ShortCode = "28",
                    DisplayName = "Hyogo",
                    OfficialName = "Hyōgo-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-29",
                    ShortCode = "29",
                    DisplayName = "Nara",
                    OfficialName = "Nara-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-30",
                    ShortCode = "30",
                    DisplayName = "Wakayama",
                    OfficialName = "Wakayama-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-31",
                    ShortCode = "31",
                    DisplayName = "Tottori",
                    OfficialName = "Tottori-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-32",
                    ShortCode = "32",
                    DisplayName = "Shimane",
                    OfficialName = "Shimane-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-33",
                    ShortCode = "33",
                    DisplayName = "Okayama",
                    OfficialName = "Okayama-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-34",
                    ShortCode = "34",
                    DisplayName = "Hiroshima",
                    OfficialName = "Hiroshima-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-35",
                    ShortCode = "35",
                    DisplayName = "Yamaguchi",
                    OfficialName = "Yamaguchi-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-36",
                    ShortCode = "36",
                    DisplayName = "Tokushima",
                    OfficialName = "Tokushima-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-37",
                    ShortCode = "37",
                    DisplayName = "Kagawa",
                    OfficialName = "Kagawa-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-38",
                    ShortCode = "38",
                    DisplayName = "Ehime",
                    OfficialName = "Ehime-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-39",
                    ShortCode = "39",
                    DisplayName = "Kochi",
                    OfficialName = "Kōchi-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-40",
                    ShortCode = "40",
                    DisplayName = "Fukuoka",
                    OfficialName = "Fukuoka-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-41",
                    ShortCode = "41",
                    DisplayName = "Saga",
                    OfficialName = "Saga-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-42",
                    ShortCode = "42",
                    DisplayName = "Nagasaki",
                    OfficialName = "Nagasaki-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-43",
                    ShortCode = "43",
                    DisplayName = "Kumamoto",
                    OfficialName = "Kumamoto-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-44",
                    ShortCode = "44",
                    DisplayName = "Oita",
                    OfficialName = "Ōita-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-45",
                    ShortCode = "45",
                    DisplayName = "Miyazaki",
                    OfficialName = "Miyazaki-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-46",
                    ShortCode = "46",
                    DisplayName = "Kagoshima",
                    OfficialName = "Kagoshima-ken",
                    CountryISO31661Alpha2Code = "JP",
                },
                new Subdivision
                {
                    ISO31662Code = "JP-47",
                    ShortCode = "47",
                    DisplayName = "Okinawa",
                    OfficialName = "Okinawa-ken",
                    CountryISO31661Alpha2Code = "JP",
                },

                // =============================================================
                // Italy (IT) - 20 Regions
                // =============================================================
                new Subdivision
                {
                    ISO31662Code = "IT-21",
                    ShortCode = "21",
                    DisplayName = "Piedmont",
                    OfficialName = "Regione Piemonte",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-23",
                    ShortCode = "23",
                    DisplayName = "Aosta Valley",
                    OfficialName = "Regione Autonoma Valle d'Aosta",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-25",
                    ShortCode = "25",
                    DisplayName = "Lombardy",
                    OfficialName = "Regione Lombardia",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-32",
                    ShortCode = "32",
                    DisplayName = "Trentino-South Tyrol",
                    OfficialName = "Regione Autonoma Trentino-Alto Adige",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-34",
                    ShortCode = "34",
                    DisplayName = "Veneto",
                    OfficialName = "Regione del Veneto",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-36",
                    ShortCode = "36",
                    DisplayName = "Friuli-Venezia Giulia",
                    OfficialName = "Regione Autonoma Friuli-Venezia Giulia",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-42",
                    ShortCode = "42",
                    DisplayName = "Liguria",
                    OfficialName = "Regione Liguria",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-45",
                    ShortCode = "45",
                    DisplayName = "Emilia-Romagna",
                    OfficialName = "Regione Emilia-Romagna",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-52",
                    ShortCode = "52",
                    DisplayName = "Tuscany",
                    OfficialName = "Regione Toscana",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-55",
                    ShortCode = "55",
                    DisplayName = "Umbria",
                    OfficialName = "Regione Umbria",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-57",
                    ShortCode = "57",
                    DisplayName = "Marche",
                    OfficialName = "Regione Marche",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-62",
                    ShortCode = "62",
                    DisplayName = "Lazio",
                    OfficialName = "Regione Lazio",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-65",
                    ShortCode = "65",
                    DisplayName = "Abruzzo",
                    OfficialName = "Regione Abruzzo",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-67",
                    ShortCode = "67",
                    DisplayName = "Molise",
                    OfficialName = "Regione Molise",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-72",
                    ShortCode = "72",
                    DisplayName = "Campania",
                    OfficialName = "Regione Campania",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-75",
                    ShortCode = "75",
                    DisplayName = "Apulia",
                    OfficialName = "Regione Puglia",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-77",
                    ShortCode = "77",
                    DisplayName = "Basilicata",
                    OfficialName = "Regione Basilicata",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-78",
                    ShortCode = "78",
                    DisplayName = "Calabria",
                    OfficialName = "Regione Calabria",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-82",
                    ShortCode = "82",
                    DisplayName = "Sicily",
                    OfficialName = "Regione Siciliana",
                    CountryISO31661Alpha2Code = "IT",
                },
                new Subdivision
                {
                    ISO31662Code = "IT-88",
                    ShortCode = "88",
                    DisplayName = "Sardinia",
                    OfficialName = "Regione Autonoma della Sardegna",
                    CountryISO31661Alpha2Code = "IT",
                },

                // =============================================================
                // Spain (ES) - 17 Autonomous Communities + 2 Autonomous Cities
                // =============================================================
                new Subdivision
                {
                    ISO31662Code = "ES-AN",
                    ShortCode = "AN",
                    DisplayName = "Andalusia",
                    OfficialName = "Comunidad Autónoma de Andalucía",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-AR",
                    ShortCode = "AR",
                    DisplayName = "Aragon",
                    OfficialName = "Comunidad Autónoma de Aragón",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-AS",
                    ShortCode = "AS",
                    DisplayName = "Asturias",
                    OfficialName = "Principado de Asturias",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-CB",
                    ShortCode = "CB",
                    DisplayName = "Cantabria",
                    OfficialName = "Comunidad Autónoma de Cantabria",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-CE",
                    ShortCode = "CE",
                    DisplayName = "Ceuta",
                    OfficialName = "Ciudad Autónoma de Ceuta",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-CL",
                    ShortCode = "CL",
                    DisplayName = "Castile and León",
                    OfficialName = "Comunidad Autónoma de Castilla y León",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-CM",
                    ShortCode = "CM",
                    DisplayName = "Castilla-La Mancha",
                    OfficialName = "Comunidad Autónoma de Castilla-La Mancha",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-CN",
                    ShortCode = "CN",
                    DisplayName = "Canary Islands",
                    OfficialName = "Comunidad Autónoma de Canarias",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-CT",
                    ShortCode = "CT",
                    DisplayName = "Catalonia",
                    OfficialName = "Comunidad Autónoma de Cataluña",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-EX",
                    ShortCode = "EX",
                    DisplayName = "Extremadura",
                    OfficialName = "Comunidad Autónoma de Extremadura",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-GA",
                    ShortCode = "GA",
                    DisplayName = "Galicia",
                    OfficialName = "Comunidad Autónoma de Galicia",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-IB",
                    ShortCode = "IB",
                    DisplayName = "Balearic Islands",
                    OfficialName = "Comunidad Autónoma de las Illes Balears",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-MC",
                    ShortCode = "MC",
                    DisplayName = "Murcia",
                    OfficialName = "Comunidad Autónoma de la Región de Murcia",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-MD",
                    ShortCode = "MD",
                    DisplayName = "Madrid",
                    OfficialName = "Comunidad de Madrid",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-ML",
                    ShortCode = "ML",
                    DisplayName = "Melilla",
                    OfficialName = "Ciudad Autónoma de Melilla",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-NC",
                    ShortCode = "NC",
                    DisplayName = "Navarre",
                    OfficialName = "Comunidad Foral de Navarra",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-PV",
                    ShortCode = "PV",
                    DisplayName = "Basque Country",
                    OfficialName = "Comunidad Autónoma del País Vasco",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-RI",
                    ShortCode = "RI",
                    DisplayName = "La Rioja",
                    OfficialName = "Comunidad Autónoma de La Rioja",
                    CountryISO31661Alpha2Code = "ES",
                },
                new Subdivision
                {
                    ISO31662Code = "ES-VC",
                    ShortCode = "VC",
                    DisplayName = "Valencia",
                    OfficialName = "Comunidad Valenciana",
                    CountryISO31661Alpha2Code = "ES",
                },
            ]);
        }
    }
}
