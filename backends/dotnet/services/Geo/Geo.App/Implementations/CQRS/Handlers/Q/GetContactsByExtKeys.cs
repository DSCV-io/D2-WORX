// -----------------------------------------------------------------------
// <copyright file="GetContactsByExtKeys.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Implementations.CQRS.Handlers.Q;

using D2.Geo.App.Interfaces.CQRS.Handlers.Q;
using D2.Geo.App.Mappers;
using D2.Geo.Domain.Entities;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.R;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.U;
using D2.Shared.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClientCacheKeys = D2.Geo.Client.CacheKeys;
using H = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.IGetContactsByExtKeysHandler;
using I = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.GetContactsByExtKeysInput;
using O = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.GetContactsByExtKeysOutput;
using ReadRepo = D2.Geo.App.Interfaces.Repository.Handlers.R.IRead;

/// <summary>
/// Handler for getting Contacts by their external keys (ContextKey + RelatedEntityId).
/// </summary>
///
/// <remarks>
/// Uses in-memory caching with ext-key-based cache keys. Each ext-key pair maps to a
/// list of contacts. Cache is checked first, and only missing keys are fetched from
/// the repository. Found results are cached for the configured expiration duration.
/// </remarks>
public partial class GetContactsByExtKeys : BaseHandler<GetContactsByExtKeys, I, O>, H
{
    private readonly IRead.IGetManyHandler<List<Contact>> r_memoryCacheGetMany;
    private readonly IUpdate.ISetManyHandler<List<Contact>> r_memoryCacheSetMany;
    private readonly ReadRepo.IGetContactsByExtKeysHandler r_getContactsFromRepo;
    private readonly IQueries.IGetLocationsByIdsHandler r_getLocationsByIds;
    private readonly GeoAppOptions r_options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetContactsByExtKeys"/> class.
    /// </summary>
    ///
    /// <param name="memoryCacheGetMany">
    /// The in-memory cache get-many handler.
    /// </param>
    /// <param name="memoryCacheSetMany">
    /// The in-memory cache set-many handler.
    /// </param>
    /// <param name="getContactsFromRepo">
    /// The repository handler for getting Contacts by external keys.
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
    public GetContactsByExtKeys(
        IRead.IGetManyHandler<List<Contact>> memoryCacheGetMany,
        IUpdate.ISetManyHandler<List<Contact>> memoryCacheSetMany,
        ReadRepo.IGetContactsByExtKeysHandler getContactsFromRepo,
        IQueries.IGetLocationsByIdsHandler getLocationsByIds,
        IOptions<GeoAppOptions> options,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCacheGetMany = memoryCacheGetMany;
        r_memoryCacheSetMany = memoryCacheSetMany;
        r_getContactsFromRepo = getContactsFromRepo;
        r_getLocationsByIds = getLocationsByIds;
        r_options = options.Value;
    }

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        // If the request was empty, return early.
        if (input.Request.Keys.Count == 0)
        {
            return D2Result<O?>.Ok(new O([]));
        }

        // Convert proto keys to domain tuples.
        var extKeys = input.Request.Keys
            .Select(k => (k.ContextKey, Guid.TryParse(k.RelatedEntityId, out var g) ? g : Guid.Empty))
            .Where(t => t.Item2 != Guid.Empty)
            .ToList();

        if (extKeys.Count == 0)
        {
            return D2Result<O?>.Ok(new O([]));
        }

        // First, try to get contacts from in-memory cache.
        var cacheKeys = extKeys.Select(k => ClientCacheKeys.ContactsByExtKey(k.ContextKey, k.Item2)).ToList();
        var getFromCacheR = await r_memoryCacheGetMany.HandleAsync(new(cacheKeys), ct);

        // If that failed (for any reason other than "NOT or SOME found"), bubble up the failure.
        if (getFromCacheR.CheckFailure(out var getFromCache)
            && getFromCacheR.ErrorCode is not (ErrorCodes.NOT_FOUND or ErrorCodes.SOME_FOUND))
        {
            return D2Result<O?>.BubbleFail(getFromCacheR);
        }

        // Add found ext-key contact lists to the result dictionary.
        Dictionary<(string ContextKey, Guid RelatedEntityId), List<Contact>> contacts = [];
        foreach (var kvp in getFromCache?.Values ?? [])
        {
            var extKey = ParseCacheKey(kvp.Key);
            if (extKey.HasValue)
            {
                contacts[extKey.Value] = kvp.Value;
            }
        }

        // If ALL ext-keys were found in cache, return them now.
        if (contacts.Count == extKeys.Count)
        {
            return await SuccessAsync(contacts, ct);
        }

        // Otherwise, fetch missing ext-keys from repository.
        var missingKeys = extKeys.Where(k => !contacts.ContainsKey(k)).ToList();
        var repoR = await r_getContactsFromRepo.HandleAsync(new(missingKeys), ct);

