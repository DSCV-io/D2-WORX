// -----------------------------------------------------------------------
// <copyright file="IQueries.GetContactsByIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.CQRS.Handlers.Q;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IQueries
{
    /// <summary>
    /// Handler for getting contacts by their IDs.
    /// </summary>
    public interface IGetContactsByIdsHandler
        : IHandler<GetContactsByIdsInput, GetContactsByIdsOutput>;

    /// <summary>
    /// Input for getting contacts by their IDs.
    /// </summary>
    ///
    /// <param name="Request">
    /// The request containing the contact IDs to retrieve.
    /// </param>
    public record GetContactsByIdsInput(GetContactsRequest Request);

    /// <summary>
    /// Output for getting contacts by their IDs.
    /// </summary>
    ///
    /// <param name="Data">
    /// A dictionary mapping contact IDs to their corresponding ContactDTOs.
    /// </param>
    public record GetContactsByIdsOutput(Dictionary<Guid, ContactDTO> Data);
}
