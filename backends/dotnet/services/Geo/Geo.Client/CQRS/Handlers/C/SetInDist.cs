// -----------------------------------------------------------------------
// <copyright file="SetInDist.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.C;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.U;
using D2.Shared.Result;
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.ISetInDistHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.SetInDistInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.SetInDistOutput;

/// <summary>
/// Handler for setting geographic reference data to a distributed cache.
/// </summary>
public class SetInDist : BaseHandler<SetInDist, I, O>, H
{
    private readonly IUpdate.ISetHandler<GeoRefData> r_distCacheSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetInDist"/> class.
    /// </summary>
    ///
    /// <param name="distCacheSet">
    /// The distributed cache set handler.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public SetInDist(
        IUpdate.ISetHandler<GeoRefData> distCacheSet,
        IHandlerContext context)
        : base(context)
    {
        r_distCacheSet = distCacheSet;
    }

    /// <inheritdoc />
    protected override HandlerOptions DefaultOptions => new(LogInput: false, LogOutput: false);

    /// <summary>
    /// Executes the handler to set geographic reference data to a distributed cache.
    /// </summary>
    ///
    /// <param name="input">
    /// The input containing the geographic reference data.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="ValueTask"/> containing a <see cref="D2Result{O}"/> indicating success or
    /// failure.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        var setR = await r_distCacheSet.HandleAsync(
            new(CacheKeys.REFDATA, input.Data),
            ct);

        return setR.Success
            ? D2Result<O?>.Ok()
            : D2Result<O?>.BubbleFail(setR);
    }
}
