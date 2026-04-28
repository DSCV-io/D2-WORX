// -----------------------------------------------------------------------
// <copyright file="GetMany.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.InMemoryCache.Default.Handlers.R;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.Extensions.Caching.Memory;
using S = D2.Shared.Interfaces.Caching.InMemory.Handlers.R.IRead;

/// <summary>
/// Handler for retrieving multiple values from the in-memory cache.
/// </summary>
///
/// <typeparam name="TValue">
/// The type of the cached values.
/// </typeparam>
public class GetMany<TValue> : BaseHandler<
        S.IGetManyHandler<TValue>, S.GetManyInput, S.GetManyOutput<TValue>>,
    S.IGetManyHandler<TValue>
{
    private readonly IMemoryCache r_memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMany{TValue}"/> class.
    /// </summary>
    ///
    /// <param name="memoryCache">
    /// The memory cache instance to use.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public GetMany(
        IMemoryCache memoryCache,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCache = memoryCache;
    }

    /// <inheritdoc/>
    protected override ValueTask<D2Result<S.GetManyOutput<TValue>?>> ExecuteAsync(
        S.GetManyInput input,
        CancellationToken ct = default)
    {
        var results = new Dictionary<string, TValue>();

        foreach (var key in input.Keys)
        {
            if (r_memoryCache.TryGetValue<TValue>(key, out var value))
            {
                results[key] = value!;
            }
        }

        if (results.Count is 0)
        {
            return ValueTask.FromResult(
                D2Result<S.GetManyOutput<TValue>?>.NotFound());
        }

        if (results.Count < input.Keys.Count)
        {
            return ValueTask.FromResult(
                D2Result<S.GetManyOutput<TValue>?>.SomeFound(new(results)));
        }

        return ValueTask.FromResult(
            D2Result<S.GetManyOutput<TValue>?>.Ok(
                new S.GetManyOutput<TValue>(results)));
    }
}
