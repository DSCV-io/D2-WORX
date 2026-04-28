// -----------------------------------------------------------------------
// <copyright file="TestHelpers.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Helper methods for tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Gets a test reference data response for GeoRefDataService tests.
    /// </summary>
    public static GeoRefData TestGeoRefData => new GeoRefData
    {
        Version = "0.0.0",
        UpdatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
        Countries =
            {
                {
                    "US", new CountryDTO
                    {
                        Iso31661Alpha2Code = "US",
                        Iso31661Alpha3Code = "USA",
                        Iso31661NumericCode = "840",
                        DisplayName = "United States",
                        OfficialName = "United States of America",
                        PhoneNumberPrefix = "1",
                        PhoneNumberFormat = "(###) ###-####",
                        PrimaryCurrencyIso4217AlphaCode = "USD",
                        PrimaryLocaleIetfBcp47Tag = "en-US",
                        SubdivisionIso31662Codes = { "US-AL", "US-AK", "US-AZ" },
                        LocaleIetfBcp47Tags = { "en-US", "es-US" },
                        GeopoliticalEntityShortCodes = { "NATO", "UN", "USMCA" },
                    }
                },
            },
        Subdivisions =
            {
                {
                    "US-AL", new SubdivisionDTO
                    {
                        Iso31662Code = "US-AL",
                        ShortCode = "AL",
                        DisplayName = "Alabama",
                        OfficialName = "State of Alabama",
                        CountryIso31661Alpha2Code = "US",
                    }
                },
            },
        Currencies =
            {
                {
                    "USD", new CurrencyDTO
                    {
                        Iso4217AlphaCode = "USD",
                        Iso4217NumericCode = "840",
                        DisplayName = "US Dollar",
                        OfficialName = "United States Dollar",
                        DecimalPlaces = 2,
                        Symbol = "$",
                    }
                },
            },
        Languages =
            {
                {
                    "en", new LanguageDTO
                    {
                        Iso6391Code = "en",
                        Name = "English",
                        Endonym = "English",
                    }
                },
            },
        Locales =
            {
                {
                    "en-US", new LocaleDTO
                    {
                        IetfBcp47Tag = "en-US",
                        Name = "English (United States)",
                        Endonym = "English (United States)",
                        LanguageIso6391Code = "en",
                        CountryIso31661Alpha2Code = "US",
                    }
                },
            },
        GeopoliticalEntities =
            {
                {
                    "NATO", new GeopoliticalEntityDTO
                    {
                        ShortCode = "NATO",
                        Name = "North Atlantic Treaty Organization",
                        Type = "MilitaryAlliance",
                        CountryIso31661Alpha2Codes = { "US" },
                    }
                },
            },
    };

    /// <summary>
    /// Creates a mock handler context for testing.
    /// </summary>
    ///
    /// <returns>
    /// A mock <see cref="IHandlerContext"/> instance.
    /// </returns>
    public static IHandlerContext CreateHandlerContext()
    {
        var mockContext = new Mock<IRequestContext>();
        mockContext.Setup(x => x.TraceId).Returns(Guid.NewGuid().ToString());

        var mockLogger = new Mock<ILogger<HandlerContext>>();

        return new HandlerContext(mockContext.Object, mockLogger.Object);
    }
}
