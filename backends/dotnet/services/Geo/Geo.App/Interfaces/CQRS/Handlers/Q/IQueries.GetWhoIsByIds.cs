// -----------------------------------------------------------------------
// <copyright file="IQueries.GetWhoIsByIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.CQRS.Handlers.Q;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IQueries
{
    /// <summary>
    /// Handler for getting WhoIs information by their IDs.
    /// </summary>
    public interface IGetWhoIsByIdsHandler
        : IHandler<GetWhoIsByIdsInput, GetWhoIsByIdsOutput>;

    /// <summary>
    /// Input for getting WhoIs information by their IDs.
    /// </summary>
    ///
    /// <param name="HashIds">
    /// The WhoIs hash IDs to retrieve.
    /// </param>
    public record GetWhoIsByIdsInput(IReadOnlyList<string> HashIds);

    /// <summary>
    /// Output for getting WhoIs information by their IDs.
    /// </summary>
    ///
    /// <param name="Data">
    /// A dictionary mapping WhoIs IDs to their corresponding WhoIsDTOs.
    /// </param>
    public record GetWhoIsByIdsOutput(Dictionary<string, WhoIsDTO> Data);
}
