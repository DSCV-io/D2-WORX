// -----------------------------------------------------------------------
// <copyright file="HttpServiceIdentityClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.ServiceIdentity;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using D2.Shared.Auth.Outbound.Telemetry;
using D2.Shared.Resilience.Singleflight;
using D2.Shared.Result;
using D2.Shared.Utilities.Diagnostics;
using D2.Shared.Utilities.Extensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// HTTP-backed implementation of <see cref="IServiceIdentityClient"/>. Calls
/// Edge's OIDC <c>token_endpoint</c> with <c>grant_type=client_credentials</c>
/// and HTTP Basic auth (<c>client_id</c> + <c>client_secret</c>), caches the
/// returned JWT in-memory until it nears expiry, and refreshes via the
/// background <see cref="ServiceIdentityRefreshHostedService"/>.
/// </summary>
[MustDisposeResource(false)]
internal sealed class HttpServiceIdentityClient : IServiceIdentityClient, IDisposable
{
    /// <summary>
    /// Named <c>HttpClient</c> identifier registered in
    /// <see cref="AuthOutboundServiceCollectionExtensions"/>. Tests + the
    /// resilience pipeline both grab the client by this name.
    /// </summary>
    public const string HTTP_CLIENT_NAME = "d2-auth-service-identity";

    // Singleflight key — there is exactly one "fetch a service-identity token"
    // operation per process, so the key is a constant. Multiple concurrent
    // callers (on-demand + the refresh hosted service) all dedup to one HTTP
    // call.
    private const string _SINGLEFLIGHT_KEY = "service-identity";

    private const string _GRANT_TYPE_CLIENT_CREDENTIALS = "client_credentials";

    private readonly IHttpClientFactory r_httpClientFactory;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> r_configManager;
    private readonly ServiceIdentityCache r_cache;
    private readonly AuthOutboundOptions r_options;
    private readonly ILogger<HttpServiceIdentityClient> r_logger;
    private readonly TimeProvider r_clock;
    private readonly Singleflight<string, FetchResult> r_singleflight = new();
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="HttpServiceIdentityClient"/> class.</summary>
    /// <param name="httpClientFactory">The named-client factory.</param>
    /// <param name="configManager">The OIDC configuration manager (handles discovery doc fetch + cache).</param>
    /// <param name="cache">The shared per-process token cache.</param>
    /// <param name="options">Outbound auth options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The time provider (overridable for tests).</param>
    [MustDisposeResource(false)]
    public HttpServiceIdentityClient(
        IHttpClientFactory httpClientFactory,
        IConfigurationManager<OpenIdConnectConfiguration> configManager,
        ServiceIdentityCache cache,
        IOptions<AuthOutboundOptions> options,
        ILogger<HttpServiceIdentityClient> logger,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(configManager);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        r_httpClientFactory = httpClientFactory;
        r_configManager = configManager;
        r_cache = cache;
        r_options = options.Value;
        r_logger = logger;
        r_clock = clock;
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result<string>> GetCurrentTokenAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var now = r_clock.GetUtcNow();
        var cached = r_cache.TryGet(now);
        if (cached is not null)
        {
            OutboundTelemetry.ServiceIdentityFetches.Add(
                1,
                new KeyValuePair<string, object?>(
                    OutboundTelemetryTags.ServiceIdentityFetches.TAG_OUTCOME,
                    OutboundTelemetryTags.ServiceIdentityFetches.Outcome.CACHE_HIT));
            return D2Result<string>.Ok(cached.Token);
        }

        // Cache miss / expired → fetch via singleflight.
        var fetchResult = await r_singleflight.ExecuteAsync(_SINGLEFLIGHT_KEY, FetchAsync, ct);
        return fetchResult.Success
            ? D2Result<string>.Ok(fetchResult.Snapshot!.Token)
            : D2Result<string>.ServiceUnavailable();
    }

