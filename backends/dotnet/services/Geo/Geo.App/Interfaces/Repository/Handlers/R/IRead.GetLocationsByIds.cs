// -----------------------------------------------------------------------
// <copyright file="IRead.GetLocationsByIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.Repository.Handlers.R;

using D2.Geo.Domain.Entities;
using D2.Shared.Handler;

public partial interface IRead
{
    /// <summary>
    /// Handler for getting locations by their IDs.
    /// </summary>
    public interface IGetLocationsByIdsHandler
        : IHandler<GetLocationsByIdInput, GetLocationsByIdOutput>;

    /// <summary>
    /// Input for getting locations by their IDs.
    /// </summary>
    ///
    /// <param name="LocationHashIds">
    /// The list of content-addressable hash IDs for the locations.
    /// </param>
    public record GetLocationsByIdInput(List<string> LocationHashIds);

    /// <summary>
    /// Output for getting locations by their IDs.
    /// </summary>
    ///
    /// <param name="Locations">
    /// The dictionary mapping location IDs to their corresponding Location entities.
    /// </param>
    public record GetLocationsByIdOutput(Dictionary<string, Location> Locations);
}
