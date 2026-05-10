// -----------------------------------------------------------------------
// <copyright file="HttpServiceIdentityClientTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.ServiceIdentity;

using System.Net;
using System.Text;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.AuthOutbound.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// Integration-y unit coverage for <see cref="HttpServiceIdentityClient"/> —
/// drives the client against an in-memory <see cref="HttpMessageHandler"/>
/// fixture so request shape + response handling + cache + Singleflight
/// behavior can be observed end-to-end without a live Edge.
/// </summary>
public sealed class HttpServiceIdentityClientTests
{
    private const string TOKEN_ENDPOINT = "https://edge.internal/oauth/token";
    private const string CLIENT_ID = "files-service";
    private const string CLIENT_SECRET = "super-secret";

    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentTokenAsync_HappyPath_ReturnsTokenFromResponse()
    {
        await using var harness = new Harness();
        harness.QueueOk("jwt-1", expiresInSeconds: 300);

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().Be("jwt-1");
        harness.Handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCurrentTokenAsync_PostsClientCredentialsGrantWithBasicAuth()
    {
        await using var harness = new Harness();
        string? capturedBody = null;
        Uri? capturedUri = null;
        HttpMethod? capturedMethod = null;
        string? capturedAuthScheme = null;
        string? capturedAuthParam = null;
        harness.Queue(async req =>
        {
            // Capture inside the handler — the request + content are disposed
            // once SendAsync returns, so accessing them after is unsafe.
            capturedMethod = req.Method;
            capturedUri = req.RequestUri;
            capturedAuthScheme = req.Headers.Authorization?.Scheme;
            capturedAuthParam = req.Headers.Authorization?.Parameter;
            capturedBody = await req.Content!.ReadAsStringAsync();
            return Ok("jwt-1", 300);
        });

        await harness.Client.GetCurrentTokenAsync();

        capturedMethod.Should().Be(HttpMethod.Post);
        capturedUri.Should().Be(new Uri(TOKEN_ENDPOINT));
        capturedAuthScheme.Should().Be("Basic");
        var expectedCredentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{CLIENT_ID}:{CLIENT_SECRET}"));
        capturedAuthParam.Should().Be(expectedCredentials);
        capturedBody.Should().Contain("grant_type=client_credentials");
    }

    // ----------------------------------------------------------------------
    // Cache hit — second call avoids the HTTP round trip
    // ----------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentTokenAsync_CachedAndNotExpired_DoesNotHitHttp()
    {
        await using var harness = new Harness();
        harness.QueueOk("jwt-1", 300);

        var first = await harness.Client.GetCurrentTokenAsync();
        var second = await harness.Client.GetCurrentTokenAsync();

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.Data.Should().Be("jwt-1");
        harness.Handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCurrentTokenAsync_AfterCacheExpiry_HitsHttpAgain()
    {
        await using var harness = new Harness();
        harness.QueueOk("jwt-1", expiresInSeconds: 300);
        harness.QueueOk("jwt-2", expiresInSeconds: 300);

        var first = await harness.Client.GetCurrentTokenAsync();
        first.Data.Should().Be("jwt-1");

        // Advance past expiry — cache should re-fetch.
        harness.Clock.Advance(TimeSpan.FromSeconds(301));
        var second = await harness.Client.GetCurrentTokenAsync();

        second.Data.Should().Be("jwt-2");
        harness.Handler.RequestCount.Should().Be(2);
    }

    // ----------------------------------------------------------------------
    // Edge unreachable / structural failures → ServiceUnavailable
    // ----------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentTokenAsync_HttpUnreachable_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness();
        harness.QueueException(new HttpRequestException("connection refused"));

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetCurrentTokenAsync_NonSuccessHttp_ReturnsServiceUnavailable(
        HttpStatusCode status)
    {
        await using var harness = new Harness();
        harness.Queue(_ => new HttpResponseMessage(status));

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("\"a string\"")]
    [InlineData("42")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task GetCurrentTokenAsync_NonObjectResponse_ReturnsServiceUnavailable(string body)
    {
        await using var harness = new Harness();
        harness.QueueRaw(body);

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    [Fact]
    public async Task GetCurrentTokenAsync_MissingAccessToken_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness();
        harness.QueueRaw("""{ "expires_in": 300 }""");

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentTokenAsync_EmptyAccessToken_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness();
        harness.QueueRaw("""{ "access_token": "", "expires_in": 300 }""");

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentTokenAsync_AccessTokenNotString_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness();
        harness.QueueRaw("""{ "access_token": 42, "expires_in": 300 }""");

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentTokenAsync_MissingExpiresIn_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness();
        harness.QueueRaw("""{ "access_token": "tok" }""");

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"300\"")]
    public async Task GetCurrentTokenAsync_InvalidExpiresIn_ReturnsServiceUnavailable(
        string expiresValue)
    {
        await using var harness = new Harness();
        harness.QueueRaw($$"""{ "access_token": "tok", "expires_in": {{expiresValue}} }""");

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentTokenAsync_DiscoveryDocMissingTokenEndpoint_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness(tokenEndpoint: string.Empty);
        harness.QueueOk("never-called", 300);

        var result = await harness.Client.GetCurrentTokenAsync();

        result.Success.Should().BeFalse();
        harness.Handler.RequestCount.Should().Be(0); // never even tried
    }

    // ----------------------------------------------------------------------
    // Singleflight — concurrent first-callers dedup to one HTTP call
    // ----------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentTokenAsync_ConcurrentFirstCallers_SingleHttpCall()
    {
        // Adversarial: 16 concurrent callers on a cold cache must resolve
        // through exactly one outbound HTTP request via Singleflight.
        await using var harness = new Harness();
        var gate = new TaskCompletionSource();
        harness.Queue(async _ =>
        {
            await gate.Task;
            return Ok("jwt-shared", 300);
        });

        var calls = Enumerable.Range(0, 16)
            .Select(_ => harness.Client.GetCurrentTokenAsync().AsTask())
            .ToArray();

        // Allow the contended fetch to complete.
        gate.SetResult();
        var results = await Task.WhenAll(calls);

        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.Data.Should().Be("jwt-shared");
        });
        harness.Handler.RequestCount.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // ForceRefreshAsync — bypasses cache freshness
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ForceRefreshAsync_HappyPath_ReturnsOk()
    {
        await using var harness = new Harness();
        harness.QueueOk("jwt-1", 300);

        var result = await harness.Client.ForceRefreshAsync();

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ForceRefreshAsync_HttpFailure_ReturnsServiceUnavailable()
    {
        await using var harness = new Harness();
        harness.QueueException(new HttpRequestException("boom"));

        var result = await harness.Client.ForceRefreshAsync();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.SERVICE_UNAVAILABLE);
    }

    [Fact]
    public async Task ForceRefreshAsync_PopulatesCache_NextGetIsCacheHit()
    {
        await using var harness = new Harness();
        harness.QueueOk("jwt-warmed", 300);

        await harness.Client.ForceRefreshAsync();
        var get = await harness.Client.GetCurrentTokenAsync();

        get.Data.Should().Be("jwt-warmed");
        harness.Handler.RequestCount.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // Singleflight cache re-check — sibling that populates cache while we're
    // queued on the singleflight slot must short-circuit our HTTP fetch.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ForceRefreshAsync_CachePopulatedDuringSingleflightWait_ServesCacheNoHttp()
    {
        // Adversarial: the hosted service may call ForceRefreshAsync while
        // another thread has just populated the cache. The fetch path must
        // re-check the cache before issuing HTTP.
        await using var harness = new Harness();
        harness.Cache.Set(new(
            "pre-populated",
            harness.Clock.GetUtcNow().AddMinutes(5)));

        var result = await harness.Client.ForceRefreshAsync();

        result.Success.Should().BeTrue();
        harness.Handler.RequestCount.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Telemetry counter emission — every code path increments the counter
    // with the right outcome tag.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentTokenAsync_FetchSuccess_IncrementsCounterWithFetchSuccessTag()
    {
        using var listener = new TaggedCounterListener("d2.auth.outbound.service_identity.fetches");
        await using var harness = new Harness();
        harness.QueueOk("jwt-1", 300);

        await harness.Client.GetCurrentTokenAsync();

        listener.Snapshot().Should().Contain(t => t.Outcome == "fetch_success");
    }

    [Fact]
    public async Task GetCurrentTokenAsync_CacheHit_IncrementsCounterWithCacheHitTag()
    {
        using var listener = new TaggedCounterListener("d2.auth.outbound.service_identity.fetches");
        await using var harness = new Harness();
        harness.QueueOk("jwt-1", 300);
        await harness.Client.GetCurrentTokenAsync();
        listener.Snapshot().Clear();

        await harness.Client.GetCurrentTokenAsync();

        listener.Snapshot().Should().Contain(t => t.Outcome == "cache_hit");
    }

    [Fact]
    public async Task GetCurrentTokenAsync_HttpFailure_IncrementsCounterWithHttpFailureTag()
    {
        using var listener = new TaggedCounterListener("d2.auth.outbound.service_identity.fetches");
        await using var harness = new Harness();
        harness.Queue(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await harness.Client.GetCurrentTokenAsync();

        listener.Snapshot().Should().Contain(t => t.Outcome == "http_failure");
    }

    [Fact]
    public async Task GetCurrentTokenAsync_FetchException_IncrementsCounterWithFetchFailureTag()
    {
        using var listener = new TaggedCounterListener("d2.auth.outbound.service_identity.fetches");
        await using var harness = new Harness();
        harness.QueueException(new HttpRequestException("boom"));

        await harness.Client.GetCurrentTokenAsync();

        listener.Snapshot().Should().Contain(t => t.Outcome == "fetch_failure");
    }

    // ----------------------------------------------------------------------
    // Disposal
    // ----------------------------------------------------------------------

    [Fact]
    public async Task GetCurrentTokenAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var harness = new Harness();
        harness.Client.Dispose();

        var act = async () => await harness.Client.GetCurrentTokenAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
        await harness.DisposeAsync();
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static HttpResponseMessage Ok(string accessToken, int expiresInSeconds) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            $$"""{ "access_token": "{{accessToken}}", "expires_in": {{expiresInSeconds}} }""",
            Encoding.UTF8,
            "application/json"),
    };

    /// <summary>Fixture wiring — instantiates the real client against stub HTTP + OIDC + clock.</summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> r_responses = new();
        private readonly HttpClient r_httpClient;

        public Harness(string tokenEndpoint = TOKEN_ENDPOINT)
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
            Cache = new ServiceIdentityCache();
            Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var configManager = new StubConfigurationManager(tokenEndpoint);
            var options = Options.Create(new AuthOutboundOptions
            {
                Issuer = "https://edge.internal",
                ClientId = CLIENT_ID,
                ClientSecret = CLIENT_SECRET,
            });
            Client = new HttpServiceIdentityClient(
                HttpClientFactory,
                configManager,
                Cache,
                options,
                NullLogger<HttpServiceIdentityClient>.Instance,
                Clock);
        }

        public StubHttpMessageHandler Handler { get; }

        public IHttpClientFactory HttpClientFactory { get; }

        public ServiceIdentityCache Cache { get; }

        public FakeTimeProvider Clock { get; }

        public HttpServiceIdentityClient Client { get; }

        public void QueueOk(string accessToken, int expiresInSeconds) =>
            r_responses.Enqueue(_ => Task.FromResult(Ok(accessToken, expiresInSeconds)));

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

        public ValueTask DisposeAsync()
        {
            Client.Dispose();
            r_httpClient.Dispose();
            Handler.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient r_client;

        public SingleClientFactory(HttpClient client) => r_client = client;

        public HttpClient CreateClient(string name) => r_client;
    }
}
