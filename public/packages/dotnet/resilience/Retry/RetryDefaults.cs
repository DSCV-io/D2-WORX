// -----------------------------------------------------------------------
// <copyright file="RetryDefaults.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Retry;

/// <summary>
/// Non-generic peer of <see cref="RetryOptions{T}"/> holding defaults that
/// don't depend on the operation's return type. Lives outside the generic
/// type so each closed <see cref="RetryOptions{T}"/> doesn't allocate its
/// own copy of these delegates / constants.
/// </summary>
internal static class RetryDefaults
{
    /// <summary>
    /// Default maximum number of attempts (including the initial call).
    /// </summary>
    internal const int MAX_ATTEMPTS = 5;

    /// <summary>
    /// Default base delay in milliseconds before the first retry.
    /// </summary>
    internal const int BASE_DELAY_MS = 1000;

    /// <summary>
    /// Default multiplier applied to the delay after each retry.
    /// </summary>
    internal const double BACKOFF_MULTIPLIER = 2.0;

    /// <summary>
    /// Default cap on the calculated delay regardless of retry count.
    /// </summary>
    internal const int MAX_DELAY_MS = 30_000;

    /// <summary>
    /// Default jitter flag (full jitter on by default).
    /// </summary>
    internal const bool JITTER = true;

    /// <summary>
    /// Default exception-classifier — delegates to
    /// <see cref="RetryHelper.IsTransientException"/>. Cached static
    /// delegate (no <c>T</c> dependency, single shared instance).
    /// </summary>
    internal static readonly Func<Exception, bool> SR_IsTransient =
        RetryHelper.IsTransientException;

    /// <summary>
    /// Default delay function —
    /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/>. Cached static
    /// delegate (no <c>T</c> dependency, single shared instance).
    /// </summary>
    internal static readonly Func<TimeSpan, CancellationToken, Task> SR_DelayFunc =
        Task.Delay;
}
