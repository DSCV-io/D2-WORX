// -----------------------------------------------------------------------
// <copyright file="TimezoneSeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding timezone data.
/// </summary>
public static class TimezoneSeeding
{
    /// <summary>
    /// Seeds the Timezone entity.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the Timezone entity.
        /// </summary>
        public void SeedTimezones()
        {
            modelBuilder.Entity<Timezone>().HasData([

                // =============
                // Africa
                // =============
                new Timezone
                {
                    IANAIdentifier = "Africa/Abidjan",
                    CountryISO31661Alpha2Code = "CI",
                    DisplayName = "Africa / Abidjan",
                    UTCOffsetSTD = "+00:00",
                    AbbreviationSTD = "GMT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Algiers",
                    CountryISO31661Alpha2Code = "DZ",
                    DisplayName = "Africa / Algiers",
                    UTCOffsetSTD = "+01:00",
                    AbbreviationSTD = "CET",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Bissau",
                    CountryISO31661Alpha2Code = "GW",
                    DisplayName = "Africa / Bissau",
                    UTCOffsetSTD = "+00:00",
                    AbbreviationSTD = "GMT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Cairo",
                    CountryISO31661Alpha2Code = "EG",
                    DisplayName = "Africa / Cairo",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Casablanca",
                    CountryISO31661Alpha2Code = "MA",
                    DisplayName = "Africa / Casablanca",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+00:00",
                    AbbreviationSTD = "+01",
                    AbbreviationDST = "+00",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Ceuta",
                    CountryISO31661Alpha2Code = "ES",
                    DisplayName = "Africa / Ceuta",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/El_Aaiun",
                    CountryISO31661Alpha2Code = "EH",
                    DisplayName = "Africa / El Aaiun",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+00:00",
                    AbbreviationSTD = "+01",
                    AbbreviationDST = "+00",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Johannesburg",
                    CountryISO31661Alpha2Code = "ZA",
                    DisplayName = "Africa / Johannesburg",
                    UTCOffsetSTD = "+02:00",
                    AbbreviationSTD = "SAST",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Juba",
                    CountryISO31661Alpha2Code = "SS",
                    DisplayName = "Africa / Juba",
                    UTCOffsetSTD = "+02:00",
                    AbbreviationSTD = "CAT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Khartoum",
                    CountryISO31661Alpha2Code = "SD",
                    DisplayName = "Africa / Khartoum",
                    UTCOffsetSTD = "+02:00",
                    AbbreviationSTD = "CAT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Lagos",
                    CountryISO31661Alpha2Code = "NG",
                    DisplayName = "Africa / Lagos",
                    UTCOffsetSTD = "+01:00",
                    AbbreviationSTD = "WAT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Maputo",
                    CountryISO31661Alpha2Code = "MZ",
                    DisplayName = "Africa / Maputo",
                    UTCOffsetSTD = "+02:00",
                    AbbreviationSTD = "CAT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Monrovia",
                    CountryISO31661Alpha2Code = "LR",
                    DisplayName = "Africa / Monrovia",
                    UTCOffsetSTD = "+00:00",
                    AbbreviationSTD = "GMT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Nairobi",
                    CountryISO31661Alpha2Code = "KE",
                    DisplayName = "Africa / Nairobi",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "EAT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Ndjamena",
                    CountryISO31661Alpha2Code = "TD",
                    DisplayName = "Africa / Ndjamena",
                    UTCOffsetSTD = "+01:00",
                    AbbreviationSTD = "WAT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Sao_Tome",
                    CountryISO31661Alpha2Code = "ST",
                    DisplayName = "Africa / Sao Tome",
                    UTCOffsetSTD = "+00:00",
                    AbbreviationSTD = "GMT",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Tripoli",
                    CountryISO31661Alpha2Code = "LY",
                    DisplayName = "Africa / Tripoli",
                    UTCOffsetSTD = "+02:00",
                    AbbreviationSTD = "EET",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Tunis",
                    CountryISO31661Alpha2Code = "TN",
                    DisplayName = "Africa / Tunis",
                    UTCOffsetSTD = "+01:00",
                    AbbreviationSTD = "CET",
                },
                new Timezone
                {
                    IANAIdentifier = "Africa/Windhoek",
                    CountryISO31661Alpha2Code = "NA",
                    DisplayName = "Africa / Windhoek",
                    UTCOffsetSTD = "+02:00",
                    AbbreviationSTD = "CAT",
                },

                // =============
                // America
                // =============
                new Timezone
                {
                    IANAIdentifier = "America/Adak",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Adak",
                    UTCOffsetSTD = "-10:00",
                    UTCOffsetDST = "-09:00",
                    AbbreviationSTD = "HST",
                    AbbreviationDST = "HDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Anchorage",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Anchorage",
                    UTCOffsetSTD = "-09:00",
                    UTCOffsetDST = "-08:00",
                    AbbreviationSTD = "AKST",
                    AbbreviationDST = "AKDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Araguaina",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Araguaina",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Buenos_Aires",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Buenos Aires",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Catamarca",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Catamarca",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Cordoba",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Cordoba",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Jujuy",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Jujuy",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/La_Rioja",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / La Rioja",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Mendoza",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Mendoza",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Rio_Gallegos",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Rio Gallegos",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Salta",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Salta",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/San_Juan",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / San Juan",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/San_Luis",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / San Luis",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Tucuman",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Tucuman",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Argentina/Ushuaia",
                    CountryISO31661Alpha2Code = "AR",
                    DisplayName = "America / Argentina / Ushuaia",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Asuncion",
                    CountryISO31661Alpha2Code = "PY",
                    DisplayName = "America / Asuncion",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Bahia",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Bahia",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Bahia_Banderas",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Bahia Banderas",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Barbados",
                    CountryISO31661Alpha2Code = "BB",
                    DisplayName = "America / Barbados",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "AST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Belem",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Belem",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Belize",
                    CountryISO31661Alpha2Code = "BZ",
                    DisplayName = "America / Belize",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Boa_Vista",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Boa Vista",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "-04",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Bogota",
                    CountryISO31661Alpha2Code = "CO",
                    DisplayName = "America / Bogota",
                    UTCOffsetSTD = "-05:00",
                    AbbreviationSTD = "-05",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Boise",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Boise",
                    UTCOffsetSTD = "-07:00",
                    UTCOffsetDST = "-06:00",
                    AbbreviationSTD = "MST",
                    AbbreviationDST = "MDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Cambridge_Bay",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Cambridge Bay",
                    UTCOffsetSTD = "-07:00",
                    UTCOffsetDST = "-06:00",
                    AbbreviationSTD = "MST",
                    AbbreviationDST = "MDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Campo_Grande",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Campo Grande",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "-04",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Cancun",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Cancun",
                    UTCOffsetSTD = "-05:00",
                    AbbreviationSTD = "EST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Caracas",
                    CountryISO31661Alpha2Code = "VE",
                    DisplayName = "America / Caracas",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "-04",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Cayenne",
                    CountryISO31661Alpha2Code = "GF",
                    DisplayName = "America / Cayenne",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Chicago",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Chicago",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Chihuahua",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Chihuahua",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Ciudad_Juarez",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Ciudad Juarez",
                    UTCOffsetSTD = "-07:00",
                    UTCOffsetDST = "-06:00",
                    AbbreviationSTD = "MST",
                    AbbreviationDST = "MDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Costa_Rica",
                    CountryISO31661Alpha2Code = "CR",
                    DisplayName = "America / Costa Rica",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Coyhaique",
                    CountryISO31661Alpha2Code = "CL",
                    DisplayName = "America / Coyhaique",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Cuiaba",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Cuiaba",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "-04",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Danmarkshavn",
                    CountryISO31661Alpha2Code = "GL",
                    DisplayName = "America / Danmarkshavn",
                    UTCOffsetSTD = "+00:00",
                    AbbreviationSTD = "GMT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Dawson",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Dawson",
                    UTCOffsetSTD = "-07:00",
                    AbbreviationSTD = "MST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Dawson_Creek",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Dawson Creek",
                    UTCOffsetSTD = "-07:00",
                    AbbreviationSTD = "MST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Denver",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Denver",
                    UTCOffsetSTD = "-07:00",
                    UTCOffsetDST = "-06:00",
                    AbbreviationSTD = "MST",
                    AbbreviationDST = "MDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Detroit",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Detroit",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Edmonton",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Edmonton",
                    UTCOffsetSTD = "-07:00",
                    UTCOffsetDST = "-06:00",
                    AbbreviationSTD = "MST",
                    AbbreviationDST = "MDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Eirunepe",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Eirunepe",
                    UTCOffsetSTD = "-05:00",
                    AbbreviationSTD = "-05",
                },
                new Timezone
                {
                    IANAIdentifier = "America/El_Salvador",
                    CountryISO31661Alpha2Code = "SV",
                    DisplayName = "America / El Salvador",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Fort_Nelson",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Fort Nelson",
                    UTCOffsetSTD = "-07:00",
                    AbbreviationSTD = "MST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Fortaleza",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Fortaleza",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Glace_Bay",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Glace Bay",
                    UTCOffsetSTD = "-04:00",
                    UTCOffsetDST = "-03:00",
                    AbbreviationSTD = "AST",
                    AbbreviationDST = "ADT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Goose_Bay",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Goose Bay",
                    UTCOffsetSTD = "-04:00",
                    UTCOffsetDST = "-03:00",
                    AbbreviationSTD = "AST",
                    AbbreviationDST = "ADT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Grand_Turk",
                    CountryISO31661Alpha2Code = "TC",
                    DisplayName = "America / Grand Turk",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Guatemala",
                    CountryISO31661Alpha2Code = "GT",
                    DisplayName = "America / Guatemala",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Guayaquil",
                    CountryISO31661Alpha2Code = "EC",
                    DisplayName = "America / Guayaquil",
                    UTCOffsetSTD = "-05:00",
                    AbbreviationSTD = "-05",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Guyana",
                    CountryISO31661Alpha2Code = "GY",
                    DisplayName = "America / Guyana",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "-04",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Halifax",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Halifax",
                    UTCOffsetSTD = "-04:00",
                    UTCOffsetDST = "-03:00",
                    AbbreviationSTD = "AST",
                    AbbreviationDST = "ADT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Havana",
                    CountryISO31661Alpha2Code = "CU",
                    DisplayName = "America / Havana",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Hermosillo",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Hermosillo",
                    UTCOffsetSTD = "-07:00",
                    AbbreviationSTD = "MST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Indiana/Indianapolis",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Indiana / Indianapolis",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Indiana/Knox",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Indiana / Knox",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Indiana/Marengo",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Indiana / Marengo",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Indiana/Petersburg",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Indiana / Petersburg",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Indiana/Tell_City",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Indiana / Tell City",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Indiana/Vevay",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Indiana / Vevay",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Indiana/Vincennes",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Indiana / Vincennes",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Indiana/Winamac",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Indiana / Winamac",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Inuvik",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Inuvik",
                    UTCOffsetSTD = "-07:00",
                    UTCOffsetDST = "-06:00",
                    AbbreviationSTD = "MST",
                    AbbreviationDST = "MDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Iqaluit",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Iqaluit",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Jamaica",
                    CountryISO31661Alpha2Code = "JM",
                    DisplayName = "America / Jamaica",
                    UTCOffsetSTD = "-05:00",
                    AbbreviationSTD = "EST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Juneau",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Juneau",
                    UTCOffsetSTD = "-09:00",
                    UTCOffsetDST = "-08:00",
                    AbbreviationSTD = "AKST",
                    AbbreviationDST = "AKDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Kentucky/Louisville",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Kentucky / Louisville",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Kentucky/Monticello",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Kentucky / Monticello",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/La_Paz",
                    CountryISO31661Alpha2Code = "BO",
                    DisplayName = "America / La Paz",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "-04",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Lima",
                    CountryISO31661Alpha2Code = "PE",
                    DisplayName = "America / Lima",
                    UTCOffsetSTD = "-05:00",
                    AbbreviationSTD = "-05",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Los_Angeles",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Los Angeles",
                    UTCOffsetSTD = "-08:00",
                    UTCOffsetDST = "-07:00",
                    AbbreviationSTD = "PST",
                    AbbreviationDST = "PDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Maceio",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Maceio",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Managua",
                    CountryISO31661Alpha2Code = "NI",
                    DisplayName = "America / Managua",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Manaus",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Manaus",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "-04",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Martinique",
                    CountryISO31661Alpha2Code = "MQ",
                    DisplayName = "America / Martinique",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "AST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Matamoros",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Matamoros",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Mazatlan",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Mazatlan",
                    UTCOffsetSTD = "-07:00",
                    AbbreviationSTD = "MST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Menominee",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Menominee",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Merida",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Merida",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Metlakatla",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Metlakatla",
                    UTCOffsetSTD = "-09:00",
                    UTCOffsetDST = "-08:00",
                    AbbreviationSTD = "AKST",
                    AbbreviationDST = "AKDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Mexico_City",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Mexico City",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Miquelon",
                    CountryISO31661Alpha2Code = "PM",
                    DisplayName = "America / Miquelon",
                    UTCOffsetSTD = "-03:00",
                    UTCOffsetDST = "-02:00",
                    AbbreviationSTD = "-03",
                    AbbreviationDST = "-02",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Moncton",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Moncton",
                    UTCOffsetSTD = "-04:00",
                    UTCOffsetDST = "-03:00",
                    AbbreviationSTD = "AST",
                    AbbreviationDST = "ADT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Monterrey",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Monterrey",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Montevideo",
                    CountryISO31661Alpha2Code = "UY",
                    DisplayName = "America / Montevideo",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/New_York",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / New York",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Nome",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Nome",
                    UTCOffsetSTD = "-09:00",
                    UTCOffsetDST = "-08:00",
                    AbbreviationSTD = "AKST",
                    AbbreviationDST = "AKDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Noronha",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Noronha",
                    UTCOffsetSTD = "-02:00",
                    AbbreviationSTD = "-02",
                },
                new Timezone
                {
                    IANAIdentifier = "America/North_Dakota/Beulah",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / North Dakota / Beulah",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/North_Dakota/Center",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / North Dakota / Center",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/North_Dakota/New_Salem",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / North Dakota / New Salem",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Nuuk",
                    CountryISO31661Alpha2Code = "GL",
                    DisplayName = "America / Nuuk",
                    UTCOffsetSTD = "-02:00",
                    UTCOffsetDST = "-01:00",
                    AbbreviationSTD = "-02",
                    AbbreviationDST = "-01",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Ojinaga",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Ojinaga",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Panama",
                    CountryISO31661Alpha2Code = "PA",
                    DisplayName = "America / Panama",
                    UTCOffsetSTD = "-05:00",
                    AbbreviationSTD = "EST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Paramaribo",
                    CountryISO31661Alpha2Code = "SR",
                    DisplayName = "America / Paramaribo",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Phoenix",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Phoenix",
                    UTCOffsetSTD = "-07:00",
                    AbbreviationSTD = "MST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Port-au-Prince",
                    CountryISO31661Alpha2Code = "HT",
                    DisplayName = "America / Port-au-Prince",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Porto_Velho",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Porto Velho",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "-04",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Puerto_Rico",
                    CountryISO31661Alpha2Code = "PR",
                    DisplayName = "America / Puerto Rico",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "AST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Punta_Arenas",
                    CountryISO31661Alpha2Code = "CL",
                    DisplayName = "America / Punta Arenas",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Rankin_Inlet",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Rankin Inlet",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Recife",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Recife",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Regina",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Regina",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Resolute",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Resolute",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Rio_Branco",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Rio Branco",
                    UTCOffsetSTD = "-05:00",
                    AbbreviationSTD = "-05",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Santarem",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Santarem",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Santiago",
                    CountryISO31661Alpha2Code = "CL",
                    DisplayName = "America / Santiago",
                    UTCOffsetSTD = "-04:00",
                    UTCOffsetDST = "-03:00",
                    AbbreviationSTD = "-04",
                    AbbreviationDST = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Santo_Domingo",
                    CountryISO31661Alpha2Code = "DO",
                    DisplayName = "America / Santo Domingo",
                    UTCOffsetSTD = "-04:00",
                    AbbreviationSTD = "AST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Sao_Paulo",
                    CountryISO31661Alpha2Code = "BR",
                    DisplayName = "America / Sao Paulo",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Scoresbysund",
                    CountryISO31661Alpha2Code = "GL",
                    DisplayName = "America / Scoresbysund",
                    UTCOffsetSTD = "-02:00",
                    UTCOffsetDST = "-01:00",
                    AbbreviationSTD = "-02",
                    AbbreviationDST = "-01",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Sitka",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Sitka",
                    UTCOffsetSTD = "-09:00",
                    UTCOffsetDST = "-08:00",
                    AbbreviationSTD = "AKST",
                    AbbreviationDST = "AKDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/St_Johns",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / St Johns",
                    UTCOffsetSTD = "-03:30",
                    UTCOffsetDST = "-02:30",
                    AbbreviationSTD = "NST",
                    AbbreviationDST = "NDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Swift_Current",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Swift Current",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Tegucigalpa",
                    CountryISO31661Alpha2Code = "HN",
                    DisplayName = "America / Tegucigalpa",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Thule",
                    CountryISO31661Alpha2Code = "GL",
                    DisplayName = "America / Thule",
                    UTCOffsetSTD = "-04:00",
                    UTCOffsetDST = "-03:00",
                    AbbreviationSTD = "AST",
                    AbbreviationDST = "ADT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Tijuana",
                    CountryISO31661Alpha2Code = "MX",
                    DisplayName = "America / Tijuana",
                    UTCOffsetSTD = "-08:00",
                    UTCOffsetDST = "-07:00",
                    AbbreviationSTD = "PST",
                    AbbreviationDST = "PDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Toronto",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Toronto",
                    UTCOffsetSTD = "-05:00",
                    UTCOffsetDST = "-04:00",
                    AbbreviationSTD = "EST",
                    AbbreviationDST = "EDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Vancouver",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Vancouver",
                    UTCOffsetSTD = "-08:00",
                    UTCOffsetDST = "-07:00",
                    AbbreviationSTD = "PST",
                    AbbreviationDST = "PDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Whitehorse",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Whitehorse",
                    UTCOffsetSTD = "-07:00",
                    AbbreviationSTD = "MST",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Winnipeg",
                    CountryISO31661Alpha2Code = "CA",
                    DisplayName = "America / Winnipeg",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "CST",
                    AbbreviationDST = "CDT",
                },
                new Timezone
                {
                    IANAIdentifier = "America/Yakutat",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "America / Yakutat",
                    UTCOffsetSTD = "-09:00",
                    UTCOffsetDST = "-08:00",
                    AbbreviationSTD = "AKST",
                    AbbreviationDST = "AKDT",
                },

                // =============
                // Antarctica
                // =============
                new Timezone
                {
                    IANAIdentifier = "Antarctica/Casey",
                    CountryISO31661Alpha2Code = "AQ",
                    DisplayName = "Antarctica / Casey",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "+08",
                },
                new Timezone
                {
                    IANAIdentifier = "Antarctica/Davis",
                    CountryISO31661Alpha2Code = "AQ",
                    DisplayName = "Antarctica / Davis",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Antarctica/Macquarie",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Antarctica / Macquarie",
                    UTCOffsetSTD = "+10:00",
                    UTCOffsetDST = "+11:00",
                    AbbreviationSTD = "AEST",
                    AbbreviationDST = "AEDT",
                },
                new Timezone
                {
                    IANAIdentifier = "Antarctica/Mawson",
                    CountryISO31661Alpha2Code = "AQ",
                    DisplayName = "Antarctica / Mawson",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Antarctica/Palmer",
                    CountryISO31661Alpha2Code = "AQ",
                    DisplayName = "Antarctica / Palmer",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "Antarctica/Rothera",
                    CountryISO31661Alpha2Code = "AQ",
                    DisplayName = "Antarctica / Rothera",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },
                new Timezone
                {
                    IANAIdentifier = "Antarctica/Troll",
                    CountryISO31661Alpha2Code = "AQ",
                    DisplayName = "Antarctica / Troll",
                    UTCOffsetSTD = "+00:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "+00",
                    AbbreviationDST = "+02",
                },
                new Timezone
                {
                    IANAIdentifier = "Antarctica/Vostok",
                    CountryISO31661Alpha2Code = "AQ",
                    DisplayName = "Antarctica / Vostok",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },

                // =============
                // Asia
                // =============
                new Timezone
                {
                    IANAIdentifier = "Asia/Almaty",
                    CountryISO31661Alpha2Code = "KZ",
                    DisplayName = "Asia / Almaty",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Amman",
                    CountryISO31661Alpha2Code = "JO",
                    DisplayName = "Asia / Amman",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "+03",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Anadyr",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Anadyr",
                    UTCOffsetSTD = "+12:00",
                    AbbreviationSTD = "+12",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Aqtau",
                    CountryISO31661Alpha2Code = "KZ",
                    DisplayName = "Asia / Aqtau",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Aqtobe",
                    CountryISO31661Alpha2Code = "KZ",
                    DisplayName = "Asia / Aqtobe",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Ashgabat",
                    CountryISO31661Alpha2Code = "TM",
                    DisplayName = "Asia / Ashgabat",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Atyrau",
                    CountryISO31661Alpha2Code = "KZ",
                    DisplayName = "Asia / Atyrau",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Baghdad",
                    CountryISO31661Alpha2Code = "IQ",
                    DisplayName = "Asia / Baghdad",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "+03",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Baku",
                    CountryISO31661Alpha2Code = "AZ",
                    DisplayName = "Asia / Baku",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Bangkok",
                    CountryISO31661Alpha2Code = "TH",
                    DisplayName = "Asia / Bangkok",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Barnaul",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Barnaul",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Beirut",
                    CountryISO31661Alpha2Code = "LB",
                    DisplayName = "Asia / Beirut",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Bishkek",
                    CountryISO31661Alpha2Code = "KG",
                    DisplayName = "Asia / Bishkek",
                    UTCOffsetSTD = "+06:00",
                    AbbreviationSTD = "+06",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Colombo",
                    CountryISO31661Alpha2Code = "LK",
                    DisplayName = "Asia / Colombo",
                    UTCOffsetSTD = "+05:30",
                    AbbreviationSTD = "+0530",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Damascus",
                    CountryISO31661Alpha2Code = "SY",
                    DisplayName = "Asia / Damascus",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "+03",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Dhaka",
                    CountryISO31661Alpha2Code = "BD",
                    DisplayName = "Asia / Dhaka",
                    UTCOffsetSTD = "+06:00",
                    AbbreviationSTD = "+06",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Dili",
                    CountryISO31661Alpha2Code = "TL",
                    DisplayName = "Asia / Dili",
                    UTCOffsetSTD = "+09:00",
                    AbbreviationSTD = "+09",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Dubai",
                    CountryISO31661Alpha2Code = "AE",
                    DisplayName = "Asia / Dubai",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Dushanbe",
                    CountryISO31661Alpha2Code = "TJ",
                    DisplayName = "Asia / Dushanbe",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Famagusta",
                    CountryISO31661Alpha2Code = "CY",
                    DisplayName = "Asia / Famagusta",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Gaza",
                    CountryISO31661Alpha2Code = "PS",
                    DisplayName = "Asia / Gaza",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Hebron",
                    CountryISO31661Alpha2Code = "PS",
                    DisplayName = "Asia / Hebron",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Ho_Chi_Minh",
                    CountryISO31661Alpha2Code = "VN",
                    DisplayName = "Asia / Ho Chi Minh",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Hong_Kong",
                    CountryISO31661Alpha2Code = "HK",
                    DisplayName = "Asia / Hong Kong",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "HKT",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Hovd",
                    CountryISO31661Alpha2Code = "MN",
                    DisplayName = "Asia / Hovd",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Irkutsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Irkutsk",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "+08",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Jakarta",
                    CountryISO31661Alpha2Code = "ID",
                    DisplayName = "Asia / Jakarta",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "WIB",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Jayapura",
                    CountryISO31661Alpha2Code = "ID",
                    DisplayName = "Asia / Jayapura",
                    UTCOffsetSTD = "+09:00",
                    AbbreviationSTD = "WIT",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Jerusalem",
                    CountryISO31661Alpha2Code = "IL",
                    DisplayName = "Asia / Jerusalem",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "IST",
                    AbbreviationDST = "IDT",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Kabul",
                    CountryISO31661Alpha2Code = "AF",
                    DisplayName = "Asia / Kabul",
                    UTCOffsetSTD = "+04:30",
                    AbbreviationSTD = "+0430",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Kamchatka",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Kamchatka",
                    UTCOffsetSTD = "+12:00",
                    AbbreviationSTD = "+12",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Karachi",
                    CountryISO31661Alpha2Code = "PK",
                    DisplayName = "Asia / Karachi",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "PKT",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Kathmandu",
                    CountryISO31661Alpha2Code = "NP",
                    DisplayName = "Asia / Kathmandu",
                    UTCOffsetSTD = "+05:45",
                    AbbreviationSTD = "+0545",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Khandyga",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Khandyga",
                    UTCOffsetSTD = "+09:00",
                    AbbreviationSTD = "+09",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Kolkata",
                    CountryISO31661Alpha2Code = "IN",
                    DisplayName = "Asia / Kolkata",
                    UTCOffsetSTD = "+05:30",
                    AbbreviationSTD = "IST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Krasnoyarsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Krasnoyarsk",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Kuching",
                    CountryISO31661Alpha2Code = "MY",
                    DisplayName = "Asia / Kuching",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "+08",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Macau",
                    CountryISO31661Alpha2Code = "MO",
                    DisplayName = "Asia / Macau",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Magadan",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Magadan",
                    UTCOffsetSTD = "+11:00",
                    AbbreviationSTD = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Makassar",
                    CountryISO31661Alpha2Code = "ID",
                    DisplayName = "Asia / Makassar",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "WITA",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Manila",
                    CountryISO31661Alpha2Code = "PH",
                    DisplayName = "Asia / Manila",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "PST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Nicosia",
                    CountryISO31661Alpha2Code = "CY",
                    DisplayName = "Asia / Nicosia",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Novokuznetsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Novokuznetsk",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Novosibirsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Novosibirsk",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Omsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Omsk",
                    UTCOffsetSTD = "+06:00",
                    AbbreviationSTD = "+06",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Oral",
                    CountryISO31661Alpha2Code = "KZ",
                    DisplayName = "Asia / Oral",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Pontianak",
                    CountryISO31661Alpha2Code = "ID",
                    DisplayName = "Asia / Pontianak",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "WIB",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Pyongyang",
                    CountryISO31661Alpha2Code = "KP",
                    DisplayName = "Asia / Pyongyang",
                    UTCOffsetSTD = "+09:00",
                    AbbreviationSTD = "KST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Qatar",
                    CountryISO31661Alpha2Code = "QA",
                    DisplayName = "Asia / Qatar",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "+03",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Qostanay",
                    CountryISO31661Alpha2Code = "KZ",
                    DisplayName = "Asia / Qostanay",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Qyzylorda",
                    CountryISO31661Alpha2Code = "KZ",
                    DisplayName = "Asia / Qyzylorda",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Riyadh",
                    CountryISO31661Alpha2Code = "SA",
                    DisplayName = "Asia / Riyadh",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "+03",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Sakhalin",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Sakhalin",
                    UTCOffsetSTD = "+11:00",
                    AbbreviationSTD = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Samarkand",
                    CountryISO31661Alpha2Code = "UZ",
                    DisplayName = "Asia / Samarkand",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Seoul",
                    CountryISO31661Alpha2Code = "KR",
                    DisplayName = "Asia / Seoul",
                    UTCOffsetSTD = "+09:00",
                    AbbreviationSTD = "KST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Shanghai",
                    CountryISO31661Alpha2Code = "CN",
                    DisplayName = "Asia / Shanghai",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Singapore",
                    CountryISO31661Alpha2Code = "SG",
                    DisplayName = "Asia / Singapore",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "+08",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Srednekolymsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Srednekolymsk",
                    UTCOffsetSTD = "+11:00",
                    AbbreviationSTD = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Taipei",
                    CountryISO31661Alpha2Code = "TW",
                    DisplayName = "Asia / Taipei",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "CST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Tashkent",
                    CountryISO31661Alpha2Code = "UZ",
                    DisplayName = "Asia / Tashkent",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Tbilisi",
                    CountryISO31661Alpha2Code = "GE",
                    DisplayName = "Asia / Tbilisi",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Tehran",
                    CountryISO31661Alpha2Code = "IR",
                    DisplayName = "Asia / Tehran",
                    UTCOffsetSTD = "+03:30",
                    AbbreviationSTD = "+0330",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Thimphu",
                    CountryISO31661Alpha2Code = "BT",
                    DisplayName = "Asia / Thimphu",
                    UTCOffsetSTD = "+06:00",
                    AbbreviationSTD = "+06",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Tokyo",
                    CountryISO31661Alpha2Code = "JP",
                    DisplayName = "Asia / Tokyo",
                    UTCOffsetSTD = "+09:00",
                    AbbreviationSTD = "JST",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Tomsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Tomsk",
                    UTCOffsetSTD = "+07:00",
                    AbbreviationSTD = "+07",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Ulaanbaatar",
                    CountryISO31661Alpha2Code = "MN",
                    DisplayName = "Asia / Ulaanbaatar",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "+08",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Urumqi",
                    CountryISO31661Alpha2Code = "CN",
                    DisplayName = "Asia / Urumqi",
                    UTCOffsetSTD = "+06:00",
                    AbbreviationSTD = "+06",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Ust-Nera",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Ust-Nera",
                    UTCOffsetSTD = "+10:00",
                    AbbreviationSTD = "+10",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Vladivostok",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Vladivostok",
                    UTCOffsetSTD = "+10:00",
                    AbbreviationSTD = "+10",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Yakutsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Yakutsk",
                    UTCOffsetSTD = "+09:00",
                    AbbreviationSTD = "+09",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Yangon",
                    CountryISO31661Alpha2Code = "MM",
                    DisplayName = "Asia / Yangon",
                    UTCOffsetSTD = "+06:30",
                    AbbreviationSTD = "+0630",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Yekaterinburg",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Asia / Yekaterinburg",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Asia/Yerevan",
                    CountryISO31661Alpha2Code = "AM",
                    DisplayName = "Asia / Yerevan",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },

                // =============
                // Atlantic
                // =============
                new Timezone
                {
                    IANAIdentifier = "Atlantic/Azores",
                    CountryISO31661Alpha2Code = "PT",
                    DisplayName = "Atlantic / Azores",
                    UTCOffsetSTD = "-01:00",
                    UTCOffsetDST = "+00:00",
                    AbbreviationSTD = "-01",
                    AbbreviationDST = "+00",
                },
                new Timezone
                {
                    IANAIdentifier = "Atlantic/Bermuda",
                    CountryISO31661Alpha2Code = "BM",
                    DisplayName = "Atlantic / Bermuda",
                    UTCOffsetSTD = "-04:00",
                    UTCOffsetDST = "-03:00",
                    AbbreviationSTD = "AST",
                    AbbreviationDST = "ADT",
                },
                new Timezone
                {
                    IANAIdentifier = "Atlantic/Cape_Verde",
                    CountryISO31661Alpha2Code = "CV",
                    DisplayName = "Atlantic / Cape Verde",
                    UTCOffsetSTD = "-01:00",
                    AbbreviationSTD = "-01",
                },
                new Timezone
                {
                    IANAIdentifier = "Atlantic/Faroe",
                    CountryISO31661Alpha2Code = "FO",
                    DisplayName = "Atlantic / Faroe",
                    UTCOffsetSTD = "+00:00",
                    UTCOffsetDST = "+01:00",
                    AbbreviationSTD = "WET",
                    AbbreviationDST = "WEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Atlantic/Madeira",
                    CountryISO31661Alpha2Code = "PT",
                    DisplayName = "Atlantic / Madeira",
                    UTCOffsetSTD = "+00:00",
                    UTCOffsetDST = "+01:00",
                    AbbreviationSTD = "WET",
                    AbbreviationDST = "WEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Atlantic/South_Georgia",
                    CountryISO31661Alpha2Code = "GS",
                    DisplayName = "Atlantic / South Georgia",
                    UTCOffsetSTD = "-02:00",
                    AbbreviationSTD = "-02",
                },
                new Timezone
                {
                    IANAIdentifier = "Atlantic/Stanley",
                    CountryISO31661Alpha2Code = "FK",
                    DisplayName = "Atlantic / Stanley",
                    UTCOffsetSTD = "-03:00",
                    AbbreviationSTD = "-03",
                },

                // =============
                // Australia
                // =============
                new Timezone
                {
                    IANAIdentifier = "Australia/Adelaide",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Adelaide",
                    UTCOffsetSTD = "+09:30",
                    UTCOffsetDST = "+10:30",
                    AbbreviationSTD = "ACST",
                    AbbreviationDST = "ACDT",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Brisbane",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Brisbane",
                    UTCOffsetSTD = "+10:00",
                    AbbreviationSTD = "AEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Broken_Hill",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Broken Hill",
                    UTCOffsetSTD = "+09:30",
                    UTCOffsetDST = "+10:30",
                    AbbreviationSTD = "ACST",
                    AbbreviationDST = "ACDT",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Darwin",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Darwin",
                    UTCOffsetSTD = "+09:30",
                    AbbreviationSTD = "ACST",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Eucla",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Eucla",
                    UTCOffsetSTD = "+08:45",
                    AbbreviationSTD = "+0845",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Hobart",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Hobart",
                    UTCOffsetSTD = "+10:00",
                    UTCOffsetDST = "+11:00",
                    AbbreviationSTD = "AEST",
                    AbbreviationDST = "AEDT",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Lindeman",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Lindeman",
                    UTCOffsetSTD = "+10:00",
                    AbbreviationSTD = "AEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Lord_Howe",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Lord Howe",
                    UTCOffsetSTD = "+10:30",
                    UTCOffsetDST = "+11:00",
                    AbbreviationSTD = "+1030",
                    AbbreviationDST = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Melbourne",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Melbourne",
                    UTCOffsetSTD = "+10:00",
                    UTCOffsetDST = "+11:00",
                    AbbreviationSTD = "AEST",
                    AbbreviationDST = "AEDT",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Perth",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Perth",
                    UTCOffsetSTD = "+08:00",
                    AbbreviationSTD = "AWST",
                },
                new Timezone
                {
                    IANAIdentifier = "Australia/Sydney",
                    CountryISO31661Alpha2Code = "AU",
                    DisplayName = "Australia / Sydney",
                    UTCOffsetSTD = "+10:00",
                    UTCOffsetDST = "+11:00",
                    AbbreviationSTD = "AEST",
                    AbbreviationDST = "AEDT",
                },

                // =============
                // Europe
                // =============
                new Timezone
                {
                    IANAIdentifier = "Europe/Andorra",
                    CountryISO31661Alpha2Code = "AD",
                    DisplayName = "Europe / Andorra",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Astrakhan",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Astrakhan",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Athens",
                    CountryISO31661Alpha2Code = "GR",
                    DisplayName = "Europe / Athens",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Belgrade",
                    CountryISO31661Alpha2Code = "RS",
                    DisplayName = "Europe / Belgrade",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Berlin",
                    CountryISO31661Alpha2Code = "DE",
                    DisplayName = "Europe / Berlin",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Brussels",
                    CountryISO31661Alpha2Code = "BE",
                    DisplayName = "Europe / Brussels",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Bucharest",
                    CountryISO31661Alpha2Code = "RO",
                    DisplayName = "Europe / Bucharest",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Budapest",
                    CountryISO31661Alpha2Code = "HU",
                    DisplayName = "Europe / Budapest",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Chisinau",
                    CountryISO31661Alpha2Code = "MD",
                    DisplayName = "Europe / Chisinau",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Dublin",
                    CountryISO31661Alpha2Code = "IE",
                    DisplayName = "Europe / Dublin",
                    UTCOffsetSTD = "+00:00",
                    UTCOffsetDST = "+01:00",
                    AbbreviationSTD = "GMT",
                    AbbreviationDST = "IST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Gibraltar",
                    CountryISO31661Alpha2Code = "GI",
                    DisplayName = "Europe / Gibraltar",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Helsinki",
                    CountryISO31661Alpha2Code = "FI",
                    DisplayName = "Europe / Helsinki",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Istanbul",
                    CountryISO31661Alpha2Code = "TR",
                    DisplayName = "Europe / Istanbul",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "+03",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Kaliningrad",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Kaliningrad",
                    UTCOffsetSTD = "+02:00",
                    AbbreviationSTD = "EET",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Kirov",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Kirov",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "MSK",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Kyiv",
                    CountryISO31661Alpha2Code = "UA",
                    DisplayName = "Europe / Kyiv",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Lisbon",
                    CountryISO31661Alpha2Code = "PT",
                    DisplayName = "Europe / Lisbon",
                    UTCOffsetSTD = "+00:00",
                    UTCOffsetDST = "+01:00",
                    AbbreviationSTD = "WET",
                    AbbreviationDST = "WEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/London",
                    CountryISO31661Alpha2Code = "GB",
                    DisplayName = "Europe / London",
                    UTCOffsetSTD = "+00:00",
                    UTCOffsetDST = "+01:00",
                    AbbreviationSTD = "GMT",
                    AbbreviationDST = "BST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Madrid",
                    CountryISO31661Alpha2Code = "ES",
                    DisplayName = "Europe / Madrid",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Malta",
                    CountryISO31661Alpha2Code = "MT",
                    DisplayName = "Europe / Malta",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Minsk",
                    CountryISO31661Alpha2Code = "BY",
                    DisplayName = "Europe / Minsk",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "+03",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Moscow",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Moscow",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "MSK",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Paris",
                    CountryISO31661Alpha2Code = "FR",
                    DisplayName = "Europe / Paris",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Prague",
                    CountryISO31661Alpha2Code = "CZ",
                    DisplayName = "Europe / Prague",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Riga",
                    CountryISO31661Alpha2Code = "LV",
                    DisplayName = "Europe / Riga",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Rome",
                    CountryISO31661Alpha2Code = "IT",
                    DisplayName = "Europe / Rome",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Samara",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Samara",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Saratov",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Saratov",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Simferopol",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Simferopol",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "MSK",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Sofia",
                    CountryISO31661Alpha2Code = "BG",
                    DisplayName = "Europe / Sofia",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Tallinn",
                    CountryISO31661Alpha2Code = "EE",
                    DisplayName = "Europe / Tallinn",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Tirane",
                    CountryISO31661Alpha2Code = "AL",
                    DisplayName = "Europe / Tirane",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Ulyanovsk",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Ulyanovsk",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Vienna",
                    CountryISO31661Alpha2Code = "AT",
                    DisplayName = "Europe / Vienna",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Vilnius",
                    CountryISO31661Alpha2Code = "LT",
                    DisplayName = "Europe / Vilnius",
                    UTCOffsetSTD = "+02:00",
                    UTCOffsetDST = "+03:00",
                    AbbreviationSTD = "EET",
                    AbbreviationDST = "EEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Volgograd",
                    CountryISO31661Alpha2Code = "RU",
                    DisplayName = "Europe / Volgograd",
                    UTCOffsetSTD = "+03:00",
                    AbbreviationSTD = "MSK",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Warsaw",
                    CountryISO31661Alpha2Code = "PL",
                    DisplayName = "Europe / Warsaw",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },
                new Timezone
                {
                    IANAIdentifier = "Europe/Zurich",
                    CountryISO31661Alpha2Code = "CH",
                    DisplayName = "Europe / Zurich",
                    UTCOffsetSTD = "+01:00",
                    UTCOffsetDST = "+02:00",
                    AbbreviationSTD = "CET",
                    AbbreviationDST = "CEST",
                },

                // =============
                // Indian
                // =============
                new Timezone
                {
                    IANAIdentifier = "Indian/Chagos",
                    CountryISO31661Alpha2Code = "IO",
                    DisplayName = "Indian / Chagos",
                    UTCOffsetSTD = "+06:00",
                    AbbreviationSTD = "+06",
                },
                new Timezone
                {
                    IANAIdentifier = "Indian/Maldives",
                    CountryISO31661Alpha2Code = "MV",
                    DisplayName = "Indian / Maldives",
                    UTCOffsetSTD = "+05:00",
                    AbbreviationSTD = "+05",
                },
                new Timezone
                {
                    IANAIdentifier = "Indian/Mauritius",
                    CountryISO31661Alpha2Code = "MU",
                    DisplayName = "Indian / Mauritius",
                    UTCOffsetSTD = "+04:00",
                    AbbreviationSTD = "+04",
                },

                // =============
                // Pacific
                // =============
                new Timezone
                {
                    IANAIdentifier = "Pacific/Apia",
                    CountryISO31661Alpha2Code = "WS",
                    DisplayName = "Pacific / Apia",
                    UTCOffsetSTD = "+13:00",
                    AbbreviationSTD = "+13",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Auckland",
                    CountryISO31661Alpha2Code = "NZ",
                    DisplayName = "Pacific / Auckland",
                    UTCOffsetSTD = "+12:00",
                    UTCOffsetDST = "+13:00",
                    AbbreviationSTD = "NZST",
                    AbbreviationDST = "NZDT",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Bougainville",
                    CountryISO31661Alpha2Code = "PG",
                    DisplayName = "Pacific / Bougainville",
                    UTCOffsetSTD = "+11:00",
                    AbbreviationSTD = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Chatham",
                    CountryISO31661Alpha2Code = "NZ",
                    DisplayName = "Pacific / Chatham",
                    UTCOffsetSTD = "+12:45",
                    UTCOffsetDST = "+13:45",
                    AbbreviationSTD = "+1245",
                    AbbreviationDST = "+1345",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Easter",
                    CountryISO31661Alpha2Code = "CL",
                    DisplayName = "Pacific / Easter",
                    UTCOffsetSTD = "-06:00",
                    UTCOffsetDST = "-05:00",
                    AbbreviationSTD = "-06",
                    AbbreviationDST = "-05",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Efate",
                    CountryISO31661Alpha2Code = "VU",
                    DisplayName = "Pacific / Efate",
                    UTCOffsetSTD = "+11:00",
                    AbbreviationSTD = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Fakaofo",
                    CountryISO31661Alpha2Code = "TK",
                    DisplayName = "Pacific / Fakaofo",
                    UTCOffsetSTD = "+13:00",
                    AbbreviationSTD = "+13",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Fiji",
                    CountryISO31661Alpha2Code = "FJ",
                    DisplayName = "Pacific / Fiji",
                    UTCOffsetSTD = "+12:00",
                    AbbreviationSTD = "+12",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Galapagos",
                    CountryISO31661Alpha2Code = "EC",
                    DisplayName = "Pacific / Galapagos",
                    UTCOffsetSTD = "-06:00",
                    AbbreviationSTD = "-06",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Gambier",
                    CountryISO31661Alpha2Code = "PF",
                    DisplayName = "Pacific / Gambier",
                    UTCOffsetSTD = "-09:00",
                    AbbreviationSTD = "-09",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Guadalcanal",
                    CountryISO31661Alpha2Code = "SB",
                    DisplayName = "Pacific / Guadalcanal",
                    UTCOffsetSTD = "+11:00",
                    AbbreviationSTD = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Guam",
                    CountryISO31661Alpha2Code = "GU",
                    DisplayName = "Pacific / Guam",
                    UTCOffsetSTD = "+10:00",
                    AbbreviationSTD = "ChST",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Honolulu",
                    CountryISO31661Alpha2Code = "US",
                    DisplayName = "Pacific / Honolulu",
                    UTCOffsetSTD = "-10:00",
                    AbbreviationSTD = "HST",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Kanton",
                    CountryISO31661Alpha2Code = "KI",
                    DisplayName = "Pacific / Kanton",
                    UTCOffsetSTD = "+13:00",
                    AbbreviationSTD = "+13",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Kiritimati",
                    CountryISO31661Alpha2Code = "KI",
                    DisplayName = "Pacific / Kiritimati",
                    UTCOffsetSTD = "+14:00",
                    AbbreviationSTD = "+14",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Kosrae",
                    CountryISO31661Alpha2Code = "FM",
                    DisplayName = "Pacific / Kosrae",
                    UTCOffsetSTD = "+11:00",
                    AbbreviationSTD = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Kwajalein",
                    CountryISO31661Alpha2Code = "MH",
                    DisplayName = "Pacific / Kwajalein",
                    UTCOffsetSTD = "+12:00",
                    AbbreviationSTD = "+12",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Marquesas",
                    CountryISO31661Alpha2Code = "PF",
                    DisplayName = "Pacific / Marquesas",
                    UTCOffsetSTD = "-09:30",
                    AbbreviationSTD = "-0930",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Nauru",
                    CountryISO31661Alpha2Code = "NR",
                    DisplayName = "Pacific / Nauru",
                    UTCOffsetSTD = "+12:00",
                    AbbreviationSTD = "+12",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Niue",
                    CountryISO31661Alpha2Code = "NU",
                    DisplayName = "Pacific / Niue",
                    UTCOffsetSTD = "-11:00",
                    AbbreviationSTD = "-11",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Norfolk",
                    CountryISO31661Alpha2Code = "NF",
                    DisplayName = "Pacific / Norfolk",
                    UTCOffsetSTD = "+11:00",
                    UTCOffsetDST = "+12:00",
                    AbbreviationSTD = "+11",
                    AbbreviationDST = "+12",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Noumea",
                    CountryISO31661Alpha2Code = "NC",
                    DisplayName = "Pacific / Noumea",
                    UTCOffsetSTD = "+11:00",
                    AbbreviationSTD = "+11",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Pago_Pago",
                    CountryISO31661Alpha2Code = "AS",
                    DisplayName = "Pacific / Pago Pago",
                    UTCOffsetSTD = "-11:00",
                    AbbreviationSTD = "SST",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Palau",
                    CountryISO31661Alpha2Code = "PW",
                    DisplayName = "Pacific / Palau",
                    UTCOffsetSTD = "+09:00",
                    AbbreviationSTD = "+09",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Pitcairn",
                    CountryISO31661Alpha2Code = "PN",
                    DisplayName = "Pacific / Pitcairn",
                    UTCOffsetSTD = "-08:00",
                    AbbreviationSTD = "-08",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Port_Moresby",
                    CountryISO31661Alpha2Code = "PG",
                    DisplayName = "Pacific / Port Moresby",
                    UTCOffsetSTD = "+10:00",
                    AbbreviationSTD = "+10",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Rarotonga",
                    CountryISO31661Alpha2Code = "CK",
                    DisplayName = "Pacific / Rarotonga",
                    UTCOffsetSTD = "-10:00",
                    AbbreviationSTD = "-10",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Tarawa",
                    CountryISO31661Alpha2Code = "KI",
                    DisplayName = "Pacific / Tarawa",
                    UTCOffsetSTD = "+12:00",
                    AbbreviationSTD = "+12",
                },
                new Timezone
                {
                    IANAIdentifier = "Pacific/Tongatapu",
                    CountryISO31661Alpha2Code = "TO",
                    DisplayName = "Pacific / Tongatapu",
                    UTCOffsetSTD = "+13:00",
                    AbbreviationSTD = "+13",
                },
            ]);
        }
    }
}
