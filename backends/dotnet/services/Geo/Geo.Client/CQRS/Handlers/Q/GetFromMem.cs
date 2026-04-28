// -----------------------------------------------------------------------
// <copyright file="GetFromMem.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.Q;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.R;
using D2.Shared.Result;

// ReSharper disable AccessToStaticMemberViaDerivedType
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.IGetFromMemHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.GetFromMemInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.GetFromMemOutput;

/// <summary>
/// Handler for getting georeference data from memory cache.
/// </summary>
public class GetFromMem : BaseHandler<GetFromMem, I, O>, H
{
    private readonly IRead.IGetHandler<GeoRefData> r_memoryCacheGet;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetFromMem"/> class.
    /// </summary>
    ///
    /// <param name="memoryCacheGet">
    /// The memory cache get handler.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public GetFromMem(
        IRead.IGetHandler<GeoRefData> memoryCacheGet,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCacheGet = memoryCacheGet;
    }

    /// <inheritdoc />
    protected override HandlerOptions DefaultOptions => new(LogInput: false, LogOutput: false);

    /// <summary>
    /// Executes the handler to get georeference data from memory cache.
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
    /// The result of the get operation, containing the georeference data if found.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        var getR = await r_memoryCacheGet.HandleAsync(new(CacheKeys.REFDATA), ct);

        if (getR.CheckSuccess(out var output))
        {
            return D2Result<O?>.Ok(new(output!.Value));
        }

        return D2Result<O?>.NotFound();
    }
}
