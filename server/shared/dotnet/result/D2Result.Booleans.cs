// -----------------------------------------------------------------------
// <copyright file="D2Result.Booleans.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

using System.Net;

/// <summary>
/// Per-error-code boolean discriminators on <see cref="D2Result"/> + concept-named
/// combined helpers. Prefer these over manual <c>ErrorCode == ErrorCodes.X</c>
/// comparisons or <c>StatusCode == HttpStatusCode.X</c> checks — they read better
/// at the call site and make the intent explicit.
/// </summary>
public partial class D2Result
{
    /// <summary>
    /// Gets a value indicating whether this result represents a successful (Ok) outcome.
    /// </summary>
    public bool IsOk => Success;

    /// <summary>
    /// Gets a value indicating whether this result represents a Created outcome (HTTP 201).
    /// </summary>
    public bool IsCreated => StatusCode == HttpStatusCode.Created;

    /// <summary>
    /// Gets a value indicating whether this result is a not-found failure
    /// (<see cref="ErrorCodes.NOT_FOUND"/>).
    /// </summary>
    public bool IsNotFound => ErrorCode == ErrorCodes.NOT_FOUND;

    /// <summary>
    /// Gets a value indicating whether this result is a partial-found read
    /// (<see cref="ErrorCodes.SOME_FOUND"/>).
    /// </summary>
    public bool IsSomeFound => ErrorCode == ErrorCodes.SOME_FOUND;

    /// <summary>
    /// Gets a value indicating whether this result is a partial-success
    /// write (<see cref="ErrorCodes.PARTIAL_SUCCESS"/>) — multi-target
    /// operation where some targets succeeded and others failed.
    /// <see cref="Success"/> is <c>true</c> here, unlike <see cref="IsSomeFound"/>.
    /// </summary>
    public bool IsPartialSuccess => ErrorCode == ErrorCodes.PARTIAL_SUCCESS;

    /// <summary>
    /// Gets a value indicating whether this result is a conflict failure
    /// (<see cref="ErrorCodes.CONFLICT"/>).
    /// </summary>
    public bool IsConflict => ErrorCode == ErrorCodes.CONFLICT;

    /// <summary>
    /// Gets a value indicating whether this result is a forbidden failure
    /// (<see cref="ErrorCodes.FORBIDDEN"/>).
    /// </summary>
    public bool IsForbidden => ErrorCode == ErrorCodes.FORBIDDEN;

    /// <summary>
    /// Gets a value indicating whether this result is an unauthorized failure
    /// (<see cref="ErrorCodes.UNAUTHORIZED"/>).
    /// </summary>
    public bool IsUnauthorized => ErrorCode == ErrorCodes.UNAUTHORIZED;

    /// <summary>
    /// Gets a value indicating whether this result is a validation failure
    /// (<see cref="ErrorCodes.VALIDATION_FAILED"/>).
    /// </summary>
    public bool IsValidationFailed => ErrorCode == ErrorCodes.VALIDATION_FAILED;

    /// <summary>
    /// Gets a value indicating whether this result is a service-unavailable failure
    /// (<see cref="ErrorCodes.SERVICE_UNAVAILABLE"/>).
    /// </summary>
    public bool IsServiceUnavailable => ErrorCode == ErrorCodes.SERVICE_UNAVAILABLE;

    /// <summary>
    /// Gets a value indicating whether this result is a rate-limited failure
    /// (<see cref="ErrorCodes.RATE_LIMITED"/>).
    /// </summary>
    public bool IsRateLimited => ErrorCode == ErrorCodes.RATE_LIMITED;

    /// <summary>
    /// Gets a value indicating whether this result is an unhandled-exception failure
    /// (<see cref="ErrorCodes.UNHANDLED_EXCEPTION"/>).
    /// </summary>
    public bool IsUnhandledException => ErrorCode == ErrorCodes.UNHANDLED_EXCEPTION;

    /// <summary>
    /// Gets a value indicating whether this result is a payload-too-large failure
    /// (<see cref="ErrorCodes.PAYLOAD_TOO_LARGE"/>).
    /// </summary>
    public bool IsPayloadTooLarge => ErrorCode == ErrorCodes.PAYLOAD_TOO_LARGE;

    /// <summary>
    /// Gets a value indicating whether this result is a canceled failure
    /// (<see cref="ErrorCodes.CANCELED"/>).
    /// </summary>
    public bool IsCanceled => ErrorCode == ErrorCodes.CANCELED;

    /// <summary>
    /// Gets a value indicating whether this result is an idempotency-in-flight failure
    /// (<see cref="ErrorCodes.IDEMPOTENCY_IN_FLIGHT"/>).
    /// </summary>
    public bool IsIdempotencyInFlight => ErrorCode == ErrorCodes.IDEMPOTENCY_IN_FLIGHT;

    /// <summary>
    /// Gets a value indicating whether this result is a partial / missing query
    /// outcome — either <see cref="IsNotFound"/> or <see cref="IsSomeFound"/>. Useful
    /// for cache-fallback flows where "we found some" or "we found none" both warrant
    /// a downstream lookup, while other failures (Forbidden, etc.) do not.
    /// </summary>
    public bool IsPartialOrMissing => IsNotFound || IsSomeFound;

    /// <summary>
    /// Gets a value indicating whether this result represents a transient retryable
    /// failure — <see cref="IsServiceUnavailable"/> or <see cref="IsRateLimited"/>.
    /// <para>
    /// <b>Important:</b> <see cref="IsUnhandledException"/> is intentionally excluded.
    /// An unknown exception means unknown system state — retrying could mask bugs or
    /// double-execute side effects. Retry helpers consult this property; the exclusion
    /// is deliberate.
    /// </para>
    /// </summary>
    public bool IsTransientRetryable => IsServiceUnavailable || IsRateLimited;
}
