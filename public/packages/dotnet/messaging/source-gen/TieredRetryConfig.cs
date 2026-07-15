// -----------------------------------------------------------------------
// <copyright file="TieredRetryConfig.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Optional broker-level tiered-retry configuration carried on a
/// subscription entry.
/// </summary>
/// <param name="Tiers">TTL per retry tier as TimeSpan strings (HH:MM:SS).
/// Length determines tier count.</param>
/// <param name="MaxAttempts">Hard cap on total delivery attempts before DLQ
/// with cause <c>RETRIES_EXHAUSTED</c>.</param>
internal sealed record TieredRetryConfig(
    ImmutableArray<string> Tiers,
    int MaxAttempts);
