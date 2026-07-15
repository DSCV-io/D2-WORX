// -----------------------------------------------------------------------
// <copyright file="HttpTokenExchangeClient.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Outbound.TokenExchange;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Outbound.Telemetry;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Singleflight;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Diagnostics;
using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// HTTP-backed implementation of <see cref="ITokenExchangeClient"/>. Calls
/// Edge's OIDC <c>token_endpoint</c> with
/// <c>grant_type=urn:ietf:params:oauth:grant-type:token-exchange</c> (RFC 8693)
/// and caches results in the shared <c>ILocalCache</c> via
/// <see cref="TokenExchangeCache"/>.
/// </summary>
[MustDisposeResource(false)]
internal sealed class HttpTokenExchangeClient : ITokenExchangeClient, IDisposable
{
    /// <summary>
    /// Named <c>HttpClient</c> identifier registered in
    /// <see cref="AuthOutboundServiceCollectionExtensions"/>.
    /// </summary>
    public const string HTTP_CLIENT_NAME = "d2-auth-token-exchange";

    private const string _GRANT_TYPE_TOKEN_EXCHANGE =
        "urn:ietf:params:oauth:grant-type:token-exchange";

    private const string _TOKEN_TYPE_JWT = "urn:ietf:params:oauth:token-type:jwt";

    /// <summary>
    /// Hard upper bound on the JWT-payload-segment length we'll attempt to
    /// base64-decode. Real-world JWT payloads are typically &lt; 2 KB; a
    /// well-formed access-rich JWT might reach a few KB. 16 KB is a generous
    /// ceiling that still defends against a maliciously-crafted oversized
    /// token blowing the stack via an unbounded <c>stackalloc</c>.
    /// </summary>
    private const int _MAX_JWT_PAYLOAD_SEGMENT_LENGTH = 16 * 1024;

