// -----------------------------------------------------------------------
// <copyright file="TieredRetryDescriptor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using System;

/// <summary>
/// Optional broker-level tiered-retry topology declared per subscription.
/// Carried inside an <see cref="MqSubscriptionDescriptor"/> when the
/// corresponding spec entry has a <c>tieredRetry</c> block.
/// </summary>
/// <param name="Tiers">TTL per retry tier. Length determines the number of
/// retry-tier queues declared.</param>
/// <param name="MaxAttempts">Hard cap on total delivery attempts before the
/// message is dead-lettered with cause <c>RETRIES_EXHAUSTED</c>. Enforced
/// via <c>x-death</c> header inspection on the consumer side.</param>
public sealed record TieredRetryDescriptor(
    TimeSpan[] Tiers,
    int MaxAttempts);
