// -----------------------------------------------------------------------
// <copyright file="IUpdate.SetMany.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Interfaces.Caching.InMemory.Handlers.U;

using D2.Shared.Handler;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;

public partial interface IUpdate
{
    /// <summary>
    /// Handler for setting multiple values in the cache.
    /// </summary>
    ///
    /// <typeparam name="TValue">
    /// The type of the values to cache.
    /// </typeparam>
    public interface ISetManyHandler<TValue> : IHandler<SetManyInput<TValue>, SetManyOutput>;

    /// <summary>
    /// Input for setting multiple values in the cache.
    /// </summary>
    ///
    /// <param name="Values">
    /// A dictionary containing the keys and their corresponding values to store in the cache.
    /// </param>
    /// <param name="Expiration">
    /// The optional expiration time for the cached items.
    /// </param>
    ///
    /// <typeparam name="TValue">
    /// The type of the values to cache.
    /// </typeparam>
    public record SetManyInput<TValue>(
        [property: RedactData(Reason = RedactReason.SecretInformation, CustomReason = "Cached value")]
        Dictionary<string, TValue> Values,
        TimeSpan? Expiration = null);

    /// <summary>
    /// Output for setting multiple values in the cache.
    /// </summary>
    public record SetManyOutput;
}
