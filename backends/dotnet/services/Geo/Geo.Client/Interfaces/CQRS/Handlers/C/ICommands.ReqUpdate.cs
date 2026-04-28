// -----------------------------------------------------------------------
// <copyright file="ICommands.ReqUpdate.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.Interfaces.CQRS.Handlers.C;

using D2.Shared.Handler;

public partial interface ICommands
{
    /// <summary>
    /// Handler for requesting an update of geographic reference data.
    /// </summary>
    /// <remarks>
    /// To be used by data consumers only.
    /// </remarks>
    public interface IReqUpdateHandler : IHandler<ReqUpdateInput, ReqUpdateOutput>;

    /// <summary>
    /// Input for requesting an update of geographic reference data.
    /// </summary>
    public record ReqUpdateInput;

    /// <summary>
    /// Output for requesting an update of geographic reference data.
    /// </summary>
    ///
    /// <param name="Version">
    /// The version of the updated geographic reference data.
    /// </param>
    public record ReqUpdateOutput(string? Version);
}
