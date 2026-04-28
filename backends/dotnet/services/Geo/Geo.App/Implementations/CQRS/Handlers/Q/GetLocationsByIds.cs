// -----------------------------------------------------------------------
// <copyright file="GetLocationsByIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Implementations.CQRS.Handlers.Q;

using D2.Geo.Domain.Entities;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.R;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.U;
using D2.Shared.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using H = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.IGetLocationsByIdsHandler;
using I = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.GetLocationsByIdsInput;
using O = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.GetLocationsByIdsOutput;
using ReadRepo = D2.Geo.App.Interfaces.Repository.Handlers.R.IRead;

/// <summary>
/// Handler for getting locations by their IDs.
/// </summary>
public partial class GetLocationsByIds : BaseHandler<GetLocationsByIds, I, O>, H
{
    private readonly IRead.IGetManyHandler<Location> r_memoryCacheGetMany;
    private readonly IUpdate.ISetManyHandler<Location> r_memoryCacheSetMany;
    private readonly ReadRepo.IGetLocationsByIdsHandler r_getLocationsFromRepo;
    private readonly GeoAppOptions r_options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLocationsByIds"/> class.
    /// </summary>
    ///
    /// <param name="memoryCacheGetMany">
    /// The in-memory cache get-many handler.
    /// </param>
    /// <param name="memoryCacheSetMany">
    /// The in-memory cache set-many handler.
    /// </param>
    /// <param name="getLocationsFromRepo">
    /// The repository handler for getting locations by IDs.
    /// </param>
    /// <param name="options">
    /// The Geo application options.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public GetLocationsByIds(
        IRead.IGetManyHandler<Location> memoryCacheGetMany,
        IUpdate.ISetManyHandler<Location> memoryCacheSetMany,
        ReadRepo.IGetLocationsByIdsHandler getLocationsFromRepo,
        IOptions<GeoAppOptions> options,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCacheGetMany = memoryCacheGetMany;
        r_memoryCacheSetMany = memoryCacheSetMany;
        r_getLocationsFromRepo = getLocationsFromRepo;
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
            return Success([]);
        }

        // First, try to get locations from in-memory cache.
        var getFromCacheR = await r_memoryCacheGetMany.HandleAsync(
            new(input.HashIds.Select(id => CacheKeys.Location(id)).ToList()), ct);

        // If that failed (for any reason other than "NOT or SOME found"), bubble up the failure.
        if (getFromCacheR.CheckFailure(out var getFromCache)
            && getFromCacheR.ErrorCode is not (ErrorCodes.NOT_FOUND or ErrorCodes.SOME_FOUND))
        {
            return D2Result<O?>.BubbleFail(getFromCacheR);
        }

        // Add found locations to the result dictionary.
        Dictionary<string, Location> locations = [];
        foreach (var kvp in getFromCache?.Values ?? [])
        {
            locations[kvp.Value.HashId] = kvp.Value;
        }

        // If ALL locations were found in cache, return them now.
        if (locations.Count == input.HashIds.Count)
        {
            return Success(locations);
        }

        // Otherwise, fetch missing locations.
        var missingIds = input.HashIds.Except(locations.Keys).ToList();
        var repoR = await r_getLocationsFromRepo.HandleAsync(new(missingIds), ct);

        // If that succeeded, add results to the list, cache and return.
        if (repoR.CheckSuccess(out var repoOutput))
        {
            foreach (var loc in repoOutput?.Locations ?? [])
            {
                locations[loc.Key] = loc.Value;
            }

            await SetInCacheAsync(repoOutput!.Locations, ct);
            return Success(locations);
        }

        // If that failed, check the reason.
        switch (repoR.ErrorCode)
        {
            // If NO locations were found, return what we have.
            case ErrorCodes.NOT_FOUND:
                {
                    // If we found some locations in cache, return [fail, SOME found].
                    if (locations.Count > 0)
                    {
                        // NOTE: Because the DB returned NOT_FOUND, that means the ones found in
                        // the cache were already present and do not need to be cached again.
                        return SomeFound(locations);
                    }

                    // Otherwise, return (fail, NOT found).
                    return D2Result<O?>.NotFound();
                }

            // If SOME locations were found, add to list, cache and return [fail, SOME found].
            case ErrorCodes.SOME_FOUND:
                {
                    foreach (var loc in repoOutput?.Locations ?? [])
                    {
                        locations[loc.Key] = loc.Value;
                    }

                    await SetInCacheAsync(repoOutput!.Locations, ct);
                    return SomeFound(locations);
                }

            // For other errors, bubble up the failure.
            default:
                {
                    return D2Result<O?>.BubbleFail(repoR);
                }
        }
    }

    private static D2Result<O?> Success(Dictionary<string, Location> locations) =>
        D2Result<O?>.Ok(new O(locations));

    private static D2Result<O?> SomeFound(Dictionary<string, Location> locations) =>
        D2Result<O?>.SomeFound(new O(locations));

    /// <summary>
    /// Logs an error when setting locations in the memory cache fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to set locations in memory cache from {HandlerName}. TraceId: {TraceId}. ErrorCode: {ErrorCode}. Messages: {Messages}.")]
    private static partial void LogCacheSetFailed(ILogger logger, Type handlerName, string? traceId, string? errorCode, List<string> messages);

    private async ValueTask SetInCacheAsync(
        Dictionary<string, Location> locationsFromDbDict,
        CancellationToken ct)
    {
        var setInCacheR = await r_memoryCacheSetMany.HandleAsync(
            new(
                locationsFromDbDict.ToDictionary(
                    kvp => CacheKeys.Location(kvp.Key),
                    kvp => kvp.Value),
                r_options.LocationExpirationDuration),
            ct);

        if (setInCacheR.Failed)
        {
            LogCacheSetFailed(Context.Logger, typeof(GetLocationsByIds), TraceId, setInCacheR.ErrorCode, setInCacheR.Messages);
        }
    }
}
