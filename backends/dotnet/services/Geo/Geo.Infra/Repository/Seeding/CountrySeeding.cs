// -----------------------------------------------------------------------
// <copyright file="CountrySeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding country data.
/// </summary>
public static class CountrySeeding
{
    /// <summary>
    /// Seeds the Country entity.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the Country entity.
        /// </summary>
        public void SeedCountries()
        {
            modelBuilder.Entity<Country>().HasData([

                // United States
                new Country
                {
                    ISO31661Alpha2Code = "US",
                    ISO31661Alpha3Code = "USA",
                    ISO31661NumericCode = "840",
                    DisplayName = "United States",
                    OfficialName = "United States of America",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-US",
                },

                // Canada
                new Country
                {
                    ISO31661Alpha2Code = "CA",
                    ISO31661Alpha3Code = "CAN",
                    ISO31661NumericCode = "124",
                    DisplayName = "Canada",
                    OfficialName = "Canada",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "CAD",
                    PrimaryLocaleIETFBCP47Tag = "en-CA",
                },

                // Afghanistan
                new Country
                {
                    ISO31661Alpha2Code = "AF",
                    ISO31661Alpha3Code = "AFG",
                    ISO31661NumericCode = "004",
                    DisplayName = "Afghanistan",
                    OfficialName = "Islamic Republic of Afghanistan",
                    PhoneNumberPrefix = "93",

                    // PrimaryCurrencyISO4217AlphaCode = "AFN",
                    // PrimaryLocaleIETFBCP47Tag = "ps-AF",
                },

                // Åland Islands (Finland)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FI",

                    ISO31661Alpha2Code = "AX",
                    ISO31661Alpha3Code = "ALA",
                    ISO31661NumericCode = "248",
                    DisplayName = "Åland Islands",
                    OfficialName = "Åland Islands",
                    PhoneNumberPrefix = "358",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "sv-AX",
                },

                // Albania
                new Country
                {
                    ISO31661Alpha2Code = "AL",
                    ISO31661Alpha3Code = "ALB",
                    ISO31661NumericCode = "008",
                    DisplayName = "Albania",
                    OfficialName = "Republic of Albania",
                    PhoneNumberPrefix = "355",

                    // PrimaryCurrencyISO4217AlphaCode = "ALL",
                    // PrimaryLocaleIETFBCP47Tag = "sq-AL",
                },

                // Algeria
                new Country
                {
                    ISO31661Alpha2Code = "DZ",
                    ISO31661Alpha3Code = "DZA",
                    ISO31661NumericCode = "012",
                    DisplayName = "Algeria",
                    OfficialName = "People's Democratic Republic of Algeria",
                    PhoneNumberPrefix = "213",

                    // PrimaryCurrencyISO4217AlphaCode = "DZD",
                    // PrimaryLocaleIETFBCP47Tag = "ar-DZ",
                },

                // American Samoa (United States)
                new Country
                {
                    SovereignISO31661Alpha2Code = "US",

                    ISO31661Alpha2Code = "AS",
                    ISO31661Alpha3Code = "ASM",
                    ISO31661NumericCode = "016",
                    DisplayName = "American Samoa",
                    OfficialName = "American Samoa",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-AS",
                },

                // Andorra
                new Country
                {
                    ISO31661Alpha2Code = "AD",
                    ISO31661Alpha3Code = "AND",
                    ISO31661NumericCode = "020",
                    DisplayName = "Andorra",
                    OfficialName = "Principality of Andorra",
                    PhoneNumberPrefix = "376",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "ca-AD",
                },

                // Angola
                new Country
                {
                    ISO31661Alpha2Code = "AO",
                    ISO31661Alpha3Code = "AGO",
                    ISO31661NumericCode = "024",
                    DisplayName = "Angola",
                    OfficialName = "Republic of Angola",
                    PhoneNumberPrefix = "244",

                    // PrimaryCurrencyISO4217AlphaCode = "AOA",
                    // PrimaryLocaleIETFBCP47Tag = "pt-AO",
                },

                // Anguilla (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "AI",
                    ISO31661Alpha3Code = "AIA",
                    ISO31661NumericCode = "660",
                    DisplayName = "Anguilla",
                    OfficialName = "Anguilla",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-AI",

                    // PrimaryCurrencyISO4217AlphaCode = "XCD",
                },

                // Antarctica (Antarctic Treaty System)
                new Country
                {
                    ISO31661Alpha2Code = "AQ",
                    ISO31661Alpha3Code = "ATA",
                    ISO31661NumericCode = "010",
                    DisplayName = "Antarctica",
                    OfficialName = "Antarctica",
                    PhoneNumberPrefix = "672",

                    // There is no official / preferred currency for Antarctica.
                    // PrimaryLocaleIETFBCP47Tag = "en-AQ",
                },

                // Antigua and Barbuda
                new Country
                {
                    ISO31661Alpha2Code = "AG",
                    ISO31661Alpha3Code = "ATG",
                    ISO31661NumericCode = "028",
                    DisplayName = "Antigua and Barbuda",
                    OfficialName = "Antigua and Barbuda",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-AG",

                    // PrimaryCurrencyISO4217AlphaCode = "XCD",
                },

                // Argentina
                new Country
                {
                    ISO31661Alpha2Code = "AR",
                    ISO31661Alpha3Code = "ARG",
                    ISO31661NumericCode = "032",
                    DisplayName = "Argentina",
                    OfficialName = "Argentine Republic",
                    PhoneNumberPrefix = "54",
                    PrimaryLocaleIETFBCP47Tag = "es-AR",

                    // PrimaryCurrencyISO4217AlphaCode = "ARS",
                },

                // Armenia
                new Country
                {
                    ISO31661Alpha2Code = "AM",
                    ISO31661Alpha3Code = "ARM",
                    ISO31661NumericCode = "051",
                    DisplayName = "Armenia",
                    OfficialName = "Republic of Armenia",
                    PhoneNumberPrefix = "374",

                    // PrimaryCurrencyISO4217AlphaCode = "AMD",
                    // PrimaryLocaleIETFBCP47Tag = "hy-AM",
                },

                // Aruba (Netherlands)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NL",

                    ISO31661Alpha2Code = "AW",
                    ISO31661Alpha3Code = "ABW",
                    ISO31661NumericCode = "533",
                    DisplayName = "Aruba",
                    OfficialName = "Aruba",
                    PhoneNumberPrefix = "297",

                    // PrimaryCurrencyISO4217AlphaCode = "AWG",
                    // PrimaryLocaleIETFBCP47Tag = "nl-AW",
                },

                // Australia
                new Country
                {
                    ISO31661Alpha2Code = "AU",
                    ISO31661Alpha3Code = "AUS",
                    ISO31661NumericCode = "036",
                    DisplayName = "Australia",
                    OfficialName = "Commonwealth of Australia",
                    PhoneNumberPrefix = "61",
                    PrimaryLocaleIETFBCP47Tag = "en-AU",

                    // PrimaryCurrencyISO4217AlphaCode = "AUD",
                },

                // Austria
                new Country
                {
                    ISO31661Alpha2Code = "AT",
                    ISO31661Alpha3Code = "AUT",
                    ISO31661NumericCode = "040",
                    DisplayName = "Austria",
                    OfficialName = "Republic of Austria",
                    PhoneNumberPrefix = "43",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "de-AT",
                },

                // Azerbaijan
                new Country
                {
                    ISO31661Alpha2Code = "AZ",
                    ISO31661Alpha3Code = "AZE",
                    ISO31661NumericCode = "031",
                    DisplayName = "Azerbaijan",
                    OfficialName = "Republic of Azerbaijan",
                    PhoneNumberPrefix = "994",

                    // PrimaryCurrencyISO4217AlphaCode = "AZN",
                    // PrimaryLocaleIETFBCP47Tag = "az-AZ",
                },

                // Bahamas
                new Country
                {
                    ISO31661Alpha2Code = "BS",
                    ISO31661Alpha3Code = "BHS",
                    ISO31661NumericCode = "044",
                    DisplayName = "Bahamas",
                    OfficialName = "Commonwealth of The Bahamas",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-BS",

                    // PrimaryCurrencyISO4217AlphaCode = "BSD",
                },

                // Bahrain
                new Country
                {
                    ISO31661Alpha2Code = "BH",
                    ISO31661Alpha3Code = "BHR",
                    ISO31661NumericCode = "048",
                    DisplayName = "Bahrain",
                    OfficialName = "Kingdom of Bahrain",
                    PhoneNumberPrefix = "973",

                    // PrimaryCurrencyISO4217AlphaCode = "BHD",
                    // PrimaryLocaleIETFBCP47Tag = "ar-BH",
                },

                // Bangladesh
                new Country
                {
                    ISO31661Alpha2Code = "BD",
                    ISO31661Alpha3Code = "BGD",
                    ISO31661NumericCode = "050",
                    DisplayName = "Bangladesh",
                    OfficialName = "People's Republic of Bangladesh",
                    PhoneNumberPrefix = "880",

                    // PrimaryCurrencyISO4217AlphaCode = "BDT",
                    // PrimaryLocaleIETFBCP47Tag = "bn-BD",
                },

                // Barbados
                new Country
                {
                    ISO31661Alpha2Code = "BB",
                    ISO31661Alpha3Code = "BRB",
                    ISO31661NumericCode = "052",
                    DisplayName = "Barbados",
                    OfficialName = "Barbados",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-BB",

                    // PrimaryCurrencyISO4217AlphaCode = "BBD",
                },

                // Belarus
                new Country
                {
                    ISO31661Alpha2Code = "BY",
                    ISO31661Alpha3Code = "BLR",
                    ISO31661NumericCode = "112",
                    DisplayName = "Belarus",
                    OfficialName = "Republic of Belarus",
                    PhoneNumberPrefix = "375",

                    // PrimaryCurrencyISO4217AlphaCode = "BYN",
                    // PrimaryLocaleIETFBCP47Tag = "be-BY",
                },

                // Belgium
                new Country
                {
                    ISO31661Alpha2Code = "BE",
                    ISO31661Alpha3Code = "BEL",
                    ISO31661NumericCode = "056",
                    DisplayName = "Belgium",
                    OfficialName = "Kingdom of Belgium",
                    PhoneNumberPrefix = "32",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "nl-BE",
                },

                // Belize
                new Country
                {
                    ISO31661Alpha2Code = "BZ",
                    ISO31661Alpha3Code = "BLZ",
                    ISO31661NumericCode = "084",
                    DisplayName = "Belize",
                    OfficialName = "Belize",
                    PhoneNumberPrefix = "501",
                    PrimaryLocaleIETFBCP47Tag = "en-BZ",

                    // PrimaryCurrencyISO4217AlphaCode = "BZD",
                },

                // Benin
                new Country
                {
                    ISO31661Alpha2Code = "BJ",
                    ISO31661Alpha3Code = "BEN",
                    ISO31661NumericCode = "204",
                    DisplayName = "Benin",
                    OfficialName = "Republic of Benin",
                    PhoneNumberPrefix = "229",
                    PrimaryLocaleIETFBCP47Tag = "fr-BJ",

                    // PrimaryCurrencyISO4217AlphaCode = "XOF",
                },

                // Bermuda (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "BM",
                    ISO31661Alpha3Code = "BMU",
                    ISO31661NumericCode = "060",
                    DisplayName = "Bermuda",
                    OfficialName = "Bermuda",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-BM",

