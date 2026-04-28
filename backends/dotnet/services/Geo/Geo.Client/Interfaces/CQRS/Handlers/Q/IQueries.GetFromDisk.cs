// -----------------------------------------------------------------------
// <copyright file="IQueries.GetFromDisk.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.Interfaces.CQRS.Handlers.Q;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IQueries
{
    /// <summary>
    /// Handler for getting geographic reference data from a file on disk.
    /// </summary>
    public interface IGetFromDiskHandler : IHandler<GetFromDiskInput, GetFromDiskOutput>;

    /// <summary>
    /// Input for getting geographic reference data from a file on disk.
    /// </summary>
    public record GetFromDiskInput;

    /// <summary>
    /// Output for getting geographic reference data from a file on disk.
    /// </summary>
    ///
    /// <param name="Data">
    /// The geographic reference data retrieved.
    /// </param>
    public record GetFromDiskOutput(GeoRefData Data);
}