        // If that succeeded, add results to the list, cache and return.
        if (repoR.CheckSuccess(out var repoOutput))
        {
            foreach (var kvp in repoOutput?.Contacts ?? [])
            {
                contacts[kvp.Key] = kvp.Value;
            }

            await SetInCacheAsync(repoOutput!.Contacts, ct);
            return await SuccessAsync(contacts, ct);
        }

        // Handle specific error codes.
        switch (repoR.ErrorCode)
        {
            // If NO contacts were found in repo, return what we have from cache.
            case ErrorCodes.NOT_FOUND:
                {
                    if (contacts.Count > 0)
                    {
                        return await SomeFoundAsync(contacts, ct);
                    }

                    return D2Result<O?>.NotFound();
                }

            // If SOME contacts were found, add to list, cache and return [fail, SOME found].
            case ErrorCodes.SOME_FOUND:
                {
                    foreach (var kvp in repoOutput?.Contacts ?? [])
                    {
                        contacts[kvp.Key] = kvp.Value;
                    }

                    await SetInCacheAsync(repoOutput!.Contacts, ct);
                    return await SomeFoundAsync(contacts, ct);
                }

            default:
                return D2Result<O?>.BubbleFail(repoR);
        }
    }

    private static (string ContextKey, Guid RelatedEntityId)? ParseCacheKey(string cacheKey)
    {
        // Format: "geo:contacts-by-extkey:{contextKey}:{relatedEntityId}"
        var prefix = $"{ClientCacheKeys.CONTACTS_BY_EXT_KEY_PREFIX}:";
        if (!cacheKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var remainder = cacheKey[prefix.Length..];
        var lastColon = remainder.LastIndexOf(':');
        if (lastColon <= 0)
        {
            return null;
        }

        var contextKey = remainder[..lastColon];
        var guidPart = remainder[(lastColon + 1)..];

        return Guid.TryParse(guidPart, out var guid) ? (contextKey, guid) : null;
    }

    private static Location? GetLocation(string? hashId, Dictionary<string, Location> locations) =>
        hashId is not null && locations.TryGetValue(hashId, out var loc) ? loc : null;

    /// <summary>
    /// Logs an error when setting contacts in the memory cache fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to set Contacts in memory cache from {HandlerName}. TraceId: {TraceId}. ErrorCode: {ErrorCode}. Messages: {Messages}.")]
    private static partial void LogCacheSetFailed(ILogger logger, Type handlerName, string? traceId, string? errorCode, List<string> messages);

    private async ValueTask SetInCacheAsync(
        Dictionary<(string ContextKey, Guid RelatedEntityId), List<Contact>> fromRepoDict,
        CancellationToken ct)
    {
        var setInCacheR = await r_memoryCacheSetMany.HandleAsync(
            new(
                fromRepoDict.ToDictionary(
                    kvp => ClientCacheKeys.ContactsByExtKey(kvp.Key.ContextKey, kvp.Key.RelatedEntityId),
                    kvp => kvp.Value),
                r_options.ContactExpirationDuration),
            ct);

        if (setInCacheR.Failed)
        {
            LogCacheSetFailed(Context.Logger, typeof(GetContactsByExtKeys), TraceId, setInCacheR.ErrorCode, setInCacheR.Messages);
        }
    }

    private async ValueTask<D2Result<O?>> SuccessAsync(
        Dictionary<(string ContextKey, Guid RelatedEntityId), List<Contact>> contacts,
        CancellationToken ct)
    {
        var result = await ConvertToProtoDictAsync(contacts, ct);
        return D2Result<O?>.Ok(new O(result));
    }

    private async ValueTask<D2Result<O?>> SomeFoundAsync(
        Dictionary<(string ContextKey, Guid RelatedEntityId), List<Contact>> contacts,
        CancellationToken ct)
    {
        var result = await ConvertToProtoDictAsync(contacts, ct);
        return D2Result<O?>.SomeFound(new O(result));
    }

    private async ValueTask<Dictionary<GetContactsExtKeys, List<ContactDTO>>> ConvertToProtoDictAsync(
        Dictionary<(string ContextKey, Guid RelatedEntityId), List<Contact>> source,
        CancellationToken ct)
    {
        // Collect all contacts from all keys.
        var allContacts = source.Values.SelectMany(list => list).ToList();

        // Fetch all locations.
        var locations = await FetchLocationsAsync(allContacts, ct);

        return source.ToDictionary(
            kvp => new GetContactsExtKeys
            {
                ContextKey = kvp.Key.ContextKey,
                RelatedEntityId = kvp.Key.RelatedEntityId.ToString(),
            },
            kvp => kvp.Value.Select(c => c.ToDTO(GetLocation(c.LocationHashId, locations))).ToList());
    }

    private async ValueTask<Dictionary<string, Location>> FetchLocationsAsync(
        IEnumerable<Contact> contacts,
        CancellationToken ct)
    {
        var locationHashIds = contacts
            .Where(c => c.LocationHashId is not null)
            .Select(c => c.LocationHashId!)
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
