// -----------------------------------------------------------------------
// <copyright file="Remove.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.InMemoryCache.Default.Handlers.D;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.Extensions.Caching.Memory;
using H = D2.Shared.Interfaces.Caching.InMemory.Handlers.D.IDelete.IRemoveHandler;
using I = D2.Shared.Interfaces.Caching.InMemory.Handlers.D.IDelete.RemoveInput;
using O = D2.Shared.Interfaces.Caching.InMemory.Handlers.D.IDelete.RemoveOutput;

/// <summary>
/// Handler for removing a value from the in-memory cache.
/// </summary>
public class Remove : BaseHandler<H, I, O>, H
{
    private readonly IMemoryCache r_memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="Remove"/> class.
    /// </summary>
    ///
    /// <param name="memoryCache">
    /// The memory cache instance to use.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Remove(
        IMemoryCache memoryCache,
        IHandlerContext context)
        : base(context)
    {
        r_memoryCache = memoryCache;
    }

    /// <inheritdoc/>
    protected override ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        r_memoryCache.Remove(input.Key);
        return ValueTask.FromResult(D2Result<O?>.Ok(new O()));
    }
}
