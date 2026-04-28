// -----------------------------------------------------------------------
// <copyright file="CountryCurrencySeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding country-currency relationships.
/// </summary>
public static class CountryCurrencySeeding
{
    /// <summary>
    /// Seeds the country_currencies join table.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the country_currencies join table.
        /// </summary>
        public void SeedCountryCurrencies()
        {
            modelBuilder.Entity("country_currencies").HasData([

                // =====================
                // USD Countries
                // =====================

                // United States
                new
                {
                    country_iso_3166_1_alpha_2_code = "US",
                    currency_iso_4217_alpha_code = "USD",
                },

                // American Samoa
                new
                {
                    country_iso_3166_1_alpha_2_code = "AS",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Bonaire, Sint Eustatius and Saba
                new
                {
                    country_iso_3166_1_alpha_2_code = "BQ",
                    currency_iso_4217_alpha_code = "USD",
                },

                // British Indian Ocean Territory
                new
                {
                    country_iso_3166_1_alpha_2_code = "IO",
                    currency_iso_4217_alpha_code = "USD",
                },

                // British Virgin Islands
                new
                {
                    country_iso_3166_1_alpha_2_code = "VG",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Ecuador
                new
                {
                    country_iso_3166_1_alpha_2_code = "EC",
                    currency_iso_4217_alpha_code = "USD",
                },

                // El Salvador
                new
                {
                    country_iso_3166_1_alpha_2_code = "SV",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Guam
                new
                {
                    country_iso_3166_1_alpha_2_code = "GU",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Marshall Islands
                new
                {
                    country_iso_3166_1_alpha_2_code = "MH",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Micronesia
                new
                {
                    country_iso_3166_1_alpha_2_code = "FM",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Northern Mariana Islands
                new
                {
                    country_iso_3166_1_alpha_2_code = "MP",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Palau
                new
                {
                    country_iso_3166_1_alpha_2_code = "PW",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Panama
                new
                {
                    country_iso_3166_1_alpha_2_code = "PA",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Puerto Rico
                new
                {
                    country_iso_3166_1_alpha_2_code = "PR",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Timor-Leste
                new
                {
                    country_iso_3166_1_alpha_2_code = "TL",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Turks and Caicos Islands
                new
                {
                    country_iso_3166_1_alpha_2_code = "TC",
                    currency_iso_4217_alpha_code = "USD",
                },

                // United States Minor Outlying Islands
                new
                {
                    country_iso_3166_1_alpha_2_code = "UM",
                    currency_iso_4217_alpha_code = "USD",
                },

                // U.S. Virgin Islands
                new
                {
                    country_iso_3166_1_alpha_2_code = "VI",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Zimbabwe
                new
                {
                    country_iso_3166_1_alpha_2_code = "ZW",
                    currency_iso_4217_alpha_code = "USD",
                },

                // =====================
                // USD Secondary
                // =====================

                // Canada
                new
                {
                    country_iso_3166_1_alpha_2_code = "CA",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Bahamas
                new
                {
                    country_iso_3166_1_alpha_2_code = "BS",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Barbados
                new
                {
                    country_iso_3166_1_alpha_2_code = "BB",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Belize
                new
                {
                    country_iso_3166_1_alpha_2_code = "BZ",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Bermuda
                new
                {
                    country_iso_3166_1_alpha_2_code = "BM",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Cayman Islands
                new
                {
                    country_iso_3166_1_alpha_2_code = "KY",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Costa Rica
                new
                {
                    country_iso_3166_1_alpha_2_code = "CR",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Guatemala
                new
                {
                    country_iso_3166_1_alpha_2_code = "GT",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Honduras
                new
                {
                    country_iso_3166_1_alpha_2_code = "HN",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Jamaica
                new
                {
                    country_iso_3166_1_alpha_2_code = "JM",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Mexico
                new
                {
                    country_iso_3166_1_alpha_2_code = "MX",
                    currency_iso_4217_alpha_code = "USD",
                },

                // Nicaragua
                new
                {
                    country_iso_3166_1_alpha_2_code = "NI",
                    currency_iso_4217_alpha_code = "USD",
                },

                // =====================
                // CAD Countries
                // =====================

                // Canada
                new
                {
                    country_iso_3166_1_alpha_2_code = "CA",
                    currency_iso_4217_alpha_code = "CAD",
                },

                // =====================
                // GBP Countries
                // =====================

                // United Kingdom
                new
                {
                    country_iso_3166_1_alpha_2_code = "GB",
                    currency_iso_4217_alpha_code = "GBP",
                },

                // Guernsey
                new
                {
                    country_iso_3166_1_alpha_2_code = "GG",
                    currency_iso_4217_alpha_code = "GBP",
                },

                // Isle of Man
                new
                {
                    country_iso_3166_1_alpha_2_code = "IM",
                    currency_iso_4217_alpha_code = "GBP",
                },

                // Jersey
                new
                {
                    country_iso_3166_1_alpha_2_code = "JE",
                    currency_iso_4217_alpha_code = "GBP",
                },

                // =====================
                // GBP Secondary
                // =====================

                // Ireland
                new
                {
                    country_iso_3166_1_alpha_2_code = "IE",
                    currency_iso_4217_alpha_code = "GBP",
                },

                // =====================
                // EUR Countries
                // =====================

                // Åland Islands
                new
                {
                    country_iso_3166_1_alpha_2_code = "AX",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Andorra
                new
                {
                    country_iso_3166_1_alpha_2_code = "AD",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Austria
                new
                {
                    country_iso_3166_1_alpha_2_code = "AT",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Belgium
                new
                {
                    country_iso_3166_1_alpha_2_code = "BE",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Croatia
                new
                {
                    country_iso_3166_1_alpha_2_code = "HR",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Cyprus
                new
                {
                    country_iso_3166_1_alpha_2_code = "CY",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Estonia
                new
                {
                    country_iso_3166_1_alpha_2_code = "EE",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Finland
                new
                {
                    country_iso_3166_1_alpha_2_code = "FI",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // France
                new
                {
                    country_iso_3166_1_alpha_2_code = "FR",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // French Guiana
                new
                {
                    country_iso_3166_1_alpha_2_code = "GF",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // French Southern Territories
                new
                {
                    country_iso_3166_1_alpha_2_code = "TF",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Germany
                new
                {
                    country_iso_3166_1_alpha_2_code = "DE",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Greece
                new
                {
                    country_iso_3166_1_alpha_2_code = "GR",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Guadeloupe
                new
                {
                    country_iso_3166_1_alpha_2_code = "GP",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Vatican City
                new
                {
                    country_iso_3166_1_alpha_2_code = "VA",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Ireland
                new
                {
                    country_iso_3166_1_alpha_2_code = "IE",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Italy
                new
                {
                    country_iso_3166_1_alpha_2_code = "IT",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Latvia
                new
                {
                    country_iso_3166_1_alpha_2_code = "LV",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Lithuania
                new
                {
                    country_iso_3166_1_alpha_2_code = "LT",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Luxembourg
                new
                {
                    country_iso_3166_1_alpha_2_code = "LU",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Malta
                new
                {
                    country_iso_3166_1_alpha_2_code = "MT",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Martinique
                new
                {
                    country_iso_3166_1_alpha_2_code = "MQ",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Mayotte
                new
                {
                    country_iso_3166_1_alpha_2_code = "YT",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Monaco
                new
                {
                    country_iso_3166_1_alpha_2_code = "MC",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Montenegro
                new
                {
                    country_iso_3166_1_alpha_2_code = "ME",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Netherlands
                new
                {
                    country_iso_3166_1_alpha_2_code = "NL",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Portugal
                new
                {
                    country_iso_3166_1_alpha_2_code = "PT",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Réunion
                new
                {
                    country_iso_3166_1_alpha_2_code = "RE",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Saint Barthélemy
                new
                {
                    country_iso_3166_1_alpha_2_code = "BL",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Saint Martin
                new
                {
                    country_iso_3166_1_alpha_2_code = "MF",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Saint Pierre and Miquelon
                new
                {
                    country_iso_3166_1_alpha_2_code = "PM",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // San Marino
                new
                {
                    country_iso_3166_1_alpha_2_code = "SM",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Slovakia
                new
                {
                    country_iso_3166_1_alpha_2_code = "SK",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Slovenia
                new
                {
                    country_iso_3166_1_alpha_2_code = "SI",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Spain
                new
                {
                    country_iso_3166_1_alpha_2_code = "ES",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // =====================
                // EUR Secondary
                // =====================

                // Switzerland
                new
                {
                    country_iso_3166_1_alpha_2_code = "CH",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Czechia
                new
                {
                    country_iso_3166_1_alpha_2_code = "CZ",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Denmark
                new
                {
                    country_iso_3166_1_alpha_2_code = "DK",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Hungary
                new
                {
                    country_iso_3166_1_alpha_2_code = "HU",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Poland
                new
                {
                    country_iso_3166_1_alpha_2_code = "PL",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Romania
                new
                {
                    country_iso_3166_1_alpha_2_code = "RO",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Sweden
                new
                {
                    country_iso_3166_1_alpha_2_code = "SE",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Bulgaria
                new
                {
                    country_iso_3166_1_alpha_2_code = "BG",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Albania
                new
                {
                    country_iso_3166_1_alpha_2_code = "AL",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // North Macedonia
                new
                {
                    country_iso_3166_1_alpha_2_code = "MK",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Serbia
                new
                {
                    country_iso_3166_1_alpha_2_code = "RS",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Bosnia and Herzegovina
                new
                {
                    country_iso_3166_1_alpha_2_code = "BA",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Morocco
                new
                {
                    country_iso_3166_1_alpha_2_code = "MA",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Türkiye
                new
                {
                    country_iso_3166_1_alpha_2_code = "TR",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // Liechtenstein
                new
                {
                    country_iso_3166_1_alpha_2_code = "LI",
                    currency_iso_4217_alpha_code = "EUR",
                },

                // =====================
                // JPY Countries
                // =====================

                // Japan
                new
                {
                    country_iso_3166_1_alpha_2_code = "JP",
                    currency_iso_4217_alpha_code = "JPY",
                }
            ]);
        }
    }
}
