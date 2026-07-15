// -----------------------------------------------------------------------
// <copyright file="RateLimitRejectedException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.RateLimiting;

/// <summary>
/// Thrown by <see cref="RateLimiter.ExecuteAsync{T}"/> (and
/// <see cref="Pipeline.RateLimiterLayer{TKey, TValue}"/>) when the caller cannot
/// acquire a concurrency permit within the configured
/// <see cref="RateLimiterOptions.AcquisitionTimeout"/> window.
/// </summary>
/// <remarks>
/// At the <see cref="Pipeline.ResilientPipeline{TKey, TValue}"/> boundary this
/// exception is caught and mapped to <c>D2Result.TooManyRequests()</c>
/// (429 / <c>RATE_LIMITED</c>, <c>IsTransientRetryable = true</c>).
/// </remarks>
public sealed class RateLimitRejectedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitRejectedException"/>
    /// class with a default message.
    /// </summary>
    public RateLimitRejectedException()
        : base("Rate limit exceeded: could not acquire a concurrency permit within the configured timeout.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitRejectedException"/>
    /// class with the specified message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public RateLimitRejectedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitRejectedException"/>
    /// class with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RateLimitRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
