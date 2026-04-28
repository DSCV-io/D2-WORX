// -----------------------------------------------------------------------
// <copyright file="ICreate.CreateLocations.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.Repository.Handlers.C;

using D2.Geo.Domain.Entities;
using D2.Shared.Handler;

public partial interface ICreate
{
    /// <summary>
    /// Handler for upserting Locations.
    /// </summary>
    public interface ICreateLocationsHandler
        : IHandler<CreateLocationsInput, CreateLocationsOutput>;

    /// <summary>
    /// Input for upserting Locations.
    /// </summary>
    ///
    /// <param name="Locations">
    /// The list of Location entities to be upserted.
    /// </param>
    public record CreateLocationsInput(List<Location> Locations);

    /// <summary>
    /// Output for upserting Locations.
    /// </summary>
    ///
    /// <param name="Created">
    /// The number of Location records that were created.
    /// </param>
    ///
    /// <remarks>
    /// The <see cref="CreateLocationsOutput.Created"/> property indicates how many Location records
    /// were created during the upsert operation. <see cref="Location"/> records are immutable,
    /// so any records that already exist in the database will not be counted as created.
    /// </remarks>
    public record CreateLocationsOutput(int Created);
}
