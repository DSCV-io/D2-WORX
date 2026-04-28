// -----------------------------------------------------------------------
// <copyright file="IRead.GetMany.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Interfaces.Caching.InMemory.Handlers.R;

using D2.Shared.Handler;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;

public partial interface IRead
{
    /// <summary>
    /// Handler for getting multiple values from the cache.
    /// </summary>
    ///
    /// <typeparam name="TValue">
    /// The type of the cached values.
    /// </typeparam>
    public interface IGetManyHandler<TValue> : IHandler<GetManyInput, GetManyOutput<TValue>>;

    /// <summary>
    /// Input for getting multiple values from the cache.
    /// </summary>
    ///
    /// <param name="Keys">
    /// A list of keys for the cached items to retrieve.
    /// </param>
    public record GetManyInput(List<string> Keys);

    /// <summary>
    /// Output for getting multiple values from the cache.
    /// </summary>
    ///
    /// <param name="Values">
    /// A dictionary containing the keys and their corresponding cached values.
    /// </param>
    ///
    /// <typeparam name="TValue">
    /// The type of the cached values.
    /// </typeparam>
    public record GetManyOutput<TValue>(
        [property: RedactData(Reason = RedactReason.SecretInformation, CustomReason = "Cached value")]
        Dictionary<string, TValue> Values);
}
