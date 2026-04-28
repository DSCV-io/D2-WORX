// -----------------------------------------------------------------------
// <copyright file="IQueries.GetFromMem.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.Interfaces.CQRS.Handlers.Q;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IQueries
{
    /// <summary>
    /// Handler for getting geographic reference data from an in-memory cache.
    /// </summary>
    public interface IGetFromMemHandler : IHandler<GetFromMemInput, GetFromMemOutput>;

    /// <summary>
    /// Input for getting geographic reference data from an in-memory cache.
    /// </summary>
    public record GetFromMemInput;

    /// <summary>
    /// Output for getting geographic reference data from an in-memory cache.
    /// </summary>
    ///
    /// <param name="Data">
    /// The geographic reference data retrieved.
    /// </param>
    public record GetFromMemOutput(GeoRefData Data);
}
