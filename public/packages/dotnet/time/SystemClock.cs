// -----------------------------------------------------------------------
// <copyright file="SystemClock.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Time;

using NodaTime;

/// <summary>
/// Production implementation of <see cref="IClock" /> that delegates to
/// <see cref="NodaTime.SystemClock.Instance" />. Register this as the
/// singleton binding for <see cref="IClock" /> in each service's DI
/// composition root.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public Instant GetCurrentInstant() => NodaTime.SystemClock.Instance.GetCurrentInstant();
}
