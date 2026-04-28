// -----------------------------------------------------------------------
// <copyright file="IdempotencyMiddleware.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Idempotency.Default;

using System.Net;
using System.Text.Json;
using D2.Shared.I18n;
using D2.Shared.Idempotency.Default.Interfaces;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.D;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.U;
using D2.Shared.RequestEnrichment.Default;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using D2.Shared.Utilities.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Middleware that enforces idempotency for mutation requests using an Idempotency-Key header.
/// </summary>
/// <remarks>
/// Uses a SET NX + GET pattern with distributed cache to detect duplicate requests.
/// Replays cached responses for previously seen keys and returns 409 for in-flight duplicates.
/// </remarks>
public partial class IdempotencyMiddleware
{
    private const string _IDEMPOTENCY_KEY_HEADER = "Idempotency-Key";
    private const string _KEY_PREFIX = "idempotency:";

    private readonly RequestDelegate r_next;
    private readonly ILogger<IdempotencyMiddleware> r_logger;
    private readonly IdempotencyOptions r_options;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyMiddleware"/> class.
    /// </summary>
    ///
    /// <param name="next">
    /// The next middleware in the pipeline.
    /// </param>
    /// <param name="options">
    /// The idempotency options.
    /// </param>
    /// <param name="logger">
    /// The logger instance.
    /// </param>
    public IdempotencyMiddleware(
        RequestDelegate next,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyMiddleware> logger)
    {
        r_next = next;
        r_options = options.Value;
        r_logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    ///
    /// <param name="context">
    /// The HTTP context.
    /// </param>
    /// <param name="checkHandler">
    /// The idempotency check handler (injected per-request).
    /// </param>
    /// <param name="setHandler">
    /// The distributed cache set handler (injected per-request).
    /// </param>
    /// <param name="removeHandler">
    /// The distributed cache remove handler (injected per-request).
    /// </param>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    public async Task InvokeAsync(
        HttpContext context,
        IIdempotency.ICheckHandler checkHandler,
        IUpdate.ISetHandler<string> setHandler,
        IDelete.IRemoveHandler removeHandler)
    {
        // 0. Skip idempotency for infrastructure endpoints (health checks, metrics).
        if (InfrastructurePaths.IsInfrastructure(context))
        {
            await r_next(context);
            return;
        }

        // 1. Skip non-mutation methods.
        if (!r_options.ApplicableMethods.Contains(context.Request.Method))
        {
            await r_next(context);
            return;
        }

        // 2. Skip if no Idempotency-Key header.
        if (!context.Request.Headers.TryGetValue(_IDEMPOTENCY_KEY_HEADER, out var headerValue) ||
            ((string?)headerValue).Falsey())
        {
            await r_next(context);
            return;
        }

        var idempotencyKey = headerValue.ToString().Trim();

        // 3. Validate key is UUID format.
        if (!Guid.TryParse(idempotencyKey, out _))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var badRequestResponse = D2Result.ValidationFailed(
                inputErrors: [[_IDEMPOTENCY_KEY_HEADER, TK.Common.Errors.VALIDATION_FAILED]],
                traceId: context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(badRequestResponse, SerializerOptions.SR_Web, context.RequestAborted);
            return;
        }

        // 3b. Scope key to authenticated user to prevent cross-user collisions.
        // Auth runs before idempotency in the pipeline, so User.Identity is available.
        var userId = context.User.FindFirst("sub")?.Value;
        if (userId is not null)
        {
            idempotencyKey = $"{userId}:{idempotencyKey}";
        }

        // 4. Check idempotency state.
        IIdempotency.CheckOutput? checkOutput = null;
        try
        {
            var checkResult = await checkHandler.HandleAsync(
                new IIdempotency.CheckInput(idempotencyKey),
                context.RequestAborted);

            if (checkResult.CheckSuccess(out checkOutput))
            {
                // Handled below.
            }
        }
        catch (Exception ex)
        {
            // Fail-open: log warning and proceed.
            LogCheckFailed(r_logger, ex, context.TraceIdentifier);
        }

        // If check failed entirely, proceed (fail-open).
        if (checkOutput is null)
        {
            await r_next(context);
            return;
        }

        // 5. Handle in-flight — return 409 Conflict.
        if (checkOutput.State == IdempotencyState.InFlight)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";

            var conflictResponse = D2Result.Fail(
                [TK.Common.Errors.CONFLICT],
                HttpStatusCode.Conflict,
                inputErrors: null,
                ErrorCodes.IDEMPOTENCY_IN_FLIGHT,
                context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(conflictResponse, SerializerOptions.SR_Web, context.RequestAborted);
            return;
        }

        // 6. Handle cached — replay response.
        if (checkOutput.State == IdempotencyState.Cached && checkOutput.CachedResponse is not null)
        {
            var cached = checkOutput.CachedResponse;
            context.Response.StatusCode = cached.StatusCode;

            if (cached.ContentType is not null)
            {
                context.Response.ContentType = cached.ContentType;
            }

            if (cached.Body is not null)
            {
                await context.Response.WriteAsync(cached.Body, context.RequestAborted);
            }

            return;
        }

        // 7. State is Acquired — execute the request with response capture.
        var cacheKey = $"{_KEY_PREFIX}{idempotencyKey}";
        var originalBodyStream = context.Response.Body;

        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await r_next(context);
        }
        finally
        {
            // Always restore the original stream.
            context.Response.Body = originalBodyStream;
        }

