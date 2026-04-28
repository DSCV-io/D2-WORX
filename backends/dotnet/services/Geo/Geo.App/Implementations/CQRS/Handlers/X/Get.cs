// -----------------------------------------------------------------------
// <copyright file="Get.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Implementations.CQRS.Handlers.X;

using D2.Geo.App.Interfaces.Messaging.Handlers.Pub;
using D2.Geo.App.Interfaces.Repository.Handlers.R;
using D2.Geo.Client.Interfaces.CQRS.Handlers.C;
using D2.Geo.Client.Interfaces.CQRS.Handlers.Q;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.Result;
using D2.Shared.Result.Retry;
using Microsoft.Extensions.Logging;
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.X.IComplex.IGetHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.X.IComplex.GetInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.X.IComplex.GetOutput;

/// <summary>
/// Handler for getting georeference data.
/// </summary>
public partial class Get : BaseHandler<Get, I, O>, H
{
    private readonly IQueries.IGetFromMemHandler r_getFromMem;
    private readonly IQueries.IGetFromDistHandler r_getFromDist;
    private readonly IQueries.IGetFromDiskHandler r_getFromDisk;
    private readonly ICommands.ISetInMemHandler r_setInMem;
    private readonly ICommands.ISetInDistHandler r_setInDist;
    private readonly ICommands.ISetOnDiskHandler r_setOnDisk;
    private readonly IRead.IGetReferenceDataHandler r_getFromRepo;
    private readonly IPubs.IUpdateHandler r_updater;

