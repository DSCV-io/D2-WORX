// -----------------------------------------------------------------------
// <copyright file="GetFromDist.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.Q;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.R;
using D2.Shared.Result;

// ReSharper disable AccessToStaticMemberViaDerivedType
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.IGetFromDistHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.GetFromDistInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.GetFromDistOutput;

/// <summary>
/// Handler for getting georeference data from distributed cache.
/// </summary>
public class GetFromDist : BaseHandler<GetFromDist, I, O>, H
{
    private readonly IRead.IGetHandler<GeoRefData> r_distCacheGet;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetFromDist"/> class.
    /// </summary>
    ///
    /// <param name="distCacheGet">
    /// The distributed cache get handler.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public GetFromDist(
        IRead.IGetHandler<GeoRefData> distCacheGet,
        IHandlerContext context)
        : base(context)
    {
        r_distCacheGet = distCacheGet;
    }

    /// <inheritdoc />
    protected override HandlerOptions DefaultOptions => new(LogInput: false, LogOutput: false);

    /// <summary>
    /// Executes the handler to get georeference data from distributed cache.
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
        var getR = await r_distCacheGet.HandleAsync(
            new(CacheKeys.REFDATA),
            ct);

        if (getR.CheckSuccess(out var output))
        {
            return D2Result<O?>.Ok(new(output!.Value!));
        }

        return D2Result<O?>.NotFound();
    }
}