    private readonly IHttpClientFactory r_httpClientFactory;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> r_configManager;
    private readonly TokenExchangeCache r_cache;
    private readonly AuthOutboundOptions r_options;
    private readonly ILogger<HttpTokenExchangeClient> r_logger;
    private readonly Singleflight<string, FetchResult> r_singleflight = new();
    private readonly CircuitBreaker<FetchResult> r_circuitBreaker;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpTokenExchangeClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The named-client factory.</param>
    /// <param name="configManager">The OIDC configuration manager.</param>
    /// <param name="cache">The shared token-exchange cache.</param>
    /// <param name="options">Outbound auth options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The time provider (overridable for tests).</param>
    [MustDisposeResource(false)]
    public HttpTokenExchangeClient(
        IHttpClientFactory httpClientFactory,
        IConfigurationManager<OpenIdConnectConfiguration> configManager,
        TokenExchangeCache cache,
        IOptions<AuthOutboundOptions> options,
        ILogger<HttpTokenExchangeClient> logger,
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

        // 5 consecutive transient-failure FetchResults → 30 s open, matching the
        // JWKS-provider breaker config (HttpJwksProvider / JwksProviderOptions
        // defaults: 5 failures, 30 s cooldown). Value-based predicate required
        // because FetchAsync catches exceptions internally and returns
        // FetchResult.TransientFailure() — the breaker would never see an
        // exception without this predicate.
        // NowFunc converts TimeProvider.GetUtcNow() to milliseconds so that tests
        // can advance FakeTimeProvider to drive cooldown deterministically.
        r_circuitBreaker = new CircuitBreaker<FetchResult>(
            isFailure: static r => !r.Success,
            options: new CircuitBreakerOptions(
                failureThreshold: AuthOutboundResilienceDefaults.FAILURE_THRESHOLD,
                cooldownDuration: AuthOutboundResilienceDefaults.SR_CooldownDuration,
                nowFunc: () => clock.GetUtcNow().ToUnixTimeMilliseconds()));
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result<string>> ExchangeAsync(
        string subjectToken,
        string targetAudience,
        IReadOnlySet<string>? narrowedScopes = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);

        if (subjectToken.Falsey() || targetAudience.Falsey())
            return D2Result<string>.ValidationFailed();

        var sessionId = TryExtractSessionId(subjectToken);
        if (sessionId is null)
        {
            // No d2_session_id claim → can't key the cache for backplane
            // invalidation. Hard-fail rather than caching by some fallback
            // key (which would defeat session-revoke purging).
            return D2Result<string>.ValidationFailed();
        }

        var key = r_cache.BuildKey((Guid)sessionId, targetAudience, narrowedScopes);
        if (key is null)
        {
            // Audience too long for a sane cache key —
            // see TokenExchangeCache.MAX_AUDIENCE_LENGTH.
            return D2Result<string>.ValidationFailed();
        }

        // Cache hit fast path.
        var cached = await r_cache.TryGetAsync(key, ct);
        if (cached is not null)
        {
            OutboundTelemetry.SR_TokenExchangeRequests.Add(
                1,
                new KeyValuePair<string, object?>(
                    OutboundTelemetryTags.TokenExchangeRequests.TAG_OUTCOME,
                    OutboundTelemetryTags.TokenExchangeRequests.Outcome.CACHE_HIT));
            return D2Result<string>.Ok(cached);
        }

        // Cache miss → singleflight (outer) wrapping circuit breaker (inner).
        // Concurrent first-callers for the same (sessionId, audience, scope-set)
        // tuple share one fetch via Singleflight; the breaker fast-fails when
        // Edge has been repeatedly unreachable.
        var request = new ExchangeRequest(
            (Guid)sessionId,
            subjectToken,
            targetAudience,
            narrowedScopes,
            key);
        try
        {
            var fetchResult = await r_singleflight.ExecuteAsync(
                key,
                innerCt => r_circuitBreaker.ExecuteAsync(
                    cbCt => FetchAsync(request, cbCt), ct: innerCt),
                ct);
            return fetchResult.Success
                ? D2Result<string>.Ok(fetchResult.Token!)
                : D2Result<string>.ServiceUnavailable();
        }
        catch (CircuitOpenException)
        {
            return D2Result<string>.ServiceUnavailable();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Volatile.Write(ref _disposed, true);
    }

    private static Guid? TryExtractSessionId(string subjectToken)
    {
        // Parse-without-validation — the inbound auth middleware already
        // validated the JWT signature / expiry / audience upstream of us. We
        // just need the d2_session_id claim for cache keying.
        var firstDot = subjectToken.IndexOf('.');
        if (firstDot < 0) return null;
        var secondDot = subjectToken.IndexOf('.', firstDot + 1);
        if (secondDot < 0) return null;

        var payloadLength = secondDot - firstDot - 1;
        if (payloadLength <= 0 || payloadLength > _MAX_JWT_PAYLOAD_SEGMENT_LENGTH)
        {
            // Defensive: reject empty or oversized payload segments before
            // hitting the stackalloc inside Base64UrlDecode. A maliciously-
            // crafted multi-MB "JWT" would otherwise blow the stack.
            return null;
        }

        var payloadSegment = subjectToken.AsSpan(firstDot + 1, payloadLength);
        try
        {
            var payloadBytes = Base64UrlDecode(payloadSegment);
            using var doc = JsonDocument.Parse(payloadBytes);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty(JwtClaimTypes.SESSION_ID, out var sidEl))
                return null;
            if (sidEl.ValueKind != JsonValueKind.String) return null;
            return sidEl.GetString().TryParseTruthyNull(out Guid? g) ? g : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(ReadOnlySpan<char> input)
    {
        // Caller (TryExtractSessionId) has already bounded input.Length to
        // _MAX_JWT_PAYLOAD_SEGMENT_LENGTH (16 KB) — stackalloc here is safe
        // up to that ceiling.
        Span<char> buffer = stackalloc char[input.Length + 4];
        for (var i = 0; i < input.Length; i++)
        {
            buffer[i] = input[i] switch
            {
                '-' => '+',
                '_' => '/',
                _ => input[i],
            };
        }

        var paddingNeeded = (4 - (input.Length % 4)) % 4;
        for (var i = 0; i < paddingNeeded; i++)
            buffer[input.Length + i] = '=';

        return Convert.FromBase64CharArray(buffer.ToArray(), 0, input.Length + paddingNeeded);
    }

    private static string EncodeBasicCredentials(string clientId, string clientSecret) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

    private static (string Token, TimeSpan Ttl) ParseTokenResponse(
        JsonElement root,
        TimeSpan fallbackTtl)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new TokenExchangeException("OAuth response root was not a JSON object.");

        if (!root.TryGetProperty("access_token", out var tokenEl) ||
            tokenEl.ValueKind != JsonValueKind.String)
        {
            throw new TokenExchangeException(
                "OAuth response missing required 'access_token' string.");
        }

        var token = tokenEl.GetString()!;
        if (token.Falsey())
            throw new TokenExchangeException("OAuth response 'access_token' was empty.");

        var ttl = fallbackTtl;
        if (root.TryGetProperty("expires_in", out var expiresEl) &&
            expiresEl.TryGetInt64(out var expiresInSeconds) &&
            expiresInSeconds > 0)
        {
            ttl = TimeSpan.FromSeconds(expiresInSeconds);
        }

        return (token, ttl);
    }

    private async ValueTask<FetchResult> FetchAsync(ExchangeRequest req, CancellationToken ct)
    {
        // Cache re-check: a sibling caller may have populated this key between
        // ExchangeAsync's TryGetAsync and Singleflight's ExecuteAsync entry.
        // Without this, two callers racing on a cold key would both issue HTTP
        // even though the second one could have hit the cache.
        var preFetchCache = await r_cache.TryGetAsync(req.CacheKey, ct);
        if (preFetchCache is not null)
        {
            OutboundTelemetry.SR_TokenExchangeRequests.Add(
                1,
                new KeyValuePair<string, object?>(
                    OutboundTelemetryTags.TokenExchangeRequests.TAG_OUTCOME,
                    OutboundTelemetryTags
                        .TokenExchangeRequests.Outcome.CACHE_HIT_AFTER_SINGLEFLIGHT));
            return FetchResult.Successful(preFetchCache);
        }

        try
        {
            var config = await r_configManager.GetConfigurationAsync(ct);
            var tokenEndpoint = config.TokenEndpoint;
            if (tokenEndpoint.Falsey())
            {
                r_logger.OidcDiscoveryMissingTokenEndpoint(r_options.Issuer);
                OutboundTelemetry.SR_TokenExchangeRequests.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        OutboundTelemetryTags.TokenExchangeRequests.TAG_OUTCOME,
                        OutboundTelemetryTags.TokenExchangeRequests.Outcome.DISCOVERY_FAILURE));
                return FetchResult.TransientFailure();
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                EncodeBasicCredentials(r_options.ClientId, r_options.ClientSecret));

            var formFields = new List<KeyValuePair<string, string>>
            {
                new("grant_type", _GRANT_TYPE_TOKEN_EXCHANGE),
                new("subject_token", req.SubjectToken),
                new("subject_token_type", _TOKEN_TYPE_JWT),
                new("requested_token_type", _TOKEN_TYPE_JWT),
                new("audience", req.TargetAudience),
            };

            if (req.NarrowedScopes is { Count: > 0 } scopes)
                formFields.Add(new("scope", string.Join(" ", scopes)));

            httpRequest.Content = new FormUrlEncodedContent(formFields);

            var http = r_httpClientFactory.CreateClient(HTTP_CLIENT_NAME);
            using var response = await http.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseContentRead,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                r_logger.TokenExchangeHttpFailure((int)response.StatusCode);
                OutboundTelemetry.SR_TokenExchangeRequests.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        OutboundTelemetryTags.TokenExchangeRequests.TAG_OUTCOME,
                        OutboundTelemetryTags.TokenExchangeRequests.Outcome.HTTP_FAILURE));
                return FetchResult.TransientFailure();
            }

            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(body, default, ct);
            var (token, ttl) = ParseTokenResponse(
                doc.RootElement,
                r_options.TokenExchangeCacheFallbackTtl);

            await r_cache.SetAsync(req.SessionId, req.CacheKey, token, ttl, ct);
            OutboundTelemetry.SR_TokenExchangeRequests.Add(
                1,
                new KeyValuePair<string, object?>(
                    OutboundTelemetryTags.TokenExchangeRequests.TAG_OUTCOME,
                    OutboundTelemetryTags.TokenExchangeRequests.Outcome.FETCH_SUCCESS));
            return FetchResult.Successful(token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Sanitize: never log ex itself (its Message could carry sensitive
            // runtime data — request URI, response body fragments). Type FullName
            // + first stack frame are safe (developer-controlled, no user input).
            r_logger.TokenExchangeFetchFailed(
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
            OutboundTelemetry.SR_TokenExchangeRequests.Add(
                1,
                new KeyValuePair<string, object?>(
                    OutboundTelemetryTags.TokenExchangeRequests.TAG_OUTCOME,
                    OutboundTelemetryTags.TokenExchangeRequests.Outcome.FETCH_FAILURE));
            return FetchResult.TransientFailure();
        }
    }

    /// <summary>
    /// Fetch outcome — a success/token pair carried out of <see cref="FetchAsync"/>.
    /// A boolean-plus-nullable-token shape (rather than a thrown exception) lets the
    /// value-based <see cref="CircuitBreaker{T}"/> predicate observe a transient
    /// failure without an exception crossing the breaker boundary.
    /// <para>
    /// <c>internal</c> (not <c>private</c>) so the assembly-level
    /// <see cref="CircuitBreaker{T}"/> generic can be instantiated with this
    /// type from within the same assembly.
    /// </para>
    /// </summary>
    internal readonly record struct FetchResult(bool Success, string? Token)
    {
        /// <summary>Returns a successful fetch outcome carrying the exchanged token.</summary>
        /// <param name="token">The exchanged access token.</param>
        public static FetchResult Successful(string token) => new(true, token);

        /// <summary>Returns a transient-failure fetch outcome (no token).</summary>
        public static FetchResult TransientFailure() => new(false, null);
    }

    /// <summary>
    /// Internal request shape carried into <see cref="FetchAsync"/>. Kept as a
    /// record so the singleflight closure captures one immutable value per
    /// concurrent caller (no shared-mutable-state surprises).
    /// </summary>
    private sealed record ExchangeRequest(
        Guid SessionId,
        string SubjectToken,
        string TargetAudience,
        IReadOnlySet<string>? NarrowedScopes,
        string CacheKey);
}
