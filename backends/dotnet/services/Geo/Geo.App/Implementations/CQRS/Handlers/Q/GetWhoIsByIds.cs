// -----------------------------------------------------------------------
// <copyright file="GetWhoIsByIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Implementations.CQRS.Handlers.Q;

using D2.Geo.App.Interfaces.CQRS.Handlers.Q;
using D2.Geo.App.Mappers;
using D2.Geo.Domain.Entities;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.R;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.U;
using D2.Shared.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using H = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.IGetWhoIsByIdsHandler;
using I = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.GetWhoIsByIdsInput;
using O = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.GetWhoIsByIdsOutput;
using ReadRepo = D2.Geo.App.Interfaces.Repository.Handlers.R.IRead;

/// <summary>
/// Handler for getting WhoIs records by their IDs.
/// </summary>
public partial class GetWhoIsByIds : BaseHandler<GetWhoIsByIds, I, O>, H
{
    private readonly IRead.IGetManyHandler<WhoIs> r_memoryCacheGetMany;
    private readonly IUpdate.ISetManyHandler<WhoIs> r_memoryCacheSetMany;
    private readonly ReadRepo.IGetWhoIsByIdsHandler r_getWhoIsFromRepo;
    private readonly IQueries.IGetLocationsByIdsHandler r_getLocationsByIds;
    private readonly GeoAppOptions r_options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetWhoIsByIds"/> class.
    /// </summary>
    ///
    /// <param name="memoryCacheGetMany">
    /// The in-memory cache get-many handler.
    /// </param>
    /// <param name="memoryCacheSetMany">
    /// The in-memory cache set-many handler.
    /// </param>
    /// <param name="getWhoIsFromRepo">
    /// The repository handler for getting WhoIs records by IDs.
    /// </param>
    /// <param name="getLocationsByIds">
    /// The handler for fetching locations by their IDs.
    /// </param>
    /// <param name="options">
    /// The Geo application options.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public GetWhoIsByIds(
        IRead.IGetManyHandler<WhoIs> memoryCacheGetMany,
        IUpdate.ISetManyHandler<WhoIs> memoryCacheSetMany,
        ReadRepo.IGetWhoIsByIdsHandler getWhoIsFromRepo,
        IQueries.IGetLocationsByIdsHandler getLocationsByIds,
        IOptions<GeoAppOptions> options,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCacheGetMany = memoryCacheGetMany;
        r_memoryCacheSetMany = memoryCacheSetMany;
        r_getWhoIsFromRepo = getWhoIsFromRepo;
        r_getLocationsByIds = getLocationsByIds;
        r_options = options.Value;
    }

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        // If the request was empty, return early.
        if (input.HashIds.Count == 0)
        {
            return D2Result<O?>.Ok(new O([]));
        }

        // First, try to get WhoIs from in-memory cache.
        var getFromCacheR = await r_memoryCacheGetMany.HandleAsync(
            new(input.HashIds.Select(id => CacheKeys.WhoIs(id)).ToList()), ct);

        // If that failed (for any reason other than "NOT or SOME found"), bubble up the failure.
        if (getFromCacheR.CheckFailure(out var getFromCache)
            && getFromCacheR.ErrorCode is not (ErrorCodes.NOT_FOUND or ErrorCodes.SOME_FOUND))
        {
            return D2Result<O?>.BubbleFail(getFromCacheR);
        }

        // Add found WhoIs to the result dictionary.
        Dictionary<string, WhoIs> whoIsRecords = [];
        foreach (var kvp in getFromCache?.Values ?? [])
        {
            whoIsRecords[kvp.Value.HashId] = kvp.Value;
        }

        // If ALL WhoIs were found in cache, return them now.
        if (whoIsRecords.Count == input.HashIds.Count)
        {
            return await SuccessAsync(whoIsRecords, ct);
        }

        // Otherwise, fetch missing WhoIs.
        var missingIds = input.HashIds.Except(whoIsRecords.Keys).ToList();
        var repoR = await r_getWhoIsFromRepo.HandleAsync(new(missingIds), ct);

        // If that succeeded, add results to the list, cache and return.
        if (repoR.CheckSuccess(out var repoOutput))
        {
            foreach (var kvp in repoOutput?.WhoIs ?? [])
            {
                whoIsRecords[kvp.Key] = kvp.Value;
            }

            await SetInCacheAsync(repoOutput!.WhoIs, ct);
            return await SuccessAsync(whoIsRecords, ct);
        }

        // If that failed, check the reason.
        switch (repoR.ErrorCode)
        {
            // If NO WhoIs were found, return what we have.
            case ErrorCodes.NOT_FOUND:
                {
                    // If we found some in cache, return [fail, SOME found].
                    if (whoIsRecords.Count > 0)
                    {
                        return await SomeFoundAsync(whoIsRecords, ct);
                    }

                    // Otherwise, return (fail, NOT found).
                    return D2Result<O?>.NotFound();
                }

            // If SOME WhoIs were found, add to list, cache and return [fail, SOME found].
            case ErrorCodes.SOME_FOUND:
                {
                    foreach (var kvp in repoOutput?.WhoIs ?? [])
                    {
                        whoIsRecords[kvp.Key] = kvp.Value;
                    }

                    await SetInCacheAsync(repoOutput!.WhoIs, ct);
                    return await SomeFoundAsync(whoIsRecords, ct);
                }

            // For other errors, bubble up the failure.
            default:
                {
                    return D2Result<O?>.BubbleFail(repoR);
                }
        }
    }

    private static Location? GetLocation(string? hashId, Dictionary<string, Location> locations) =>
        hashId is not null && locations.TryGetValue(hashId, out var loc) ? loc : null;

    /// <summary>
    /// Logs an error when setting WhoIs records in the memory cache fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to set WhoIs in memory cache from {HandlerName}. TraceId: {TraceId}. ErrorCode: {ErrorCode}. Messages: {Messages}.")]
    private static partial void LogCacheSetFailed(ILogger logger, Type handlerName, string? traceId, string? errorCode, List<string> messages);

    private async ValueTask SetInCacheAsync(
        Dictionary<string, WhoIs> fromDbDict,
        CancellationToken ct)
    {
        var setInCacheR = await r_memoryCacheSetMany.HandleAsync(
            new(
                fromDbDict.ToDictionary(
                    kvp => CacheKeys.WhoIs(kvp.Key),
                    kvp => kvp.Value),
                r_options.WhoIsExpirationDuration),
            ct);

        if (setInCacheR.Failed)
        {
            LogCacheSetFailed(Context.Logger, typeof(GetWhoIsByIds), TraceId, setInCacheR.ErrorCode, setInCacheR.Messages);
        }
    }

    private async ValueTask<D2Result<O?>> SuccessAsync(
        Dictionary<string, WhoIs> whoIsRecords,
        CancellationToken ct)
    {
        var locations = await FetchLocationsAsync(whoIsRecords.Values, ct);
        var dtoDict = whoIsRecords.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToDTO(GetLocation(kvp.Value.LocationHashId, locations)));
        return D2Result<O?>.Ok(new O(dtoDict));
    }

    private async ValueTask<D2Result<O?>> SomeFoundAsync(
        Dictionary<string, WhoIs> whoIsRecords,
        CancellationToken ct)
    {
        var locations = await FetchLocationsAsync(whoIsRecords.Values, ct);
        var dtoDict = whoIsRecords.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToDTO(GetLocation(kvp.Value.LocationHashId, locations)));
        return D2Result<O?>.SomeFound(new O(dtoDict));
    }

    private async ValueTask<Dictionary<string, Location>> FetchLocationsAsync(
        IEnumerable<WhoIs> whoIsRecords,
        CancellationToken ct)
    {
        var locationHashIds = whoIsRecords
            .Where(w => w.LocationHashId is not null)
            .Select(w => w.LocationHashId!)
            .Distinct()
            .ToList();

        if (locationHashIds.Count == 0)
        {
            return [];
        }

        var locationsR = await r_getLocationsByIds.HandleAsync(
            new IQueries.GetLocationsByIdsInput(locationHashIds),
            ct);

        return locationsR.Data?.Data ?? [];
    }
}
