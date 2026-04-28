// -----------------------------------------------------------------------
// <copyright file="GetContactsByIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Implementations.CQRS.Handlers.Q;

using D2.Geo.App.Interfaces.CQRS.Handlers.Q;
using D2.Geo.App.Mappers;
using D2.Geo.Domain.Entities;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.R;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.U;
using D2.Shared.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClientCacheKeys = D2.Geo.Client.CacheKeys;
using H = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.IGetContactsByIdsHandler;
using I = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.GetContactsByIdsInput;
using O = D2.Geo.App.Interfaces.CQRS.Handlers.Q.IQueries.GetContactsByIdsOutput;
using ReadRepo = D2.Geo.App.Interfaces.Repository.Handlers.R.IRead;

/// <summary>
/// Handler for getting Contacts by their IDs.
/// </summary>
public partial class GetContactsByIds : BaseHandler<GetContactsByIds, I, O>, H
{
    private readonly IRead.IGetManyHandler<Contact> r_memoryCacheGetMany;
    private readonly IUpdate.ISetManyHandler<Contact> r_memoryCacheSetMany;
    private readonly ReadRepo.IGetContactsByIdsHandler r_getContactsFromRepo;
    private readonly IQueries.IGetLocationsByIdsHandler r_getLocationsByIds;
    private readonly GeoAppOptions r_options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetContactsByIds"/> class.
    /// </summary>
    ///
    /// <param name="memoryCacheGetMany">
    /// The in-memory cache get-many handler.
    /// </param>
    /// <param name="memoryCacheSetMany">
    /// The in-memory cache set-many handler.
    /// </param>
    /// <param name="getContactsFromRepo">
    /// The repository handler for getting Contacts by IDs.
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
    public GetContactsByIds(
        IRead.IGetManyHandler<Contact> memoryCacheGetMany,
        IUpdate.ISetManyHandler<Contact> memoryCacheSetMany,
        ReadRepo.IGetContactsByIdsHandler getContactsFromRepo,
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
        if (input.Request.Ids.Count == 0)
        {
            return D2Result<O?>.Ok(new O([]));
        }

        // Validate: all IDs must be valid, non-empty GUIDs.
        List<List<string>> allErrors = [];
        var requestedIds = new List<Guid>(input.Request.Ids.Count);
        for (var i = 0; i < input.Request.Ids.Count; i++)
        {
            if (!Guid.TryParse(input.Request.Ids[i], out var guid) || guid == Guid.Empty)
            {
                allErrors.Add([$"ids[{i}]", TK.Geo.Validation.ID_INVALID]);
            }
            else
            {
                requestedIds.Add(guid);
            }
        }

        if (allErrors.Count > 0)
        {
            return D2Result<O?>.BubbleFail(
                D2Result.ValidationFailed(inputErrors: allErrors));
        }

        // First, try to get Contacts from in-memory cache.
        var getFromCacheR = await r_memoryCacheGetMany.HandleAsync(
            new(requestedIds.Select(id => ClientCacheKeys.Contact(id)).ToList()), ct);

        // If that failed (for any reason other than "NOT or SOME found"), bubble up the failure.
        if (getFromCacheR.CheckFailure(out var getFromCache)
            && getFromCacheR.ErrorCode is not (ErrorCodes.NOT_FOUND or ErrorCodes.SOME_FOUND))
        {
            return D2Result<O?>.BubbleFail(getFromCacheR);
        }

        // Add found Contacts to the result dictionary.
        Dictionary<Guid, Contact> contacts = [];
        foreach (var kvp in getFromCache?.Values ?? [])
        {
            contacts[kvp.Value.Id] = kvp.Value;
        }

        // If ALL Contacts were found in cache, return them now.
        if (contacts.Count == requestedIds.Count)
        {
            return await SuccessAsync(contacts, ct);
        }

        // Otherwise, fetch missing Contacts.
        var missingIds = requestedIds.Except(contacts.Keys).ToList();
        var repoR = await r_getContactsFromRepo.HandleAsync(new(missingIds), ct);

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

        // If that failed, check the reason.
        switch (repoR.ErrorCode)
        {
            // If NO Contacts were found, return what we have.
            case ErrorCodes.NOT_FOUND:
                {
                    // If we found some in cache, return [fail, SOME found].
                    if (contacts.Count > 0)
                    {
                        return await SomeFoundAsync(contacts, ct);
                    }

                    // Otherwise, return (fail, NOT found).
                    return D2Result<O?>.NotFound();
                }

            // If SOME Contacts were found, add to list, cache and return [fail, SOME found].
            case ErrorCodes.SOME_FOUND:
                {
                    foreach (var kvp in repoOutput?.Contacts ?? [])
                    {
                        contacts[kvp.Key] = kvp.Value;
                    }

                    await SetInCacheAsync(repoOutput!.Contacts, ct);
                    return await SomeFoundAsync(contacts, ct);
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
    /// Logs an error when setting contacts in the memory cache fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to set Contacts in memory cache from {HandlerName}. TraceId: {TraceId}. ErrorCode: {ErrorCode}. Messages: {Messages}.")]
    private static partial void LogCacheSetFailed(ILogger logger, Type handlerName, string? traceId, string? errorCode, List<string> messages);

    private async ValueTask SetInCacheAsync(
        Dictionary<Guid, Contact> fromDbDict,
        CancellationToken ct)
    {
        var setInCacheR = await r_memoryCacheSetMany.HandleAsync(
            new(
                fromDbDict.ToDictionary(
                    kvp => ClientCacheKeys.Contact(kvp.Key),
                    kvp => kvp.Value),
                r_options.ContactExpirationDuration),
            ct);

        if (setInCacheR.Failed)
        {
            LogCacheSetFailed(Context.Logger, typeof(GetContactsByIds), TraceId, setInCacheR.ErrorCode, setInCacheR.Messages);
        }
    }

    private async ValueTask<D2Result<O?>> SuccessAsync(
        Dictionary<Guid, Contact> contacts,
        CancellationToken ct)
    {
        var locations = await FetchLocationsAsync(contacts.Values, ct);
        var dtoDict = contacts.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToDTO(GetLocation(kvp.Value.LocationHashId, locations)));
        return D2Result<O?>.Ok(new O(dtoDict));
    }

    private async ValueTask<D2Result<O?>> SomeFoundAsync(
        Dictionary<Guid, Contact> contacts,
        CancellationToken ct)
    {
        var locations = await FetchLocationsAsync(contacts.Values, ct);
        var dtoDict = contacts.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToDTO(GetLocation(kvp.Value.LocationHashId, locations)));
        return D2Result<O?>.SomeFound(new O(dtoDict));
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
