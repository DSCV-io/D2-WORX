// -----------------------------------------------------------------------
// <copyright file="CurrencySeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding currency data.
/// </summary>
public static class CurrencySeeding
{
    /// <summary>
    /// Seeds the Currency entity.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the Currency entity.
        /// </summary>
        public void SeedCurrencies()
        {
            modelBuilder.Entity<Currency>().HasData(
            [

                // US Dollar
                new Currency
                {
                    ISO4217AlphaCode = "USD",
                    ISO4217NumericCode = "840",
                    DisplayName = "US Dollar",
                    OfficialName = "United States Dollar",
                    DecimalPlaces = 2,
                    Symbol = "$",
                },

                // Canadian Dollar
                new Currency
                {
                    ISO4217AlphaCode = "CAD",
                    ISO4217NumericCode = "124",
                    DisplayName = "Canadian Dollar",
                    OfficialName = "Canadian Dollar",
                    DecimalPlaces = 2,
                    Symbol = "$",
                },

                // British Pound
                new Currency
                {
                    ISO4217AlphaCode = "GBP",
                    ISO4217NumericCode = "826",
                    DisplayName = "British Pound",
                    OfficialName = "Pound Sterling",
                    DecimalPlaces = 2,
                    Symbol = "£",
                },

                // Euro
                new Currency
                {
                    ISO4217AlphaCode = "EUR",
                    ISO4217NumericCode = "978",
                    DisplayName = "Euro",
                    OfficialName = "Euro",
                    DecimalPlaces = 2,
                    Symbol = "€",
                },

                // Japanese Yen
                new Currency
                {
                    ISO4217AlphaCode = "JPY",
                    ISO4217NumericCode = "392",
                    DisplayName = "Yen",
                    OfficialName = "Japanese Yen",
                    DecimalPlaces = 0,
                    Symbol = "¥",
                }
            ]);
        }
    }
}
