// -----------------------------------------------------------------------
// <copyright file="IRead.Exists.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Interfaces.Caching.Distributed.Handlers.R;

using D2.Shared.Handler;

public partial interface IRead
{
    /// <summary>
    /// Handler for checking if a key exists in the cache.
    /// </summary>
    public interface IExistsHandler : IHandler<ExistsInput, ExistsOutput>;

    /// <summary>
    /// Input for checking if a key exists in the cache.
    /// </summary>
    ///
    /// <param name="Key">
    /// The key to check for existence in the cache.
    /// </param>
    public record ExistsInput(string Key);

    /// <summary>
    /// Output for checking if a key exists in the cache.
    /// </summary>
    ///
    /// <param name="Exists">
    /// Indicates whether the key exists in the cache.
    /// </param>
    public record ExistsOutput(bool Exists);
}
