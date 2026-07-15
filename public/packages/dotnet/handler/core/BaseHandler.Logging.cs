// -----------------------------------------------------------------------
// <copyright file="BaseHandler.Logging.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler;

using System;
using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for <see cref="BaseHandler{TSelf, TInput, TOutput}"/>.
/// Per <c>docs/dev/rules.md §3.1</c>, NO delegate accepts <see cref="Exception"/> —
/// the receiving log delegate signature IS the contract; future callers will
/// pass real exceptions whose <c>Message</c> can carry broker URIs, connection
/// strings, OAuth tokens, presigned URLs, raw user input, and similar PII.
/// Callers compute <c>ex.GetType().Name</c> (and any structured detail they
/// want surfaced) before invoking the delegate.
/// </summary>
/// <remarks>
/// Marked <c>partial</c> because the <c>LoggerMessage</c> source generator
/// emits the matching method bodies as a generated partial; both halves
/// combine into a single type at compile time.
/// </remarks>
internal static partial class BaseHandlerLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Handler {HandlerName} invoked with {@Input}")]
    public static partial void HandlerInvoked(
        ILogger logger,
        string handlerName,
        object? input);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Handler {HandlerName} returned {Status} in {DurationMs}ms")]
    public static partial void HandlerReturned(
        ILogger logger,
        string handlerName,
        string status,
        double durationMs);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Handler {HandlerName} canceled after {DurationMs}ms")]
    public static partial void HandlerCanceled(
        ILogger logger,
        string handlerName,
        double durationMs);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Handler {HandlerName} threw {ExceptionType} after {DurationMs}ms")]
    public static partial void HandlerThrew(
        ILogger logger,
        string handlerName,
        string exceptionType,
        double durationMs);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Handler {HandlerName} exceeded critical threshold "
            + "{ThresholdMs}ms with {DurationMs}ms")]
    public static partial void HandlerCriticalThresholdExceeded(
        ILogger logger,
        string handlerName,
        double thresholdMs,
        double durationMs);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "Handler {HandlerName} exceeded slow threshold "
            + "{ThresholdMs}ms with {DurationMs}ms")]
    public static partial void HandlerSlowThresholdExceeded(
        ILogger logger,
        string handlerName,
        double thresholdMs,
        double durationMs);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "Handler {HandlerName} hit a downstream timeout "
            + "({ExceptionType} without our token canceled) after {DurationMs}ms")]
    public static partial void HandlerDownstreamTimeout(
        ILogger logger,
        string handlerName,
        string exceptionType,
        double durationMs);
}
