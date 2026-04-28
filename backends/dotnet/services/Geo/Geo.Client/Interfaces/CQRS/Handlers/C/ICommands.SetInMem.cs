// -----------------------------------------------------------------------
// <copyright file="ICommands.SetInMem.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.Interfaces.CQRS.Handlers.C;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface ICommands
{
    /// <summary>
    /// Handler for setting geographic reference data to an in-memory cache.
    /// </summary>
    public interface ISetInMemHandler : IHandler<SetInMemInput, SetInMemOutput>;

    /// <summary>
    /// Input for setting geographic reference data to an in-memory cache.
    /// </summary>
    ///
    /// <param name="Data">
    /// The geographic reference data.
    /// </param>
    public record SetInMemInput(GeoRefData Data);

    /// <summary>
    /// Output for setting geographic reference data to an in-memory cache.
    /// </summary>
    public record SetInMemOutput;
}
