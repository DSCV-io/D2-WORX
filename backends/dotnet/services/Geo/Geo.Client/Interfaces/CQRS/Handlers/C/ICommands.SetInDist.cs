// -----------------------------------------------------------------------
// <copyright file="ICommands.SetInDist.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.Interfaces.CQRS.Handlers.C;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface ICommands
{
    /// <summary>
    /// Handler for setting geographic reference data to a distributed cache.
    /// </summary>
    /// <remarks>
    /// To be used by data publishers only.
    /// </remarks>
    public interface ISetInDistHandler : IHandler<SetInDistInput, SetInDistOutput>;

    /// <summary>
    /// Input for setting geographic reference data to a distributed cache.
    /// </summary>
    ///
    /// <param name="Data">
    /// The geographic reference data.
    /// </param>
    public record SetInDistInput(GeoRefData Data);

    /// <summary>
    /// Output for setting geographic reference data to a distributed cache.
    /// </summary>
    public record SetInDistOutput;
}