    /// <summary>
    /// Initializes a new instance of the <see cref="Get"/> class.
    /// </summary>
    ///
    /// <param name="getFromMem">
    /// The get from in-memory cache handler.
    /// </param>
    /// <param name="getFromDist">
    /// The get from distributed cache handler.
    /// </param>
    /// <param name="getFromDisk">
    /// The get from disk handler.
    /// </param>
    /// <param name="setInMem">
    /// The set in-memory cache handler.
    /// </param>
    /// <param name="setInDist">
    /// The set in distributed cache handler.
    /// </param>
    /// <param name="setOnDisk">
    /// The set on-disk handler.
    /// </param>
    /// <param name="getFromRepo">
    /// The get from repository handler.
    /// </param>
    /// <param name="updater">
    /// The update notification publisher handler.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Get(
        IQueries.IGetFromMemHandler getFromMem,
        IQueries.IGetFromDistHandler getFromDist,
        IQueries.IGetFromDiskHandler getFromDisk,
        ICommands.ISetInMemHandler setInMem,
        ICommands.ISetInDistHandler setInDist,
        ICommands.ISetOnDiskHandler setOnDisk,
        IRead.IGetReferenceDataHandler getFromRepo,
        IPubs.IUpdateHandler updater,
        IHandlerContext context)
        : base(context)
    {
        r_getFromMem = getFromMem;
        r_getFromDist = getFromDist;
        r_getFromDisk = getFromDisk;
        r_setInMem = setInMem;
        r_setInDist = setInDist;
        r_setOnDisk = setOnDisk;
        r_getFromRepo = getFromRepo;
        r_updater = updater;
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
    /// The result of the get operation.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        // 6 attempts with exponential backoff (1s, 2s, 4s, 8s, 16s). Total: ~31 seconds.
        // Retry any non-success (i.e. NotFound = data not available yet, try again).
        return await D2RetryHelper.RetryResultAsync<O?>(
            (_, innerCt) => GetAttempt(innerCt),
            new D2RetryOptions
            {
                MaxAttempts = 6,
                BaseDelayMs = 1000,
                BackoffMultiplier = 2,
                Jitter = false,
                IsTransientResult = r => !r.Success,
            },
            ct);
    }

    /// <summary>
    /// Logs an error when fetching reference data from the database fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to get data from database (provider). TraceId: {TraceId}")]
    private static partial void LogDatabaseFetchFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs an error when setting reference data in the memory cache fails.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to set data in memory cache (provider). TraceId: {TraceId}")]
    private static partial void LogSetInMemoryFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs an error when setting reference data on disk fails.
    /// </summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to set data on disk (provider). TraceId: {TraceId}")]
    private static partial void LogSetOnDiskFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs an error when setting reference data in the distributed cache fails.
    /// </summary>
    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to set data in distributed cache (provider). TraceId: {TraceId}")]
    private static partial void LogSetInDistFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs an error when publishing the update notification fails.
    /// </summary>
    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to publish update notification (provider). TraceId: {TraceId}")]
    private static partial void LogNotifyUpdateFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Attempts to get the georeference data from various sources.
    /// </summary>
    ///
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> containing a <see cref="D2Result{O}"/> the georeference data
    /// if found; otherwise, NotFound.
    /// </returns>
    private async ValueTask<D2Result<O?>> GetAttempt(
        CancellationToken ct)
    {
        // First, try to get the data from in-memory cache.
        var memoryR = await r_getFromMem.HandleAsync(new(), ct);
        if (memoryR.CheckSuccess(out var memoryOutput))
        {
            // If successful, return the data.
            return D2Result<O?>.Ok(new O(memoryOutput!.Data));
        }

        // If that fails, try to get it from distributed cache.
        var distR = await r_getFromDist.HandleAsync(new(), ct);
        if (distR.CheckSuccess(out var distOutput))
        {
            var data = distOutput!.Data;

            // If successful, store it locally.
            await SetInMemAsync(data, ct);
            await SetOnDiskAsync(data, ct);

            // Then return the data.
            return D2Result<O?>.Ok(new O(data));
        }

        // If that failed, try to get it from the database.
        var dbR = await r_getFromRepo.HandleAsync(new(), ct);
        if (dbR.CheckSuccess(out var dbOutput))
        {
            var data = dbOutput!.Data;

            // Set it everywhere and notify update.
            await SetInMemAsync(data, ct);
            await SetOnDiskAsync(data, ct);
            var setDistR = await SetInDistAsync(data, ct);
            if (setDistR.Success)
            {
                await NotifyUpdateAsync(data, ct);
            }

            // Then return the data.
            return D2Result<O?>.Ok(new O(dbOutput.Data));
        }

        // If we failed to get it from the database, log the error.
        LogDatabaseFetchFailed(Context.Logger, TraceId);

        // Then try to get it from disk.
        var diskR = await r_getFromDisk.HandleAsync(new(), ct);
        if (diskR.CheckSuccess(out var diskOutput))
        {
            var data = diskOutput!.Data;

            // Set everywhere (except where it came from) and notify update.
            await SetInMemAsync(data, ct);
            var setDistR = await SetInDistAsync(data, ct);
            if (setDistR.Success)
            {
                await NotifyUpdateAsync(data, ct);
            }

            // NOTE: If, for whatever reason, the version on disk is outdated compared to the
            // database, a scheduled job will eventually reconcile it.

            // Then return the data.
            return D2Result<O?>.Ok(new O(data));
        }

        // If all attempts failed, return NotFound.
        return D2Result<O?>.NotFound();
    }

    /// <summary>
    /// Sets the reference data in memory cache.
    /// </summary>
    ///
    /// <param name="data">
    /// The reference data.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    private async ValueTask SetInMemAsync(
        GeoRefData data,
        CancellationToken ct)
    {
        var setInMemR = await r_setInMem.HandleAsync(new(data), ct);
        if (setInMemR.Failed)
        {
            LogSetInMemoryFailed(Context.Logger, TraceId);
        }
    }

    /// <summary>
    /// Sets the reference data on disk.
    /// </summary>
    ///
    /// <param name="data">
    /// The reference data.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    private async ValueTask SetOnDiskAsync(
        GeoRefData data,
        CancellationToken ct)
    {
        var setOnDiskR = await r_setOnDisk.HandleAsync(new(data), ct);
        if (setOnDiskR.Failed)
        {
            LogSetOnDiskFailed(Context.Logger, TraceId);
        }
    }

    /// <summary>
    /// Sets the reference data in the distributed cache.
    /// </summary>
    ///
    /// <param name="data">
    /// The reference data.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> containing a <see cref="D2Result"/> indicating success or failure.
    /// </returns>
    private async ValueTask<D2Result> SetInDistAsync(
        GeoRefData data,
        CancellationToken ct)
    {
        var setInDistR = await r_setInDist.HandleAsync(new(data), ct);

        if (setInDistR.Success)
        {
            return D2Result.Ok();
        }

        LogSetInDistFailed(Context.Logger, TraceId);
        return D2Result.UnhandledException();
    }

    /// <summary>
    /// Notifies other services of an update to the reference data.
    /// </summary>
    ///
    /// <param name="data">
    /// The reference data.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    private async ValueTask NotifyUpdateAsync(
        GeoRefData data,
        CancellationToken ct)
    {
        var updateR = await r_updater.HandleAsync(new(data.Version), ct);
        if (updateR.Failed)
        {
            LogNotifyUpdateFailed(Context.Logger, TraceId);
        }
    }
}
