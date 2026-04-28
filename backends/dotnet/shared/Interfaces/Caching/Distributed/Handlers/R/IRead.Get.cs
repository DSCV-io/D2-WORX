// -----------------------------------------------------------------------
// <copyright file="IRead.Get.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Interfaces.Caching.Distributed.Handlers.R;

using D2.Shared.Handler;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;

public partial interface IRead
{
    /// <summary>
    /// Handler for getting a value from the cache.
    /// </summary>
    ///
    /// <typeparam name="TValue">
    /// The type of the cached value.
    /// </typeparam>
    public interface IGetHandler<TValue> : IHandler<GetInput, GetOutput<TValue>>;

    /// <summary>
    /// Input for getting a value from the cache.
    /// </summary>
    ///
    /// <param name="Key">
    /// The key of the cached item to retrieve.
    /// </param>
    public record GetInput(string Key);

    /// <summary>
    /// Output for getting a value from the cache.
    /// </summary>
    ///
    /// <param name="Value">
    /// The retrieved value, or null if the key does not exist.
    /// </param>
    ///
    /// <typeparam name="TValue">
    /// The type of the cached value.
    /// </typeparam>
    public record GetOutput<TValue>(
        [property: RedactData(Reason = RedactReason.SecretInformation, CustomReason = "Cached value")]
        TValue? Value);
}
