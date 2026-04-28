// -----------------------------------------------------------------------
// <copyright file="IRead.GetWhoIsByIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.Repository.Handlers.R;

using D2.Geo.Domain.Entities;
using D2.Shared.Handler;

public partial interface IRead
{
    /// <summary>
    /// Handler for getting WhoIs by their IDs.
    /// </summary>
    public interface IGetWhoIsByIdsHandler
        : IHandler<GetWhoIsByIdInput, GetWhoIsByIdOutput>;

    /// <summary>
    /// Input for getting WhoIs by their IDs.
    /// </summary>
    ///
    /// <param name="WhoIsHashIds">
    /// The list of content-addressible hash identifiers of the WhoIs.
    /// </param>
    public record GetWhoIsByIdInput(List<string> WhoIsHashIds);

    /// <summary>
    /// Output for getting WhoIs by their IDs.
    /// </summary>
    ///
    /// <param name="WhoIs">
    /// The dictionary mapping WhoIs IDs to their corresponding WhoIs entities.
    /// </param>
    public record GetWhoIsByIdOutput(Dictionary<string, WhoIs> WhoIs);
}
