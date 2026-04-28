// -----------------------------------------------------------------------
// <copyright file="IQueries.GetFromDist.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.Interfaces.CQRS.Handlers.Q;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IQueries
{
    /// <summary>
    /// Handler for getting geographic reference data from a distributed cache.
    /// </summary>
    public interface IGetFromDistHandler : IHandler<GetFromDistInput, GetFromDistOutput>;

    /// <summary>
    /// Input for getting geographic reference data from a distributed cache.
    /// </summary>
    public record GetFromDistInput;

    /// <summary>
    /// Output for getting geographic reference data from a distributed cache.
    /// </summary>
    ///
    /// <param name="Data">
    /// The geographic reference data retrieved.
    /// </param>
    public record GetFromDistOutput(GeoRefData Data);
}
