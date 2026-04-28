// -----------------------------------------------------------------------
// <copyright file="ApiKeyInterceptor.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Geo.API.Interceptors;

using System.Security.Cryptography;
using System.Text;
using D2.Geo.App;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Utilities.Extensions;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;

/// <summary>
/// gRPC server interceptor that validates API keys from incoming requests.
/// Reads the <c>x-api-key</c> metadata header and validates it against
/// <see cref="GeoAppOptions.ApiKeyMappings"/>.
///
/// Behavior is driven by <see cref="RequiresApiKeyAttribute"/> on service methods:
/// <list type="bullet">
///   <item>No attribute → pass through (no API key check).</item>
///   <item><c>[RequiresApiKey]</c> → validate API key only.</item>
///   <item><c>[RequiresApiKey(ValidateContextKeys = true)]</c> → validate API key
///         AND verify request context keys are in the caller's allowed set.</item>
/// </list>
/// </summary>
public partial class ApiKeyInterceptor : Interceptor
{
    private readonly ILogger<ApiKeyInterceptor> r_logger;
    private readonly (byte[] KeyBytes, List<string> AllowedContextKeys)[] r_validKeys;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyInterceptor"/> class.
    /// </summary>
    /// <param name="options">The Geo app options containing API key mappings.</param>
    /// <param name="logger">The logger.</param>
    public ApiKeyInterceptor(
        IOptions<GeoAppOptions> options,
        ILogger<ApiKeyInterceptor> logger)
    {
        r_logger = logger;

        // Pre-compute key bytes for constant-time comparison.
        r_validKeys = options.Value.ApiKeyMappings
            .Select(kvp => (Encoding.UTF8.GetBytes(kvp.Key), kvp.Value))
            .ToArray();
    }

    /// <inheritdoc/>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        // Read the RequiresApiKey attribute from endpoint metadata.
        var httpContext = context.GetHttpContext();
        var endpoint = httpContext.GetEndpoint();
        var attr = endpoint?.Metadata.GetMetadata<RequiresApiKeyAttribute>();

        // No attribute → pass through without API key checks.
        if (attr is null)
        {
            return await continuation(request, context);
        }

        var methodName = GetMethodName(context.Method);

        // Fail closed: if API key mappings are not configured, reject protected RPCs.
        if (r_validKeys.Length == 0)
        {
            LogApiKeyMappingsNotConfigured(r_logger, methodName);
            throw new RpcException(
                new Status(
                    StatusCode.Unauthenticated,
                    "API key authentication is not configured."));
        }

        // Extract API key from metadata.
        var apiKey = context.RequestHeaders.GetValue("x-api-key");
        if (apiKey.Falsey())
        {
            LogMissingApiKeyHeader(r_logger, methodName);
            throw new RpcException(
                new Status(StatusCode.Unauthenticated, "Missing x-api-key header."));
        }

        // Timing-safe key validation: compare against ALL keys using
        // FixedTimeEquals to prevent timing side-channel attacks.
        var apiKeyBytes = Encoding.UTF8.GetBytes(apiKey!);
        List<string>? allowedContextKeys = null;
        foreach (var (keyBytes, contextKeys) in r_validKeys)
        {
            if (CryptographicOperations.FixedTimeEquals(apiKeyBytes, keyBytes))
            {
                allowedContextKeys = contextKeys;
            }

            // Continue loop — always compare ALL keys to prevent timing leaks.
        }

        if (allowedContextKeys is null)
        {
            LogInvalidApiKey(r_logger, methodName);
            throw new RpcException(
                new Status(StatusCode.Unauthenticated, "Invalid API key."));
        }

        // If the attribute does not require context key validation, we're done.
        if (!attr.ValidateContextKeys)
        {
            return await continuation(request, context);
        }

        // Extract and validate context keys from the request.
        var requestContextKeys = ExtractContextKeys(request);
        foreach (var contextKey in requestContextKeys)
        {
            if (!allowedContextKeys.Contains(contextKey))
            {
                LogUnauthorizedContextKey(r_logger, contextKey, methodName);
                throw new RpcException(
                    new Status(
                        StatusCode.PermissionDenied,
                        $"Not authorized for context key \"{contextKey}\"."));
            }
        }

        return await continuation(request, context);
    }

    /// <summary>
    /// Extracts the short method name from the full gRPC method path.
    /// </summary>
    private static string GetMethodName(string fullMethod)
    {
        // fullMethod is like "/d2.geo.v1.GeoService/CreateContacts"
        var lastSlash = fullMethod.LastIndexOf('/');
        return lastSlash >= 0 ? fullMethod[(lastSlash + 1)..] : fullMethod;
    }

    /// <summary>
    /// Extracts context keys from the request message by pattern-matching
    /// on the known proto request types.
    /// </summary>
    private static HashSet<string> ExtractContextKeys<TRequest>(TRequest request)
        where TRequest : class
    {
        var keys = new HashSet<string>();

        if (request is CreateContactsRequest createReq)
        {
            foreach (var contact in createReq.ContactsToCreate)
            {
                keys.Add(contact.ContextKey);
            }
        }
        else if (request is GetContactsByExtKeysRequest getExtReq)
        {
            foreach (var key in getExtReq.Keys)
            {
                keys.Add(key.ContextKey);
            }
        }
        else if (request is DeleteContactsByExtKeysRequest deleteExtReq)
        {
            foreach (var key in deleteExtReq.Keys)
            {
                keys.Add(key.ContextKey);
            }
        }
        else if (request is UpdateContactsByExtKeysRequest updateExtReq)
        {
            foreach (var contact in updateExtReq.Contacts)
            {
                keys.Add(contact.ContextKey);
            }
        }

        return keys;
    }

    /// <summary>
    /// Logs an error when API key mappings are not configured but a protected RPC is called.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "API key mappings are not configured but RPC {Method} requires an API key")]
    private static partial void LogApiKeyMappingsNotConfigured(ILogger logger, string method);

    /// <summary>
    /// Logs a warning when a request is missing the x-api-key header.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Missing x-api-key header on RPC {Method}")]
    private static partial void LogMissingApiKeyHeader(ILogger logger, string method);

    /// <summary>
    /// Logs a warning when an invalid API key is provided.
    /// </summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Invalid API key on RPC {Method}")]
    private static partial void LogInvalidApiKey(ILogger logger, string method);

    /// <summary>
    /// Logs a warning when an API key is not authorized for a specific context key.
    /// </summary>
    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "API key not authorized for context key \"{ContextKey}\" on {Method}")]
    private static partial void LogUnauthorizedContextKey(ILogger logger, string contextKey, string method);
}
