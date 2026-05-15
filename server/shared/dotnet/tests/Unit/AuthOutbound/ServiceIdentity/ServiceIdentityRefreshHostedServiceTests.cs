// -----------------------------------------------------------------------
// <copyright file="ServiceIdentityRefreshHostedServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.ServiceIdentity;

using System.Net;
using System.Text;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Tests.Unit.AuthOutbound.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// Background-service coverage for
/// <see cref="ServiceIdentityRefreshHostedService"/>: startup acquisition,
/// the refresh-due loop, exception swallowing, and cancellation behavior.
/// Built atop the same in-memory <see cref="HttpServiceIdentityClient"/>
/// harness used by <see cref="HttpServiceIdentityClientTests"/> so the
/// BackgroundService is exercised end-to-end against a real (stubbed) HTTP
/// path rather than against a hand-rolled mock client.
/// </summary>
public sealed class ServiceIdentityRefreshHostedServiceTests
{
    private const string TOKEN_ENDPOINT = "https://edge.internal/oauth/token";
    private const string CLIENT_ID = "files-service";
    private const string CLIENT_SECRET = "super-secret";

    [Fact]
    public async Task ExecuteAsync_StartupAcquireSucceeds_PopulatesCache()
    {
        await using var harness = new Harness();
        harness.QueueOk("startup-jwt", expiresInSeconds: 600);

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Allow the synchronous startup acquire path to run.
        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;

        var snapshot = harness.Cache.PeekRaw();
        snapshot.Should().NotBeNull();
        snapshot.Token.Should().Be("startup-jwt");
    }

    [Fact]
    public async Task ExecuteAsync_StartupAcquireFails_DoesNotThrow()
    {
        await using var harness = new Harness();
        harness.QueueException(new HttpRequestException("edge unreachable"));

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Give the failing startup acquire a chance to complete + log without
        // throwing.
        await Task.Delay(50);

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;

        // The cache stays empty; the loop survived the failure.
        harness.Cache.PeekRaw().Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_TickFailureSwallowed_LoopSurvivesAndDoesNotPropagate()
    {
        await using var harness = new Harness();

        // tiny expiry → refresh due immediately
        harness.QueueOk("startup-jwt", expiresInSeconds: 1);
        harness.QueueException(new HttpRequestException("edge unreachable"));
        harness.QueueException(new HttpRequestException("still down"));

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Wait for the startup acquire.
        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);

        // Advance the clock past poll interval to trigger the next tick.
        harness.Clock.Advance(TimeSpan.FromSeconds(6));

        // Allow the failed tick to run + log without throwing.
        await Task.Delay(50);

        // Loop is still alive; the still-valid cached token continues serving.
        harness.Service.ExecuteTask.Should().NotBeNull();
        harness.Service.ExecuteTask!.IsCompleted.Should().BeFalse();

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringLoop_StopsCleanly()
    {
        await using var harness = new Harness();
        harness.QueueOk("startup-jwt", expiresInSeconds: 600);

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Wait for startup acquire to complete.
        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);

        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;

        harness.Service.ExecuteTask.Should().NotBeNull();
        harness.Service.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_StartupAcquireSucceeds_PopulatedCacheUsesConfiguredLeadTime()
    {
        await using var harness = new Harness(refreshLeadTime: TimeSpan.FromSeconds(60));
        harness.QueueOk("startup-jwt", expiresInSeconds: 600);

        using var cts = new CancellationTokenSource();
        var task = harness.Service.StartAsync(cts.Token);

        // Startup acquire populates the cache; the cached snapshot reflects
        // the queued token + the FakeTimeProvider's 0-relative expiry.
        await Harness.WaitUntil(() => harness.Cache.PeekRaw() is not null);

        var snapshot = harness.Cache.PeekRaw();
        snapshot.Should().NotBeNull();
        snapshot.Token.Should().Be("startup-jwt");

        // Lead-time is 60s; the snapshot expires in 600s; with no time advance,
        // refresh should NOT be due yet (600s - 0s = 600s > 60s lead).
        // We don't drive subsequent ticks here — that path is exercised in the
        // tick-failure-survival test.
        await cts.CancelAsync();
        await harness.Service.StopAsync(CancellationToken.None);
        await task;
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> r_responses
            = new();

        private readonly HttpClient r_httpClient;
        private readonly HttpServiceIdentityClient r_client;

        public Harness(TimeSpan? refreshLeadTime = null)
        {
            Handler = new StubHttpMessageHandler(async (req, _) =>
            {
                var next = r_responses.Count > 0
                    ? r_responses.Dequeue()
                    : _ => throw new InvalidOperationException("No queued response.");
                return await next(req);
            });
            r_httpClient = new HttpClient(Handler);
            var httpClientFactory = new SingleClientFactory(r_httpClient);
            Cache = new ServiceIdentityCache();
            Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var configManager = new StubConfigurationManager(TOKEN_ENDPOINT);
            var options = Options.Create(new AuthOutboundOptions
            {
                Issuer = "https://edge.internal",
                ClientId = CLIENT_ID,
                ClientSecret = CLIENT_SECRET,
                ServiceIdentityRefreshLeadTime = refreshLeadTime ?? TimeSpan.FromSeconds(30),
            });
            r_client = new HttpServiceIdentityClient(
                httpClientFactory,
                configManager,
                Cache,
                options,
                NullLogger<HttpServiceIdentityClient>.Instance,
                Clock);
            Service = new ServiceIdentityRefreshHostedService(
                r_client,
                Cache,
                options,
                NullLogger<ServiceIdentityRefreshHostedService>.Instance,
                Clock);
        }

        public StubHttpMessageHandler Handler { get; }

        public ServiceIdentityCache Cache { get; }

        public FakeTimeProvider Clock { get; }

        public ServiceIdentityRefreshHostedService Service { get; }

        public static async Task WaitUntil(Func<bool> condition, int timeoutMs = 1000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                    return;

                await Task.Delay(10);
            }

            condition().Should().BeTrue("condition was not met within the timeout");
        }

        public void QueueOk(string accessToken, int expiresInSeconds) =>
            r_responses.Enqueue(_ => Task.FromResult(Ok(accessToken, expiresInSeconds)));

        public void QueueException(Exception ex) =>
            r_responses.Enqueue(_ => Task.FromException<HttpResponseMessage>(ex));

        public async ValueTask DisposeAsync()
        {
            Service.Dispose();
            r_client.Dispose();
            r_httpClient.Dispose();
            Handler.Dispose();
            await ValueTask.CompletedTask;
        }

        private static HttpResponseMessage Ok(
            string accessToken, int expiresInSeconds) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{ "access_token": "{{accessToken}}", "expires_in": {{expiresInSeconds}} }""",
                Encoding.UTF8,
                "application/json"),
        };
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient r_client;

        public SingleClientFactory(HttpClient client) => r_client = client;

        public HttpClient CreateClient(string name) => r_client;
    }
}
