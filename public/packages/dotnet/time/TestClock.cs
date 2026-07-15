// -----------------------------------------------------------------------
// <copyright file="TestClock.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Time;

using NodaTime;

/// <summary>
/// Test-only implementation of <see cref="IClock" /> with a controllable
/// current instant. Construct with an initial <see cref="Instant" /> and
/// advance or set the clock as needed. Thread-safe: concurrent reads and
/// writes are safe.
/// </summary>
/// <remarks>
/// Never register this type in production DI. Construct it directly in test
/// setup and pass it as the <see cref="IClock" /> argument.
/// </remarks>
public sealed class TestClock : IClock
{
    // Lock object for thread-safe Now reads and writes.
    private readonly object r_lock = new();

    // Mutable backing field; protected by r_lock.
    private Instant _instant;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestClock" /> class with
    /// the given initial instant.
    /// </summary>
    /// <param name="initial">
    /// The instant that <see cref="GetCurrentInstant" /> will return until
    /// <see cref="Advance" /> or <see cref="SetTo" /> is called.
    /// </param>
    public TestClock(Instant initial)
    {
        _instant = initial;
    }

    /// <summary>
    /// Gets the current simulated instant. Equivalent to calling
    /// <see cref="GetCurrentInstant" />.
    /// </summary>
    public Instant Now
    {
        get
        {
            lock (r_lock)
                return _instant;
        }
    }

    /// <inheritdoc />
    public Instant GetCurrentInstant()
    {
        lock (r_lock)
            return _instant;
    }

    /// <summary>
    /// Advances the current simulated instant forward (or backward for a
    /// negative <paramref name="duration" />) by the given duration.
    /// A zero-duration advance is a no-op.
    /// </summary>
    /// <param name="duration">The amount of time to advance.</param>
    public void Advance(Duration duration)
    {
        lock (r_lock)
            _instant = _instant.Plus(duration);
    }

    /// <summary>
    /// Sets the current simulated instant to an explicit value, replacing
    /// any previously-advanced instant.
    /// </summary>
    /// <param name="instant">The new instant to use.</param>
    public void SetTo(Instant instant)
    {
        lock (r_lock)
            _instant = instant;
    }
}
