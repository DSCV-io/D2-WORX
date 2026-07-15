// -----------------------------------------------------------------------
// <copyright file="MiddlewareOrderRecorder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults.Infrastructure;

/// <summary>
/// Thread-safe sequence recorder used by the middleware-ordering tests.
/// Marker middleware installed AROUND each <c>UseDefault*</c> step appends
/// a label here at request time; the test asserts the captured sequence
/// matches the locked middleware order documented on
/// <c>WebApplicationServiceDefaultsExtensions.UseD2DefaultPipeline</c>.
/// </summary>
/// <remarks>
/// <para>
/// One instance per test host (registered as a singleton in
/// <see cref="CompositeTestHostBuilder"/>); ordering tests issue ONE
/// request per test method + assert on the single-request execution
/// sequence so concurrent requests can't interleave.
/// </para>
/// <para>
/// Uses <c>lock</c> over a private list rather than
/// <c>ConcurrentBag&lt;T&gt;</c> because order semantics matter — the
/// test asserts on the captured sequence, and <c>ConcurrentBag</c> does
/// not preserve insertion order.
/// </para>
/// </remarks>
internal sealed class MiddlewareOrderRecorder
{
    private readonly List<string> r_entries = new();

    /// <summary>
    /// Gets a snapshot of the recorded entries in the order they were
    /// appended.
    /// </summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (r_entries)
                return r_entries.ToList();
        }
    }

    /// <summary>
    /// Appends <paramref name="label"/> to the recorded sequence.
    /// </summary>
    /// <param name="label">The marker label (e.g. "before-routing").</param>
    public void Record(string label)
    {
        lock (r_entries)
            r_entries.Add(label);
    }

    /// <summary>
    /// Discards every recorded entry so a single test can re-use the
    /// recorder across multiple requests.
    /// </summary>
    public void Reset()
    {
        lock (r_entries)
            r_entries.Clear();
    }
}
