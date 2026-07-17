// -----------------------------------------------------------------------
// <copyright file="ILocalCache.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

/// <summary>
/// Per-process in-memory cache. Atomic primitives operate at process
/// scope — they do not coordinate across instances. There is no broadcast
/// surface because nothing outside this process can observe local cache
/// state.
/// </summary>
/// <remarks>
/// The interface name documents the cache scope at the dependency site.
/// Reading <c>ILocalCache cache</c> at a constructor tells you, without
/// looking at registration, that this cache lives in process memory and
/// dies with the process.
/// </remarks>
public interface ILocalCache : ICacheBasic, ICacheAtomic
{
}
