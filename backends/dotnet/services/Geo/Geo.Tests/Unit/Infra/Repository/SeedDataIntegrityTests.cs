// -----------------------------------------------------------------------
// <copyright file="SeedDataIntegrityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.Infra.Repository;

using D2.Geo.Domain.Entities;
using D2.Geo.Infra.Repository;
using D2.Geo.Infra.Repository.Entities;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// Tests for seed data referential integrity.
/// </summary>
public class SeedDataIntegrityTests
{
    /// <summary>
    /// Verifies every country with an uncommented primary currency references a seeded currency.
    /// </summary>
    [Fact]
    public void Countries_WithPrimaryCurrency_ReferencesSeededCurrency()
    {
        // Arrange
        using var context = CreateContext();

        var countries = GetSeedData<Country>(context);
        var currencies = GetSeedData<Currency>(context);

        var seededCurrencyCodes = currencies
            .Select(c => c["ISO4217AlphaCode"]?.ToString())
            .Where(c => c is not null)
            .ToHashSet();

        // Act
        var countriesWithMissingCurrency = countries
            .Where(c => c["PrimaryCurrencyISO4217AlphaCode"] is not null)
            .Where(c => !seededCurrencyCodes.Contains(c["PrimaryCurrencyISO4217AlphaCode"]?.ToString()))
            .Select(c => $"{c["ISO31661Alpha2Code"]} ({c["DisplayName"]}) -> {c["PrimaryCurrencyISO4217AlphaCode"]}")
            .ToList();

        // Assert
        Assert.Empty(countriesWithMissingCurrency);
    }

    /// <summary>
    /// Verifies every country with an uncommented primary locale references a seeded locale.
    /// </summary>
    [Fact]
    public void Countries_WithPrimaryLocale_ReferencesSeededLocale()
    {
        // Arrange
        using var context = CreateContext();

        var countries = GetSeedData<Country>(context);
        var locales = GetSeedData<Locale>(context);

        var seededLocaleTags = locales
            .Select(l => l["IETFBCP47Tag"]?.ToString())
            .Where(l => l is not null)
            .ToHashSet();

        // Act
        var countriesWithMissingLocale = countries
            .Where(c => c["PrimaryLocaleIETFBCP47Tag"] is not null)
            .Where(c => !seededLocaleTags.Contains(c["PrimaryLocaleIETFBCP47Tag"]?.ToString()))
            .Select(c => $"{c["ISO31661Alpha2Code"]} ({c["DisplayName"]}) -> {c["PrimaryLocaleIETFBCP47Tag"]}")
            .ToList();

        // Assert
        Assert.Empty(countriesWithMissingLocale);
    }

    /// <summary>
    /// Verifies every locale references a seeded language.
    /// </summary>
    [Fact]
    public void Locales_ReferenceSeededLanguage()
    {
        // Arrange
        using var context = CreateContext();

        var locales = GetSeedData<Locale>(context);
        var languages = GetSeedData<Language>(context);

        var seededLanguageCodes = languages
            .Select(l => l["ISO6391Code"]?.ToString())
            .Where(l => l is not null)
            .ToHashSet();

        // Act
        var localesWithMissingLanguage = locales
            .Where(l => !seededLanguageCodes.Contains(l["LanguageISO6391Code"]?.ToString()))
            .Select(l => $"{l["IETFBCP47Tag"]} -> {l["LanguageISO6391Code"]}")
            .ToList();

        // Assert
        Assert.Empty(localesWithMissingLanguage);
    }

    /// <summary>
    /// Verifies every locale references a seeded country.
    /// </summary>
    [Fact]
    public void Locales_ReferenceSeededCountry()
    {
        // Arrange
        using var context = CreateContext();

        var locales = GetSeedData<Locale>(context);
        var countries = GetSeedData<Country>(context);

        var seededCountryCodes = countries
            .Select(c => c["ISO31661Alpha2Code"]?.ToString())
            .Where(c => c is not null)
            .ToHashSet();

        // Act
        var localesWithMissingCountry = locales
            .Where(l => !seededCountryCodes.Contains(l["CountryISO31661Alpha2Code"]?.ToString()))
            .Select(l => $"{l["IETFBCP47Tag"]} -> {l["CountryISO31661Alpha2Code"]}")
            .ToList();

        // Assert
        Assert.Empty(localesWithMissingCountry);
    }

