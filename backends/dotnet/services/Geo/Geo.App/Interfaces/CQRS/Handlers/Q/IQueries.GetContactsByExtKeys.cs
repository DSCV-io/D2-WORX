// -----------------------------------------------------------------------
// <copyright file="IQueries.GetContactsByExtKeys.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.CQRS.Handlers.Q;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IQueries
{
    /// <summary>
    /// Handler for getting contacts by external keys.
    /// </summary>
    public interface IGetContactsByExtKeysHandler
        : IHandler<GetContactsByExtKeysInput, GetContactsByExtKeysOutput>;

    /// <summary>
    /// Input for getting contacts by external keys.
    /// </summary>
    ///
    /// <param name="Request">
    /// The request containing the external keys to retrieve.
    /// </param>
    public record GetContactsByExtKeysInput(GetContactsByExtKeysRequest Request);

    /// <summary>
    /// Output for getting contacts by external keys.
    /// </summary>
    ///
    /// <param name="Data">
    /// A dictionary mapping external keys to their corresponding lists of ContactDTOs.
    /// </param>
    public record GetContactsByExtKeysOutput(Dictionary<GetContactsExtKeys, List<ContactDTO>> Data);
}
