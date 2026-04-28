// -----------------------------------------------------------------------
// <copyright file="ICommands.SetOnDisk.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.Interfaces.CQRS.Handlers.C;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface ICommands
{
    /// <summary>
    /// Handler for setting geographic reference data to a file on disk.
    /// </summary>
    public interface ISetOnDiskHandler : IHandler<SetOnDiskInput, SetOnDiskOutput>;

    /// <summary>
    /// Input for setting geographic reference data to a file on disk.
    /// </summary>
    ///
    /// <param name="Data">
    /// The geographic reference data.
    /// </param>
    public record SetOnDiskInput(GeoRefData Data);

    /// <summary>
    /// Output for setting geographic reference data to a file on disk.
    /// </summary>
    public record SetOnDiskOutput;
}
