// -----------------------------------------------------------------------
// <copyright file="ReqUpdate.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.C;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.Result;
using D2.Shared.Result.Extensions;
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.IReqUpdateHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.ReqUpdateInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.ReqUpdateOutput;

/// <summary>
/// Handler for requesting a reference data update from the Geo service.
/// </summary>
/// <remarks>
/// This implementation is meant for consumer services only.
/// </remarks>
public class ReqUpdate : BaseHandler<ReqUpdate, I, O>, H
{
    private readonly GeoService.GeoServiceClient r_geoClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReqUpdate"/> class.
    /// </summary>
    ///
    /// <param name="geoClient">
    /// The Geo service gRPC client.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public ReqUpdate(
        GeoService.GeoServiceClient geoClient,
        IHandlerContext context)
        : base(context)
    {
        r_geoClient = geoClient;
    }

    /// <summary>
    /// Executes the handler to request a reference data update.
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
    /// The result of the request operation.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        var r = await r_geoClient.RequestReferenceDataUpdateAsync(
                new(),
                cancellationToken: ct)
            .HandleAsync(
                r => r.Result,
                r => r.Data,
                Context.Logger);

        return D2Result<O?>.Bubble(r, new(r.Data?.Version));
    }
}
