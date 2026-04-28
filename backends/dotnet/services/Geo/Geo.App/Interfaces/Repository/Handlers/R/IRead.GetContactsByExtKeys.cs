// -----------------------------------------------------------------------
// <copyright file="IRead.GetContactsByExtKeys.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.Repository.Handlers.R;

using D2.Geo.Domain.Entities;
using D2.Shared.Handler;

public partial interface IRead
{
    /// <summary>
    /// Handler for getting Contacts by their external keys.
    /// </summary>
    public interface IGetContactsByExtKeysHandler
        : IHandler<GetContactsByExtKeysInput, GetContactsByExtKeysOutput>;

    /// <summary>
    /// Input for getting Contacts by their external keys.
    /// </summary>
    ///
    /// <param name="ContactExtKeys">
    /// The list of tuples containing context keys and related entity IDs for the Contacts.
    /// </param>
    public record GetContactsByExtKeysInput(
        List<(string ContextKey, Guid RelatedEntityId)> ContactExtKeys);

    /// <summary>
    /// Output for getting Contacts by their external keys.
    /// </summary>
    ///
    /// <param name="Contacts">
    /// The dictionary mapping tuples of context keys and related entity IDs to their corresponding
    /// lists of Contact entities.
    /// </param>
    public record GetContactsByExtKeysOutput(
        Dictionary<(string ContextKey, Guid RelatedEntityId), List<Contact>> Contacts);
}