        // 8. Read captured response.
        memoryStream.Seek(0, SeekOrigin.Begin);
        var bodyBytes = memoryStream.ToArray();

        // 9. Write captured response to original stream.
        if (bodyBytes.Length > 0)
        {
            await originalBodyStream.WriteAsync(bodyBytes, context.RequestAborted);
        }

        // 10. Cache or cleanup.
        var isSuccess = context.Response.StatusCode is >= 200 and < 300;
        var shouldCache = (isSuccess || r_options.CacheErrorResponses) &&
                          bodyBytes.Length <= r_options.MaxBodySizeBytes;

        if (shouldCache)
        {
            try
            {
                var bodyString = bodyBytes.Length > 0
                    ? System.Text.Encoding.UTF8.GetString(bodyBytes)
                    : null;

                var cachedResponse = new CachedResponse(
                    context.Response.StatusCode,
                    bodyString,
                    context.Response.ContentType);

                var serialized = JsonSerializer.Serialize(cachedResponse);

                await setHandler.HandleAsync(
                    new IUpdate.SetInput<string>(cacheKey, serialized, r_options.CacheTtl),
                    context.RequestAborted);
            }
            catch (Exception ex)
            {
                // Response already sent — just log.
                LogCacheFailed(r_logger, ex, context.TraceIdentifier);
            }
        }
        else
        {
            // Non-cacheable response — remove sentinel so client can retry.
            try
            {
                await removeHandler.HandleAsync(
                    new IDelete.RemoveInput(cacheKey),
                    context.RequestAborted);
            }
            catch (Exception ex)
            {
                LogSentinelRemovalFailed(r_logger, ex, context.TraceIdentifier);
            }

            if (!isSuccess)
            {
                LogNonSuccessNotCached(r_logger, context.Response.StatusCode, context.TraceIdentifier);
            }
            else
            {
                LogBodyTooLarge(r_logger, bodyBytes.Length, r_options.MaxBodySizeBytes, context.TraceIdentifier);
            }
        }
    }

    /// <summary>
    /// Logs that the idempotency check failed and the request is being allowed through (fail-open).
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Idempotency check failed. Allowing request through (fail-open). TraceId: {TraceId}")]
    private static partial void LogCheckFailed(ILogger logger, Exception ex, string? traceId);

    /// <summary>
    /// Logs that caching the idempotent response failed.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Failed to cache idempotent response. TraceId: {TraceId}")]
    private static partial void LogCacheFailed(ILogger logger, Exception ex, string? traceId);

    /// <summary>
    /// Logs that removing the idempotency sentinel failed.
    /// </summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to remove idempotency sentinel. TraceId: {TraceId}")]
    private static partial void LogSentinelRemovalFailed(ILogger logger, Exception ex, string? traceId);

    /// <summary>
    /// Logs that a non-success response was not cached and the sentinel was removed.
    /// </summary>
    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Non-success response ({StatusCode}) not cached for idempotency key. Sentinel removed. TraceId: {TraceId}")]
    private static partial void LogNonSuccessNotCached(ILogger logger, int statusCode, string? traceId);

    /// <summary>
    /// Logs that the response body exceeds the maximum size and was not cached.
    /// </summary>
    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Response body ({BodySize} bytes) exceeds MaxBodySizeBytes ({MaxSize}). Not cached. TraceId: {TraceId}")]
    private static partial void LogBodyTooLarge(ILogger logger, int bodySize, int maxSize, string? traceId);
}
