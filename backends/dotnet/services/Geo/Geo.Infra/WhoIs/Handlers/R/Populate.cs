// -----------------------------------------------------------------------
// <copyright file="Populate.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.WhoIs.Handlers.R;

using System.Globalization;
using System.Net;
using D2.Geo.App.Interfaces.WhoIs;
using D2.Geo.Domain.Entities;
using D2.Geo.Domain.Exceptions;
using D2.Geo.Domain.ValueObjects;
using D2.Shared.Handler;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using CreateRepo = D2.Geo.App.Interfaces.Repository.Handlers.C.ICreate;
using GetGeoRef = D2.Geo.Client.Interfaces.CQRS.Handlers.X.IComplex.IGetHandler;
using H = D2.Geo.App.Interfaces.WhoIs.Handlers.R.IRead.IPopulateHandler;
using I = D2.Geo.App.Interfaces.WhoIs.Handlers.R.IRead.PopulateInput;
using InMemCacheR = D2.Shared.Interfaces.Caching.InMemory.Handlers.R.IRead;
using InMemCacheU = D2.Shared.Interfaces.Caching.InMemory.Handlers.U.IUpdate;
using O = D2.Geo.App.Interfaces.WhoIs.Handlers.R.IRead.PopulateOutput;

/// <summary>
/// Handler for populating WhoIs records with data from IPinfo.io.
/// </summary>
/// <remarks>
/// Takes partial WhoIs records and populates them with ASN, location, and privacy data
/// from the IPinfo.io API. Also creates any required Location records.
/// </remarks>
public partial class Populate : BaseHandler<Populate, I, O>, H
{
    private readonly IIpInfoClient r_ipInfoClient;
    private readonly CreateRepo.ICreateLocationsHandler r_createLocations;
    private readonly GetGeoRef r_getGeoRef;
    private readonly InMemCacheR.IGetManyHandler<string> r_getManyCache;
    private readonly InMemCacheU.ISetManyHandler<string> r_setManyCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="Populate"/> class.
    /// </summary>
    ///
    /// <param name="ipInfoClient">
    /// The IP info client for looking up IP details.
    /// </param>
    /// <param name="createLocations">
    /// The handler for creating Location records.
    /// </param>
    /// <param name="getGeoRef">
    /// The get geo reference data handler.
    /// </param>
    /// <param name="getManyCache">
    /// The in-memory cache get many handler.
    /// </param>
    /// <param name="setManyCache">
    /// The in-memory cache set many handler.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Populate(
        IIpInfoClient ipInfoClient,
        CreateRepo.ICreateLocationsHandler createLocations,
        GetGeoRef getGeoRef,
        InMemCacheR.IGetManyHandler<string> getManyCache,
        InMemCacheU.ISetManyHandler<string> setManyCache,
        IHandlerContext context)
        : base(context)
    {
        r_ipInfoClient = ipInfoClient;
        r_createLocations = createLocations;
        r_getGeoRef = getGeoRef;
        r_getManyCache = getManyCache;
        r_setManyCache = setManyCache;
    }

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        // If the request was empty, return early.
        if (input.WhoIsRecords.Count == 0)
        {
            return D2Result<O?>.Ok(new O([]));
        }

        // Step 1: Fetch IP details for each record.
        var apiResults = new Dictionary<string, IpInfoResponse>();
        foreach (var (hashId, partialWhoIs) in input.WhoIsRecords)
        {
            var ipResponse = await FetchIpDetailsAsync(partialWhoIs.IPAddress, ct);
            if (ipResponse is not null)
            {
                apiResults[hashId] = ipResponse;
            }
        }

        // If no API results, return empty.
        if (apiResults.Count == 0)
        {
            return D2Result<O?>.Ok(new O([]));
        }

        // Step 2: Resolve subdivision codes.
        var regionsToSubdivisionCodes = await BuildRegionToSubdivisionCodeMap(
            apiResults.Values.ToList(), ct);

        // Step 2: Extract and dedupe locations.
        var locationsToCreate = new Dictionary<string, Location>();
        var hashIdToLocationHashId = new Dictionary<string, string?>();

        foreach (var (hashId, ipResponse) in apiResults)
        {
            var location = ExtractLocation(ipResponse, regionsToSubdivisionCodes);
            if (location is not null)
            {
                locationsToCreate.TryAdd(location.HashId, location);
                hashIdToLocationHashId[hashId] = location.HashId;
            }
            else
            {
                hashIdToLocationHashId[hashId] = null;
            }
        }

        // Step 3: Create locations.
        if (locationsToCreate.Count > 0)
        {
            var createLocR = await r_createLocations.HandleAsync(
                new([.. locationsToCreate.Values]),
                ct);

            if (createLocR.CheckFailure(out _))
            {
                LogCreateLocationsFailed(Context.Logger, TraceId, createLocR.ErrorCode);
            }
        }

        // Step 4: Build fully populated WhoIs records.
        var populatedRecords = new Dictionary<string, WhoIs>();
        foreach (var (hashId, ipResponse) in apiResults)
        {
            var partialWhoIs = input.WhoIsRecords[hashId];
            var locationHashId = hashIdToLocationHashId.GetValueOrDefault(hashId);
            var populatedWhoIs = BuildPopulatedWhoIs(partialWhoIs, ipResponse, locationHashId);
            populatedRecords[hashId] = populatedWhoIs;
        }

        return D2Result<O?>.Ok(new O(populatedRecords));
    }

    private static Location? ExtractLocation(
        IpInfoResponse ipResponse,
        Dictionary<string, string> regionsToSubdivisionCodes)
    {
        // Check if we have any location data.
        if (ipResponse.City.Falsey()
            && ipResponse.Region.Falsey()
            && ipResponse.Country.Falsey()
            && ipResponse.Postal.Falsey()
            && ipResponse.Latitude.Falsey()
            && ipResponse.Longitude.Falsey())
        {
            return null;
        }

        // Parse coordinates if available.
        Coordinates? coordinates = null;
        if (ipResponse.Latitude.Truthy()
            && ipResponse.Longitude.Truthy()
            && double.TryParse(ipResponse.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(ipResponse.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            try
            {
                coordinates = Coordinates.Create(lat, lon);
            }
            catch (GeoValidationException)
            {
                // Invalid coordinates — skip location association.
            }
        }

        // Try to get subdivision code from region.
        string? subdivisionCode = null;
        if (ipResponse.Region.Truthy())
        {
            regionsToSubdivisionCodes.TryGetValue(ipResponse.Region!, out subdivisionCode);
            if (subdivisionCode.Falsey())
            {
                subdivisionCode = null;
            }
        }

        // Build and return Location.
        return Location.Create(
            coordinates: coordinates,
            address: null,
            city: ipResponse.City,
            postalCode: ipResponse.Postal,
            subdivisionISO31662Code: subdivisionCode,
            countryISO31661Alpha2Code: ipResponse.Country);
    }

    private static WhoIs BuildPopulatedWhoIs(
        WhoIs partialWhoIs,
        IpInfoResponse ipResponse,
        string? locationHashId)
    {
        // Parse ASN from Org field (format: "AS12345 ISP Name").
        int? asn = null;
        string? asName = null;
        if (ipResponse.Org.Truthy())
        {
            var parts = ipResponse.Org!.Split(' ', 2);

            if (parts.Length >= 1 &&
                parts[0].StartsWith("AS", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[0][2..], out var parsedAsn))
            {
                asn = parsedAsn;
            }

            if (parts.Length >= 2)
            {
                asName = parts[1];
            }
        }

        // Build fully populated WhoIs using the factory method (preserves identity fields).
        return WhoIs.Create(
            ipAddress: partialWhoIs.IPAddress,
            year: partialWhoIs.Year,
            month: partialWhoIs.Month,
            asn: asn,
            asName: asName,
            asDomain: null,
            asType: null,
            carrierName: null,
            mcc: null,
            mnc: null,
            asChanged: null,
            geoChanged: null,
            isAnonymous: ipResponse.Privacy?.Vpn == true
                || ipResponse.Privacy?.Proxy == true
                || ipResponse.Privacy?.Tor == true
                || ipResponse.Privacy?.Relay == true
                || ipResponse.Privacy?.Hosting == true,
            isAnycast: null,
            isHosting: ipResponse.Privacy?.Hosting,
            isMobile: null,
            isSatellite: null,
            isProxy: ipResponse.Privacy?.Proxy,
            isRelay: ipResponse.Privacy?.Relay,
            isTor: ipResponse.Privacy?.Tor,
            isVpn: ipResponse.Privacy?.Vpn,
            privacyName: null,
            locationHashId: locationHashId);
    }

    /// <summary>
    /// Logs a warning when creating locations during WhoIs population fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to create locations during WhoIs population. TraceId: {TraceId}. ErrorCode: {ErrorCode}")]
    private static partial void LogCreateLocationsFailed(ILogger logger, string? traceId, string? errorCode);

    /// <summary>
    /// Logs a warning when an invalid IP address is encountered during WhoIs population.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Invalid IP address encountered. TraceId: {TraceId}")]
    private static partial void LogInvalidIpAddress(ILogger logger, string? traceId);

    /// <summary>
    /// Logs a debug message when skipping a localhost IP address.
    /// </summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Skipping localhost IP address")]
    private static partial void LogSkippingLocalhostIp(ILogger logger);

    /// <summary>
    /// Logs an error when fetching geo reference data for region population fails.
    /// </summary>
    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to get geo ref data for who-is region population. TraceId: {TraceId}")]
    private static partial void LogGeoRefDataFetchFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs a warning when setting the region-to-subdivision-code cache fails.
    /// </summary>
    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Failed to set region to subdivision code map in cache during WhoIs population. TraceId: {TraceId}. ErrorCode: {ErrorCode}")]
    private static partial void LogSetRegionCacheFailed(ILogger logger, string? traceId, string? errorCode);

    /// <summary>
    /// Logs an error when building the region-to-subdivision-code map fails unexpectedly.
    /// </summary>
    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Error building region to subdivision code map during WhoIs population. TraceId: {TraceId}")]
    private static partial void LogBuildRegionMapError(ILogger logger, Exception exception, string? traceId);

    private async Task<IpInfoResponse?> FetchIpDetailsAsync(
        string ipAddress,
        CancellationToken ct)
    {
        // Validate IP address.
        if (ipAddress.Falsey())
        {
            return null;
        }

        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            // Do not log raw ipAddress — it is PII.
            LogInvalidIpAddress(Context.Logger, TraceId);
            return null;
        }

        // Skip localhost.
        var ipStr = ip.ToString();
        if (ipStr is "127.0.0.1" or "::1")
        {
            LogSkippingLocalhostIp(Context.Logger);
            return null;
        }

        return await r_ipInfoClient.GetDetailsAsync(ipStr, ct);
    }

    private async ValueTask<Dictionary<string, string>> BuildRegionToSubdivisionCodeMap(
    List<IpInfoResponse>? apiResults = null,
    CancellationToken ct = default)
    {
        try
        {
            // If no API results, return empty.
            if (apiResults is null || apiResults.Count == 0)
            {
                return [];
            }

            // Extract unique regions from API results.
            var regions = apiResults
                .Select(x => x.Region)
                .Where(x => x.Truthy())
                .Select(x => x!)
                .Distinct()
                .ToList();

            // Get cache keys for regions.
            var regionKeys = regions
                .Select(CtorCacheKey)
                .ToList();

            // Initialize region to subdivision code map with empty values.
            var regionMap = regions.ToDictionary(x => x, _ => string.Empty);

            // Try to get from cached data in-memory.
            var cachedR = await r_getManyCache.HandleAsync(new(regionKeys), ct);
            var inCacheCtr = 0;
            if (cachedR.Success || cachedR.ErrorCode is ErrorCodes.SOME_FOUND)
            {
                foreach (var (key, value) in cachedR.Data!.Values)
                {
                    regionMap[key.Split(':')[^1]] = value;
                    inCacheCtr++;
                }
            }

            // If all regions were found in cache, return early.
            if (inCacheCtr == regions.Count)
            {
                return regionMap;
            }

            // Fetch missing region subdivision codes from geo reference data.
            var geoRefR = await r_getGeoRef.HandleAsync(new(), ct);

            // If that failed, log it and return what we have.
            if (geoRefR.Failed)
            {
                LogGeoRefDataFetchFailed(Context.Logger, TraceId);
                return regionMap;
            }

            // Otherwise, find the missing subdivision codes.
            foreach (var region in regions)
            {
                // If already found (from cache), skip.
                if (regionMap[region].Truthy())
                {
                    continue;
                }

                // Find subdivision by display name (case-insensitive).
                var subdivision = geoRefR.Data!.Data.Subdivisions.Values.FirstOrDefault(x
                    => x.DisplayName.Equals(region, StringComparison.OrdinalIgnoreCase));

                // If not found, skip.
                if (subdivision is null)
                {
                    continue;
                }

                // Update the map.
                regionMap[region] = subdivision.Iso31662Code;
            }

            // Construct cache entries to set.
            var cacheEntries = regionMap
                .Where(kv => kv.Value.Truthy())
                .ToDictionary(
                    kv => CtorCacheKey(kv.Key),
                    kv => kv.Value);

            // Set the constructed map in cache.
            var setCacheR = await r_setManyCache.HandleAsync(
                new(cacheEntries, TimeSpan.FromDays(1)),
                ct);

            // Log if cache set failed.
            if (setCacheR.Failed)
            {
                LogSetRegionCacheFailed(Context.Logger, TraceId, setCacheR.ErrorCode);
            }

            // Return the constructed map.
            return regionMap;

            // Local function to construct cache key.
            string CtorCacheKey(string region)
                => $"{nameof(Populate)}:{nameof(BuildRegionToSubdivisionCodeMap)}:{region}";
        }
        catch (Exception ex)
        {
            LogBuildRegionMapError(Context.Logger, ex, TraceId);

            return [];
        }
    }
}
