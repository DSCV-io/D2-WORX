// -----------------------------------------------------------------------
// <copyright file="Get.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.X;

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
/// <remarks>
/// This implementation is meant for consumer services only.
/// </remarks>
public partial class Get : BaseHandler<Get, I, O>, H
{
    private readonly IQueries.IGetFromMemHandler r_getFromMem;
    private readonly IQueries.IGetFromDistHandler r_getFromDist;
    private readonly ICommands.ISetInMemHandler r_setInMem;
    private readonly ICommands.ISetOnDiskHandler r_setOnDisk;
    private readonly IQueries.IGetFromDiskHandler r_getFromDisk;
    private readonly ICommands.IReqUpdateHandler r_requestUpdate;

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
    /// <param name="setInMem">
    /// The set in-memory cache handler.
    /// </param>
    /// <param name="setOnDisk">
    /// The set on-disk handler.
    /// </param>
    /// <param name="getFromDisk">
    /// The get from disk handler.
    /// </param>
    /// <param name="requestUpdate">
    /// The request update handler.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Get(
        IQueries.IGetFromMemHandler getFromMem,
        IQueries.IGetFromDistHandler getFromDist,
        ICommands.ISetInMemHandler setInMem,
        ICommands.ISetOnDiskHandler setOnDisk,
        IQueries.IGetFromDiskHandler getFromDisk,
        ICommands.IReqUpdateHandler requestUpdate,
        IHandlerContext context)
        : base(context)
    {
        r_getFromMem = getFromMem;
        r_setInMem = setInMem;
        r_setOnDisk = setOnDisk;
        r_getFromDist = getFromDist;
        r_getFromDisk = getFromDisk;
        r_requestUpdate = requestUpdate;
    }

    /// <inheritdoc />
    protected override HandlerOptions DefaultOptions => new(LogInput: false, LogOutput: false);

    /// <summary>
    /// Gets the georeference data.
    /// </summary>
    ///
    /// <param name="input">
    /// The get input.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> containing a <see cref="D2Result{O}"/> the georeference data
    /// if found; otherwise, NotFound.
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
    /// Logs an error when requesting an update of georeference data from the Geo service fails.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to request update of georeference data (consumer). TraceId: {TraceId}")]
    private static partial void LogRequestUpdateFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs an error when setting georeference data in the memory cache fails.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to set data in memory cache (consumer). TraceId: {TraceId}")]
    private static partial void LogSetInMemoryFailed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs an error when setting georeference data on disk fails.
    /// </summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to set data on disk (consumer). TraceId: {TraceId}")]
    private static partial void LogSetOnDiskFailed(ILogger logger, string? traceId);

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
            await SetInMemoryAndOnDiskAsync(data, ct);

            // Then return the data.
            return D2Result<O?>.Ok(new O(data));
        }

        // If that failed, tell the Geo service to update the distributed cache.
        var updateR = await r_requestUpdate.HandleAsync(new(), ct);
        if (updateR.Failed)
        {
            // If we failed to reach the Geo service, log an error.
            LogRequestUpdateFailed(Context.Logger, TraceId);
        }

        // Then try to get it from disk.
        var diskR = await r_getFromDisk.HandleAsync(new(), ct);
        if (diskR.CheckSuccess(out var diskOutput))
        {
            var data = diskOutput!.Data;

            // If successful, store it in memory cache.
            var setInMemR = await r_setInMem.HandleAsync(new(data), ct);
            if (setInMemR.Failed)
            {
                LogSetInMemoryFailed(Context.Logger, TraceId);
            }

            // Then return the data.
            return D2Result<O?>.Ok(new O(data));
        }

        // If all attempts failed, return NotFound.
        return D2Result<O?>.NotFound();
    }

    /// <summary>
    /// Sets the georeference data in both in-memory and on-disk.
    /// </summary>
    ///
    /// <param name="data">
    /// The georeference data.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    private async ValueTask SetInMemoryAndOnDiskAsync(
        GeoRefData data,
        CancellationToken ct)
    {
        var setInMemR = await r_setInMem.HandleAsync(new(data), ct);
        if (setInMemR.Failed)
        {
            LogSetInMemoryFailed(Context.Logger, TraceId);
        }

        var setOnDiskR = await r_setOnDisk.HandleAsync(new(data), ct);
        if (setOnDiskR.Failed)
        {
            LogSetOnDiskFailed(Context.Logger, TraceId);
        }
    }
}