    /// <summary>
    /// Forces a refresh of the cached token. Called by
    /// <see cref="ServiceIdentityRefreshHostedService"/> on the proactive
    /// refresh schedule. Goes through the same singleflight as on-demand
    /// callers — a forced refresh + a concurrent on-demand fetch dedup to
    /// one HTTP call. If the cache was populated by a sibling refresh
    /// while this call was waiting on the singleflight, the cache hit
    /// short-circuits the HTTP fetch (handled inside <see cref="FetchAsync"/>).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="D2Result"/> describing the refresh outcome.</returns>
    public async ValueTask<D2Result> ForceRefreshAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fetchResult = await r_singleflight.ExecuteAsync(_SINGLEFLIGHT_KEY, FetchAsync, ct);
        return fetchResult.Success
            ? D2Result.Ok()
            : D2Result.ServiceUnavailable();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
    }

    private static string EncodeBasicCredentials(string clientId, string clientSecret) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

    private static ServiceIdentitySnapshot ParseTokenResponse(JsonElement root, DateTimeOffset now)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new ServiceIdentityException("OAuth response root was not a JSON object.");

        if (!root.TryGetProperty("access_token", out var tokenEl) ||
            tokenEl.ValueKind != JsonValueKind.String)
        {
            throw new ServiceIdentityException(
                "OAuth response missing required 'access_token' string.");
        }

        var token = tokenEl.GetString()!;
        if (token.Falsey())
            throw new ServiceIdentityException("OAuth response 'access_token' was empty.");

        if (!root.TryGetProperty("expires_in", out var expiresEl) ||
            !expiresEl.TryGetInt64(out var expiresInSeconds) ||
            expiresInSeconds <= 0)
        {
            throw new ServiceIdentityException(
                "OAuth response missing or invalid 'expires_in' value.");
        }

        return new ServiceIdentitySnapshot(
            Token: token,
            ExpiresAt: now.AddSeconds(expiresInSeconds));
    }

    private async ValueTask<FetchResult> FetchAsync(CancellationToken ct)
    {
        // Cache re-check: a sibling caller may have populated the cache between
        // GetCurrentTokenAsync's TryGet and Singleflight's ExecuteAsync entry.
        // Without this re-check, we'd issue a redundant HTTP call right after
        // a peer just refreshed.
        var preFetchCache = r_cache.TryGet(r_clock.GetUtcNow());
        if (preFetchCache is not null)
        {
            OutboundTelemetry.ServiceIdentityFetches.Add(
                1,
                new KeyValuePair<string, object?>(
                    OutboundTelemetryTags.ServiceIdentityFetches.TAG_OUTCOME,
                    OutboundTelemetryTags
                        .ServiceIdentityFetches.Outcome.CACHE_HIT_AFTER_SINGLEFLIGHT));
            return FetchResult.Successful(preFetchCache);
        }

        // Note: ct here is CancellationToken.None per Singleflight's contract —
        // one caller bailing must not cancel the shared fetch. We still pass
        // it through so the timeout from the named HttpClient registration
        // gets honored at the transport layer.
        try
        {
            var config = await r_configManager.GetConfigurationAsync(ct);
            var tokenEndpoint = config.TokenEndpoint;
            if (tokenEndpoint.Falsey())
            {
                r_logger.OidcDiscoveryMissingTokenEndpoint(r_options.Issuer);
                OutboundTelemetry.ServiceIdentityFetches.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        OutboundTelemetryTags.ServiceIdentityFetches.TAG_OUTCOME,
                        OutboundTelemetryTags.ServiceIdentityFetches.Outcome.DISCOVERY_FAILURE));
                return FetchResult.TransientFailure();
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                EncodeBasicCredentials(r_options.ClientId, r_options.ClientSecret));
            request.Content = new FormUrlEncodedContent(
            [
                new("grant_type", _GRANT_TYPE_CLIENT_CREDENTIALS)
            ]);

            var http = r_httpClientFactory.CreateClient(HTTP_CLIENT_NAME);
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                r_logger.ServiceIdentityHttpFailure((int)response.StatusCode);
                OutboundTelemetry.ServiceIdentityFetches.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        OutboundTelemetryTags.ServiceIdentityFetches.TAG_OUTCOME,
                        OutboundTelemetryTags.ServiceIdentityFetches.Outcome.HTTP_FAILURE));
                return FetchResult.TransientFailure();
            }

            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(body, default, ct);
            var snapshot = ParseTokenResponse(doc.RootElement, r_clock.GetUtcNow());
            r_cache.Set(snapshot);
            OutboundTelemetry.ServiceIdentityFetches.Add(
                1,
                new KeyValuePair<string, object?>(
                    OutboundTelemetryTags.ServiceIdentityFetches.TAG_OUTCOME,
                    OutboundTelemetryTags.ServiceIdentityFetches.Outcome.FETCH_SUCCESS));
            return FetchResult.Successful(snapshot);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Catch broad: HttpRequestException, JsonException,
            // ServiceIdentityException, TaskCanceledException (HttpClient
            // timeout), etc. Any of these = transient/Edge unreachable.
            // Sanitize: never log ex itself (its Message could carry sensitive
            // runtime data). Type FullName + first stack frame are safe.
            r_logger.ServiceIdentityFetchFailed(
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
            OutboundTelemetry.ServiceIdentityFetches.Add(
                1,
                new KeyValuePair<string, object?>(
                    OutboundTelemetryTags.ServiceIdentityFetches.TAG_OUTCOME,
                    OutboundTelemetryTags.ServiceIdentityFetches.Outcome.FETCH_FAILURE));
            return FetchResult.TransientFailure();
        }
    }

    /// <summary>
    /// Internal fetch outcome. Carries the <see cref="ServiceIdentitySnapshot"/>
    /// on success; <c>null</c> on transient failure (Edge unreachable, OIDC
    /// discovery failure, malformed response). Callers branch on
    /// <see cref="Success"/> to decide whether to fall through to a cached
    /// token or surface a hard failure.
    /// </summary>
    private readonly record struct FetchResult(bool Success, ServiceIdentitySnapshot? Snapshot)
    {
        public static FetchResult Successful(ServiceIdentitySnapshot snapshot) =>
            new(true, snapshot);

        public static FetchResult TransientFailure() =>
            new(false, null);
    }
}
