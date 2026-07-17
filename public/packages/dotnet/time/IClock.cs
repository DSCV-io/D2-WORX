// -----------------------------------------------------------------------
// <copyright file="IClock.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Time;

using NodaTime;

/// <summary>
/// Seam for retrieving the current instant in time. Inject this interface
/// rather than calling <see cref="NodaTime.SystemClock.Instance" /> directly
/// so unit tests can supply a deterministic <see cref="TestClock" />.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Returns the current instant in time as a UTC <see cref="Instant" />.
    /// Callers must not cache the return value; call this method each time a
    /// fresh timestamp is needed.
    /// </summary>
    /// <returns>The current UTC <see cref="Instant" />.</returns>
    Instant GetCurrentInstant();
}
