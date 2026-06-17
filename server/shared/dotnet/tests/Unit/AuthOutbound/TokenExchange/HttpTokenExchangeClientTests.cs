// -----------------------------------------------------------------------
// <copyright file="HttpTokenExchangeClientTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.TokenExchange;

using System.Net;
using System.Text;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.TokenExchange;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.AuthOutbound.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="HttpTokenExchangeClient"/> — covers
/// input-validation surface (especially the JWT-payload-no-validation
/// session-id extraction), cache fast-path, fail-fast on Edge unreachable,
/// Singleflight dedup, and the OAuth Token Exchange request shape.
/// </summary>
public sealed class HttpTokenExchangeClientTests
{
    private const string TOKEN_ENDPOINT = "https://edge.internal/oauth/token";
    private const string TARGET_AUDIENCE = "https://files.internal";

    // ----------------------------------------------------------------------
    // Input validation (subjectToken / targetAudience / sessionId extraction)
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExchangeAsync_FalseySubjectToken_ReturnsValidationFailed(string? subject)
    {
        await using var harness = new Harness();

        var result = await harness.Client.ExchangeAsync(subject!, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        harness.Handler.RequestCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExchangeAsync_FalseyTargetAudience_ReturnsValidationFailed(string? aud)
    {
        await using var harness = new Harness();
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var result = await harness.Client.ExchangeAsync(subject, aud!);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        harness.Handler.RequestCount.Should().Be(0);
    }

    [Theory]
    [InlineData("not-a-jwt")] // no dots
    [InlineData("only-one-dot.payload")] // one dot
    [InlineData("....")] // all delimiters
    [InlineData("header.!!!not-base64!!!.signature")] // payload not valid base64
    [InlineData("header.dGVzdA.sig")] // payload base64-decodes but isn't JSON
    [InlineData("header.eyJzdWIiOiJ1c2VyIn0.sig")] // payload is JSON but no d2_session_id
    public async Task ExchangeAsync_MalformedSubjectToken_ReturnsValidationFailed(string subject)
    {
        await using var harness = new Harness();

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        harness.Handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ExchangeAsync_SessionIdNotAGuid_ReturnsValidationFailed()
    {
        await using var harness = new Harness();
        var subject = TestJwt.Build(new() { ["d2_session_id"] = "not-a-guid" });

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
    }

    [Fact]
    public async Task ExchangeAsync_SessionIdNotAString_ReturnsValidationFailed()
    {
        await using var harness = new Harness();
        var subject = TestJwt.Build(new() { ["d2_session_id"] = 12345 });

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExchangeAsync_PayloadIsJsonArrayNotObject_ReturnsValidationFailed()
    {
        await using var harness = new Harness();
        var header = Base64UrlEncode("""{"alg":"none"}"""u8.ToArray());
        var payload = Base64UrlEncode("""[1,2,3]"""u8.ToArray());
        var subject = $"{header}.{payload}.sig";

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Happy path + request shape
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_HappyPath_ReturnsTokenFromResponse()
    {
        await using var harness = new Harness();
        harness.QueueOk("exchanged-jwt", expiresInSeconds: 300);
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeTrue();
        result.Data.Should().Be("exchanged-jwt");
    }

    [Fact]
    public async Task ExchangeAsync_PostsTokenExchangeGrant()
    {
        await using var harness = new Harness();
        var capturedBody = await CaptureBody(harness, () => OkResponse("exchanged-jwt", 300));
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        capturedBody.Value.Should().Contain(
            "grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Atoken-exchange");
        capturedBody.Value.Should().Contain(
            "subject_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Ajwt");
        capturedBody.Value.Should().Contain(
            "requested_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Ajwt");
        capturedBody.Value.Should().Contain("audience=https%3A%2F%2Ffiles.internal");
    }

    [Fact]
    public async Task ExchangeAsync_NarrowedScopes_IncludesScopeFormField()
    {
        await using var harness = new Harness();
        var capturedBody = await CaptureBody(harness, () => OkResponse("exchanged-jwt", 300));
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        await harness.Client.ExchangeAsync(
            subject,
            TARGET_AUDIENCE,
            new HashSet<string> { "self.read", "self.write" });

        // Scopes are SP-joined (RFC 6749 §3.3) — order is not pinned, only presence.
        capturedBody.Value.Should().Contain("scope=");
        capturedBody.Value.Should().Contain("self.read");
        capturedBody.Value.Should().Contain("self.write");
    }

    [Fact]
    public async Task ExchangeAsync_EmptyScopeSet_OmitsScopeFormField()
    {
        await using var harness = new Harness();
        var capturedBody = await CaptureBody(harness, () => OkResponse("exchanged-jwt", 300));
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        await harness.Client.ExchangeAsync(
            subject,
            TARGET_AUDIENCE,
            new HashSet<string>());

        capturedBody.Value.Should().NotContain("scope=");
    }

    // ----------------------------------------------------------------------
    // Cache fast path
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_SecondCallSameKey_ServesFromCache()
    {
        await using var harness = new Harness();
        harness.QueueOk("exchanged-jwt", 300);
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var first = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);
        var second = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        first.Data.Should().Be("exchanged-jwt");
        second.Data.Should().Be("exchanged-jwt");
        harness.Handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task ExchangeAsync_DifferentAudienceSameSession_BypassesCache()
    {
        await using var harness = new Harness();
        harness.QueueOk("for-files", 300);
        harness.QueueOk("for-notifications", 300);
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var r1 = await harness.Client.ExchangeAsync(subject, "https://files.internal");
        var r2 = await harness.Client.ExchangeAsync(subject, "https://notifications.internal");

        r1.Data.Should().Be("for-files");
        r2.Data.Should().Be("for-notifications");
        harness.Handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task ExchangeAsync_DifferentSessionsSameAudience_BypassCache()
    {
        await using var harness = new Harness();
        harness.QueueOk("for-session-a", 300);
        harness.QueueOk("for-session-b", 300);
        var sa = TestJwt.WithSessionId(Guid.NewGuid());
        var sb = TestJwt.WithSessionId(Guid.NewGuid());

        var r1 = await harness.Client.ExchangeAsync(sa, TARGET_AUDIENCE);
        var r2 = await harness.Client.ExchangeAsync(sb, TARGET_AUDIENCE);

        r1.Data.Should().Be("for-session-a");
        r2.Data.Should().Be("for-session-b");
        harness.Handler.RequestCount.Should().Be(2);
    }

    // ----------------------------------------------------------------------
    // Edge unreachable / failures → ServiceUnavailable (fail-fast, no fallback)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_HttpUnreachable_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness();
        harness.QueueException(new HttpRequestException("connection refused"));
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ExchangeAsync_NonSuccessHttp_ReturnsServiceUnavailable(HttpStatusCode status)
    {
        await using var harness = new Harness();
        harness.Queue(_ => new HttpResponseMessage(status));
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    [Fact]
    public async Task ExchangeAsync_FailedFetch_DoesNotPoisonCache()
    {
        // Adversarial: a transient failure must not write a "failed" entry to
        // the cache that subsequent callers would observe. After the failure,
        // a retry that succeeds must populate the cache normally.
        await using var harness = new Harness();
        harness.QueueException(new HttpRequestException("boom"));
        harness.QueueOk("recovered", 300);
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var failed = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);
        var recovered = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        failed.Success.Should().BeFalse();
        recovered.Success.Should().BeTrue();
        recovered.Data.Should().Be("recovered");
        harness.Handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task ExchangeAsync_DiscoveryDocMissingTokenEndpoint_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness(tokenEndpoint: string.Empty);
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
        harness.Handler.RequestCount.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Singleflight — concurrent first-callers for the same key dedup
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_ConcurrentSameKey_SingleHttpCall()
    {
        await using var harness = new Harness();
        var gate = new TaskCompletionSource();
        harness.Queue(async _ =>
        {
            await gate.Task;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"shared","expires_in":300}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var calls = Enumerable.Range(0, 8)
            .Select(_ => harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE).AsTask())
            .ToArray();

        gate.SetResult();
        var results = await Task.WhenAll(calls);

        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.Data.Should().Be("shared");
        });
        harness.Handler.RequestCount.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // expires_in fallback
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_ResponseMissingExpiresIn_UsesFallbackTtl()
    {
        await using var harness = new Harness();
        harness.QueueRaw("""{"access_token":"tok-no-ttl"}""");
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        // No expires_in → fallback TTL applies; the call still succeeds.
        result.Success.Should().BeTrue();
        result.Data.Should().Be("tok-no-ttl");
    }

    // ----------------------------------------------------------------------
    // JWT-payload length bound — defensive against malicious oversized JWTs
    // blowing the stack inside the base64 decoder.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_OversizedSubjectTokenPayload_RejectedAsValidationFailed()
    {
        // Adversarial: a JWT with a payload segment larger than the bound
        // (16 KB) would hit an unbounded stackalloc inside Base64UrlDecode.
        // The bound rejects oversized tokens before the allocation.
        await using var harness = new Harness();
        var oversizedPayload = new string('a', 32 * 1024);
        var oversizedToken = $"header.{oversizedPayload}.sig";

        var result = await harness.Client.ExchangeAsync(oversizedToken, TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        harness.Handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ExchangeAsync_PayloadAtBoundary_StillProcessed()
    {
        // Boundary: a JWT whose payload base64-decodes to a moderately large
        // but valid JSON object (under the 16 KB cap) is processed normally.
        await using var harness = new Harness();
        var bigButValidToken = TestJwt.Build(new()
        {
            ["d2_session_id"] = Guid.NewGuid().ToString(),
            ["padding"] = new string('x', 4 * 1024),
        });
        harness.QueueOk("exchanged-jwt", 300);

        var result = await harness.Client.ExchangeAsync(bigButValidToken, TARGET_AUDIENCE);

        result.Success.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Audience length bound — TokenExchangeCache.BuildKey rejects audiences
    // beyond 2048 chars; ExchangeAsync surfaces ValidationFailed.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_OversizedAudience_ReturnsValidationFailed()
    {
        await using var harness = new Harness();
        var subject = TestJwt.WithSessionId(Guid.NewGuid());
        var oversizedAudience = new string('a', TokenExchangeCache.MAX_AUDIENCE_LENGTH + 1);

        var result = await harness.Client.ExchangeAsync(subject, oversizedAudience);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        harness.Handler.RequestCount.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Singleflight cache re-check — sibling that populates cache while we're
    // queued on the singleflight slot must short-circuit the HTTP fetch.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_CachePopulatedDuringSingleflightWait_ServesCacheNoHttp()
    {
        await using var harness = new Harness();
        var sessionId = Guid.NewGuid();
        var subject = TestJwt.WithSessionId(sessionId);
        var key = harness.Cache.BuildKey(sessionId, TARGET_AUDIENCE, null)!;

        await harness.Cache.SetAsync(sessionId, key, "pre-populated", TimeSpan.FromMinutes(5));

        var result = await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        result.Success.Should().BeTrue();
        result.Data.Should().Be("pre-populated");
        harness.Handler.RequestCount.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Telemetry counter emission — every code path increments the counter
    // with the right outcome tag.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_FetchSuccess_IncrementsCounterWithFetchSuccessTag()
    {
        using var listener = new TaggedCounterListener("d2.auth.outbound.token_exchange.requests");
        await using var harness = new Harness();
        harness.QueueOk("exchanged-jwt", 300);
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        listener.Snapshot().Should().Contain(t => t.Outcome == "fetch_success");
    }

    [Fact]
    public async Task ExchangeAsync_CacheHit_IncrementsCounterWithCacheHitTag()
    {
        using var listener = new TaggedCounterListener("d2.auth.outbound.token_exchange.requests");
        await using var harness = new Harness();
        harness.QueueOk("exchanged-jwt", 300);
        var subject = TestJwt.WithSessionId(Guid.NewGuid());
        await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);
        listener.Snapshot().Clear();

        await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        listener.Snapshot().Should().Contain(t => t.Outcome == "cache_hit");
    }

    [Fact]
    public async Task ExchangeAsync_FetchException_IncrementsCounterWithFetchFailureTag()
    {
        using var listener = new TaggedCounterListener("d2.auth.outbound.token_exchange.requests");
        await using var harness = new Harness();
        harness.QueueException(new HttpRequestException("boom"));
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        listener.Snapshot().Should().Contain(t => t.Outcome == "fetch_failure");
    }

    // ----------------------------------------------------------------------
    // Circuit breaker — trips after N consecutive transient failures,
    // fast-fails while open (no HTTP attempt), resets after cooldown.
    // Deterministic via FakeTimeProvider injected into the client's NowFunc.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_CircuitBreaker_OpensAfterThresholdFailures()
    {
        // Adversarial: queue exactly FAILURE_THRESHOLD failures, then one
        // more request. The post-trip call must fast-fail (ServiceUnavailable)
        // WITHOUT hitting the HTTP handler — HTTP call count must not climb
        // past the threshold.
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var harness = new Harness(clock: clock);
        const int threshold = 5; // matches AuthOutboundResilienceDefaults.FAILURE_THRESHOLD
        for (var i = 0; i < threshold; i++)
            harness.QueueException(new HttpRequestException($"down-{i}"));

        for (var i = 0; i < threshold; i++)
        {
            // Use a distinct session per call so each goes through to the CB
            // (not served from cache after first fail).
            var subject = TestJwt.WithSessionId(Guid.NewGuid());
            await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);
        }

        // Breaker is now open — next call should fast-fail.
        var result = await harness.Client.ExchangeAsync(
            TestJwt.WithSessionId(Guid.NewGuid()), TARGET_AUDIENCE);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);

        // HTTP handler was not called for the fast-fail call.
        harness.Handler.RequestCount.Should().Be(threshold);
    }

    [Fact]
    public async Task ExchangeAsync_CircuitBreaker_StaysOpenDuringCooldown()
    {
        // While the breaker is open, repeated calls must not increase the
        // HTTP call count regardless of how many times the client is called.
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var harness = new Harness(clock: clock);
        const int threshold = 5;
        for (var i = 0; i < threshold; i++)
            harness.QueueException(new HttpRequestException($"down-{i}"));

        for (var i = 0; i < threshold; i++)
        {
            var subject = TestJwt.WithSessionId(Guid.NewGuid());
            await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);
        }

        // Call 3 more times during open window — no new HTTP calls.
        var r1 = await harness.Client.ExchangeAsync(
            TestJwt.WithSessionId(Guid.NewGuid()), TARGET_AUDIENCE);
        var r2 = await harness.Client.ExchangeAsync(
            TestJwt.WithSessionId(Guid.NewGuid()), TARGET_AUDIENCE);
        var r3 = await harness.Client.ExchangeAsync(
            TestJwt.WithSessionId(Guid.NewGuid()), TARGET_AUDIENCE);

        r1.Success.Should().BeFalse();
        r2.Success.Should().BeFalse();
        r3.Success.Should().BeFalse();
        harness.Handler.RequestCount.Should().Be(threshold);
    }

    [Fact]
    public async Task ExchangeAsync_CircuitBreaker_ClosesAfterCooldownOnSuccess()
    {
        // After the cooldown elapses, the breaker allows a half-open probe.
        // A successful probe resets the breaker to closed and subsequent calls
        // go through normally. Uses FakeTimeProvider to drive cooldown
        // deterministically without wall-clock sleep.
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var harness = new Harness(clock: clock);
        const int threshold = 5;
        for (var i = 0; i < threshold; i++)
            harness.QueueException(new HttpRequestException($"down-{i}"));

        for (var i = 0; i < threshold; i++)
        {
            var subject = TestJwt.WithSessionId(Guid.NewGuid());
            await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);
        }

        // Advance clock past 30 s cooldown (AuthOutboundResilienceDefaults).
        clock.Advance(TimeSpan.FromSeconds(31));

        // Queue a success for the half-open probe.
        harness.QueueOk("exchanged-recovered", 300);
        var probeResult = await harness.Client.ExchangeAsync(
            TestJwt.WithSessionId(Guid.NewGuid()), TARGET_AUDIENCE);

        probeResult.Success.Should().BeTrue();
        probeResult.Data.Should().Be("exchanged-recovered");
        harness.Handler.RequestCount.Should().Be(threshold + 1);
    }

    // ----------------------------------------------------------------------
    // Disposal
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ExchangeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var harness = new Harness();
        harness.Client.Dispose();
        var subject = TestJwt.WithSessionId(Guid.NewGuid());

        var act = async () => await harness.Client.ExchangeAsync(subject, TARGET_AUDIENCE);

        await act.Should().ThrowAsync<ObjectDisposedException>();
        await harness.DisposeAsync();
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    /// <summary>
    /// Capture-the-body helper — reads the request body inside the handler
    /// callback (before the request is disposed by the using-block in the
    /// client) and surfaces it via a mutable holder for the test to inspect.
    /// </summary>
    private static Task<BodyHolder> CaptureBody(
        Harness harness,
        Func<HttpResponseMessage> responseFactory)
    {
        var holder = new BodyHolder();
        harness.Queue(async req =>
        {
            holder.Value = await req.Content!.ReadAsStringAsync();
            return responseFactory();
        });
        return Task.FromResult(holder);
    }

    private static HttpResponseMessage OkResponse(string accessToken, int expiresInSeconds)
        => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            $$"""{ "access_token": "{{accessToken}}", "expires_in": {{expiresInSeconds}} }""",
            Encoding.UTF8,
            "application/json"),
    };

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class BodyHolder
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            r_responses = new();

        private readonly HttpClient r_httpClient;
        private readonly DefaultLocalCache r_localCache;
        private readonly TokenExchangeCache r_cache;

        public Harness(string tokenEndpoint = TOKEN_ENDPOINT, TimeProvider? clock = null)
        {
            Handler = new StubHttpMessageHandler(async (req, _) =>
            {
                var next = r_responses.Count > 0
                    ? r_responses.Dequeue()
                    : _ => throw new InvalidOperationException("No queued response.");
                return await next(req);
            });
            r_httpClient = new HttpClient(Handler);
            HttpClientFactory = new SingleClientFactory(r_httpClient);
            var configManager = new StubConfigurationManager(tokenEndpoint);
            var options = Options.Create(new AuthOutboundOptions
            {
                Issuer = "https://edge.internal",
                ClientId = "test-client",
                ClientSecret = "test-secret",
            });
            r_localCache = new DefaultLocalCache(
                Options.Create(new D2.Shared.Caching.LocalCacheOptions()));
            r_cache = new TokenExchangeCache(
                r_localCache,
                options,
                NullLogger<TokenExchangeCache>.Instance,
                backplane: null);
            Clock = clock ?? new FakeTimeProvider(
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            Client = new HttpTokenExchangeClient(
                HttpClientFactory,
                configManager,
                r_cache,
                options,
                NullLogger<HttpTokenExchangeClient>.Instance,
                Clock);
        }

        public StubHttpMessageHandler Handler { get; }

        public IHttpClientFactory HttpClientFactory { get; }

        public HttpTokenExchangeClient Client { get; }

        public TimeProvider Clock { get; }

        public TokenExchangeCache Cache => r_cache;

        public void QueueOk(string accessToken, int expiresInSeconds) => QueueRaw(
            $$"""{ "access_token": "{{accessToken}}", "expires_in": {{expiresInSeconds}} }""");

        public void QueueRaw(string body) =>
            r_responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }));

        public void QueueException(Exception ex) =>
            r_responses.Enqueue(_ => Task.FromException<HttpResponseMessage>(ex));

        public void Queue(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            r_responses.Enqueue(req => Task.FromResult(handler(req)));

        public void Queue(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
            r_responses.Enqueue(handler);

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await r_cache.DisposeAsync();
            r_localCache.Dispose();
            r_httpClient.Dispose();
            Handler.Dispose();
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient r_client;

        public SingleClientFactory(HttpClient client) => r_client = client;

        public HttpClient CreateClient(string name) => r_client;
    }
}
