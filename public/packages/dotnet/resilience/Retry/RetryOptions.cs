// -----------------------------------------------------------------------
// <copyright file="RetryOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Retry;

/// <summary>
/// Configuration for <see cref="RetryHelper.RetryAsync{T}"/>.
/// </summary>
/// <typeparam name="T">The return type of the operation being retried.</typeparam>
/// <remarks>
/// This type (together with its non-generic peer <see cref="RetryDefaults"/>)
/// is the single source of truth for retry defaults.
/// <see cref="RetryHelper.RetryAsync{T}"/> defers to it via
/// <c>options ??= new RetryOptions&lt;T&gt;()</c>; no defaults are restated
/// in the helper itself.
/// <para>
/// Eight properties — past the threshold for the small-Options-record ctor
/// pattern. Use object-initializer syntax for overrides.
/// </para>
/// </remarks>
public sealed record RetryOptions<T>
{
    /// <summary>
    /// Default value-failure predicate — accepts every returned value (i.e.
    /// retries are driven by exceptions only). The only default that
    /// genuinely depends on <c>T</c>; lives on the generic type so the
    /// signature matches without runtime casting. <see cref="RetryHelper"/>
    /// uses <b>reference-equality</b> against this delegate to detect
    /// "caller didn't customize ShouldRetry" — do NOT rebind to a
    /// structurally-identical lambda (e.g. <c>options with { ShouldRetry =
    /// _ => false }</c>) expecting the smart-substitution path to fire.
    /// Reference, not behavior, is the discriminator.
    /// </summary>
    internal static readonly Func<T, bool> SR_DefaultShouldRetry = static _ => false;

    /// <summary>
    /// Gets the maximum number of attempts (including the initial call).
    /// Default: 5. <c>0</c> and <c>1</c> are observably equivalent — the
    /// attempt counter is post-incremented, so a single execution always
    /// happens regardless of the cap; values below 2 disable retries.
    /// </summary>
    public int MaxAttempts { get; init; } = RetryDefaults.MAX_ATTEMPTS;

    /// <summary>
    /// Gets the base delay (in milliseconds) before the first retry.
    /// Default: 1000.
    /// </summary>
    public int BaseDelayMs { get; init; } = RetryDefaults.BASE_DELAY_MS;

    /// <summary>
    /// Gets the multiplier applied to the delay after each retry.
    /// Default: 2.0.
    /// </summary>
    public double BackoffMultiplier { get; init; } = RetryDefaults.BACKOFF_MULTIPLIER;

    /// <summary>
    /// Gets the maximum delay (in milliseconds) regardless of how many
    /// retries have occurred. Default: 30000.
    /// </summary>
    public int MaxDelayMs { get; init; } = RetryDefaults.MAX_DELAY_MS;

    /// <summary>
    /// Gets a value indicating whether to apply full jitter
    /// (uniform <c>[0, calculated)</c>). Default: <c>true</c>.
    /// </summary>
    public bool Jitter { get; init; } = RetryDefaults.JITTER;

    /// <summary>
    /// Gets the delegate that inspects a returned value to decide whether to
    /// retry. Default: never retry returns (every value is accepted).
    /// </summary>
    public Func<T, bool> ShouldRetry { get; init; } = SR_DefaultShouldRetry;

    /// <summary>
    /// Gets the delegate that inspects a thrown exception to decide whether
    /// to retry. Default: <see cref="RetryHelper.IsTransientException"/>.
    /// </summary>
    public Func<Exception, bool> IsTransient { get; init; } = RetryDefaults.SR_IsTransient;

    /// <summary>
    /// Gets the delay function used between retries. Default:
    /// <see cref="Task.Delay(TimeSpan, CancellationToken)"/>. Override only
    /// for tests that need synchronous / instant delay control.
    /// </summary>
    public Func<TimeSpan, CancellationToken, Task> DelayFunc { get; init; } =
        RetryDefaults.SR_DelayFunc;
}
