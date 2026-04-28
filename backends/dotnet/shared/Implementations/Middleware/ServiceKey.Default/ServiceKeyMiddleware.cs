// -----------------------------------------------------------------------
// <copyright file="ServiceKeyMiddleware.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceKey.Default;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.RequestEnrichment.Default;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Middleware that identifies trusted service-to-service requests by validating
/// the <c>X-Api-Key</c> header. Runs before rate limiting so trusted services
/// bypass rate limits and fingerprint checks.
/// </summary>
/// <remarks>
/// <para>Must run AFTER <see cref="RequestEnrichmentMiddleware"/> (needs <see cref="IRequestContext"/> on features).</para>
/// <para>Must run BEFORE rate limiting middleware.</para>
/// <para>
/// Behavior:
/// <list type="bullet">
///   <item>No <c>X-Api-Key</c> header → pass through (browser request).</item>
///   <item>Valid key → set <see cref="MutableRequestContext.IsTrustedService"/> to true, continue.</item>
///   <item>Invalid key → 401 immediately (fail fast, before rate limiting).</item>
/// </list>
/// </para>
/// </remarks>
public partial class ServiceKeyMiddleware
{
    private readonly RequestDelegate r_next;
    private readonly ILogger<ServiceKeyMiddleware> r_logger;
    private readonly byte[][] r_validKeyBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceKeyMiddleware"/> class.
    /// </summary>
    ///
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">The service key configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public ServiceKeyMiddleware(
        RequestDelegate next,
        IOptions<ServiceKeyOptions> options,
        ILogger<ServiceKeyMiddleware> logger)
    {
        r_next = next;
        r_logger = logger;
        r_validKeyBytes = options.Value.ValidKeys
            .Select(k => Encoding.UTF8.GetBytes(k))
            .ToArray();
    }

    /// <summary>
    /// Validates the <c>X-Api-Key</c> header and sets the trust flag on <see cref="MutableRequestContext"/>.
    /// </summary>
    ///
    /// <param name="context">The HTTP context.</param>
    ///
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (InfrastructurePaths.IsInfrastructure(context))
        {
            await r_next(context);
            return;
        }

        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();

        // No key → browser request, continue normally.
        if (apiKey.Falsey())
        {
            // Explicitly mark as not-trusted (transitions from null → false).
            if (context.Features.Get<IRequestContext>() is MutableRequestContext noKeyCtx)
            {
                noKeyCtx.IsTrustedService = false;
            }

            await r_next(context);
            return;
        }

        // Timing-safe key validation: compare against ALL keys using
        // FixedTimeEquals to prevent timing side-channel attacks.
        var apiKeyBytes = Encoding.UTF8.GetBytes(apiKey!);
        var matched = false;
        foreach (var validKeyBytes in r_validKeyBytes)
        {
            if (CryptographicOperations.FixedTimeEquals(apiKeyBytes, validKeyBytes))
            {
                matched = true;
            }

            // Continue loop — always compare ALL keys to prevent timing leaks.
        }

        // Invalid key → 401 immediately (fail fast).
        if (!matched)
        {
            LogInvalidServiceKey(r_logger);

            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                D2Result.Fail(
                    [TK.Common.Errors.UNAUTHORIZED],
                    HttpStatusCode.Unauthorized,
                    errorCode: "INVALID_SERVICE_KEY",
                    traceId: context.TraceIdentifier),
                context.RequestAborted);
            return;
        }

        // Valid key → mark as trusted.
        if (context.Features.Get<IRequestContext>() is MutableRequestContext mutableCtx)
        {
            mutableCtx.IsTrustedService = true;
        }

        LogServiceKeyAuthenticated(r_logger);
        await r_next(context);
    }

    /// <summary>
    /// Logs that an invalid service API key was presented.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Invalid service API key presented")]
    private static partial void LogInvalidServiceKey(ILogger logger);

    /// <summary>
    /// Logs that a request was authenticated via a service key.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Request authenticated via service key")]
    private static partial void LogServiceKeyAuthenticated(ILogger logger);
}