    /// <summary>
    /// Verifies every country with a sovereign reference points to a seeded country.
    /// </summary>
    [Fact]
    public void Countries_WithSovereign_ReferencesSeededCountry()
    {
        // Arrange
        using var context = CreateContext();

        var countries = GetSeedData<Country>(context);

        var seededCountryCodes = countries
            .Select(c => c["ISO31661Alpha2Code"]?.ToString())
            .Where(c => c is not null)
            .ToHashSet();

        // Act
        var countriesWithMissingSovereign = countries
            .Where(c => c["SovereignISO31661Alpha2Code"] is not null)
            .Where(c => !seededCountryCodes.Contains(c["SovereignISO31661Alpha2Code"]?.ToString()))
            .Select(c => $"{c["ISO31661Alpha2Code"]} ({c["DisplayName"]}) -> {c["SovereignISO31661Alpha2Code"]}")
            .ToList();

        // Assert
        Assert.Empty(countriesWithMissingSovereign);
    }

    /// <summary>
    /// Verifies every country_currency join entry references a seeded country.
    /// </summary>
    [Fact]
    public void CountryCurrencies_ReferenceSeededCountry()
    {
        // Arrange
        using var context = CreateContext();

        var countries = GetSeedData<Country>(context);
        var countryCurrencies = GetJoinTableSeedData(context, "country_currencies");

        var seededCountryCodes = countries
            .Select(c => c["ISO31661Alpha2Code"]?.ToString())
            .Where(c => c is not null)
            .ToHashSet();

        // Act
        var missingCountries = countryCurrencies
            .Select(cc => cc["country_iso_3166_1_alpha_2_code"]?.ToString())
            .Where(code => code is not null && !seededCountryCodes.Contains(code))
            .Distinct()
            .ToList();

        // Assert
        Assert.Empty(missingCountries);
    }

    /// <summary>
    /// Verifies every country_currency join entry references a seeded currency.
    /// </summary>
    [Fact]
    public void CountryCurrencies_ReferenceSeededCurrency()
    {
        // Arrange
        using var context = CreateContext();

        var currencies = GetSeedData<Currency>(context);
        var countryCurrencies = GetJoinTableSeedData(context, "country_currencies");

        var seededCurrencyCodes = currencies
            .Select(c => c["ISO4217AlphaCode"]?.ToString())
            .Where(c => c is not null)
            .ToHashSet();

        // Act
        var missingCurrencies = countryCurrencies
            .Select(cc => cc["currency_iso_4217_alpha_code"]?.ToString())
            .Where(code => code is not null && !seededCurrencyCodes.Contains(code))
            .Distinct()
            .ToList();

        // Assert
        Assert.Empty(missingCurrencies);
    }

    /// <summary>
    /// Verifies every country_geopolitical_entities entry references a seeded country.
    /// </summary>
    [Fact]
    public void CountryGeopoliticalEntities_ReferenceSeededCountry()
    {
        // Arrange
        using var context = CreateContext();

        var countries = GetSeedData<Country>(context);
        var countryGeoPolEntities = GetJoinTableSeedData(context, "country_geopolitical_entities");

        var seededCountryCodes = countries
            .Select(c => c["ISO31661Alpha2Code"]?.ToString())
            .Where(c => c is not null)
            .ToHashSet();

        // Act
        var missingCountries = countryGeoPolEntities
            .Select(cge => cge["country_iso_3166_1_alpha_2_code"]?.ToString())
            .Where(code => code is not null && !seededCountryCodes.Contains(code))
            .Distinct()
            .ToList();

        // Assert
        Assert.Empty(missingCountries);
    }

    /// <summary>
    /// Verifies every country_geopolitical_entities entry references a seeded entity.
    /// </summary>
    [Fact]
    public void CountryGeopoliticalEntities_ReferenceSeededGeopoliticalEntity()
    {
        // Arrange
        using var context = CreateContext();

        var geoPolEntities = GetSeedData<GeopoliticalEntity>(context);
        var countryGeoPolEntities = GetJoinTableSeedData(context, "country_geopolitical_entities");

        var seededGeoPolCodes = geoPolEntities
            .Select(g => g["ShortCode"]?.ToString())
            .Where(g => g is not null)
            .ToHashSet();

        // Act
        var missingEntities = countryGeoPolEntities
            .Select(cge => cge["geopolitical_entity_short_code"]?.ToString())
            .Where(code => code is not null && !seededGeoPolCodes.Contains(code))
            .Distinct()
            .ToList();

        // Assert
        Assert.Empty(missingEntities);
    }

