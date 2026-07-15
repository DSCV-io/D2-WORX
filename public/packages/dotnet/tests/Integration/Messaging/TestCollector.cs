// -----------------------------------------------------------------------
// <copyright file="TestCollector.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System.Collections.Concurrent;

/// <summary>
/// Test handlers capture deliveries into a STATIC bag so the test author can
/// assert on what arrived without holding a singleton handler reference
/// (which would break the scoped <c>IRequestContext</c> dependency chain).
/// Each test must call <see cref="Reset{THandler}"/> at the start to drain prior state.
/// </summary>
public static class TestCollector
{
    private static readonly ConcurrentDictionary<Type, ConcurrentBag<object>> sr_byHandler = new();

    /// <summary>Drains state for one handler type — call at the top of each test.</summary>
    /// <typeparam name="THandler">The handler type whose state to clear.</typeparam>
    public static void Reset<THandler>()
    {
        sr_byHandler.AddOrUpdate(
            typeof(THandler),
            _ => [],
            (_, _) => []);
    }

    /// <summary>Records a delivery against a handler type.</summary>
    /// <typeparam name="THandler">The handler type that received the delivery.</typeparam>
    /// <typeparam name="TMessage">Payload type.</typeparam>
    /// <param name="msg">The captured message instance.</param>
    public static void Add<THandler, TMessage>(TMessage msg)
        where TMessage : class
    {
        var bag = sr_byHandler.GetOrAdd(typeof(THandler), _ => []);
        bag.Add(msg);
    }

    /// <summary>Returns a snapshot of captured messages for a handler type.</summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TMessage">Payload type to filter to.</typeparam>
    public static IReadOnlyList<TMessage> Captured<THandler, TMessage>()
        where TMessage : class
    {
        if (!sr_byHandler.TryGetValue(typeof(THandler), out var bag))
            return [];
        return [.. bag.OfType<TMessage>()];
    }

    /// <summary>Returns the number of captured deliveries for a handler type.</summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <remarks>
    /// Enumerates rather than reading <see cref="ConcurrentBag{T}.Count"/> —
    /// bag.Count is documented as approximate under concurrent writers and has
    /// produced false-empty WaitFor timeouts when a single delivery was already
    /// present (handler log visible, poll still at 0).
    /// </remarks>
    public static int Count<THandler>()
    {
        if (!sr_byHandler.TryGetValue(typeof(THandler), out var bag))
            return 0;

        // Snapshot count — ConcurrentBag.Count is approximate.
        var n = 0;
        foreach (var item in bag)
        {
            _ = item;
            n++;
        }

        return n;
    }
}
