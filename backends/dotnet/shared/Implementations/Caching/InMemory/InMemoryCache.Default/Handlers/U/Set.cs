// -----------------------------------------------------------------------
// <copyright file="Set.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable AccessToStaticMemberViaDerivedType
namespace D2.Shared.InMemoryCache.Default.Handlers.U;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.Extensions.Caching.Memory;
using S = D2.Shared.Interfaces.Caching.InMemory.Handlers.U.IUpdate;

/// <summary>
/// Handler for setting a value in the in-memory cache.
/// </summary>
///
/// <typeparam name="TValue">
/// The type of the value to cache.
/// </typeparam>
public class Set<TValue> : BaseHandler<
        S.ISetHandler<TValue>, S.SetInput<TValue>, S.SetOutput>,
    S.ISetHandler<TValue>
{
    private readonly IMemoryCache r_memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="Set{TValue}"/> class.
    /// </summary>
    ///
    /// <param name="memoryCache">
    /// The memory cache instance to use.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Set(
        IMemoryCache memoryCache,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCache = memoryCache;
    }

    /// <inheritdoc/>
    protected override ValueTask<D2Result<S.SetOutput?>> ExecuteAsync(
        S.SetInput<TValue> input,
        CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions { Size = 1 };

        if (input.Expiration is not null)
        {
            options.SetAbsoluteExpiration((TimeSpan)input.Expiration);
        }

        r_memoryCache.Set(input.Key, input.Value, options);

        return ValueTask.FromResult(D2Result<S.SetOutput?>.Ok(new S.SetOutput()));
    }
}
