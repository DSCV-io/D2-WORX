// -----------------------------------------------------------------------
// <copyright file="IRead.GetContactsByIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.Repository.Handlers.R;

using D2.Geo.Domain.Entities;
using D2.Shared.Handler;

public partial interface IRead
{
    /// <summary>
    /// Handler for getting Contacts by their IDs.
    /// </summary>
    public interface IGetContactsByIdsHandler
        : IHandler<GetContactsByIdsInput, GetContactsByIdsOutput>;

    /// <summary>
    /// Input for getting Contacts by their IDs.
    /// </summary>
    ///
    /// <param name="ContactIds">
    /// The list of Contact unique identifiers.
    /// </param>
    public record GetContactsByIdsInput(List<Guid> ContactIds);

    /// <summary>
    /// Output for getting Contacts by their IDs.
    /// </summary>
    ///
    /// <param name="Contacts">
    /// The dictionary mapping Contact IDs to their corresponding Contact entities.
    /// </param>
    public record GetContactsByIdsOutput(Dictionary<Guid, Contact> Contacts);
}