                    // PrimaryCurrencyISO4217AlphaCode = "BMD",
                },

                // Bhutan
                new Country
                {
                    ISO31661Alpha2Code = "BT",
                    ISO31661Alpha3Code = "BTN",
                    ISO31661NumericCode = "064",
                    DisplayName = "Bhutan",
                    OfficialName = "Kingdom of Bhutan",
                    PhoneNumberPrefix = "975",

                    // PrimaryCurrencyISO4217AlphaCode = "BTN",
                    // PrimaryLocaleIETFBCP47Tag = "dz-BT",
                },

                // Bolivia
                new Country
                {
                    ISO31661Alpha2Code = "BO",
                    ISO31661Alpha3Code = "BOL",
                    ISO31661NumericCode = "068",
                    DisplayName = "Bolivia",
                    OfficialName = "Plurinational State of Bolivia",
                    PhoneNumberPrefix = "591",
                    PrimaryLocaleIETFBCP47Tag = "es-BO",

                    // PrimaryCurrencyISO4217AlphaCode = "BOB",
                },

                // Bonaire, Sint Eustatius and Saba (Netherlands)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NL",

                    ISO31661Alpha2Code = "BQ",
                    ISO31661Alpha3Code = "BES",
                    ISO31661NumericCode = "535",
                    DisplayName = "Bonaire, Sint Eustatius and Saba",
                    OfficialName = "Bonaire, Sint Eustatius and Saba",
                    PhoneNumberPrefix = "599",
                    PrimaryCurrencyISO4217AlphaCode = "USD",

                    // PrimaryLocaleIETFBCP47Tag = "nl-BQ",
                },

                // Bosnia and Herzegovina
                new Country
                {
                    ISO31661Alpha2Code = "BA",
                    ISO31661Alpha3Code = "BIH",
                    ISO31661NumericCode = "070",
                    DisplayName = "Bosnia and Herzegovina",
                    OfficialName = "Bosnia and Herzegovina",
                    PhoneNumberPrefix = "387",

                    // PrimaryCurrencyISO4217AlphaCode = "BAM",
                    // PrimaryLocaleIETFBCP47Tag = "bs-BA",
                },

                // Botswana
                new Country
                {
                    ISO31661Alpha2Code = "BW",
                    ISO31661Alpha3Code = "BWA",
                    ISO31661NumericCode = "072",
                    DisplayName = "Botswana",
                    OfficialName = "Republic of Botswana",
                    PhoneNumberPrefix = "267",
                    PrimaryLocaleIETFBCP47Tag = "en-BW",

                    // PrimaryCurrencyISO4217AlphaCode = "BWP",
                },

                // Bouvet Island (Norway)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NO",

                    ISO31661Alpha2Code = "BV",
                    ISO31661Alpha3Code = "BVT",
                    ISO31661NumericCode = "074",
                    DisplayName = "Bouvet Island",
                    OfficialName = "Bouvet Island",
                    PhoneNumberPrefix = "47",
                },

                // Brazil
                new Country
                {
                    ISO31661Alpha2Code = "BR",
                    ISO31661Alpha3Code = "BRA",
                    ISO31661NumericCode = "076",
                    DisplayName = "Brazil",
                    OfficialName = "Federative Republic of Brazil",
                    PhoneNumberPrefix = "55",

                    // PrimaryCurrencyISO4217AlphaCode = "BRL",
                    // PrimaryLocaleIETFBCP47Tag = "pt-BR",
                },

                // British Indian Ocean Territory (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "IO",
                    ISO31661Alpha3Code = "IOT",
                    ISO31661NumericCode = "086",
                    DisplayName = "British Indian Ocean Territory",
                    OfficialName = "British Indian Ocean Territory",
                    PhoneNumberPrefix = "246",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-IO",
                },

                // British Virgin Islands (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "VG",
                    ISO31661Alpha3Code = "VGB",
                    ISO31661NumericCode = "092",
                    DisplayName = "British Virgin Islands",
                    OfficialName = "Virgin Islands",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-VG",
                },

                // Brunei
                new Country
                {
                    ISO31661Alpha2Code = "BN",
                    ISO31661Alpha3Code = "BRN",
                    ISO31661NumericCode = "096",
                    DisplayName = "Brunei",
                    OfficialName = "Brunei Darussalam",
                    PhoneNumberPrefix = "673",

                    // PrimaryCurrencyISO4217AlphaCode = "BND",
                    // PrimaryLocaleIETFBCP47Tag = "ms-BN",
                },

                // Bulgaria
                new Country
                {
                    ISO31661Alpha2Code = "BG",
                    ISO31661Alpha3Code = "BGR",
                    ISO31661NumericCode = "100",
                    DisplayName = "Bulgaria",
                    OfficialName = "Republic of Bulgaria",
                    PhoneNumberPrefix = "359",

                    // PrimaryCurrencyISO4217AlphaCode = "BGN",
                    // PrimaryLocaleIETFBCP47Tag = "bg-BG",
                },

                // Burkina Faso
                new Country
                {
                    ISO31661Alpha2Code = "BF",
                    ISO31661Alpha3Code = "BFA",
                    ISO31661NumericCode = "854",
                    DisplayName = "Burkina Faso",
                    OfficialName = "Burkina Faso",
                    PhoneNumberPrefix = "226",
                    PrimaryLocaleIETFBCP47Tag = "fr-BF",

                    // PrimaryCurrencyISO4217AlphaCode = "XOF",
                },

                // Burundi
                new Country
                {
                    ISO31661Alpha2Code = "BI",
                    ISO31661Alpha3Code = "BDI",
                    ISO31661NumericCode = "108",
                    DisplayName = "Burundi",
                    OfficialName = "Republic of Burundi",
                    PhoneNumberPrefix = "257",
                    PrimaryLocaleIETFBCP47Tag = "fr-BI",

                    // PrimaryCurrencyISO4217AlphaCode = "BIF",
                },

                // Cabo Verde
                new Country
                {
                    ISO31661Alpha2Code = "CV",
                    ISO31661Alpha3Code = "CPV",
                    ISO31661NumericCode = "132",
                    DisplayName = "Cabo Verde",
                    OfficialName = "Republic of Cabo Verde",
                    PhoneNumberPrefix = "238",

                    // PrimaryCurrencyISO4217AlphaCode = "CVE",
                    // PrimaryLocaleIETFBCP47Tag = "pt-CV",
                },

                // Cambodia
                new Country
                {
                    ISO31661Alpha2Code = "KH",
                    ISO31661Alpha3Code = "KHM",
                    ISO31661NumericCode = "116",
                    DisplayName = "Cambodia",
                    OfficialName = "Kingdom of Cambodia",
                    PhoneNumberPrefix = "855",

                    // PrimaryCurrencyISO4217AlphaCode = "KHR",
                    // PrimaryLocaleIETFBCP47Tag = "km-KH",
                },

                // Cameroon
                new Country
                {
                    ISO31661Alpha2Code = "CM",
                    ISO31661Alpha3Code = "CMR",
                    ISO31661NumericCode = "120",
                    DisplayName = "Cameroon",
                    OfficialName = "Republic of Cameroon",
                    PhoneNumberPrefix = "237",
                    PrimaryLocaleIETFBCP47Tag = "fr-CM",

                    // PrimaryCurrencyISO4217AlphaCode = "XAF",
                },

                // Cayman Islands (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "KY",
                    ISO31661Alpha3Code = "CYM",
                    ISO31661NumericCode = "136",
                    DisplayName = "Cayman Islands",
                    OfficialName = "Cayman Islands",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-KY",

                    // PrimaryCurrencyISO4217AlphaCode = "KYD",
                },

                // Central African Republic
                new Country
                {
                    ISO31661Alpha2Code = "CF",
                    ISO31661Alpha3Code = "CAF",
                    ISO31661NumericCode = "140",
                    DisplayName = "Central African Republic",
                    OfficialName = "Central African Republic",
                    PhoneNumberPrefix = "236",
                    PrimaryLocaleIETFBCP47Tag = "fr-CF",

                    // PrimaryCurrencyISO4217AlphaCode = "XAF",
                },

                // Chad
                new Country
                {
                    ISO31661Alpha2Code = "TD",
                    ISO31661Alpha3Code = "TCD",
                    ISO31661NumericCode = "148",
                    DisplayName = "Chad",
                    OfficialName = "Republic of Chad",
                    PhoneNumberPrefix = "235",
                    PrimaryLocaleIETFBCP47Tag = "fr-TD",

                    // PrimaryCurrencyISO4217AlphaCode = "XAF",
                },

                // Chile
                new Country
                {
                    ISO31661Alpha2Code = "CL",
                    ISO31661Alpha3Code = "CHL",
                    ISO31661NumericCode = "152",
                    DisplayName = "Chile",
                    OfficialName = "Republic of Chile",
                    PhoneNumberPrefix = "56",
                    PrimaryLocaleIETFBCP47Tag = "es-CL",

                    // PrimaryCurrencyISO4217AlphaCode = "CLP",
                },

                // China
                new Country
                {
                    ISO31661Alpha2Code = "CN",
                    ISO31661Alpha3Code = "CHN",
                    ISO31661NumericCode = "156",
                    DisplayName = "China",
                    OfficialName = "People's Republic of China",
                    PhoneNumberPrefix = "86",

                    // PrimaryCurrencyISO4217AlphaCode = "CNY",
                    // PrimaryLocaleIETFBCP47Tag = "zh-CN",
                },

                // Republic of China (Taiwan)
                new Country
                {
                    ISO31661Alpha2Code = "TW",
                    ISO31661Alpha3Code = "TWN",
                    ISO31661NumericCode = "158",
                    DisplayName = "Taiwan",
                    OfficialName = "Republic of China",
                    PhoneNumberPrefix = "886",

                    // PrimaryCurrencyISO4217AlphaCode = "TWD",
                    // PrimaryLocaleIETFBCP47Tag = "zh-TW",
                },

                // Christmas Island (Australia)
                new Country
                {
                    SovereignISO31661Alpha2Code = "AU",

                    ISO31661Alpha2Code = "CX",
                    ISO31661Alpha3Code = "CXR",
                    ISO31661NumericCode = "162",
                    DisplayName = "Christmas Island",
                    OfficialName = "Territory of Christmas Island",
                    PhoneNumberPrefix = "61",
                    PrimaryLocaleIETFBCP47Tag = "en-CX",

                    // PrimaryCurrencyISO4217AlphaCode = "AUD",
                },

                // Cocos (Keeling) Islands (Australia)
                new Country
                {
                    SovereignISO31661Alpha2Code = "AU",

                    ISO31661Alpha2Code = "CC",
                    ISO31661Alpha3Code = "CCK",
                    ISO31661NumericCode = "166",
                    DisplayName = "Cocos (Keeling) Islands",
                    OfficialName = "Territory of Cocos (Keeling) Islands",
                    PhoneNumberPrefix = "61",
                    PrimaryLocaleIETFBCP47Tag = "en-CC",

                    // PrimaryCurrencyISO4217AlphaCode = "AUD",
                },

                // Colombia
                new Country
                {
                    ISO31661Alpha2Code = "CO",
                    ISO31661Alpha3Code = "COL",
                    ISO31661NumericCode = "170",
                    DisplayName = "Colombia",
                    OfficialName = "Republic of Colombia",
                    PhoneNumberPrefix = "57",
                    PrimaryLocaleIETFBCP47Tag = "es-CO",

                    // PrimaryCurrencyISO4217AlphaCode = "COP",
                },

                // Comoros
                new Country
                {
                    ISO31661Alpha2Code = "KM",
                    ISO31661Alpha3Code = "COM",
                    ISO31661NumericCode = "174",
                    DisplayName = "Comoros",
                    OfficialName = "Union of the Comoros",
                    PhoneNumberPrefix = "269",

                    // PrimaryCurrencyISO4217AlphaCode = "KMF",
                    // PrimaryLocaleIETFBCP47Tag = "ar-KM",
                },

                // DR Congo
                new Country
                {
                    ISO31661Alpha2Code = "CD",
                    ISO31661Alpha3Code = "COD",
                    ISO31661NumericCode = "180",
                    DisplayName = "DR Congo",
                    OfficialName = "Democratic Republic of the Congo",
                    PhoneNumberPrefix = "243",
                    PrimaryLocaleIETFBCP47Tag = "fr-CD",

                    // PrimaryCurrencyISO4217AlphaCode = "CDF",
                },

                // Congo
                new Country
                {
                    ISO31661Alpha2Code = "CG",
                    ISO31661Alpha3Code = "COG",
                    ISO31661NumericCode = "178",
                    DisplayName = "Congo",
                    OfficialName = "Republic of the Congo",
                    PhoneNumberPrefix = "242",
                    PrimaryLocaleIETFBCP47Tag = "fr-CG",

                    // PrimaryCurrencyISO4217AlphaCode = "XAF",
                },

                // Cook Islands (New Zealand)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NZ",

                    ISO31661Alpha2Code = "CK",
                    ISO31661Alpha3Code = "COK",
                    ISO31661NumericCode = "184",
                    DisplayName = "Cook Islands",
                    OfficialName = "Cook Islands",
                    PhoneNumberPrefix = "682",
                    PrimaryLocaleIETFBCP47Tag = "en-CK",

                    // PrimaryCurrencyISO4217AlphaCode = "NZD",
                },

                // Costa Rica
                new Country
                {
                    ISO31661Alpha2Code = "CR",
                    ISO31661Alpha3Code = "CRI",
                    ISO31661NumericCode = "188",
                    DisplayName = "Costa Rica",
                    OfficialName = "Republic of Costa Rica",
                    PhoneNumberPrefix = "506",
                    PrimaryLocaleIETFBCP47Tag = "es-CR",

                    // PrimaryCurrencyISO4217AlphaCode = "CRC",
                },

                // Côte d'Ivoire
                new Country
                {
                    ISO31661Alpha2Code = "CI",
                    ISO31661Alpha3Code = "CIV",
                    ISO31661NumericCode = "384",
                    DisplayName = "Côte d'Ivoire",
                    OfficialName = "Republic of Côte d'Ivoire",
                    PhoneNumberPrefix = "225",
                    PrimaryLocaleIETFBCP47Tag = "fr-CI",

                    // PrimaryCurrencyISO4217AlphaCode = "XOF",
                },

                // Croatia
                new Country
                {
                    ISO31661Alpha2Code = "HR",
                    ISO31661Alpha3Code = "HRV",
                    ISO31661NumericCode = "191",
                    DisplayName = "Croatia",
                    OfficialName = "Republic of Croatia",
                    PhoneNumberPrefix = "385",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "hr-HR",
                },

                // Cuba
                new Country
                {
                    ISO31661Alpha2Code = "CU",
                    ISO31661Alpha3Code = "CUB",
                    ISO31661NumericCode = "192",
                    DisplayName = "Cuba",
                    OfficialName = "Republic of Cuba",
                    PhoneNumberPrefix = "53",
                    PrimaryLocaleIETFBCP47Tag = "es-CU",

                    // PrimaryCurrencyISO4217AlphaCode = "CUP",
                },

                // Curaçao (Netherlands)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NL",

                    ISO31661Alpha2Code = "CW",
                    ISO31661Alpha3Code = "CUW",
                    ISO31661NumericCode = "531",
                    DisplayName = "Curaçao",
                    OfficialName = "Country of Curaçao",
                    PhoneNumberPrefix = "599",

                    // PrimaryCurrencyISO4217AlphaCode = "ANG",
                    // PrimaryLocaleIETFBCP47Tag = "nl-CW",
                },

                // Cyprus
                new Country
                {
                    ISO31661Alpha2Code = "CY",
                    ISO31661Alpha3Code = "CYP",
                    ISO31661NumericCode = "196",
                    DisplayName = "Cyprus",
                    OfficialName = "Republic of Cyprus",
                    PhoneNumberPrefix = "357",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "el-CY",
                },

                // Czech Republic
                new Country
                {
                    ISO31661Alpha2Code = "CZ",
                    ISO31661Alpha3Code = "CZE",
                    ISO31661NumericCode = "203",
                    DisplayName = "Czechia",
                    OfficialName = "Czech Republic",
                    PhoneNumberPrefix = "420",

                    // PrimaryCurrencyISO4217AlphaCode = "CZK",
                    // PrimaryLocaleIETFBCP47Tag = "cs-CZ",
                },

                // Denmark
                new Country
                {
                    ISO31661Alpha2Code = "DK",
                    ISO31661Alpha3Code = "DNK",
                    ISO31661NumericCode = "208",
                    DisplayName = "Denmark",
                    OfficialName = "Kingdom of Denmark",
                    PhoneNumberPrefix = "45",

                    // PrimaryCurrencyISO4217AlphaCode = "DKK",
                    // PrimaryLocaleIETFBCP47Tag = "da-DK",
                },

                // Djibouti
                new Country
                {
                    ISO31661Alpha2Code = "DJ",
                    ISO31661Alpha3Code = "DJI",
                    ISO31661NumericCode = "262",
                    DisplayName = "Djibouti",
                    OfficialName = "Republic of Djibouti",
                    PhoneNumberPrefix = "253",
                    PrimaryLocaleIETFBCP47Tag = "fr-DJ",

                    // PrimaryCurrencyISO4217AlphaCode = "DJF",
                },

                // Dominica
                new Country
                {
                    ISO31661Alpha2Code = "DM",
                    ISO31661Alpha3Code = "DMA",
                    ISO31661NumericCode = "212",
                    DisplayName = "Dominica",
                    OfficialName = "Commonwealth of Dominica",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-DM",

                    // PrimaryCurrencyISO4217AlphaCode = "XCD",
                },

                // Dominican Republic
                new Country
                {
                    ISO31661Alpha2Code = "DO",
                    ISO31661Alpha3Code = "DOM",
                    ISO31661NumericCode = "214",
                    DisplayName = "Dominican Republic",
                    OfficialName = "Dominican Republic",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "es-DO",

                    // PrimaryCurrencyISO4217AlphaCode = "DOP",
                },

                // Ecuador
                new Country
                {
                    ISO31661Alpha2Code = "EC",
                    ISO31661Alpha3Code = "ECU",
                    ISO31661NumericCode = "218",
                    DisplayName = "Ecuador",
                    OfficialName = "Republic of Ecuador",
                    PhoneNumberPrefix = "593",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "es-EC",
                },

                // Egypt
                new Country
                {
                    ISO31661Alpha2Code = "EG",
                    ISO31661Alpha3Code = "EGY",
                    ISO31661NumericCode = "818",
                    DisplayName = "Egypt",
                    OfficialName = "Arab Republic of Egypt",
                    PhoneNumberPrefix = "20",

                    // PrimaryCurrencyISO4217AlphaCode = "EGP",
                    // PrimaryLocaleIETFBCP47Tag = "ar-EG",
                },

                // El Salvador
                new Country
                {
                    ISO31661Alpha2Code = "SV",
                    ISO31661Alpha3Code = "SLV",
                    ISO31661NumericCode = "222",
                    DisplayName = "El Salvador",
                    OfficialName = "Republic of El Salvador",
                    PhoneNumberPrefix = "503",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "es-SV",
                },

                // Equatorial Guinea
                new Country
                {
                    ISO31661Alpha2Code = "GQ",
                    ISO31661Alpha3Code = "GNQ",
                    ISO31661NumericCode = "226",
                    DisplayName = "Equatorial Guinea",
                    OfficialName = "Republic of Equatorial Guinea",
                    PhoneNumberPrefix = "240",
                    PrimaryLocaleIETFBCP47Tag = "es-GQ",

                    // PrimaryCurrencyISO4217AlphaCode = "XAF",
                },

                // Eritrea
                new Country
                {
                    ISO31661Alpha2Code = "ER",
                    ISO31661Alpha3Code = "ERI",
                    ISO31661NumericCode = "232",
                    DisplayName = "Eritrea",
                    OfficialName = "State of Eritrea",
                    PhoneNumberPrefix = "291",

                    // PrimaryCurrencyISO4217AlphaCode = "ERN",
                    // PrimaryLocaleIETFBCP47Tag = "ti-ER",
                },

                // Estonia
                new Country
                {
                    ISO31661Alpha2Code = "EE",
                    ISO31661Alpha3Code = "EST",
                    ISO31661NumericCode = "233",
                    DisplayName = "Estonia",
                    OfficialName = "Republic of Estonia",
                    PhoneNumberPrefix = "372",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "et-EE",
                },

                // Eswatini
                new Country
                {
                    ISO31661Alpha2Code = "SZ",
                    ISO31661Alpha3Code = "SWZ",
                    ISO31661NumericCode = "748",
                    DisplayName = "Eswatini",
                    OfficialName = "Kingdom of Eswatini",
                    PhoneNumberPrefix = "268",
                    PrimaryLocaleIETFBCP47Tag = "en-SZ",

                    // PrimaryCurrencyISO4217AlphaCode = "SZL",
                },

                // Ethiopia
                new Country
                {
                    ISO31661Alpha2Code = "ET",
                    ISO31661Alpha3Code = "ETH",
                    ISO31661NumericCode = "231",
                    DisplayName = "Ethiopia",
                    OfficialName = "Federal Democratic Republic of Ethiopia",
                    PhoneNumberPrefix = "251",

                    // PrimaryCurrencyISO4217AlphaCode = "ETB",
                    // PrimaryLocaleIETFBCP47Tag = "am-ET",
                },

                // Falkland Islands (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "FK",
                    ISO31661Alpha3Code = "FLK",
                    ISO31661NumericCode = "238",
                    DisplayName = "Falkland Islands",
                    OfficialName = "Falkland Islands",
                    PhoneNumberPrefix = "500",
                    PrimaryLocaleIETFBCP47Tag = "en-FK",

                    // PrimaryCurrencyISO4217AlphaCode = "FKP",
                },

                // Faroe Islands (Denmark)
                new Country
                {
                    SovereignISO31661Alpha2Code = "DK",

                    ISO31661Alpha2Code = "FO",
                    ISO31661Alpha3Code = "FRO",
                    ISO31661NumericCode = "234",
                    DisplayName = "Faroe Islands",
                    OfficialName = "Faroe Islands",
                    PhoneNumberPrefix = "298",

                    // PrimaryCurrencyISO4217AlphaCode = "DKK",
                    // PrimaryLocaleIETFBCP47Tag = "fo-FO",
                },

                // Fiji
                new Country
                {
                    ISO31661Alpha2Code = "FJ",
                    ISO31661Alpha3Code = "FJI",
                    ISO31661NumericCode = "242",
                    DisplayName = "Fiji",
                    OfficialName = "Republic of Fiji",
                    PhoneNumberPrefix = "679",
                    PrimaryLocaleIETFBCP47Tag = "en-FJ",

                    // PrimaryCurrencyISO4217AlphaCode = "FJD",
                },

                // Finland
                new Country
                {
                    ISO31661Alpha2Code = "FI",
                    ISO31661Alpha3Code = "FIN",
                    ISO31661NumericCode = "246",
                    DisplayName = "Finland",
                    OfficialName = "Republic of Finland",
                    PhoneNumberPrefix = "358",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "fi-FI",
                },

                // France
                new Country
                {
                    ISO31661Alpha2Code = "FR",
                    ISO31661Alpha3Code = "FRA",
                    ISO31661NumericCode = "250",
                    DisplayName = "France",
                    OfficialName = "French Republic",
                    PhoneNumberPrefix = "33",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-FR",
                },

                // French Guiana (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "GF",
                    ISO31661Alpha3Code = "GUF",
                    ISO31661NumericCode = "254",
                    DisplayName = "French Guiana",
                    OfficialName = "French Guiana",
                    PhoneNumberPrefix = "594",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-GF",
                },

                // French Polynesia (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "PF",
                    ISO31661Alpha3Code = "PYF",
                    ISO31661NumericCode = "258",
                    DisplayName = "French Polynesia",
                    OfficialName = "French Polynesia",
                    PhoneNumberPrefix = "689",
                    PrimaryLocaleIETFBCP47Tag = "fr-PF",

                    // PrimaryCurrencyISO4217AlphaCode = "XPF",
                },

                // French Southern Territories (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "TF",
                    ISO31661Alpha3Code = "ATF",
                    ISO31661NumericCode = "260",
                    DisplayName = "French Southern Territories",
                    OfficialName = "French Southern and Antarctic Lands",
                    PhoneNumberPrefix = "33",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "fr-TF",
                },

                // Gabon
                new Country
                {
                    ISO31661Alpha2Code = "GA",
                    ISO31661Alpha3Code = "GAB",
                    ISO31661NumericCode = "266",
                    DisplayName = "Gabon",
                    OfficialName = "Gabonese Republic",
                    PhoneNumberPrefix = "241",
                    PrimaryLocaleIETFBCP47Tag = "fr-GA",

                    // PrimaryCurrencyISO4217AlphaCode = "XAF",
                },

                // Gambia
                new Country
                {
                    ISO31661Alpha2Code = "GM",
                    ISO31661Alpha3Code = "GMB",
                    ISO31661NumericCode = "270",
                    DisplayName = "Gambia",
                    OfficialName = "Republic of The Gambia",
                    PhoneNumberPrefix = "220",
                    PrimaryLocaleIETFBCP47Tag = "en-GM",

                    // PrimaryCurrencyISO4217AlphaCode = "GMD",
                },

                // Georgia
                new Country
                {
                    ISO31661Alpha2Code = "GE",
                    ISO31661Alpha3Code = "GEO",
                    ISO31661NumericCode = "268",
                    DisplayName = "Georgia",
                    OfficialName = "Georgia",
                    PhoneNumberPrefix = "995",

                    // PrimaryCurrencyISO4217AlphaCode = "GEL",
                    // PrimaryLocaleIETFBCP47Tag = "ka-GE",
                },

                // Germany
                new Country
                {
                    ISO31661Alpha2Code = "DE",
                    ISO31661Alpha3Code = "DEU",
                    ISO31661NumericCode = "276",
                    DisplayName = "Germany",
                    OfficialName = "Federal Republic of Germany",
                    PhoneNumberPrefix = "49",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "de-DE",
                },

                // Ghana
                new Country
                {
                    ISO31661Alpha2Code = "GH",
                    ISO31661Alpha3Code = "GHA",
                    ISO31661NumericCode = "288",
                    DisplayName = "Ghana",
                    OfficialName = "Republic of Ghana",
                    PhoneNumberPrefix = "233",
                    PrimaryLocaleIETFBCP47Tag = "en-GH",

                    // PrimaryCurrencyISO4217AlphaCode = "GHS",
                },

                // Gibraltar (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "GI",
                    ISO31661Alpha3Code = "GIB",
                    ISO31661NumericCode = "292",
                    DisplayName = "Gibraltar",
                    OfficialName = "Gibraltar",
                    PhoneNumberPrefix = "350",
                    PrimaryLocaleIETFBCP47Tag = "en-GI",

                    // PrimaryCurrencyISO4217AlphaCode = "GIP",
                },

                // Greece
                new Country
                {
                    ISO31661Alpha2Code = "GR",
                    ISO31661Alpha3Code = "GRC",
                    ISO31661NumericCode = "300",
                    DisplayName = "Greece",
                    OfficialName = "Hellenic Republic",
                    PhoneNumberPrefix = "30",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "el-GR",
                },

                // Greenland (Denmark)
                new Country
                {
                    SovereignISO31661Alpha2Code = "DK",

                    ISO31661Alpha2Code = "GL",
                    ISO31661Alpha3Code = "GRL",
                    ISO31661NumericCode = "304",
                    DisplayName = "Greenland",
                    OfficialName = "Greenland",
                    PhoneNumberPrefix = "299",

                    // PrimaryCurrencyISO4217AlphaCode = "DKK",
                    // PrimaryLocaleIETFBCP47Tag = "kl-GL",
                },

                // Grenada
                new Country
                {
                    ISO31661Alpha2Code = "GD",
                    ISO31661Alpha3Code = "GRD",
                    ISO31661NumericCode = "308",
                    DisplayName = "Grenada",
                    OfficialName = "Grenada",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-GD",

                    // PrimaryCurrencyISO4217AlphaCode = "XCD",
                },

                // Guadeloupe (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "GP",
                    ISO31661Alpha3Code = "GLP",
                    ISO31661NumericCode = "312",
                    DisplayName = "Guadeloupe",
                    OfficialName = "Guadeloupe",
                    PhoneNumberPrefix = "590",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-GP",
                },

                // Guam (United States)
                new Country
                {
                    SovereignISO31661Alpha2Code = "US",

                    ISO31661Alpha2Code = "GU",
                    ISO31661Alpha3Code = "GUM",
                    ISO31661NumericCode = "316",
                    DisplayName = "Guam",
                    OfficialName = "Guam",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-GU",
                },

                // Guatemala
                new Country
                {
                    ISO31661Alpha2Code = "GT",
                    ISO31661Alpha3Code = "GTM",
                    ISO31661NumericCode = "320",
                    DisplayName = "Guatemala",
                    OfficialName = "Republic of Guatemala",
                    PhoneNumberPrefix = "502",
                    PrimaryLocaleIETFBCP47Tag = "es-GT",

                    // PrimaryCurrencyISO4217AlphaCode = "GTQ",
                },

                // Guernsey (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "GG",
                    ISO31661Alpha3Code = "GGY",
                    ISO31661NumericCode = "831",
                    DisplayName = "Guernsey",
                    OfficialName = "Bailiwick of Guernsey",
                    PhoneNumberPrefix = "44",
                    PrimaryCurrencyISO4217AlphaCode = "GBP",
                    PrimaryLocaleIETFBCP47Tag = "en-GG",
                },

                // Guinea
                new Country
                {
                    ISO31661Alpha2Code = "GN",
                    ISO31661Alpha3Code = "GIN",
                    ISO31661NumericCode = "324",
                    DisplayName = "Guinea",
                    OfficialName = "Republic of Guinea",
                    PhoneNumberPrefix = "224",
                    PrimaryLocaleIETFBCP47Tag = "fr-GN",

                    // PrimaryCurrencyISO4217AlphaCode = "GNF",
                },

                // Guinea-Bissau
                new Country
                {
                    ISO31661Alpha2Code = "GW",
                    ISO31661Alpha3Code = "GNB",
                    ISO31661NumericCode = "624",
                    DisplayName = "Guinea-Bissau",
                    OfficialName = "Republic of Guinea-Bissau",
                    PhoneNumberPrefix = "245",

                    // PrimaryCurrencyISO4217AlphaCode = "XOF",
                    // PrimaryLocaleIETFBCP47Tag = "pt-GW",
                },

                // Guyana
                new Country
                {
                    ISO31661Alpha2Code = "GY",
                    ISO31661Alpha3Code = "GUY",
                    ISO31661NumericCode = "328",
                    DisplayName = "Guyana",
                    OfficialName = "Co-operative Republic of Guyana",
                    PhoneNumberPrefix = "592",
                    PrimaryLocaleIETFBCP47Tag = "en-GY",

                    // PrimaryCurrencyISO4217AlphaCode = "GYD",
                },

                // Haiti
                new Country
                {
                    ISO31661Alpha2Code = "HT",
                    ISO31661Alpha3Code = "HTI",
                    ISO31661NumericCode = "332",
                    DisplayName = "Haiti",
                    OfficialName = "Republic of Haiti",
                    PhoneNumberPrefix = "509",
                    PrimaryLocaleIETFBCP47Tag = "fr-HT",

                    // PrimaryCurrencyISO4217AlphaCode = "HTG",
                },

                // Heard Island and McDonald Islands (Australia)
                new Country
                {
                    SovereignISO31661Alpha2Code = "AU",

                    ISO31661Alpha2Code = "HM",
                    ISO31661Alpha3Code = "HMD",
                    ISO31661NumericCode = "334",
                    DisplayName = "Heard Island and McDonald Islands",
                    OfficialName = "Heard Island and McDonald Islands",
                    PhoneNumberPrefix = "61",
                },

                // Holy See
                new Country
                {
                    ISO31661Alpha2Code = "VA",
                    ISO31661Alpha3Code = "VAT",
                    ISO31661NumericCode = "336",
                    DisplayName = "Vatican City",
                    OfficialName = "Holy See",
                    PhoneNumberPrefix = "39",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "it-VA",
                },

                // Honduras
                new Country
                {
                    ISO31661Alpha2Code = "HN",
                    ISO31661Alpha3Code = "HND",
                    ISO31661NumericCode = "340",
                    DisplayName = "Honduras",
                    OfficialName = "Republic of Honduras",
                    PhoneNumberPrefix = "504",
                    PrimaryLocaleIETFBCP47Tag = "es-HN",

                    // PrimaryCurrencyISO4217AlphaCode = "HNL",
                },

                // Hong Kong (China)
                new Country
                {
                    SovereignISO31661Alpha2Code = "CN",

                    ISO31661Alpha2Code = "HK",
                    ISO31661Alpha3Code = "HKG",
                    ISO31661NumericCode = "344",
                    DisplayName = "Hong Kong",
                    OfficialName = "Hong Kong Special Administrative Region of China",
                    PhoneNumberPrefix = "852",

                    // PrimaryCurrencyISO4217AlphaCode = "HKD",
                    // PrimaryLocaleIETFBCP47Tag = "zh-HK",
                },

                // Hungary
                new Country
                {
                    ISO31661Alpha2Code = "HU",
                    ISO31661Alpha3Code = "HUN",
                    ISO31661NumericCode = "348",
                    DisplayName = "Hungary",
                    OfficialName = "Hungary",
                    PhoneNumberPrefix = "36",

                    // PrimaryCurrencyISO4217AlphaCode = "HUF",
                    // PrimaryLocaleIETFBCP47Tag = "hu-HU",
                },

                // Iceland
                new Country
                {
                    ISO31661Alpha2Code = "IS",
                    ISO31661Alpha3Code = "ISL",
                    ISO31661NumericCode = "352",
                    DisplayName = "Iceland",
                    OfficialName = "Iceland",
                    PhoneNumberPrefix = "354",

                    // PrimaryCurrencyISO4217AlphaCode = "ISK",
                    // PrimaryLocaleIETFBCP47Tag = "is-IS",
                },

                // India
                new Country
                {
                    ISO31661Alpha2Code = "IN",
                    ISO31661Alpha3Code = "IND",
                    ISO31661NumericCode = "356",
                    DisplayName = "India",
                    OfficialName = "Republic of India",
                    PhoneNumberPrefix = "91",

                    // PrimaryCurrencyISO4217AlphaCode = "INR",
                    // PrimaryLocaleIETFBCP47Tag = "hi-IN",
                },

                // Indonesia
                new Country
                {
                    ISO31661Alpha2Code = "ID",
                    ISO31661Alpha3Code = "IDN",
                    ISO31661NumericCode = "360",
                    DisplayName = "Indonesia",
                    OfficialName = "Republic of Indonesia",
                    PhoneNumberPrefix = "62",

                    // PrimaryCurrencyISO4217AlphaCode = "IDR",
                    // PrimaryLocaleIETFBCP47Tag = "id-ID",
                },

                // Iran
                new Country
                {
                    ISO31661Alpha2Code = "IR",
                    ISO31661Alpha3Code = "IRN",
                    ISO31661NumericCode = "364",
                    DisplayName = "Iran",
                    OfficialName = "Islamic Republic of Iran",
                    PhoneNumberPrefix = "98",

                    // PrimaryCurrencyISO4217AlphaCode = "IRR",
                    // PrimaryLocaleIETFBCP47Tag = "fa-IR",
                },

                // Iraq
                new Country
                {
                    ISO31661Alpha2Code = "IQ",
                    ISO31661Alpha3Code = "IRQ",
                    ISO31661NumericCode = "368",
                    DisplayName = "Iraq",
                    OfficialName = "Republic of Iraq",
                    PhoneNumberPrefix = "964",

                    // PrimaryCurrencyISO4217AlphaCode = "IQD",
                    // PrimaryLocaleIETFBCP47Tag = "ar-IQ",
                },

                // Ireland
                new Country
                {
                    ISO31661Alpha2Code = "IE",
                    ISO31661Alpha3Code = "IRL",
                    ISO31661NumericCode = "372",
                    DisplayName = "Ireland",
                    OfficialName = "Ireland",
                    PhoneNumberPrefix = "353",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "en-IE",
                },

                // Isle of Man (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "IM",
                    ISO31661Alpha3Code = "IMN",
                    ISO31661NumericCode = "833",
                    DisplayName = "Isle of Man",
                    OfficialName = "Isle of Man",
                    PhoneNumberPrefix = "44",
                    PrimaryCurrencyISO4217AlphaCode = "GBP",
                    PrimaryLocaleIETFBCP47Tag = "en-IM",
                },

                // Israel
                new Country
                {
                    ISO31661Alpha2Code = "IL",
                    ISO31661Alpha3Code = "ISR",
                    ISO31661NumericCode = "376",
                    DisplayName = "Israel",
                    OfficialName = "State of Israel",
                    PhoneNumberPrefix = "972",

                    // PrimaryCurrencyISO4217AlphaCode = "ILS",
                    // PrimaryLocaleIETFBCP47Tag = "he-IL",
                },

                // Italy
                new Country
                {
                    ISO31661Alpha2Code = "IT",
                    ISO31661Alpha3Code = "ITA",
                    ISO31661NumericCode = "380",
                    DisplayName = "Italy",
                    OfficialName = "Italian Republic",
                    PhoneNumberPrefix = "39",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "it-IT",
                },

                // Jamaica
                new Country
                {
                    ISO31661Alpha2Code = "JM",
                    ISO31661Alpha3Code = "JAM",
                    ISO31661NumericCode = "388",
                    DisplayName = "Jamaica",
                    OfficialName = "Jamaica",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-JM",

                    // PrimaryCurrencyISO4217AlphaCode = "JMD",
                },

                // Japan
                new Country
                {
                    ISO31661Alpha2Code = "JP",
                    ISO31661Alpha3Code = "JPN",
                    ISO31661NumericCode = "392",
                    DisplayName = "Japan",
                    OfficialName = "Japan",
                    PhoneNumberPrefix = "81",
                    PrimaryCurrencyISO4217AlphaCode = "JPY",
                    PrimaryLocaleIETFBCP47Tag = "ja-JP",
                },

                // Jersey (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "JE",
                    ISO31661Alpha3Code = "JEY",
                    ISO31661NumericCode = "832",
                    DisplayName = "Jersey",
                    OfficialName = "Bailiwick of Jersey",
                    PhoneNumberPrefix = "44",
                    PrimaryCurrencyISO4217AlphaCode = "GBP",
                    PrimaryLocaleIETFBCP47Tag = "en-JE",
                },

                // Jordan
                new Country
                {
                    ISO31661Alpha2Code = "JO",
                    ISO31661Alpha3Code = "JOR",
                    ISO31661NumericCode = "400",
                    DisplayName = "Jordan",
                    OfficialName = "Hashemite Kingdom of Jordan",
                    PhoneNumberPrefix = "962",

                    // PrimaryCurrencyISO4217AlphaCode = "JOD",
                    // PrimaryLocaleIETFBCP47Tag = "ar-JO",
                },

                // Kazakhstan
                new Country
                {
                    ISO31661Alpha2Code = "KZ",
                    ISO31661Alpha3Code = "KAZ",
                    ISO31661NumericCode = "398",
                    DisplayName = "Kazakhstan",
                    OfficialName = "Republic of Kazakhstan",
                    PhoneNumberPrefix = "7",

                    // PrimaryCurrencyISO4217AlphaCode = "KZT",
                    // PrimaryLocaleIETFBCP47Tag = "kk-KZ",
                },

                // Kenya
                new Country
                {
                    ISO31661Alpha2Code = "KE",
                    ISO31661Alpha3Code = "KEN",
                    ISO31661NumericCode = "404",
                    DisplayName = "Kenya",
                    OfficialName = "Republic of Kenya",
                    PhoneNumberPrefix = "254",

                    // PrimaryCurrencyISO4217AlphaCode = "KES",
                    // PrimaryLocaleIETFBCP47Tag = "sw-KE",
                },

                // Kiribati
                new Country
                {
                    ISO31661Alpha2Code = "KI",
                    ISO31661Alpha3Code = "KIR",
                    ISO31661NumericCode = "296",
                    DisplayName = "Kiribati",
                    OfficialName = "Republic of Kiribati",
                    PhoneNumberPrefix = "686",
                    PrimaryLocaleIETFBCP47Tag = "en-KI",

                    // PrimaryCurrencyISO4217AlphaCode = "AUD",
                },

                // Kosovo
                new Country
                {
                    ISO31661Alpha2Code = "XK",
                    ISO31661Alpha3Code = "XKX",
                    ISO31661NumericCode = "383",
                    DisplayName = "Kosovo",
                    OfficialName = "Republic of Kosovo",
                    PhoneNumberPrefix = "383",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "sq-XK",
                },

                // North Korea
                new Country
                {
                    ISO31661Alpha2Code = "KP",
                    ISO31661Alpha3Code = "PRK",
                    ISO31661NumericCode = "408",
                    DisplayName = "North Korea",
                    OfficialName = "Democratic People's Republic of Korea",
                    PhoneNumberPrefix = "850",

                    // PrimaryCurrencyISO4217AlphaCode = "KPW",
                    // PrimaryLocaleIETFBCP47Tag = "ko-KP",
                },

                // South Korea
                new Country
                {
                    ISO31661Alpha2Code = "KR",
                    ISO31661Alpha3Code = "KOR",
                    ISO31661NumericCode = "410",
                    DisplayName = "South Korea",
                    OfficialName = "Republic of Korea",
                    PhoneNumberPrefix = "82",

                    // PrimaryCurrencyISO4217AlphaCode = "KRW",
                    // PrimaryLocaleIETFBCP47Tag = "ko-KR",
                },

                // Kuwait
                new Country
                {
                    ISO31661Alpha2Code = "KW",
                    ISO31661Alpha3Code = "KWT",
                    ISO31661NumericCode = "414",
                    DisplayName = "Kuwait",
                    OfficialName = "State of Kuwait",
                    PhoneNumberPrefix = "965",

                    // PrimaryCurrencyISO4217AlphaCode = "KWD",
                    // PrimaryLocaleIETFBCP47Tag = "ar-KW",
                },

                // Kyrgyzstan
                new Country
                {
                    ISO31661Alpha2Code = "KG",
                    ISO31661Alpha3Code = "KGZ",
                    ISO31661NumericCode = "417",
                    DisplayName = "Kyrgyzstan",
                    OfficialName = "Kyrgyz Republic",
                    PhoneNumberPrefix = "996",

                    // PrimaryCurrencyISO4217AlphaCode = "KGS",
                    // PrimaryLocaleIETFBCP47Tag = "ky-KG",
                },

                // Laos
                new Country
                {
                    ISO31661Alpha2Code = "LA",
                    ISO31661Alpha3Code = "LAO",
                    ISO31661NumericCode = "418",
                    DisplayName = "Laos",
                    OfficialName = "Lao People's Democratic Republic",
                    PhoneNumberPrefix = "856",

                    // PrimaryCurrencyISO4217AlphaCode = "LAK",
                    // PrimaryLocaleIETFBCP47Tag = "lo-LA",
                },

                // Latvia
                new Country
                {
                    ISO31661Alpha2Code = "LV",
                    ISO31661Alpha3Code = "LVA",
                    ISO31661NumericCode = "428",
                    DisplayName = "Latvia",
                    OfficialName = "Republic of Latvia",
                    PhoneNumberPrefix = "371",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "lv-LV",
                },

                // Lebanon
                new Country
                {
                    ISO31661Alpha2Code = "LB",
                    ISO31661Alpha3Code = "LBN",
                    ISO31661NumericCode = "422",
                    DisplayName = "Lebanon",
                    OfficialName = "Lebanese Republic",
                    PhoneNumberPrefix = "961",

                    // PrimaryCurrencyISO4217AlphaCode = "LBP",
                    // PrimaryLocaleIETFBCP47Tag = "ar-LB",
                },

                // Lesotho
                new Country
                {
                    ISO31661Alpha2Code = "LS",
                    ISO31661Alpha3Code = "LSO",
                    ISO31661NumericCode = "426",
                    DisplayName = "Lesotho",
                    OfficialName = "Kingdom of Lesotho",
                    PhoneNumberPrefix = "266",
                    PrimaryLocaleIETFBCP47Tag = "en-LS",

                    // PrimaryCurrencyISO4217AlphaCode = "LSL",
                },

                // Liberia
                new Country
                {
                    ISO31661Alpha2Code = "LR",
                    ISO31661Alpha3Code = "LBR",
                    ISO31661NumericCode = "430",
                    DisplayName = "Liberia",
                    OfficialName = "Republic of Liberia",
                    PhoneNumberPrefix = "231",
                    PrimaryLocaleIETFBCP47Tag = "en-LR",

                    // PrimaryCurrencyISO4217AlphaCode = "LRD",
                },

                // Libya
                new Country
                {
                    ISO31661Alpha2Code = "LY",
                    ISO31661Alpha3Code = "LBY",
                    ISO31661NumericCode = "434",
                    DisplayName = "Libya",
                    OfficialName = "State of Libya",
                    PhoneNumberPrefix = "218",

                    // PrimaryCurrencyISO4217AlphaCode = "LYD",
                    // PrimaryLocaleIETFBCP47Tag = "ar-LY",
                },

                // Liechtenstein
                new Country
                {
                    ISO31661Alpha2Code = "LI",
                    ISO31661Alpha3Code = "LIE",
                    ISO31661NumericCode = "438",
                    DisplayName = "Liechtenstein",
                    OfficialName = "Principality of Liechtenstein",
                    PhoneNumberPrefix = "423",
                    PrimaryLocaleIETFBCP47Tag = "de-LI",

                    // PrimaryCurrencyISO4217AlphaCode = "CHF",
                },

                // Lithuania
                new Country
                {
                    ISO31661Alpha2Code = "LT",
                    ISO31661Alpha3Code = "LTU",
                    ISO31661NumericCode = "440",
                    DisplayName = "Lithuania",
                    OfficialName = "Republic of Lithuania",
                    PhoneNumberPrefix = "370",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "lt-LT",
                },

                // Luxembourg
                new Country
                {
                    ISO31661Alpha2Code = "LU",
                    ISO31661Alpha3Code = "LUX",
                    ISO31661NumericCode = "442",
                    DisplayName = "Luxembourg",
                    OfficialName = "Grand Duchy of Luxembourg",
                    PhoneNumberPrefix = "352",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "lb-LU",
                },

                // Macao (China)
                new Country
                {
                    SovereignISO31661Alpha2Code = "CN",

                    ISO31661Alpha2Code = "MO",
                    ISO31661Alpha3Code = "MAC",
                    ISO31661NumericCode = "446",
                    DisplayName = "Macao",
                    OfficialName = "Macao Special Administrative Region of China",
                    PhoneNumberPrefix = "853",

                    // PrimaryCurrencyISO4217AlphaCode = "MOP",
                    // PrimaryLocaleIETFBCP47Tag = "zh-MO",
                },

                // Madagascar
                new Country
                {
                    ISO31661Alpha2Code = "MG",
                    ISO31661Alpha3Code = "MDG",
                    ISO31661NumericCode = "450",
                    DisplayName = "Madagascar",
                    OfficialName = "Republic of Madagascar",
                    PhoneNumberPrefix = "261",

                    // PrimaryCurrencyISO4217AlphaCode = "MGA",
                    // PrimaryLocaleIETFBCP47Tag = "mg-MG",
                },

                // Malawi
                new Country
                {
                    ISO31661Alpha2Code = "MW",
                    ISO31661Alpha3Code = "MWI",
                    ISO31661NumericCode = "454",
                    DisplayName = "Malawi",
                    OfficialName = "Republic of Malawi",
                    PhoneNumberPrefix = "265",
                    PrimaryLocaleIETFBCP47Tag = "en-MW",

                    // PrimaryCurrencyISO4217AlphaCode = "MWK",
                },

                // Malaysia
                new Country
                {
                    ISO31661Alpha2Code = "MY",
                    ISO31661Alpha3Code = "MYS",
                    ISO31661NumericCode = "458",
                    DisplayName = "Malaysia",
                    OfficialName = "Malaysia",
                    PhoneNumberPrefix = "60",

                    // PrimaryCurrencyISO4217AlphaCode = "MYR",
                    // PrimaryLocaleIETFBCP47Tag = "ms-MY",
                },

                // Maldives
                new Country
                {
                    ISO31661Alpha2Code = "MV",
                    ISO31661Alpha3Code = "MDV",
                    ISO31661NumericCode = "462",
                    DisplayName = "Maldives",
                    OfficialName = "Republic of Maldives",
                    PhoneNumberPrefix = "960",

                    // PrimaryCurrencyISO4217AlphaCode = "MVR",
                    // PrimaryLocaleIETFBCP47Tag = "dv-MV",
                },

                // Mali
                new Country
                {
                    ISO31661Alpha2Code = "ML",
                    ISO31661Alpha3Code = "MLI",
                    ISO31661NumericCode = "466",
                    DisplayName = "Mali",
                    OfficialName = "Republic of Mali",
                    PhoneNumberPrefix = "223",
                    PrimaryLocaleIETFBCP47Tag = "fr-ML",

                    // PrimaryCurrencyISO4217AlphaCode = "XOF",
                },

                // Malta
                new Country
                {
                    ISO31661Alpha2Code = "MT",
                    ISO31661Alpha3Code = "MLT",
                    ISO31661NumericCode = "470",
                    DisplayName = "Malta",
                    OfficialName = "Republic of Malta",
                    PhoneNumberPrefix = "356",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "mt-MT",
                },

                // Marshall Islands
                new Country
                {
                    ISO31661Alpha2Code = "MH",
                    ISO31661Alpha3Code = "MHL",
                    ISO31661NumericCode = "584",
                    DisplayName = "Marshall Islands",
                    OfficialName = "Republic of the Marshall Islands",
                    PhoneNumberPrefix = "692",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-MH",
                },

                // Martinique (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "MQ",
                    ISO31661Alpha3Code = "MTQ",
                    ISO31661NumericCode = "474",
                    DisplayName = "Martinique",
                    OfficialName = "Martinique",
                    PhoneNumberPrefix = "596",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-MQ",
                },

                // Mauritania
                new Country
                {
                    ISO31661Alpha2Code = "MR",
                    ISO31661Alpha3Code = "MRT",
                    ISO31661NumericCode = "478",
                    DisplayName = "Mauritania",
                    OfficialName = "Islamic Republic of Mauritania",
                    PhoneNumberPrefix = "222",

                    // PrimaryCurrencyISO4217AlphaCode = "MRU",
                    // PrimaryLocaleIETFBCP47Tag = "ar-MR",
                },

                // Mauritius
                new Country
                {
                    ISO31661Alpha2Code = "MU",
                    ISO31661Alpha3Code = "MUS",
                    ISO31661NumericCode = "480",
                    DisplayName = "Mauritius",
                    OfficialName = "Republic of Mauritius",
                    PhoneNumberPrefix = "230",
                    PrimaryLocaleIETFBCP47Tag = "en-MU",

                    // PrimaryCurrencyISO4217AlphaCode = "MUR",
                },

                // Mayotte (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "YT",
                    ISO31661Alpha3Code = "MYT",
                    ISO31661NumericCode = "175",
                    DisplayName = "Mayotte",
                    OfficialName = "Department of Mayotte",
                    PhoneNumberPrefix = "262",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-YT",
                },

                // Mexico
                new Country
                {
                    ISO31661Alpha2Code = "MX",
                    ISO31661Alpha3Code = "MEX",
                    ISO31661NumericCode = "484",
                    DisplayName = "Mexico",
                    OfficialName = "United Mexican States",
                    PhoneNumberPrefix = "52",
                    PrimaryLocaleIETFBCP47Tag = "es-MX",

                    // PrimaryCurrencyISO4217AlphaCode = "MXN",
                },

                // Micronesia
                new Country
                {
                    ISO31661Alpha2Code = "FM",
                    ISO31661Alpha3Code = "FSM",
                    ISO31661NumericCode = "583",
                    DisplayName = "Micronesia",
                    OfficialName = "Federated States of Micronesia",
                    PhoneNumberPrefix = "691",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-FM",
                },

                // Moldova
                new Country
                {
                    ISO31661Alpha2Code = "MD",
                    ISO31661Alpha3Code = "MDA",
                    ISO31661NumericCode = "498",
                    DisplayName = "Moldova",
                    OfficialName = "Republic of Moldova",
                    PhoneNumberPrefix = "373",

                    // PrimaryCurrencyISO4217AlphaCode = "MDL",
                    // PrimaryLocaleIETFBCP47Tag = "ro-MD",
                },

                // Monaco
                new Country
                {
                    ISO31661Alpha2Code = "MC",
                    ISO31661Alpha3Code = "MCO",
                    ISO31661NumericCode = "492",
                    DisplayName = "Monaco",
                    OfficialName = "Principality of Monaco",
                    PhoneNumberPrefix = "377",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-MC",
                },

                // Mongolia
                new Country
                {
                    ISO31661Alpha2Code = "MN",
                    ISO31661Alpha3Code = "MNG",
                    ISO31661NumericCode = "496",
                    DisplayName = "Mongolia",
                    OfficialName = "Mongolia",
                    PhoneNumberPrefix = "976",

                    // PrimaryCurrencyISO4217AlphaCode = "MNT",
                    // PrimaryLocaleIETFBCP47Tag = "mn-MN",
                },

                // Montenegro
                new Country
                {
                    ISO31661Alpha2Code = "ME",
                    ISO31661Alpha3Code = "MNE",
                    ISO31661NumericCode = "499",
                    DisplayName = "Montenegro",
                    OfficialName = "Montenegro",
                    PhoneNumberPrefix = "382",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "sr-ME",
                },

                // Montserrat (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "MS",
                    ISO31661Alpha3Code = "MSR",
                    ISO31661NumericCode = "500",
                    DisplayName = "Montserrat",
                    OfficialName = "Montserrat",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-MS",

                    // PrimaryCurrencyISO4217AlphaCode = "XCD",
                },

                // Morocco
                new Country
                {
                    ISO31661Alpha2Code = "MA",
                    ISO31661Alpha3Code = "MAR",
                    ISO31661NumericCode = "504",
                    DisplayName = "Morocco",
                    OfficialName = "Kingdom of Morocco",
                    PhoneNumberPrefix = "212",

                    // PrimaryCurrencyISO4217AlphaCode = "MAD",
                    // PrimaryLocaleIETFBCP47Tag = "ar-MA",
                },

                // Mozambique
                new Country
                {
                    ISO31661Alpha2Code = "MZ",
                    ISO31661Alpha3Code = "MOZ",
                    ISO31661NumericCode = "508",
                    DisplayName = "Mozambique",
                    OfficialName = "Republic of Mozambique",
                    PhoneNumberPrefix = "258",

                    // PrimaryCurrencyISO4217AlphaCode = "MZN",
                    // PrimaryLocaleIETFBCP47Tag = "pt-MZ",
                },

                // Myanmar
                new Country
                {
                    ISO31661Alpha2Code = "MM",
                    ISO31661Alpha3Code = "MMR",
                    ISO31661NumericCode = "104",
                    DisplayName = "Myanmar",
                    OfficialName = "Republic of the Union of Myanmar",
                    PhoneNumberPrefix = "95",

                    // PrimaryCurrencyISO4217AlphaCode = "MMK",
                    // PrimaryLocaleIETFBCP47Tag = "my-MM",
                },

                // Namibia
                new Country
                {
                    ISO31661Alpha2Code = "NA",
                    ISO31661Alpha3Code = "NAM",
                    ISO31661NumericCode = "516",
                    DisplayName = "Namibia",
                    OfficialName = "Republic of Namibia",
                    PhoneNumberPrefix = "264",
                    PrimaryLocaleIETFBCP47Tag = "en-NA",

                    // PrimaryCurrencyISO4217AlphaCode = "NAD",
                },

                // Nauru
                new Country
                {
                    ISO31661Alpha2Code = "NR",
                    ISO31661Alpha3Code = "NRU",
                    ISO31661NumericCode = "520",
                    DisplayName = "Nauru",
                    OfficialName = "Republic of Nauru",
                    PhoneNumberPrefix = "674",
                    PrimaryLocaleIETFBCP47Tag = "en-NR",

                    // PrimaryCurrencyISO4217AlphaCode = "AUD",
                },

                // Nepal
                new Country
                {
                    ISO31661Alpha2Code = "NP",
                    ISO31661Alpha3Code = "NPL",
                    ISO31661NumericCode = "524",
                    DisplayName = "Nepal",
                    OfficialName = "Federal Democratic Republic of Nepal",
                    PhoneNumberPrefix = "977",

                    // PrimaryCurrencyISO4217AlphaCode = "NPR",
                    // PrimaryLocaleIETFBCP47Tag = "ne-NP",
                },

                // Netherlands
                new Country
                {
                    ISO31661Alpha2Code = "NL",
                    ISO31661Alpha3Code = "NLD",
                    ISO31661NumericCode = "528",
                    DisplayName = "Netherlands",
                    OfficialName = "Kingdom of the Netherlands",
                    PhoneNumberPrefix = "31",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "nl-NL",
                },

                // New Caledonia (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "NC",
                    ISO31661Alpha3Code = "NCL",
                    ISO31661NumericCode = "540",
                    DisplayName = "New Caledonia",
                    OfficialName = "New Caledonia",
                    PhoneNumberPrefix = "687",
                    PrimaryLocaleIETFBCP47Tag = "fr-NC",

                    // PrimaryCurrencyISO4217AlphaCode = "XPF",
                },

                // New Zealand
                new Country
                {
                    ISO31661Alpha2Code = "NZ",
                    ISO31661Alpha3Code = "NZL",
                    ISO31661NumericCode = "554",
                    DisplayName = "New Zealand",
                    OfficialName = "New Zealand",
                    PhoneNumberPrefix = "64",
                    PrimaryLocaleIETFBCP47Tag = "en-NZ",

                    // PrimaryCurrencyISO4217AlphaCode = "NZD",
                },

                // Nicaragua
                new Country
                {
                    ISO31661Alpha2Code = "NI",
                    ISO31661Alpha3Code = "NIC",
                    ISO31661NumericCode = "558",
                    DisplayName = "Nicaragua",
                    OfficialName = "Republic of Nicaragua",
                    PhoneNumberPrefix = "505",
                    PrimaryLocaleIETFBCP47Tag = "es-NI",

                    // PrimaryCurrencyISO4217AlphaCode = "NIO",
                },

                // Niger
                new Country
                {
                    ISO31661Alpha2Code = "NE",
                    ISO31661Alpha3Code = "NER",
                    ISO31661NumericCode = "562",
                    DisplayName = "Niger",
                    OfficialName = "Republic of the Niger",
                    PhoneNumberPrefix = "227",
                    PrimaryLocaleIETFBCP47Tag = "fr-NE",

                    // PrimaryCurrencyISO4217AlphaCode = "XOF",
                },

                // Nigeria
                new Country
                {
                    ISO31661Alpha2Code = "NG",
                    ISO31661Alpha3Code = "NGA",
                    ISO31661NumericCode = "566",
                    DisplayName = "Nigeria",
                    OfficialName = "Federal Republic of Nigeria",
                    PhoneNumberPrefix = "234",
                    PrimaryLocaleIETFBCP47Tag = "en-NG",

                    // PrimaryCurrencyISO4217AlphaCode = "NGN",
                },

                // Niue (New Zealand)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NZ",

                    ISO31661Alpha2Code = "NU",
                    ISO31661Alpha3Code = "NIU",
                    ISO31661NumericCode = "570",
                    DisplayName = "Niue",
                    OfficialName = "Niue",
                    PhoneNumberPrefix = "683",
                    PrimaryLocaleIETFBCP47Tag = "en-NU",

                    // PrimaryCurrencyISO4217AlphaCode = "NZD",
                },

                // Norfolk Island (Australia)
                new Country
                {
                    SovereignISO31661Alpha2Code = "AU",

                    ISO31661Alpha2Code = "NF",
                    ISO31661Alpha3Code = "NFK",
                    ISO31661NumericCode = "574",
                    DisplayName = "Norfolk Island",
                    OfficialName = "Territory of Norfolk Island",
                    PhoneNumberPrefix = "672",
                    PrimaryLocaleIETFBCP47Tag = "en-NF",

                    // PrimaryCurrencyISO4217AlphaCode = "AUD",
                },

                // North Macedonia
                new Country
                {
                    ISO31661Alpha2Code = "MK",
                    ISO31661Alpha3Code = "MKD",
                    ISO31661NumericCode = "807",
                    DisplayName = "North Macedonia",
                    OfficialName = "Republic of North Macedonia",
                    PhoneNumberPrefix = "389",

                    // PrimaryCurrencyISO4217AlphaCode = "MKD",
                    // PrimaryLocaleIETFBCP47Tag = "mk-MK",
                },

                // Northern Mariana Islands (United States)
                new Country
                {
                    SovereignISO31661Alpha2Code = "US",

                    ISO31661Alpha2Code = "MP",
                    ISO31661Alpha3Code = "MNP",
                    ISO31661NumericCode = "580",
                    DisplayName = "Northern Mariana Islands",
                    OfficialName = "Commonwealth of the Northern Mariana Islands",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-MP",
                },

                // Norway
                new Country
                {
                    ISO31661Alpha2Code = "NO",
                    ISO31661Alpha3Code = "NOR",
                    ISO31661NumericCode = "578",
                    DisplayName = "Norway",
                    OfficialName = "Kingdom of Norway",
                    PhoneNumberPrefix = "47",

                    // PrimaryCurrencyISO4217AlphaCode = "NOK",
                    // PrimaryLocaleIETFBCP47Tag = "nb-NO",
                },

                // Oman
                new Country
                {
                    ISO31661Alpha2Code = "OM",
                    ISO31661Alpha3Code = "OMN",
                    ISO31661NumericCode = "512",
                    DisplayName = "Oman",
                    OfficialName = "Sultanate of Oman",
                    PhoneNumberPrefix = "968",

                    // PrimaryCurrencyISO4217AlphaCode = "OMR",
                    // PrimaryLocaleIETFBCP47Tag = "ar-OM",
                },

                // Pakistan
                new Country
                {
                    ISO31661Alpha2Code = "PK",
                    ISO31661Alpha3Code = "PAK",
                    ISO31661NumericCode = "586",
                    DisplayName = "Pakistan",
                    OfficialName = "Islamic Republic of Pakistan",
                    PhoneNumberPrefix = "92",

                    // PrimaryCurrencyISO4217AlphaCode = "PKR",
                    // PrimaryLocaleIETFBCP47Tag = "ur-PK",
                },

                // Palau
                new Country
                {
                    ISO31661Alpha2Code = "PW",
                    ISO31661Alpha3Code = "PLW",
                    ISO31661NumericCode = "585",
                    DisplayName = "Palau",
                    OfficialName = "Republic of Palau",
                    PhoneNumberPrefix = "680",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-PW",
                },

                // Palestine
                new Country
                {
                    ISO31661Alpha2Code = "PS",
                    ISO31661Alpha3Code = "PSE",
                    ISO31661NumericCode = "275",
                    DisplayName = "Palestine",
                    OfficialName = "State of Palestine",
                    PhoneNumberPrefix = "970",

                    // PrimaryCurrencyISO4217AlphaCode = "ILS",
                    // PrimaryLocaleIETFBCP47Tag = "ar-PS",
                },

                // Panama
                new Country
                {
                    ISO31661Alpha2Code = "PA",
                    ISO31661Alpha3Code = "PAN",
                    ISO31661NumericCode = "591",
                    DisplayName = "Panama",
                    OfficialName = "Republic of Panama",
                    PhoneNumberPrefix = "507",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "es-PA",
                },

                // Papua New Guinea
                new Country
                {
                    ISO31661Alpha2Code = "PG",
                    ISO31661Alpha3Code = "PNG",
                    ISO31661NumericCode = "598",
                    DisplayName = "Papua New Guinea",
                    OfficialName = "Independent State of Papua New Guinea",
                    PhoneNumberPrefix = "675",
                    PrimaryLocaleIETFBCP47Tag = "en-PG",

                    // PrimaryCurrencyISO4217AlphaCode = "PGK",
                },

                // Paraguay
                new Country
                {
                    ISO31661Alpha2Code = "PY",
                    ISO31661Alpha3Code = "PRY",
                    ISO31661NumericCode = "600",
                    DisplayName = "Paraguay",
                    OfficialName = "Republic of Paraguay",
                    PhoneNumberPrefix = "595",
                    PrimaryLocaleIETFBCP47Tag = "es-PY",

                    // PrimaryCurrencyISO4217AlphaCode = "PYG",
                },

                // Peru
                new Country
                {
                    ISO31661Alpha2Code = "PE",
                    ISO31661Alpha3Code = "PER",
                    ISO31661NumericCode = "604",
                    DisplayName = "Peru",
                    OfficialName = "Republic of Peru",
                    PhoneNumberPrefix = "51",
                    PrimaryLocaleIETFBCP47Tag = "es-PE",

                    // PrimaryCurrencyISO4217AlphaCode = "PEN",
                },

                // Philippines
                new Country
                {
                    ISO31661Alpha2Code = "PH",
                    ISO31661Alpha3Code = "PHL",
                    ISO31661NumericCode = "608",
                    DisplayName = "Philippines",
                    OfficialName = "Republic of the Philippines",
                    PhoneNumberPrefix = "63",
                    PrimaryLocaleIETFBCP47Tag = "en-PH",

                    // PrimaryCurrencyISO4217AlphaCode = "PHP",
                },

                // Pitcairn Islands (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "PN",
                    ISO31661Alpha3Code = "PCN",
                    ISO31661NumericCode = "612",
                    DisplayName = "Pitcairn Islands",
                    OfficialName = "Pitcairn, Henderson, Ducie and Oeno Islands",
                    PhoneNumberPrefix = "64",
                    PrimaryLocaleIETFBCP47Tag = "en-PN",

                    // PrimaryCurrencyISO4217AlphaCode = "NZD",
                },

                // Poland
                new Country
                {
                    ISO31661Alpha2Code = "PL",
                    ISO31661Alpha3Code = "POL",
                    ISO31661NumericCode = "616",
                    DisplayName = "Poland",
                    OfficialName = "Republic of Poland",
                    PhoneNumberPrefix = "48",

                    // PrimaryCurrencyISO4217AlphaCode = "PLN",
                    // PrimaryLocaleIETFBCP47Tag = "pl-PL",
                },

                // Portugal
                new Country
                {
                    ISO31661Alpha2Code = "PT",
                    ISO31661Alpha3Code = "PRT",
                    ISO31661NumericCode = "620",
                    DisplayName = "Portugal",
                    OfficialName = "Portuguese Republic",
                    PhoneNumberPrefix = "351",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "pt-PT",
                },

                // Puerto Rico (United States)
                new Country
                {
                    SovereignISO31661Alpha2Code = "US",

                    ISO31661Alpha2Code = "PR",
                    ISO31661Alpha3Code = "PRI",
                    ISO31661NumericCode = "630",
                    DisplayName = "Puerto Rico",
                    OfficialName = "Commonwealth of Puerto Rico",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "es-PR",
                },

                // Qatar
                new Country
                {
                    ISO31661Alpha2Code = "QA",
                    ISO31661Alpha3Code = "QAT",
                    ISO31661NumericCode = "634",
                    DisplayName = "Qatar",
                    OfficialName = "State of Qatar",
                    PhoneNumberPrefix = "974",

                    // PrimaryCurrencyISO4217AlphaCode = "QAR",
                    // PrimaryLocaleIETFBCP47Tag = "ar-QA",
                },

                // Réunion (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "RE",
                    ISO31661Alpha3Code = "REU",
                    ISO31661NumericCode = "638",
                    DisplayName = "Réunion",
                    OfficialName = "Réunion",
                    PhoneNumberPrefix = "262",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-RE",
                },

                // Romania
                new Country
                {
                    ISO31661Alpha2Code = "RO",
                    ISO31661Alpha3Code = "ROU",
                    ISO31661NumericCode = "642",
                    DisplayName = "Romania",
                    OfficialName = "Romania",
                    PhoneNumberPrefix = "40",

                    // PrimaryCurrencyISO4217AlphaCode = "RON",
                    // PrimaryLocaleIETFBCP47Tag = "ro-RO",
                },

                // Russia
                new Country
                {
                    ISO31661Alpha2Code = "RU",
                    ISO31661Alpha3Code = "RUS",
                    ISO31661NumericCode = "643",
                    DisplayName = "Russia",
                    OfficialName = "Russian Federation",
                    PhoneNumberPrefix = "7",

                    // PrimaryCurrencyISO4217AlphaCode = "RUB",
                    // PrimaryLocaleIETFBCP47Tag = "ru-RU",
                },

                // Rwanda
                new Country
                {
                    ISO31661Alpha2Code = "RW",
                    ISO31661Alpha3Code = "RWA",
                    ISO31661NumericCode = "646",
                    DisplayName = "Rwanda",
                    OfficialName = "Republic of Rwanda",
                    PhoneNumberPrefix = "250",

                    // PrimaryCurrencyISO4217AlphaCode = "RWF",
                    // PrimaryLocaleIETFBCP47Tag = "rw-RW",
                },

                // Saint Barthélemy (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "BL",
                    ISO31661Alpha3Code = "BLM",
                    ISO31661NumericCode = "652",
                    DisplayName = "Saint Barthélemy",
                    OfficialName = "Collectivity of Saint Barthélemy",
                    PhoneNumberPrefix = "590",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-BL",
                },

                // Saint Helena, Ascension and Tristan da Cunha (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "SH",
                    ISO31661Alpha3Code = "SHN",
                    ISO31661NumericCode = "654",
                    DisplayName = "Saint Helena, Ascension and Tristan da Cunha",
                    OfficialName = "Saint Helena, Ascension and Tristan da Cunha",
                    PhoneNumberPrefix = "290",
                    PrimaryLocaleIETFBCP47Tag = "en-SH",

                    // PrimaryCurrencyISO4217AlphaCode = "SHP",
                },

                // Saint Kitts and Nevis
                new Country
                {
                    ISO31661Alpha2Code = "KN",
                    ISO31661Alpha3Code = "KNA",
                    ISO31661NumericCode = "659",
                    DisplayName = "Saint Kitts and Nevis",
                    OfficialName = "Federation of Saint Christopher and Nevis",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-KN",

                    // PrimaryCurrencyISO4217AlphaCode = "XCD",
                },

                // Saint Lucia
                new Country
                {
                    ISO31661Alpha2Code = "LC",
                    ISO31661Alpha3Code = "LCA",
                    ISO31661NumericCode = "662",
                    DisplayName = "Saint Lucia",
                    OfficialName = "Saint Lucia",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-LC",

                    // PrimaryCurrencyISO4217AlphaCode = "XCD",
                },

                // Saint Martin (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "MF",
                    ISO31661Alpha3Code = "MAF",
                    ISO31661NumericCode = "663",
                    DisplayName = "Saint Martin",
                    OfficialName = "Collectivity of Saint Martin",
                    PhoneNumberPrefix = "590",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-MF",
                },

                // Saint Pierre and Miquelon (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "PM",
                    ISO31661Alpha3Code = "SPM",
                    ISO31661NumericCode = "666",
                    DisplayName = "Saint Pierre and Miquelon",
                    OfficialName = "Saint Pierre and Miquelon",
                    PhoneNumberPrefix = "508",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "fr-PM",
                },

                // Saint Vincent and the Grenadines
                new Country
                {
                    ISO31661Alpha2Code = "VC",
                    ISO31661Alpha3Code = "VCT",
                    ISO31661NumericCode = "670",
                    DisplayName = "Saint Vincent and the Grenadines",
                    OfficialName = "Saint Vincent and the Grenadines",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-VC",

                    // PrimaryCurrencyISO4217AlphaCode = "XCD",
                },

                // Samoa
                new Country
                {
                    ISO31661Alpha2Code = "WS",
                    ISO31661Alpha3Code = "WSM",
                    ISO31661NumericCode = "882",
                    DisplayName = "Samoa",
                    OfficialName = "Independent State of Samoa",
                    PhoneNumberPrefix = "685",

                    // PrimaryCurrencyISO4217AlphaCode = "WST",
                    // PrimaryLocaleIETFBCP47Tag = "sm-WS",
                },

                // San Marino
                new Country
                {
                    ISO31661Alpha2Code = "SM",
                    ISO31661Alpha3Code = "SMR",
                    ISO31661NumericCode = "674",
                    DisplayName = "San Marino",
                    OfficialName = "Republic of San Marino",
                    PhoneNumberPrefix = "378",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",
                    PrimaryLocaleIETFBCP47Tag = "it-SM",
                },

                // São Tomé and Príncipe
                new Country
                {
                    ISO31661Alpha2Code = "ST",
                    ISO31661Alpha3Code = "STP",
                    ISO31661NumericCode = "678",
                    DisplayName = "São Tomé and Príncipe",
                    OfficialName = "Democratic Republic of São Tomé and Príncipe",
                    PhoneNumberPrefix = "239",

                    // PrimaryCurrencyISO4217AlphaCode = "STN",
                    // PrimaryLocaleIETFBCP47Tag = "pt-ST",
                },

                // Saudi Arabia
                new Country
                {
                    ISO31661Alpha2Code = "SA",
                    ISO31661Alpha3Code = "SAU",
                    ISO31661NumericCode = "682",
                    DisplayName = "Saudi Arabia",
                    OfficialName = "Kingdom of Saudi Arabia",
                    PhoneNumberPrefix = "966",

                    // PrimaryCurrencyISO4217AlphaCode = "SAR",
                    // PrimaryLocaleIETFBCP47Tag = "ar-SA",
                },

                // Senegal
                new Country
                {
                    ISO31661Alpha2Code = "SN",
                    ISO31661Alpha3Code = "SEN",
                    ISO31661NumericCode = "686",
                    DisplayName = "Senegal",
                    OfficialName = "Republic of Senegal",
                    PhoneNumberPrefix = "221",
                    PrimaryLocaleIETFBCP47Tag = "fr-SN",

                    // PrimaryCurrencyISO4217AlphaCode = "XOF",
                },

                // Serbia
                new Country
                {
                    ISO31661Alpha2Code = "RS",
                    ISO31661Alpha3Code = "SRB",
                    ISO31661NumericCode = "688",
                    DisplayName = "Serbia",
                    OfficialName = "Republic of Serbia",
                    PhoneNumberPrefix = "381",

                    // PrimaryCurrencyISO4217AlphaCode = "RSD",
                    // PrimaryLocaleIETFBCP47Tag = "sr-RS",
                },

                // Seychelles
                new Country
                {
                    ISO31661Alpha2Code = "SC",
                    ISO31661Alpha3Code = "SYC",
                    ISO31661NumericCode = "690",
                    DisplayName = "Seychelles",
                    OfficialName = "Republic of Seychelles",
                    PhoneNumberPrefix = "248",
                    PrimaryLocaleIETFBCP47Tag = "en-SC",

                    // PrimaryCurrencyISO4217AlphaCode = "SCR",
                },

                // Sierra Leone
                new Country
                {
                    ISO31661Alpha2Code = "SL",
                    ISO31661Alpha3Code = "SLE",
                    ISO31661NumericCode = "694",
                    DisplayName = "Sierra Leone",
                    OfficialName = "Republic of Sierra Leone",
                    PhoneNumberPrefix = "232",
                    PrimaryLocaleIETFBCP47Tag = "en-SL",

                    // PrimaryCurrencyISO4217AlphaCode = "SLE",
                },

                // Singapore
                new Country
                {
                    ISO31661Alpha2Code = "SG",
                    ISO31661Alpha3Code = "SGP",
                    ISO31661NumericCode = "702",
                    DisplayName = "Singapore",
                    OfficialName = "Republic of Singapore",
                    PhoneNumberPrefix = "65",
                    PrimaryLocaleIETFBCP47Tag = "en-SG",

                    // PrimaryCurrencyISO4217AlphaCode = "SGD",
                },

                // Sint Maarten (Netherlands)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NL",

                    ISO31661Alpha2Code = "SX",
                    ISO31661Alpha3Code = "SXM",
                    ISO31661NumericCode = "534",
                    DisplayName = "Sint Maarten",
                    OfficialName = "Sint Maarten",
                    PhoneNumberPrefix = "1",

                    // PrimaryCurrencyISO4217AlphaCode = "ANG",
                    // PrimaryLocaleIETFBCP47Tag = "nl-SX",
                },

                // Slovakia
                new Country
                {
                    ISO31661Alpha2Code = "SK",
                    ISO31661Alpha3Code = "SVK",
                    ISO31661NumericCode = "703",
                    DisplayName = "Slovakia",
                    OfficialName = "Slovak Republic",
                    PhoneNumberPrefix = "421",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "sk-SK",
                },

                // Slovenia
                new Country
                {
                    ISO31661Alpha2Code = "SI",
                    ISO31661Alpha3Code = "SVN",
                    ISO31661NumericCode = "705",
                    DisplayName = "Slovenia",
                    OfficialName = "Republic of Slovenia",
                    PhoneNumberPrefix = "386",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "sl-SI",
                },

                // Solomon Islands
                new Country
                {
                    ISO31661Alpha2Code = "SB",
                    ISO31661Alpha3Code = "SLB",
                    ISO31661NumericCode = "090",
                    DisplayName = "Solomon Islands",
                    OfficialName = "Solomon Islands",
                    PhoneNumberPrefix = "677",
                    PrimaryLocaleIETFBCP47Tag = "en-SB",

                    // PrimaryCurrencyISO4217AlphaCode = "SBD",
                },

                // Somalia
                new Country
                {
                    ISO31661Alpha2Code = "SO",
                    ISO31661Alpha3Code = "SOM",
                    ISO31661NumericCode = "706",
                    DisplayName = "Somalia",
                    OfficialName = "Federal Republic of Somalia",
                    PhoneNumberPrefix = "252",

                    // PrimaryCurrencyISO4217AlphaCode = "SOS",
                    // PrimaryLocaleIETFBCP47Tag = "so-SO",
                },

                // South Africa
                new Country
                {
                    ISO31661Alpha2Code = "ZA",
                    ISO31661Alpha3Code = "ZAF",
                    ISO31661NumericCode = "710",
                    DisplayName = "South Africa",
                    OfficialName = "Republic of South Africa",
                    PhoneNumberPrefix = "27",
                    PrimaryLocaleIETFBCP47Tag = "en-ZA",
                },

                // South Georgia and the South Sandwich Islands (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "GS",
                    ISO31661Alpha3Code = "SGS",
                    ISO31661NumericCode = "239",
                    DisplayName = "South Georgia and the South Sandwich Islands",
                    OfficialName = "South Georgia and the South Sandwich Islands",
                    PhoneNumberPrefix = "44",
                },

                // South Sudan
                new Country
                {
                    ISO31661Alpha2Code = "SS",
                    ISO31661Alpha3Code = "SSD",
                    ISO31661NumericCode = "728",
                    DisplayName = "South Sudan",
                    OfficialName = "Republic of South Sudan",
                    PhoneNumberPrefix = "211",
                    PrimaryLocaleIETFBCP47Tag = "en-SS",

                    // PrimaryCurrencyISO4217AlphaCode = "SSP",
                },

                // Spain
                new Country
                {
                    ISO31661Alpha2Code = "ES",
                    ISO31661Alpha3Code = "ESP",
                    ISO31661NumericCode = "724",
                    DisplayName = "Spain",
                    OfficialName = "Kingdom of Spain",
                    PhoneNumberPrefix = "34",
                    PrimaryCurrencyISO4217AlphaCode = "EUR",

                    // PrimaryLocaleIETFBCP47Tag = "es-ES",
                },

                // Sri Lanka
                new Country
                {
                    ISO31661Alpha2Code = "LK",
                    ISO31661Alpha3Code = "LKA",
                    ISO31661NumericCode = "144",
                    DisplayName = "Sri Lanka",
                    OfficialName = "Democratic Socialist Republic of Sri Lanka",
                    PhoneNumberPrefix = "94",

                    // PrimaryCurrencyISO4217AlphaCode = "LKR",
                    // PrimaryLocaleIETFBCP47Tag = "si-LK",
                },

                // Sudan
                new Country
                {
                    ISO31661Alpha2Code = "SD",
                    ISO31661Alpha3Code = "SDN",
                    ISO31661NumericCode = "729",
                    DisplayName = "Sudan",
                    OfficialName = "Republic of the Sudan",
                    PhoneNumberPrefix = "249",

                    // PrimaryCurrencyISO4217AlphaCode = "SDG",
                    // PrimaryLocaleIETFBCP47Tag = "ar-SD",
                },

                // Suriname
                new Country
                {
                    ISO31661Alpha2Code = "SR",
                    ISO31661Alpha3Code = "SUR",
                    ISO31661NumericCode = "740",
                    DisplayName = "Suriname",
                    OfficialName = "Republic of Suriname",
                    PhoneNumberPrefix = "597",

                    // PrimaryCurrencyISO4217AlphaCode = "SRD",
                    // PrimaryLocaleIETFBCP47Tag = "nl-SR",
                },

                // Svalbard and Jan Mayen (Norway)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NO",

                    ISO31661Alpha2Code = "SJ",
                    ISO31661Alpha3Code = "SJM",
                    ISO31661NumericCode = "744",
                    DisplayName = "Svalbard and Jan Mayen",
                    OfficialName = "Svalbard and Jan Mayen",
                    PhoneNumberPrefix = "47",

                    // PrimaryCurrencyISO4217AlphaCode = "NOK",
                    // PrimaryLocaleIETFBCP47Tag = "nb-SJ",
                },

                // Sweden
                new Country
                {
                    ISO31661Alpha2Code = "SE",
                    ISO31661Alpha3Code = "SWE",
                    ISO31661NumericCode = "752",
                    DisplayName = "Sweden",
                    OfficialName = "Kingdom of Sweden",
                    PhoneNumberPrefix = "46",

                    // PrimaryCurrencyISO4217AlphaCode = "SEK",
                    // PrimaryLocaleIETFBCP47Tag = "sv-SE",
                },

                // Switzerland
                new Country
                {
                    ISO31661Alpha2Code = "CH",
                    ISO31661Alpha3Code = "CHE",
                    ISO31661NumericCode = "756",
                    DisplayName = "Switzerland",
                    OfficialName = "Swiss Confederation",
                    PhoneNumberPrefix = "41",
                    PrimaryLocaleIETFBCP47Tag = "de-CH",

                    // PrimaryCurrencyISO4217AlphaCode = "CHF",
                },

                // Syria
                new Country
                {
                    ISO31661Alpha2Code = "SY",
                    ISO31661Alpha3Code = "SYR",
                    ISO31661NumericCode = "760",
                    DisplayName = "Syria",
                    OfficialName = "Syrian Arab Republic",
                    PhoneNumberPrefix = "963",

                    // PrimaryCurrencyISO4217AlphaCode = "SYP",
                    // PrimaryLocaleIETFBCP47Tag = "ar-SY",
                },

                // Tajikistan
                new Country
                {
                    ISO31661Alpha2Code = "TJ",
                    ISO31661Alpha3Code = "TJK",
                    ISO31661NumericCode = "762",
                    DisplayName = "Tajikistan",
                    OfficialName = "Republic of Tajikistan",
                    PhoneNumberPrefix = "992",

                    // PrimaryCurrencyISO4217AlphaCode = "TJS",
                    // PrimaryLocaleIETFBCP47Tag = "tg-TJ",
                },

                // Tanzania
                new Country
                {
                    ISO31661Alpha2Code = "TZ",
                    ISO31661Alpha3Code = "TZA",
                    ISO31661NumericCode = "834",
                    DisplayName = "Tanzania",
                    OfficialName = "United Republic of Tanzania",
                    PhoneNumberPrefix = "255",

                    // PrimaryCurrencyISO4217AlphaCode = "TZS",
                    // PrimaryLocaleIETFBCP47Tag = "sw-TZ",
                },

                // Thailand
                new Country
                {
                    ISO31661Alpha2Code = "TH",
                    ISO31661Alpha3Code = "THA",
                    ISO31661NumericCode = "764",
                    DisplayName = "Thailand",
                    OfficialName = "Kingdom of Thailand",
                    PhoneNumberPrefix = "66",

                    // PrimaryCurrencyISO4217AlphaCode = "THB",
                    // PrimaryLocaleIETFBCP47Tag = "th-TH",
                },

                // Timor-Leste
                new Country
                {
                    ISO31661Alpha2Code = "TL",
                    ISO31661Alpha3Code = "TLS",
                    ISO31661NumericCode = "626",
                    DisplayName = "Timor-Leste",
                    OfficialName = "Democratic Republic of Timor-Leste",
                    PhoneNumberPrefix = "670",
                    PrimaryCurrencyISO4217AlphaCode = "USD",

                    // PrimaryLocaleIETFBCP47Tag = "pt-TL",
                },

                // Togo
                new Country
                {
                    ISO31661Alpha2Code = "TG",
                    ISO31661Alpha3Code = "TGO",
                    ISO31661NumericCode = "768",
                    DisplayName = "Togo",
                    OfficialName = "Togolese Republic",
                    PhoneNumberPrefix = "228",
                    PrimaryLocaleIETFBCP47Tag = "fr-TG",

                    // PrimaryCurrencyISO4217AlphaCode = "XOF",
                },

                // Tokelau (New Zealand)
                new Country
                {
                    SovereignISO31661Alpha2Code = "NZ",

                    ISO31661Alpha2Code = "TK",
                    ISO31661Alpha3Code = "TKL",
                    ISO31661NumericCode = "772",
                    DisplayName = "Tokelau",
                    OfficialName = "Tokelau",
                    PhoneNumberPrefix = "690",
                    PrimaryLocaleIETFBCP47Tag = "en-TK",

                    // PrimaryCurrencyISO4217AlphaCode = "NZD",
                },

                // Tonga
                new Country
                {
                    ISO31661Alpha2Code = "TO",
                    ISO31661Alpha3Code = "TON",
                    ISO31661NumericCode = "776",
                    DisplayName = "Tonga",
                    OfficialName = "Kingdom of Tonga",
                    PhoneNumberPrefix = "676",

                    // PrimaryCurrencyISO4217AlphaCode = "TOP",
                    // PrimaryLocaleIETFBCP47Tag = "to-TO",
                },

                // Trinidad and Tobago
                new Country
                {
                    ISO31661Alpha2Code = "TT",
                    ISO31661Alpha3Code = "TTO",
                    ISO31661NumericCode = "780",
                    DisplayName = "Trinidad and Tobago",
                    OfficialName = "Republic of Trinidad and Tobago",
                    PhoneNumberPrefix = "1",
                    PrimaryLocaleIETFBCP47Tag = "en-TT",

                    // PrimaryCurrencyISO4217AlphaCode = "TTD",
                },

                // Tunisia
                new Country
                {
                    ISO31661Alpha2Code = "TN",
                    ISO31661Alpha3Code = "TUN",
                    ISO31661NumericCode = "788",
                    DisplayName = "Tunisia",
                    OfficialName = "Republic of Tunisia",
                    PhoneNumberPrefix = "216",

                    // PrimaryCurrencyISO4217AlphaCode = "TND",
                    // PrimaryLocaleIETFBCP47Tag = "ar-TN",
                },

                // Turkey
                new Country
                {
                    ISO31661Alpha2Code = "TR",
                    ISO31661Alpha3Code = "TUR",
                    ISO31661NumericCode = "792",
                    DisplayName = "Türkiye",
                    OfficialName = "Republic of Türkiye",
                    PhoneNumberPrefix = "90",

                    // PrimaryCurrencyISO4217AlphaCode = "TRY",
                    // PrimaryLocaleIETFBCP47Tag = "tr-TR",
                },

                // Turkmenistan
                new Country
                {
                    ISO31661Alpha2Code = "TM",
                    ISO31661Alpha3Code = "TKM",
                    ISO31661NumericCode = "795",
                    DisplayName = "Turkmenistan",
                    OfficialName = "Turkmenistan",
                    PhoneNumberPrefix = "993",

                    // PrimaryCurrencyISO4217AlphaCode = "TMT",
                    // PrimaryLocaleIETFBCP47Tag = "tk-TM",
                },

                // Turks and Caicos Islands (United Kingdom)
                new Country
                {
                    SovereignISO31661Alpha2Code = "GB",

                    ISO31661Alpha2Code = "TC",
                    ISO31661Alpha3Code = "TCA",
                    ISO31661NumericCode = "796",
                    DisplayName = "Turks and Caicos Islands",
                    OfficialName = "Turks and Caicos Islands",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-TC",
                },

                // Tuvalu
                new Country
                {
                    ISO31661Alpha2Code = "TV",
                    ISO31661Alpha3Code = "TUV",
                    ISO31661NumericCode = "798",
                    DisplayName = "Tuvalu",
                    OfficialName = "Tuvalu",
                    PhoneNumberPrefix = "688",
                    PrimaryLocaleIETFBCP47Tag = "en-TV",

                    // PrimaryCurrencyISO4217AlphaCode = "AUD",
                },

                // Uganda
                new Country
                {
                    ISO31661Alpha2Code = "UG",
                    ISO31661Alpha3Code = "UGA",
                    ISO31661NumericCode = "800",
                    DisplayName = "Uganda",
                    OfficialName = "Republic of Uganda",
                    PhoneNumberPrefix = "256",
                    PrimaryLocaleIETFBCP47Tag = "en-UG",

                    // PrimaryCurrencyISO4217AlphaCode = "UGX",
                },

                // Ukraine
                new Country
                {
                    ISO31661Alpha2Code = "UA",
                    ISO31661Alpha3Code = "UKR",
                    ISO31661NumericCode = "804",
                    DisplayName = "Ukraine",
                    OfficialName = "Ukraine",
                    PhoneNumberPrefix = "380",

                    // PrimaryCurrencyISO4217AlphaCode = "UAH",
                    // PrimaryLocaleIETFBCP47Tag = "uk-UA",
                },

                // United Arab Emirates
                new Country
                {
                    ISO31661Alpha2Code = "AE",
                    ISO31661Alpha3Code = "ARE",
                    ISO31661NumericCode = "784",
                    DisplayName = "United Arab Emirates",
                    OfficialName = "United Arab Emirates",
                    PhoneNumberPrefix = "971",

                    // PrimaryCurrencyISO4217AlphaCode = "AED",
                    // PrimaryLocaleIETFBCP47Tag = "ar-AE",
                },

                // United Kingdom
                new Country
                {
                    ISO31661Alpha2Code = "GB",
                    ISO31661Alpha3Code = "GBR",
                    ISO31661NumericCode = "826",
                    DisplayName = "United Kingdom",
                    OfficialName = "United Kingdom of Great Britain and Northern Ireland",
                    PhoneNumberPrefix = "44",
                    PrimaryCurrencyISO4217AlphaCode = "GBP",
                    PrimaryLocaleIETFBCP47Tag = "en-GB",
                },

                // United States Minor Outlying Islands (United States)
                new Country
                {
                    SovereignISO31661Alpha2Code = "US",

                    ISO31661Alpha2Code = "UM",
                    ISO31661Alpha3Code = "UMI",
                    ISO31661NumericCode = "581",
                    DisplayName = "United States Minor Outlying Islands",
                    OfficialName = "United States Minor Outlying Islands",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-UM",
                },

                // U.S. Virgin Islands (United States)
                new Country
                {
                    SovereignISO31661Alpha2Code = "US",

                    ISO31661Alpha2Code = "VI",
                    ISO31661Alpha3Code = "VIR",
                    ISO31661NumericCode = "850",
                    DisplayName = "U.S. Virgin Islands",
                    OfficialName = "Virgin Islands of the United States",
                    PhoneNumberPrefix = "1",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-VI",
                },

                // Uruguay
                new Country
                {
                    ISO31661Alpha2Code = "UY",
                    ISO31661Alpha3Code = "URY",
                    ISO31661NumericCode = "858",
                    DisplayName = "Uruguay",
                    OfficialName = "Oriental Republic of Uruguay",
                    PhoneNumberPrefix = "598",
                    PrimaryLocaleIETFBCP47Tag = "es-UY",

                    // PrimaryCurrencyISO4217AlphaCode = "UYU",
                },

                // Uzbekistan
                new Country
                {
                    ISO31661Alpha2Code = "UZ",
                    ISO31661Alpha3Code = "UZB",
                    ISO31661NumericCode = "860",
                    DisplayName = "Uzbekistan",
                    OfficialName = "Republic of Uzbekistan",
                    PhoneNumberPrefix = "998",

                    // PrimaryCurrencyISO4217AlphaCode = "UZS",
                    // PrimaryLocaleIETFBCP47Tag = "uz-UZ",
                },

                // Vanuatu
                new Country
                {
                    ISO31661Alpha2Code = "VU",
                    ISO31661Alpha3Code = "VUT",
                    ISO31661NumericCode = "548",
                    DisplayName = "Vanuatu",
                    OfficialName = "Republic of Vanuatu",
                    PhoneNumberPrefix = "678",

                    // PrimaryCurrencyISO4217AlphaCode = "VUV",
                    // PrimaryLocaleIETFBCP47Tag = "bi-VU",
                },

                // Venezuela
                new Country
                {
                    ISO31661Alpha2Code = "VE",
                    ISO31661Alpha3Code = "VEN",
                    ISO31661NumericCode = "862",
                    DisplayName = "Venezuela",
                    OfficialName = "Bolivarian Republic of Venezuela",
                    PhoneNumberPrefix = "58",
                    PrimaryLocaleIETFBCP47Tag = "es-VE",

                    // PrimaryCurrencyISO4217AlphaCode = "VES",
                },

                // Vietnam
                new Country
                {
                    ISO31661Alpha2Code = "VN",
                    ISO31661Alpha3Code = "VNM",
                    ISO31661NumericCode = "704",
                    DisplayName = "Vietnam",
                    OfficialName = "Socialist Republic of Vietnam",
                    PhoneNumberPrefix = "84",

                    // PrimaryCurrencyISO4217AlphaCode = "VND",
                    // PrimaryLocaleIETFBCP47Tag = "vi-VN",
                },

                // Wallis and Futuna (France)
                new Country
                {
                    SovereignISO31661Alpha2Code = "FR",

                    ISO31661Alpha2Code = "WF",
                    ISO31661Alpha3Code = "WLF",
                    ISO31661NumericCode = "876",
                    DisplayName = "Wallis and Futuna",
                    OfficialName = "Territory of the Wallis and Futuna Islands",
                    PhoneNumberPrefix = "681",
                    PrimaryLocaleIETFBCP47Tag = "fr-WF",

                    // PrimaryCurrencyISO4217AlphaCode = "XPF",
                },

                // Western Sahara
                new Country
                {
                    ISO31661Alpha2Code = "EH",
                    ISO31661Alpha3Code = "ESH",
                    ISO31661NumericCode = "732",
                    DisplayName = "Western Sahara",
                    OfficialName = "Western Sahara",
                    PhoneNumberPrefix = "212",

                    // PrimaryCurrencyISO4217AlphaCode = "MAD",
                    // PrimaryLocaleIETFBCP47Tag = "ar-EH",
                },

                // Yemen
                new Country
                {
                    ISO31661Alpha2Code = "YE",
                    ISO31661Alpha3Code = "YEM",
                    ISO31661NumericCode = "887",
                    DisplayName = "Yemen",
                    OfficialName = "Republic of Yemen",
                    PhoneNumberPrefix = "967",

                    // PrimaryCurrencyISO4217AlphaCode = "YER",
                    // PrimaryLocaleIETFBCP47Tag = "ar-YE",
                },

                // Zambia
                new Country
                {
                    ISO31661Alpha2Code = "ZM",
                    ISO31661Alpha3Code = "ZMB",
                    ISO31661NumericCode = "894",
                    DisplayName = "Zambia",
                    OfficialName = "Republic of Zambia",
                    PhoneNumberPrefix = "260",
                    PrimaryLocaleIETFBCP47Tag = "en-ZM",

                    // PrimaryCurrencyISO4217AlphaCode = "ZMW",
                },

                // Zimbabwe
                new Country
                {
                    ISO31661Alpha2Code = "ZW",
                    ISO31661Alpha3Code = "ZWE",
                    ISO31661NumericCode = "716",
                    DisplayName = "Zimbabwe",
                    OfficialName = "Republic of Zimbabwe",
                    PhoneNumberPrefix = "263",
                    PrimaryCurrencyISO4217AlphaCode = "USD",
                    PrimaryLocaleIETFBCP47Tag = "en-ZW",
                },
            ]);
        }
    }
}
