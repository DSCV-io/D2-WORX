// -----------------------------------------------------------------------
// <copyright file="IPubs.Update.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.Messaging.Handlers.Pub;

using D2.Shared.Handler;

public partial interface IPubs
{
    /// <summary>
    /// Handler for publishing geographic reference data update notifications.
    /// </summary>
    public interface IUpdateHandler : IHandler<UpdateInput, UpdateOutput>;

    /// <summary>
    /// Input for publishing a geographic reference data update notification.
    /// </summary>
    ///
    /// <param name="Version">
    /// The version of the updated geographic reference data.
    /// </param>
    public record UpdateInput(string Version);

    /// <summary>
    /// Output for publishing a geographic reference data update notification.
    /// </summary>
    public record UpdateOutput;
}