    /// <summary>
    /// Verifies every subdivision references a seeded country.
    /// </summary>
    [Fact]
    public void Subdivisions_ReferenceSeededCountry()
    {
        // Arrange
        using var context = CreateContext();

        var subdivisions = GetSeedData<Subdivision>(context);
        var countries = GetSeedData<Country>(context);

        var seededCountryCodes = countries
            .Select(c => c["ISO31661Alpha2Code"]?.ToString())
            .Where(c => c is not null)
            .ToHashSet();

        // Act
        var subdivisionsWithMissingCountry = subdivisions
            .Where(s => !seededCountryCodes.Contains(s["CountryISO31661Alpha2Code"]?.ToString()))
            .Select(s => $"{s["ISO31662Code"]} -> {s["CountryISO31661Alpha2Code"]}")
            .ToList();

        // Assert
        Assert.Empty(subdivisionsWithMissingCountry);
    }

    /// <summary>
    /// Verifies seed data exists for all reference data entities.
    /// </summary>
    ///
    /// <param name="entityType">
    /// The entity type to check for seed data.
    /// </param>
    /// <param name="minimumExpected">
    /// The minimum expected number of seed data entries.
    /// </param>
    [Theory]
    [InlineData(typeof(ReferenceDataVersion), 1)]
    [InlineData(typeof(Language), 6)]
    [InlineData(typeof(Currency), 5)]
    [InlineData(typeof(Country), 249)]
    [InlineData(typeof(GeopoliticalEntity), 53)]
    [InlineData(typeof(Locale), 100)]
    [InlineData(typeof(Subdivision), 183)]
    public void SeedData_Exists_ForEntity(Type entityType, int minimumExpected)
    {
        // Arrange
        using var context = CreateContext();
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        var entity = designTimeModel.FindEntityType(entityType)
                     ?? throw new InvalidOperationException($"Entity type {entityType.Name} not found.");

        // Act
        var seedData = entity.GetSeedData().ToList();

        // Assert
        Assert.True(
            seedData.Count >= minimumExpected,
            $"{entityType.Name}: Expected at least {minimumExpected}, found {seedData.Count}");
    }

    /// <summary>
    /// Verifies seed data exists for join tables.
    /// </summary>
    ///
    /// <param name="tableName">
    /// The name of the join table to check for seed data.
    /// </param>
    /// <param name="minimumExpected">
    /// The minimum expected number of seed data entries.
    /// </param>
    [Theory]
    [InlineData("country_currencies", 88)]
    [InlineData("country_geopolitical_entities", 1000)]
    public void SeedData_Exists_ForJoinTable(string tableName, int minimumExpected)
    {
        // Arrange
        using var context = CreateContext();
        var joinTableData = GetJoinTableSeedData(context, tableName);

        // Assert
        Assert.True(
            joinTableData.Count >= minimumExpected,
            $"{tableName}: Expected at least {minimumExpected}, found {joinTableData.Count}");
    }

    [MustDisposeResource]
    private static GeoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GeoDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy")
            .Options;

        return new GeoDbContext(options);
    }

    private static List<IDictionary<string, object?>> GetSeedData<T>(GeoDbContext context)
        where T : class
    {
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        var entityType = designTimeModel.FindEntityType(typeof(T))
            ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} not found in model.");

        return entityType
            .GetSeedData()
            .Select(IDictionary<string, object?> (d) => d.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
            .ToList();
    }

    private static List<IDictionary<string, object?>> GetJoinTableSeedData(
        GeoDbContext context,
        string tableName)
    {
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        var entityType = designTimeModel
            .GetEntityTypes()
            .FirstOrDefault(e => e.GetTableName() == tableName)
            ?? throw new InvalidOperationException($"Table {tableName} not found in model.");

        return entityType
            .GetSeedData()
            .Select(IDictionary<string, object?> (d) => d.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
            .ToList();
    }
}
