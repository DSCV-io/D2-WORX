// -----------------------------------------------------------------------
// <copyright file="SetMany.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.InMemoryCache.Default.Handlers.U;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.Extensions.Caching.Memory;
using S = D2.Shared.Interfaces.Caching.InMemory.Handlers.U.IUpdate;

/// <summary>
/// Handler for setting multiple values in the in-memory cache.
/// </summary>
///
/// <typeparam name="TValue">
/// The type of the values to cache.
/// </typeparam>
public class SetMany<TValue> : BaseHandler<
        S.ISetManyHandler<TValue>, S.SetManyInput<TValue>, S.SetManyOutput>,
    S.ISetManyHandler<TValue>
{
    private readonly IMemoryCache r_memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetMany{TValue}"/> class.
    /// </summary>
    ///
    /// <param name="memoryCache">
    /// The memory cache instance.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public SetMany(
        IMemoryCache memoryCache,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCache = memoryCache;
    }

    /// <inheritdoc/>
    protected override ValueTask<D2Result<S.SetManyOutput?>> ExecuteAsync(
        S.SetManyInput<TValue> input,
        CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions { Size = 1 };

        if (input.Expiration is not null)
        {
            options.SetAbsoluteExpiration((TimeSpan)input.Expiration);
        }

        foreach (var kvp in input.Values)
        {
            r_memoryCache.Set(kvp.Key, kvp.Value, options);
        }

        return ValueTask.FromResult(
            D2Result<S.SetManyOutput?>.Ok(new S.SetManyOutput()));
    }
}
