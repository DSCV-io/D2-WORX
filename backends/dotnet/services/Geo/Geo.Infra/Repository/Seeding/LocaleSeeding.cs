// -----------------------------------------------------------------------
// <copyright file="LocaleSeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding locale data.
/// </summary>
public static class LocaleSeeding
{
    /// <summary>
    /// Seeds the Locale entity.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the Locale entity.
        /// </summary>
        public void SeedLocales()
        {
            modelBuilder.Entity<Locale>().HasData([

                // =====================
                // English Locales (en)
                // =====================

                // United States
                new Locale
                {
                    IETFBCP47Tag = "en-US",
                    Name = "English (United States)",
                    Endonym = "English (United States)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "US",
                },

                // Canada
                new Locale
                {
                    IETFBCP47Tag = "en-CA",
                    Name = "English (Canada)",
                    Endonym = "English (Canada)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "CA",
                },

                // United Kingdom
                new Locale
                {
                    IETFBCP47Tag = "en-GB",
                    Name = "English (United Kingdom)",
                    Endonym = "English (United Kingdom)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "GB",
                },

                // Australia
                new Locale
                {
                    IETFBCP47Tag = "en-AU",
                    Name = "English (Australia)",
                    Endonym = "English (Australia)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "AU",
                },

                // New Zealand
                new Locale
                {
                    IETFBCP47Tag = "en-NZ",
                    Name = "English (New Zealand)",
                    Endonym = "English (New Zealand)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "NZ",
                },

                // Ireland
                new Locale
                {
                    IETFBCP47Tag = "en-IE",
                    Name = "English (Ireland)",
                    Endonym = "English (Ireland)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "IE",
                },

                // South Africa
                new Locale
                {
                    IETFBCP47Tag = "en-ZA",
                    Name = "English (South Africa)",
                    Endonym = "English (South Africa)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "ZA",
                },

                // Singapore
                new Locale
                {
                    IETFBCP47Tag = "en-SG",
                    Name = "English (Singapore)",
                    Endonym = "English (Singapore)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "SG",
                },

                // Jamaica
                new Locale
                {
                    IETFBCP47Tag = "en-JM",
                    Name = "English (Jamaica)",
                    Endonym = "English (Jamaica)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "JM",
                },

                // Philippines
                new Locale
                {
                    IETFBCP47Tag = "en-PH",
                    Name = "English (Philippines)",
                    Endonym = "English (Philippines)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "PH",
                },

                // American Samoa
                new Locale
                {
                    IETFBCP47Tag = "en-AS",
                    Name = "English (American Samoa)",
                    Endonym = "English (American Samoa)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "AS",
                },

                // Anguilla
                new Locale
                {
                    IETFBCP47Tag = "en-AI",
                    Name = "English (Anguilla)",
                    Endonym = "English (Anguilla)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "AI",
                },

                // Antigua and Barbuda
                new Locale
                {
                    IETFBCP47Tag = "en-AG",
                    Name = "English (Antigua and Barbuda)",
                    Endonym = "English (Antigua and Barbuda)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "AG",
                },

                // Bahamas
                new Locale
                {
                    IETFBCP47Tag = "en-BS",
                    Name = "English (Bahamas)",
                    Endonym = "English (Bahamas)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "BS",
                },

                // Barbados
                new Locale
                {
                    IETFBCP47Tag = "en-BB",
                    Name = "English (Barbados)",
                    Endonym = "English (Barbados)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "BB",
                },

                // Belize
                new Locale
                {
                    IETFBCP47Tag = "en-BZ",
                    Name = "English (Belize)",
                    Endonym = "English (Belize)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "BZ",
                },

                // Bermuda
                new Locale
                {
                    IETFBCP47Tag = "en-BM",
                    Name = "English (Bermuda)",
                    Endonym = "English (Bermuda)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "BM",
                },

                // Botswana
                new Locale
                {
                    IETFBCP47Tag = "en-BW",
                    Name = "English (Botswana)",
                    Endonym = "English (Botswana)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "BW",
                },

                // British Indian Ocean Territory
                new Locale
                {
                    IETFBCP47Tag = "en-IO",
                    Name = "English (British Indian Ocean Territory)",
                    Endonym = "English (British Indian Ocean Territory)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "IO",
                },

                // British Virgin Islands
                new Locale
                {
                    IETFBCP47Tag = "en-VG",
                    Name = "English (British Virgin Islands)",
                    Endonym = "English (British Virgin Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "VG",
                },

                // Cayman Islands
                new Locale
                {
                    IETFBCP47Tag = "en-KY",
                    Name = "English (Cayman Islands)",
                    Endonym = "English (Cayman Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "KY",
                },

                // Christmas Island
                new Locale
                {
                    IETFBCP47Tag = "en-CX",
                    Name = "English (Christmas Island)",
                    Endonym = "English (Christmas Island)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "CX",
                },

                // Cocos (Keeling) Islands
                new Locale
                {
                    IETFBCP47Tag = "en-CC",
                    Name = "English (Cocos (Keeling) Islands)",
                    Endonym = "English (Cocos (Keeling) Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "CC",
                },

                // Cook Islands
                new Locale
                {
                    IETFBCP47Tag = "en-CK",
                    Name = "English (Cook Islands)",
                    Endonym = "English (Cook Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "CK",
                },

                // Dominica
                new Locale
                {
                    IETFBCP47Tag = "en-DM",
                    Name = "English (Dominica)",
                    Endonym = "English (Dominica)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "DM",
                },

                // Eswatini
                new Locale
                {
                    IETFBCP47Tag = "en-SZ",
                    Name = "English (Eswatini)",
                    Endonym = "English (Eswatini)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "SZ",
                },

                // Falkland Islands
                new Locale
                {
                    IETFBCP47Tag = "en-FK",
                    Name = "English (Falkland Islands)",
                    Endonym = "English (Falkland Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "FK",
                },

                // Fiji
                new Locale
                {
                    IETFBCP47Tag = "en-FJ",
                    Name = "English (Fiji)",
                    Endonym = "English (Fiji)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "FJ",
                },

                // Gambia
                new Locale
                {
                    IETFBCP47Tag = "en-GM",
                    Name = "English (Gambia)",
                    Endonym = "English (Gambia)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "GM",
                },

                // Ghana
                new Locale
                {
                    IETFBCP47Tag = "en-GH",
                    Name = "English (Ghana)",
                    Endonym = "English (Ghana)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "GH",
                },

                // Gibraltar
                new Locale
                {
                    IETFBCP47Tag = "en-GI",
                    Name = "English (Gibraltar)",
                    Endonym = "English (Gibraltar)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "GI",
                },

                // Grenada
                new Locale
                {
                    IETFBCP47Tag = "en-GD",
                    Name = "English (Grenada)",
                    Endonym = "English (Grenada)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "GD",
                },

                // Guam
                new Locale
                {
                    IETFBCP47Tag = "en-GU",
                    Name = "English (Guam)",
                    Endonym = "English (Guam)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "GU",
                },

                // Guernsey
                new Locale
                {
                    IETFBCP47Tag = "en-GG",
                    Name = "English (Guernsey)",
                    Endonym = "English (Guernsey)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "GG",
                },

                // Guyana
                new Locale
                {
                    IETFBCP47Tag = "en-GY",
                    Name = "English (Guyana)",
                    Endonym = "English (Guyana)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "GY",
                },

                // Isle of Man
                new Locale
                {
                    IETFBCP47Tag = "en-IM",
                    Name = "English (Isle of Man)",
                    Endonym = "English (Isle of Man)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "IM",
                },

                // Jersey
                new Locale
                {
                    IETFBCP47Tag = "en-JE",
                    Name = "English (Jersey)",
                    Endonym = "English (Jersey)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "JE",
                },

                // Kiribati
                new Locale
                {
                    IETFBCP47Tag = "en-KI",
                    Name = "English (Kiribati)",
                    Endonym = "English (Kiribati)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "KI",
                },

                // Lesotho
                new Locale
                {
                    IETFBCP47Tag = "en-LS",
                    Name = "English (Lesotho)",
                    Endonym = "English (Lesotho)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "LS",
                },

                // Liberia
                new Locale
                {
                    IETFBCP47Tag = "en-LR",
                    Name = "English (Liberia)",
                    Endonym = "English (Liberia)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "LR",
                },

                // Malawi
                new Locale
                {
                    IETFBCP47Tag = "en-MW",
                    Name = "English (Malawi)",
                    Endonym = "English (Malawi)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "MW",
                },

                // Marshall Islands
                new Locale
                {
                    IETFBCP47Tag = "en-MH",
                    Name = "English (Marshall Islands)",
                    Endonym = "English (Marshall Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "MH",
                },

                // Mauritius
                new Locale
                {
                    IETFBCP47Tag = "en-MU",
                    Name = "English (Mauritius)",
                    Endonym = "English (Mauritius)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "MU",
                },

                // Micronesia
                new Locale
                {
                    IETFBCP47Tag = "en-FM",
                    Name = "English (Micronesia)",
                    Endonym = "English (Micronesia)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "FM",
                },

                // Montserrat
                new Locale
                {
                    IETFBCP47Tag = "en-MS",
                    Name = "English (Montserrat)",
                    Endonym = "English (Montserrat)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "MS",
                },

                // Namibia
                new Locale
                {
                    IETFBCP47Tag = "en-NA",
                    Name = "English (Namibia)",
                    Endonym = "English (Namibia)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "NA",
                },

                // Nauru
                new Locale
                {
                    IETFBCP47Tag = "en-NR",
                    Name = "English (Nauru)",
                    Endonym = "English (Nauru)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "NR",
                },

                // Nigeria
                new Locale
                {
                    IETFBCP47Tag = "en-NG",
                    Name = "English (Nigeria)",
                    Endonym = "English (Nigeria)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "NG",
                },

                // Niue
                new Locale
                {
                    IETFBCP47Tag = "en-NU",
                    Name = "English (Niue)",
                    Endonym = "English (Niue)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "NU",
                },

                // Norfolk Island
                new Locale
                {
                    IETFBCP47Tag = "en-NF",
                    Name = "English (Norfolk Island)",
                    Endonym = "English (Norfolk Island)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "NF",
                },

                // Northern Mariana Islands
                new Locale
                {
                    IETFBCP47Tag = "en-MP",
                    Name = "English (Northern Mariana Islands)",
                    Endonym = "English (Northern Mariana Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "MP",
                },

                // Palau
                new Locale
                {
                    IETFBCP47Tag = "en-PW",
                    Name = "English (Palau)",
                    Endonym = "English (Palau)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "PW",
                },

                // Papua New Guinea
                new Locale
                {
                    IETFBCP47Tag = "en-PG",
                    Name = "English (Papua New Guinea)",
                    Endonym = "English (Papua New Guinea)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "PG",
                },

                // Pitcairn Islands
                new Locale
                {
                    IETFBCP47Tag = "en-PN",
                    Name = "English (Pitcairn Islands)",
                    Endonym = "English (Pitcairn Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "PN",
                },

                // Saint Helena, Ascension and Tristan da Cunha
                new Locale
                {
                    IETFBCP47Tag = "en-SH",
                    Name = "English (Saint Helena)",
                    Endonym = "English (Saint Helena)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "SH",
                },

                // Saint Kitts and Nevis
                new Locale
                {
                    IETFBCP47Tag = "en-KN",
                    Name = "English (Saint Kitts and Nevis)",
                    Endonym = "English (Saint Kitts and Nevis)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "KN",
                },

                // Saint Lucia
                new Locale
                {
                    IETFBCP47Tag = "en-LC",
                    Name = "English (Saint Lucia)",
                    Endonym = "English (Saint Lucia)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "LC",
                },

                // Saint Vincent and the Grenadines
                new Locale
                {
                    IETFBCP47Tag = "en-VC",
                    Name = "English (Saint Vincent and the Grenadines)",
                    Endonym = "English (Saint Vincent and the Grenadines)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "VC",
                },

                // Seychelles
                new Locale
                {
                    IETFBCP47Tag = "en-SC",
                    Name = "English (Seychelles)",
                    Endonym = "English (Seychelles)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "SC",
                },

                // Sierra Leone
                new Locale
                {
                    IETFBCP47Tag = "en-SL",
                    Name = "English (Sierra Leone)",
                    Endonym = "English (Sierra Leone)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "SL",
                },

                // Solomon Islands
                new Locale
                {
                    IETFBCP47Tag = "en-SB",
                    Name = "English (Solomon Islands)",
                    Endonym = "English (Solomon Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "SB",
                },

                // South Sudan
                new Locale
                {
                    IETFBCP47Tag = "en-SS",
                    Name = "English (South Sudan)",
                    Endonym = "English (South Sudan)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "SS",
                },

                // Tokelau
                new Locale
                {
                    IETFBCP47Tag = "en-TK",
                    Name = "English (Tokelau)",
                    Endonym = "English (Tokelau)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "TK",
                },

                // Trinidad and Tobago
                new Locale
                {
                    IETFBCP47Tag = "en-TT",
                    Name = "English (Trinidad and Tobago)",
                    Endonym = "English (Trinidad and Tobago)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "TT",
                },

                // Turks and Caicos Islands
                new Locale
                {
                    IETFBCP47Tag = "en-TC",
                    Name = "English (Turks and Caicos Islands)",
                    Endonym = "English (Turks and Caicos Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "TC",
                },

                // Tuvalu
                new Locale
                {
                    IETFBCP47Tag = "en-TV",
                    Name = "English (Tuvalu)",
                    Endonym = "English (Tuvalu)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "TV",
                },

                // Uganda
                new Locale
                {
                    IETFBCP47Tag = "en-UG",
                    Name = "English (Uganda)",
                    Endonym = "English (Uganda)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "UG",
                },

                // United States Minor Outlying Islands
                new Locale
                {
                    IETFBCP47Tag = "en-UM",
                    Name = "English (U.S. Outlying Islands)",
                    Endonym = "English (U.S. Outlying Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "UM",
                },

                // U.S. Virgin Islands
                new Locale
                {
                    IETFBCP47Tag = "en-VI",
                    Name = "English (U.S. Virgin Islands)",
                    Endonym = "English (U.S. Virgin Islands)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "VI",
                },

                // Zambia
                new Locale
                {
                    IETFBCP47Tag = "en-ZM",
                    Name = "English (Zambia)",
                    Endonym = "English (Zambia)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "ZM",
                },

                // Zimbabwe
                new Locale
                {
                    IETFBCP47Tag = "en-ZW",
                    Name = "English (Zimbabwe)",
                    Endonym = "English (Zimbabwe)",
                    LanguageISO6391Code = "en",
                    CountryISO31661Alpha2Code = "ZW",
                },

                // =====================
                // French Locales (fr)
                // =====================

                // France
                new Locale
                {
                    IETFBCP47Tag = "fr-FR",
                    Name = "French (France)",
                    Endonym = "Français (France)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "FR",
                },

                // Canada (Secondary)
                new Locale
                {
                    IETFBCP47Tag = "fr-CA",
                    Name = "French (Canada)",
                    Endonym = "Français (Canada)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "CA",
                },

                // Belgium
                new Locale
                {
                    IETFBCP47Tag = "fr-BE",
                    Name = "French (Belgium)",
                    Endonym = "Français (Belgique)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "BE",
                },

                // Switzerland
                new Locale
                {
                    IETFBCP47Tag = "fr-CH",
                    Name = "French (Switzerland)",
                    Endonym = "Français (Suisse)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "CH",
                },

                // Monaco
                new Locale
                {
                    IETFBCP47Tag = "fr-MC",
                    Name = "French (Monaco)",
                    Endonym = "Français (Monaco)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "MC",
                },

                // Luxembourg
                new Locale
                {
                    IETFBCP47Tag = "fr-LU",
                    Name = "French (Luxembourg)",
                    Endonym = "Français (Luxembourg)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "LU",
                },

                // Benin
                new Locale
                {
                    IETFBCP47Tag = "fr-BJ",
                    Name = "French (Benin)",
                    Endonym = "Français (Bénin)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "BJ",
                },

                // Burkina Faso
                new Locale
                {
                    IETFBCP47Tag = "fr-BF",
                    Name = "French (Burkina Faso)",
                    Endonym = "Français (Burkina Faso)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "BF",
                },

                // Burundi
                new Locale
                {
                    IETFBCP47Tag = "fr-BI",
                    Name = "French (Burundi)",
                    Endonym = "Français (Burundi)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "BI",
                },

                // Cameroon
                new Locale
                {
                    IETFBCP47Tag = "fr-CM",
                    Name = "French (Cameroon)",
                    Endonym = "Français (Cameroun)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "CM",
                },

                // Central African Republic
                new Locale
                {
                    IETFBCP47Tag = "fr-CF",
                    Name = "French (Central African Republic)",
                    Endonym = "Français (République centrafricaine)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "CF",
                },

                // Chad
                new Locale
                {
                    IETFBCP47Tag = "fr-TD",
                    Name = "French (Chad)",
                    Endonym = "Français (Tchad)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "TD",
                },

                // Congo
                new Locale
                {
                    IETFBCP47Tag = "fr-CG",
                    Name = "French (Congo)",
                    Endonym = "Français (Congo)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "CG",
                },

                // DR Congo
                new Locale
                {
                    IETFBCP47Tag = "fr-CD",
                    Name = "French (DR Congo)",
                    Endonym = "Français (RD Congo)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "CD",
                },

                // Côte d'Ivoire
                new Locale
                {
                    IETFBCP47Tag = "fr-CI",
                    Name = "French (Côte d'Ivoire)",
                    Endonym = "Français (Côte d'Ivoire)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "CI",
                },

                // Djibouti
                new Locale
                {
                    IETFBCP47Tag = "fr-DJ",
                    Name = "French (Djibouti)",
                    Endonym = "Français (Djibouti)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "DJ",
                },

                // French Guiana
                new Locale
                {
                    IETFBCP47Tag = "fr-GF",
                    Name = "French (French Guiana)",
                    Endonym = "Français (Guyane française)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "GF",
                },

                // French Polynesia
                new Locale
                {
                    IETFBCP47Tag = "fr-PF",
                    Name = "French (French Polynesia)",
                    Endonym = "Français (Polynésie française)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "PF",
                },

                // Gabon
                new Locale
                {
                    IETFBCP47Tag = "fr-GA",
                    Name = "French (Gabon)",
                    Endonym = "Français (Gabon)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "GA",
                },

                // Guadeloupe
                new Locale
                {
                    IETFBCP47Tag = "fr-GP",
                    Name = "French (Guadeloupe)",
                    Endonym = "Français (Guadeloupe)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "GP",
                },

                // Guinea
                new Locale
                {
                    IETFBCP47Tag = "fr-GN",
                    Name = "French (Guinea)",
                    Endonym = "Français (Guinée)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "GN",
                },

                // Haiti
                new Locale
                {
                    IETFBCP47Tag = "fr-HT",
                    Name = "French (Haiti)",
                    Endonym = "Français (Haïti)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "HT",
                },

                // Mali
                new Locale
                {
                    IETFBCP47Tag = "fr-ML",
                    Name = "French (Mali)",
                    Endonym = "Français (Mali)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "ML",
                },

                // Martinique
                new Locale
                {
                    IETFBCP47Tag = "fr-MQ",
                    Name = "French (Martinique)",
                    Endonym = "Français (Martinique)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "MQ",
                },

                // Mayotte
                new Locale
                {
                    IETFBCP47Tag = "fr-YT",
                    Name = "French (Mayotte)",
                    Endonym = "Français (Mayotte)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "YT",
                },

                // New Caledonia
                new Locale
                {
                    IETFBCP47Tag = "fr-NC",
                    Name = "French (New Caledonia)",
                    Endonym = "Français (Nouvelle-Calédonie)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "NC",
                },

                // Niger
                new Locale
                {
                    IETFBCP47Tag = "fr-NE",
                    Name = "French (Niger)",
                    Endonym = "Français (Niger)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "NE",
                },

                // Réunion
                new Locale
                {
                    IETFBCP47Tag = "fr-RE",
                    Name = "French (Réunion)",
                    Endonym = "Français (La Réunion)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "RE",
                },

                // Saint Barthélemy
                new Locale
                {
                    IETFBCP47Tag = "fr-BL",
                    Name = "French (Saint Barthélemy)",
                    Endonym = "Français (Saint-Barthélemy)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "BL",
                },

                // Saint Martin
                new Locale
                {
                    IETFBCP47Tag = "fr-MF",
                    Name = "French (Saint Martin)",
                    Endonym = "Français (Saint-Martin)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "MF",
                },

                // Saint Pierre and Miquelon
                new Locale
                {
                    IETFBCP47Tag = "fr-PM",
                    Name = "French (Saint Pierre and Miquelon)",
                    Endonym = "Français (Saint-Pierre-et-Miquelon)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "PM",
                },

                // Senegal
                new Locale
                {
                    IETFBCP47Tag = "fr-SN",
                    Name = "French (Senegal)",
                    Endonym = "Français (Sénégal)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "SN",
                },

                // Togo
                new Locale
                {
                    IETFBCP47Tag = "fr-TG",
                    Name = "French (Togo)",
                    Endonym = "Français (Togo)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "TG",
                },

                // Wallis and Futuna
                new Locale
                {
                    IETFBCP47Tag = "fr-WF",
                    Name = "French (Wallis and Futuna)",
                    Endonym = "Français (Wallis-et-Futuna)",
                    LanguageISO6391Code = "fr",
                    CountryISO31661Alpha2Code = "WF",
                },

                // =====================
                // Spanish Locales (es)
                // =====================

                // Spain
                new Locale
                {
                    IETFBCP47Tag = "es-ES",
                    Name = "Spanish (Spain)",
                    Endonym = "Español (España)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "ES",
                },

                // United States (Secondary)
                new Locale
                {
                    IETFBCP47Tag = "es-US",
                    Name = "Spanish (United States)",
                    Endonym = "Español (Estados Unidos)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "US",
                },

                // Mexico
                new Locale
                {
                    IETFBCP47Tag = "es-MX",
                    Name = "Spanish (Mexico)",
                    Endonym = "Español (México)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "MX",
                },

                // Argentina
                new Locale
                {
                    IETFBCP47Tag = "es-AR",
                    Name = "Spanish (Argentina)",
                    Endonym = "Español (Argentina)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "AR",
                },

                // Colombia
                new Locale
                {
                    IETFBCP47Tag = "es-CO",
                    Name = "Spanish (Colombia)",
                    Endonym = "Español (Colombia)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "CO",
                },

                // Chile
                new Locale
                {
                    IETFBCP47Tag = "es-CL",
                    Name = "Spanish (Chile)",
                    Endonym = "Español (Chile)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "CL",
                },

                // Peru
                new Locale
                {
                    IETFBCP47Tag = "es-PE",
                    Name = "Spanish (Peru)",
                    Endonym = "Español (Perú)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "PE",
                },

                // Venezuela
                new Locale
                {
                    IETFBCP47Tag = "es-VE",
                    Name = "Spanish (Venezuela)",
                    Endonym = "Español (Venezuela)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "VE",
                },

                // Bolivia
                new Locale
                {
                    IETFBCP47Tag = "es-BO",
                    Name = "Spanish (Bolivia)",
                    Endonym = "Español (Bolivia)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "BO",
                },

                // Costa Rica
                new Locale
                {
                    IETFBCP47Tag = "es-CR",
                    Name = "Spanish (Costa Rica)",
                    Endonym = "Español (Costa Rica)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "CR",
                },

                // Cuba
                new Locale
                {
                    IETFBCP47Tag = "es-CU",
                    Name = "Spanish (Cuba)",
                    Endonym = "Español (Cuba)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "CU",
                },

                // Dominican Republic
                new Locale
                {
                    IETFBCP47Tag = "es-DO",
                    Name = "Spanish (Dominican Republic)",
                    Endonym = "Español (República Dominicana)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "DO",
                },

                // Ecuador
                new Locale
                {
                    IETFBCP47Tag = "es-EC",
                    Name = "Spanish (Ecuador)",
                    Endonym = "Español (Ecuador)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "EC",
                },

                // El Salvador
                new Locale
                {
                    IETFBCP47Tag = "es-SV",
                    Name = "Spanish (El Salvador)",
                    Endonym = "Español (El Salvador)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "SV",
                },

                // Equatorial Guinea
                new Locale
                {
                    IETFBCP47Tag = "es-GQ",
                    Name = "Spanish (Equatorial Guinea)",
                    Endonym = "Español (Guinea Ecuatorial)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "GQ",
                },

                // Guatemala
                new Locale
                {
                    IETFBCP47Tag = "es-GT",
                    Name = "Spanish (Guatemala)",
                    Endonym = "Español (Guatemala)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "GT",
                },

                // Honduras
                new Locale
                {
                    IETFBCP47Tag = "es-HN",
                    Name = "Spanish (Honduras)",
                    Endonym = "Español (Honduras)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "HN",
                },

                // Nicaragua
                new Locale
                {
                    IETFBCP47Tag = "es-NI",
                    Name = "Spanish (Nicaragua)",
                    Endonym = "Español (Nicaragua)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "NI",
                },

                // Panama
                new Locale
                {
                    IETFBCP47Tag = "es-PA",
                    Name = "Spanish (Panama)",
                    Endonym = "Español (Panamá)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "PA",
                },

                // Paraguay
                new Locale
                {
                    IETFBCP47Tag = "es-PY",
                    Name = "Spanish (Paraguay)",
                    Endonym = "Español (Paraguay)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "PY",
                },

                // Puerto Rico
                new Locale
                {
                    IETFBCP47Tag = "es-PR",
                    Name = "Spanish (Puerto Rico)",
                    Endonym = "Español (Puerto Rico)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "PR",
                },

                // Uruguay
                new Locale
                {
                    IETFBCP47Tag = "es-UY",
                    Name = "Spanish (Uruguay)",
                    Endonym = "Español (Uruguay)",
                    LanguageISO6391Code = "es",
                    CountryISO31661Alpha2Code = "UY",
                },

                // =====================
                // German Locales (de)
                // =====================

                // Germany
                new Locale
                {
                    IETFBCP47Tag = "de-DE",
                    Name = "German (Germany)",
                    Endonym = "Deutsch (Deutschland)",
                    LanguageISO6391Code = "de",
                    CountryISO31661Alpha2Code = "DE",
                },

                // Austria
                new Locale
                {
                    IETFBCP47Tag = "de-AT",
                    Name = "German (Austria)",
                    Endonym = "Deutsch (Österreich)",
                    LanguageISO6391Code = "de",
                    CountryISO31661Alpha2Code = "AT",
                },

                // Switzerland
                new Locale
                {
                    IETFBCP47Tag = "de-CH",
                    Name = "German (Switzerland)",
                    Endonym = "Deutsch (Schweiz)",
                    LanguageISO6391Code = "de",
                    CountryISO31661Alpha2Code = "CH",
                },

                // Liechtenstein
                new Locale
                {
                    IETFBCP47Tag = "de-LI",
                    Name = "German (Liechtenstein)",
                    Endonym = "Deutsch (Liechtenstein)",
                    LanguageISO6391Code = "de",
                    CountryISO31661Alpha2Code = "LI",
                },

                // Luxembourg
                new Locale
                {
                    IETFBCP47Tag = "de-LU",
                    Name = "German (Luxembourg)",
                    Endonym = "Deutsch (Luxemburg)",
                    LanguageISO6391Code = "de",
                    CountryISO31661Alpha2Code = "LU",
                },

                // Belgium
                new Locale
                {
                    IETFBCP47Tag = "de-BE",
                    Name = "German (Belgium)",
                    Endonym = "Deutsch (Belgien)",
                    LanguageISO6391Code = "de",
                    CountryISO31661Alpha2Code = "BE",
                },

                // =====================
                // Italian Locales (it)
                // =====================

                // Italy
                new Locale
                {
                    IETFBCP47Tag = "it-IT",
                    Name = "Italian (Italy)",
                    Endonym = "Italiano (Italia)",
                    LanguageISO6391Code = "it",
                    CountryISO31661Alpha2Code = "IT",
                },

                // Switzerland
                new Locale
                {
                    IETFBCP47Tag = "it-CH",
                    Name = "Italian (Switzerland)",
                    Endonym = "Italiano (Svizzera)",
                    LanguageISO6391Code = "it",
                    CountryISO31661Alpha2Code = "CH",
                },

                // San Marino
                new Locale
                {
                    IETFBCP47Tag = "it-SM",
                    Name = "Italian (San Marino)",
                    Endonym = "Italiano (San Marino)",
                    LanguageISO6391Code = "it",
                    CountryISO31661Alpha2Code = "SM",
                },

                // Vatican City
                new Locale
                {
                    IETFBCP47Tag = "it-VA",
                    Name = "Italian (Vatican City)",
                    Endonym = "Italiano (Città del Vaticano)",
                    LanguageISO6391Code = "it",
                    CountryISO31661Alpha2Code = "VA",
                },

                // =====================
                // Japanese Locales (ja)
                // =====================

                // Japan
                new Locale
                {
                    IETFBCP47Tag = "ja-JP",
                    Name = "Japanese (Japan)",
                    Endonym = "日本語 (日本)",
                    LanguageISO6391Code = "ja",
                    CountryISO31661Alpha2Code = "JP",
                },
            ]);
        }
    }
}
