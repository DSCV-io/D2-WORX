// -----------------------------------------------------------------------
// <copyright file="SetInMem.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.C;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.U;
using D2.Shared.Result;

// ReSharper disable AccessToStaticMemberViaDerivedType
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.ISetInMemHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.SetInMemInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.SetInMemOutput;

/// <summary>
/// Handler for setting georeference data in the in-memory cache.
/// </summary>
public class SetInMem : BaseHandler<SetInMem, I, O>, H
{
    private readonly IUpdate.ISetHandler<GeoRefData> r_memoryCacheSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetInMem"/> class.
    /// </summary>
    ///
    /// <param name="memoryCacheSet">
    /// The memory cache set handler.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public SetInMem(
        IUpdate.ISetHandler<GeoRefData> memoryCacheSet,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCacheSet = memoryCacheSet;
    }

    /// <inheritdoc />
    protected override HandlerOptions DefaultOptions => new(LogInput: false, LogOutput: false);

    /// <summary>
    /// Executes the handler to set georeference data in the in-memory cache.
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
    /// The result of the set operation.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        var setR = await r_memoryCacheSet.HandleAsync(
            new(CacheKeys.REFDATA, input.Data),
            ct);

        return setR.Success
            ? D2Result<O?>.Ok()
            : D2Result<O?>.BubbleFail(setR);
    }
}
