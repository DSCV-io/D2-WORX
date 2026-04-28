// -----------------------------------------------------------------------
// <copyright file="GetReferenceData.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Handlers.R;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.EntityFrameworkCore;
using H = global::D2.Geo.App.Interfaces.Repository.Handlers.R.IRead.IGetReferenceDataHandler;
using I = global::D2.Geo.App.Interfaces.Repository.Handlers.R.IRead.GetReferenceDataInput;
using O = global::D2.Geo.App.Interfaces.Repository.Handlers.R.IRead.GetReferenceDataOutput;

/// <summary>
/// Handler for getting all geographic reference data.
/// </summary>
public class GetReferenceData : BaseHandler<GetReferenceData, I, O>, H
{
    private readonly GeoDbContext r_db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetReferenceData"/> class.
    /// </summary>
    ///
    /// <param name="db">
    /// The geographic database context.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public GetReferenceData(
        GeoDbContext db,
        IHandlerContext context)
        : base(context)
    {
        r_db = db;
    }

    /// <summary>
    /// Executes the handler to get geographic reference data.
    /// </summary>
    ///
    /// <param name="input">
    /// The input parameters for the handler.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A task that represents the asynchronous operation, containing the result of the handler
    /// execution.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        // Query countries with relationships (project to anonymous type for EF compatibility).
        var countriesRaw = await r_db.Countries
            .AsNoTracking()
            .Select(c => new
            {
                c.ISO31661Alpha2Code,
                c.ISO31661Alpha3Code,
                c.ISO31661NumericCode,
                c.DisplayName,
                c.OfficialName,
                c.PhoneNumberPrefix,
                SovereignCode = c.SovereignISO31661Alpha2Code,
                PrimaryCurrency = c.PrimaryCurrencyISO4217AlphaCode,
                PrimaryLocale = c.PrimaryLocaleIETFBCP47Tag,
                Subdivisions = c.Subdivisions.Select(s => s.ISO31662Code).ToList(),
                Currencies = c.Currencies.Select(cur => cur.ISO4217AlphaCode).ToList(),
                Locales = c.Locales.Select(l => l.IETFBCP47Tag).ToList(),
                GeopoliticalEntities = c.GeopoliticalEntities.Select(g => g.ShortCode).ToList(),
            })
            .ToListAsync(ct);

        // Map countries to DTOs.
        var countries = countriesRaw.ToDictionary(
            c => c.ISO31661Alpha2Code,
            c =>
            {
                var dto = new CountryDTO
                {
                    Iso31661Alpha2Code = c.ISO31661Alpha2Code,
                    Iso31661Alpha3Code = c.ISO31661Alpha3Code,
                    Iso31661NumericCode = c.ISO31661NumericCode,
                    DisplayName = c.DisplayName,
                    OfficialName = c.OfficialName,
                    PhoneNumberPrefix = c.PhoneNumberPrefix,
                };

                // Optional fields — only set when non-null to avoid proto CheckNotNull.
                if (c.SovereignCode != null)
                {
                    dto.SovereignIso31661Alpha2Code = c.SovereignCode;
                }

                if (c.PrimaryCurrency != null)
                {
                    dto.PrimaryCurrencyIso4217AlphaCode = c.PrimaryCurrency;
                }

                if (c.PrimaryLocale != null)
                {
                    dto.PrimaryLocaleIetfBcp47Tag = c.PrimaryLocale;
                }

                dto.SubdivisionIso31662Codes.AddRange(c.Subdivisions);
                dto.CurrencyIso4217AlphaCodes.AddRange(c.Currencies);
                dto.LocaleIetfBcp47Tags.AddRange(c.Locales);
                dto.GeopoliticalEntityShortCodes.AddRange(c.GeopoliticalEntities);
                return dto;
            });

        // Query subdivisions.
        var subdivisions = await r_db.Subdivisions
            .AsNoTracking()
            .Select(s => new SubdivisionDTO
            {
                Iso31662Code = s.ISO31662Code,
                ShortCode = s.ShortCode,
                DisplayName = s.DisplayName,
                OfficialName = s.OfficialName,
                CountryIso31661Alpha2Code = s.CountryISO31661Alpha2Code,
            })
            .ToDictionaryAsync(s => s.Iso31662Code, ct);

        // Query currencies.
        var currencies = await r_db.Currencies
            .AsNoTracking()
            .Select(c => new CurrencyDTO
            {
                Iso4217AlphaCode = c.ISO4217AlphaCode,
                Iso4217NumericCode = c.ISO4217NumericCode,
                DisplayName = c.DisplayName,
                OfficialName = c.OfficialName,
                DecimalPlaces = c.DecimalPlaces,
                Symbol = c.Symbol,
            })
            .ToDictionaryAsync(c => c.Iso4217AlphaCode, ct);

        // Query languages.
        var languages = await r_db.Languages
            .AsNoTracking()
            .Select(l => new LanguageDTO
            {
                Iso6391Code = l.ISO6391Code,
                Name = l.Name,
                Endonym = l.Endonym,
            })
            .ToDictionaryAsync(l => l.Iso6391Code, ct);

        // Query locales.
        var locales = await r_db.Locales
            .AsNoTracking()
            .Select(l => new LocaleDTO
            {
                IetfBcp47Tag = l.IETFBCP47Tag,
                Name = l.Name,
                Endonym = l.Endonym,
                LanguageIso6391Code = l.LanguageISO6391Code,
                CountryIso31661Alpha2Code = l.CountryISO31661Alpha2Code,
            })
            .ToDictionaryAsync(l => l.IetfBcp47Tag, ct);

        // Query timezones (project to anonymous type for nullable DST fields).
        var timezonesRaw = await r_db.Timezones
            .AsNoTracking()
            .Select(t => new
            {
                t.IANAIdentifier,
                t.DisplayName,
                t.UTCOffsetSTD,
                t.UTCOffsetDST,
                t.AbbreviationSTD,
                t.AbbreviationDST,
                t.CountryISO31661Alpha2Code,
            })
            .ToListAsync(ct);

        var timezones = timezonesRaw.ToDictionary(
            t => t.IANAIdentifier,
            t =>
            {
                var dto = new TimezoneDTO
                {
                    IanaIdentifier = t.IANAIdentifier,
                    DisplayName = t.DisplayName,
                    UtcOffsetStd = t.UTCOffsetSTD,
                    AbbreviationStd = t.AbbreviationSTD,
                    CountryIso31661Alpha2Code = t.CountryISO31661Alpha2Code,
                };

                if (t.UTCOffsetDST != null)
                {
                    dto.UtcOffsetDst = t.UTCOffsetDST;
                }

                if (t.AbbreviationDST != null)
                {
                    dto.AbbreviationDst = t.AbbreviationDST;
                }

                return dto;
            });

        // Query geopolitical entities (project to anonymous type for EF compatibility).
        var geopoliticalEntitiesRaw = await r_db.GeopoliticalEntities
            .AsNoTracking()
            .Select(g => new
            {
                g.ShortCode,
                g.Name,
                Type = g.Type.ToString(),
                Countries = g.Countries.Select(c => c.ISO31661Alpha2Code).ToList(),
            })
            .ToListAsync(ct);

        // Map geopolitical entities to DTOs.
        var geopoliticalEntities = geopoliticalEntitiesRaw.ToDictionary(
            g => g.ShortCode,
            g =>
            {
                var dto = new GeopoliticalEntityDTO
                {
                    ShortCode = g.ShortCode,
                    Name = g.Name,
                    Type = g.Type,
                };
                dto.CountryIso31661Alpha2Codes.AddRange(g.Countries);
                return dto;
            });

        // Query version.
        var version = await r_db.ReferenceDataVersions
            .AsNoTracking()
            .FirstAsync(ct);

        // Populate CountryIso31661Alpha2Codes in currencies.
        foreach (var country in countries.Values)
        {
            foreach (var currencyCode in country.CurrencyIso4217AlphaCodes)
            {
                if (currencies.TryGetValue(currencyCode, out var currency))
                {
                    currency.CountryIso31661Alpha2Codes.Add(country.Iso31661Alpha2Code);
                }
            }
        }

        // Create response.
        var response = new GeoRefData
        {
            Version = version.Version,
            UpdatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(version.UpdatedAt),
            Countries = { countries },
            Subdivisions = { subdivisions },
            Currencies = { currencies },
            Languages = { languages },
            Locales = { locales },
            Timezones = { timezones },
            GeopoliticalEntities = { geopoliticalEntities },
        };

        // Return result.
        return D2Result<O?>.Ok(new O(response));
    }
}
