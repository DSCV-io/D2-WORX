// -----------------------------------------------------------------------
// <copyright file="IComplex.FindWhoIs.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.CQRS.Handlers.X;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IComplex
{
    /// <summary>
    /// Handler for finding WhoIs information.
    /// </summary>
    public interface IFindWhoIsHandler : IHandler<FindWhoIsInput, FindWhoIsOutput>;

    /// <summary>
    /// Input for finding WhoIs information.
    /// </summary>
    ///
    /// <param name="Request">
    /// The request containing the parameters for the WhoIs lookup.
    /// </param>
    public record FindWhoIsInput(FindWhoIsRequest Request);

    /// <summary>
    /// Output for finding WhoIs information.
    /// </summary>
    ///
    /// <param name="Data">
    /// A dictionary mapping FindWhoIsKeys to their corresponding WhoIsDTOs.
    /// </param>
    public record FindWhoIsOutput(Dictionary<FindWhoIsKeys, WhoIsDTO> Data);
}
