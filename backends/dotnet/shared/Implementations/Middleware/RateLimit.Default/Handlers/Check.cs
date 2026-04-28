// -----------------------------------------------------------------------
// <copyright file="Check.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.RateLimit.Default.Handlers;

using System.Globalization;
using D2.Shared.Handler;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.R;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.U;
using D2.Shared.RequestEnrichment.Default;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using H = D2.Shared.RateLimit.Default.Interfaces.IRateLimit.ICheckHandler;
using I = D2.Shared.RateLimit.Default.Interfaces.IRateLimit.CheckInput;
using O = D2.Shared.RateLimit.Default.Interfaces.IRateLimit.CheckOutput;

/// <summary>
/// Handler for checking rate limits using sliding window approximation.
/// </summary>
/// <remarks>
/// Uses two fixed-window counters per dimension with weighted average to approximate
/// a sliding window. Uses distributed cache handlers for storage abstraction.
/// </remarks>
public partial class Check : BaseHandler<Check, I, O>, H
{
    private readonly IRead.IGetTtlHandler r_getTtl;
    private readonly IUpdate.IIncrementHandler r_increment;
    private readonly IUpdate.ISetHandler<string> r_set;
    private readonly RateLimitOptions r_options;

    /// <summary>
    /// Initializes a new instance of the <see cref="Check"/> class.
    /// </summary>
    ///
    /// <param name="getTtl">
    /// The handler for getting TTL of cache keys.
    /// </param>
    /// <param name="increment">
    /// The handler for atomic increment operations.
    /// </param>
    /// <param name="set">
    /// The handler for setting cache values.
    /// </param>
    /// <param name="options">
    /// The rate limit options.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Check(
        IRead.IGetTtlHandler getTtl,
        IUpdate.IIncrementHandler increment,
        IUpdate.ISetHandler<string> set,
        IOptions<RateLimitOptions> options,
        IHandlerContext context)
        : base(context)
    {
        r_getTtl = getTtl;
        r_increment = increment;
        r_set = set;
        r_options = options.Value;
    }

    /// <inheritdoc />
    protected override HandlerOptions DefaultOptions => new(LogInput: false);

    /// <summary>
    /// Checks rate limits across all dimensions.
    /// </summary>
    ///
    /// <param name="input">
    /// The input containing request information.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="D2Result{O}"/> indicating whether the request is blocked.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        var requestContext = input.RequestContext;

        // Trusted services bypass all rate limiting.
        if (requestContext.IsTrustedService == true)
        {
            LogTrustedServiceBypassed(Context.Logger, TraceId);
            return D2Result<O?>.Ok(new O(false, null, null));
        }

        // Build the list of applicable dimension checks, then fire them all concurrently.
        // Each dimension's internal Redis operations (GetTtl + 2× Increment) also run
        // concurrently. This reduces total latency from N×3 sequential Redis round-trips
        // to a single round-trip time (~5-8ms regardless of dimension count).
        var checks = new List<Task<O>>(4);

        // Device fingerprint is always present (combined from client FP + server FP + IP).
        if (requestContext.DeviceFingerprint.Truthy())
        {
            checks.Add(CheckDimensionAsync(
                RateLimitDimension.DeviceFingerprint,
                requestContext.DeviceFingerprint!,
                r_options.DeviceFingerprintThreshold,
                ct));
        }

        if (requestContext.ClientIp.Truthy() &&
            !IpResolver.IsLocalhost(requestContext.ClientIp!))
        {
            checks.Add(CheckDimensionAsync(
                RateLimitDimension.Ip,
                requestContext.ClientIp!,
                r_options.IpThreshold,
                ct));
        }

        if (requestContext.City.Truthy())
        {
            checks.Add(CheckDimensionAsync(
                RateLimitDimension.City,
                requestContext.City!,
                r_options.CityThreshold,
                ct));
        }

        if (requestContext.CountryCode.Truthy() &&
            !r_options.WhitelistedCountryCodes.Contains(requestContext.CountryCode!, StringComparer.OrdinalIgnoreCase))
        {
            checks.Add(CheckDimensionAsync(
                RateLimitDimension.Country,
                requestContext.CountryCode!,
                r_options.CountryThreshold,
                ct));
        }

        var results = await Task.WhenAll(checks);
        var blocked = results.FirstOrDefault(r => r.IsBlocked);
        if (blocked is not null)
        {
            return D2Result<O?>.Ok(blocked);
        }

        return D2Result<O?>.Ok(new O(false, null, null));
    }

    /// <summary>
    /// Gets the window ID for a given timestamp.
    /// </summary>
    ///
    /// <param name="time">
    /// The timestamp.
    /// </param>
    ///
    /// <returns>
    /// A string representing the window ID (minute-granularity UTC timestamp).
    /// </returns>
    private static string GetWindowId(DateTime time)
    {
        // Use minute-granularity for 60-second windows.
        return time.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Logs that rate limiting was bypassed for a trusted service.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Rate limit bypassed for trusted service. TraceId: {TraceId}")]
    private static partial void LogTrustedServiceBypassed(ILogger logger, string? traceId);

    /// <summary>
    /// Logs that a request is blocked on a specific dimension.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Request blocked on {Dimension} dimension. TTL: {TTL}. TraceId: {TraceId}")]
    private static partial void LogRequestBlocked(ILogger logger, RateLimitDimension dimension, TimeSpan ttl, string? traceId);

    /// <summary>
    /// Logs that incrementing the rate limit counter failed.
    /// </summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to increment rate limit counter for {Dimension}. Failing open. TraceId: {TraceId}")]
    private static partial void LogIncrementFailed(ILogger logger, RateLimitDimension dimension, string? traceId);

    /// <summary>
    /// Logs that the rate limit was exceeded on a specific dimension.
    /// </summary>
    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Rate limit exceeded on {Dimension} dimension. Count: {Count}/{Threshold}. TraceId: {TraceId}")]
    private static partial void LogRateLimitExceeded(ILogger logger, RateLimitDimension dimension, long count, int threshold, string? traceId);

    /// <summary>
    /// Logs an error when checking the rate limit for a dimension.
    /// </summary>
    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Error checking rate limit for {Dimension}. Failing open. TraceId: {TraceId}")]
    private static partial void LogCheckError(ILogger logger, Exception ex, RateLimitDimension dimension, string? traceId);

    /// <summary>
    /// Checks a single dimension using sliding window approximation.
    /// </summary>
    ///
    /// <param name="dimension">
    /// The dimension being checked.
    /// </param>
    /// <param name="value">
    /// The value for the dimension (e.g., IP address, fingerprint).
    /// </param>
    /// <param name="threshold">
    /// The maximum allowed requests per window.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// The check output indicating if blocked.
    /// </returns>
    private async Task<O> CheckDimensionAsync(
        RateLimitDimension dimension,
        string value,
        int threshold,
        CancellationToken ct)
    {
        var dimensionKey = dimension.ToString().ToLowerInvariant();
        var blockedKey = $"blocked:{dimensionKey}:{value}";

        try
        {
            // Compute window keys upfront so all Redis operations can fire concurrently.
            var now = DateTime.UtcNow;
            var windowSeconds = (int)r_options.Window.TotalSeconds;
            var currentWindowId = GetWindowId(now);
            var previousWindowId = GetWindowId(now.AddSeconds(-windowSeconds));

            var currentKey = $"ratelimit:{dimensionKey}:{value}:{currentWindowId}";
            var previousKey = $"ratelimit:{dimensionKey}:{value}:{previousWindowId}";

            // Fire all three Redis operations concurrently: blocked check, previous
            // window read, and current window increment. If already blocked, the
            // extra increment is harmless (counter auto-expires via TTL).
            // Convert ValueTasks to Tasks immediately — ValueTask must not be consumed more than once.
            var ttlTask = r_getTtl.HandleAsync(new IRead.GetTtlInput(blockedKey), ct).AsTask();
            var prevTask = r_increment.HandleAsync(
                new IUpdate.IncrementInput(previousKey, 0, r_options.Window * 2), ct).AsTask();
            var incrTask = r_increment.HandleAsync(
                new IUpdate.IncrementInput(currentKey, 1, r_options.Window * 2), ct).AsTask();

            await Task.WhenAll(ttlTask, prevTask, incrTask);

            var ttlResult = await ttlTask;
            var prevResult = await prevTask;
            var incrResult = await incrTask;

            // 1. Check if already blocked.
            if (ttlResult.CheckSuccess(out var ttlOutput) && ttlOutput?.TimeToLive.HasValue == true)
            {
                // Do not log raw `value` — it may be an IP address or fingerprint (PII).
                LogRequestBlocked(Context.Logger, dimension, ttlOutput.TimeToLive.Value, TraceId);

                return new O(true, dimension, ttlOutput.TimeToLive.Value);
            }

            // 2. Evaluate increment results.
            if (!incrResult.CheckSuccess(out var incrOutput))
            {
                // Fail-open on cache errors. Do not log raw `value` — may be PII.
                LogIncrementFailed(Context.Logger, dimension, TraceId);

                return new O(false, null, null);
            }

            var prevCount = prevResult.CheckSuccess(out var prevOutput) ? prevOutput?.NewValue ?? 0 : 0;
            var currCount = incrOutput?.NewValue ?? 0;

            // 3. Calculate weighted estimate.
            var secondsIntoCurrentWindow = now.TimeOfDay.TotalSeconds % windowSeconds;
            var weight = 1.0 - (secondsIntoCurrentWindow / windowSeconds);
            var estimated = (long)((prevCount * weight) + currCount);

            // 4. Check if over threshold.
            if (estimated > threshold)
            {
                // Block the dimension.
                await r_set.HandleAsync(
                    new IUpdate.SetInput<string>(blockedKey, "1", r_options.BlockDuration),
                    ct);

                // Do not log raw `value` — may be an IP address or fingerprint (PII).
                LogRateLimitExceeded(Context.Logger, dimension, estimated, threshold, TraceId);

                return new O(true, dimension, r_options.BlockDuration);
            }

            return new O(false, null, null);
        }
        catch (Exception ex)
        {
            // Fail-open on cache errors. Do not log raw `value` — may be PII.
            LogCheckError(Context.Logger, ex, dimension, TraceId);

            return new O(false, null, null);
        }
    }
}
