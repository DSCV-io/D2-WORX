// -----------------------------------------------------------------------
// <copyright file="IDelete.Remove.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Interfaces.Caching.Distributed.Handlers.D;

using D2.Shared.Handler;

public partial interface IDelete
{
    /// <summary>
    /// Handler for removing a value from the cache.
    /// </summary>
    public interface IRemoveHandler : IHandler<RemoveInput, RemoveOutput>;

    /// <summary>
    /// Input for removing a value from the cache.
    /// </summary>
    ///
    /// <param name="Key">
    /// The key of the cached item to remove.
    /// </param>
    public record RemoveInput(string Key);

    /// <summary>
    /// Output for removing a value from the cache.
    /// </summary>
    public record RemoveOutput;
}
