// -----------------------------------------------------------------------
// <copyright file="RouteIdempotencyGateTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute;

using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Auth.Http;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecRoute.Generated;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecRoute.Generated.Facade;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using DcsvIo.D2.Result;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// TestServer tests for the idempotency gate woven by the TypeSpec emitter into
/// the generated <c>SignFixtureRouteRegistration</c> and <c>SignFixtureDerivedRouteRegistration</c>.
///
/// Each test exercises one observable behavior of the gate:
///   duplicate key - stored result replayed (no second façade call);
///   missing key header - 400 ValidationFailed;
///   store read outage - fail-open (delegate still invoked);
///   stored failure replayed on retry;
///   derived key - key is a SHA-256 hash of the kid field;
///   first call stores the outcome; TTL honored by fake.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestJwtBuilder lifetime is bounded by individual tests.")]
public sealed class RouteIdempotencyGateTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "keycustodian";
    private const string _SIGN_SCOPE = "self.write";
    private const string _IDEMPOTENCY_KEY_HEADER = "Idempotency-Key";

    // ── Header keySource: duplicate key → replay stored result ───────────────

    [Fact]
    public async Task SignRoute_DuplicateIdempotencyKey_ReturnsStoredResult_WithoutCallingFacade()
    {
        // First call stores the result; second call replays it from the store.
        const string expectedSig = "replayed-sig-abc";
        using var jwt = new TestJwtBuilder();
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput(expectedSig)));
        var store = new FakeIdempotencyStore();
        using var host = await BuildSignHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        const string idempotencyKey = "idem-key-001";

        // First request — populates the store.
        var first = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-001", []),
            _IDEMPOTENCY_KEY_HEADER,
            idempotencyKey);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.SignCallCount.Should().Be(1);
        store.StoreCallCount.Should().Be(1);

        // Second request — same key; must replay without calling the façade.
        var second = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-001", []),
            _IDEMPOTENCY_KEY_HEADER,
            idempotencyKey);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadAsStringAsync();
        body.Should().Contain(expectedSig);

        // Façade was NOT called a second time.
        fake.SignCallCount.Should().Be(1);
        store.TryGetCallCount.Should().Be(2);
    }

    // ── Header keySource: missing key → 400 ValidationFailed ─────────────────

    [Fact]
    public async Task SignRoute_MissingIdempotencyKeyHeader_Returns400ValidationFailed()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput("sig")));
        var store = new FakeIdempotencyStore();
        using var host = await BuildSignHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        // No Idempotency-Key header — gate rejects with 400.
        var response = await client.PostAsJsonAsync(
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-001", []));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // Façade must NOT have been invoked.
        fake.SignCallCount.Should().Be(0);
        store.TryGetCallCount.Should().Be(0);
    }

    // ── Header keySource: whitespace-only key → 400 ValidationFailed ─────────

    [Fact]
    public async Task SignRoute_WhitespaceIdempotencyKey_Returns400ValidationFailed()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput("sig")));
        var store = new FakeIdempotencyStore();
        using var host = await BuildSignHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        var response = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-001", []),
            _IDEMPOTENCY_KEY_HEADER,
            "   ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fake.SignCallCount.Should().Be(0);
    }

    // ── Header keySource: store read outage → fail-open (delegate still runs) ─

    [Fact]
    public async Task SignRoute_StoreReadOutage_FailsOpen_DelegateStillInvoked()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput("sig-on-outage")));
        var store = new FakeIdempotencyStore();
        store.SetFaulted();
        using var host = await BuildSignHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        var response = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-001", []),
            _IDEMPOTENCY_KEY_HEADER,
            "idem-outage");

        // Gate fails-open: store error on TryGet → proceed to delegate.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.SignCallCount.Should().Be(1);
    }

    // ── Header keySource: stored failure replayed on second call ─────────────

    [Fact]
    public async Task SignRoute_StoredFailure_ReplaysFailureOnSecondCall()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.ServiceUnavailable());
        var store = new FakeIdempotencyStore();
        using var host = await BuildSignHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        const string idempotencyKey = "idem-fail-001";

        // First request — façade returns failure; gate stores the failure result.
        var first = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-001", []),
            _IDEMPOTENCY_KEY_HEADER,
            idempotencyKey);
        first.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        store.StoreCallCount.Should().Be(1);

        // Second request — same key; failure is replayed without another façade call.
        var second = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-001", []),
            _IDEMPOTENCY_KEY_HEADER,
            idempotencyKey);
        second.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        fake.SignCallCount.Should().Be(1);
    }

    // ── Header keySource: store write outage → still returns delegate result ───

    [Fact]
    public async Task SignRoute_StoreWriteOutage_SuccessResultStillReturned()
    {
        // Write-fault: TryGet behaves normally (returns NotFound → cache miss);
        // StoreAsync returns ServiceUnavailable. Best-effort write — the gate must
        // NOT surface the write failure to the caller; the delegate result is returned.
        using var jwt = new TestJwtBuilder();
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput("sig-write-fault")));
        var store = new FakeIdempotencyStore();
        store.SetWriteFaulted();
        using var host = await BuildSignHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        var response = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-001", []),
            _IDEMPOTENCY_KEY_HEADER,
            "idem-write-fault");

        // Delegate was invoked and result was returned despite the write failure.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.SignCallCount.Should().Be(1);
        store.StoreCallCount.Should().Be(1, "gate attempted the write even though it failed");
    }

    // ── Derived keySource: key is SHA-256 of kid field ────────────────────────

    [Fact]
    public async Task SignDerivedRoute_DuplicateKid_ReturnsStoredResult_WithoutCallingFacade()
    {
        const string expectedSig = "derived-replay-sig";
        const string kid = "key-derived-001";
        using var jwt = new TestJwtBuilder();
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput("sign-default")),
            signDerivedResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput(expectedSig)));
        var store = new FakeIdempotencyStore();
        using var host = await BuildSignDerivedHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        // First call — stores the result; key is derived from kid.
        var first = await client.PostAsJsonAsync(
            "/internal/v1/fixtures/sign-fixture-derived",
            new SignFixtureInput(kid, []));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.SignDerivedCallCount.Should().Be(1);
        store.StoreCallCount.Should().Be(1);

        // Verify the key stored matches SHA-256(kid).
        var expectedKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(kid)));
        store.StoredKeys.Should().Contain(expectedKey);

        // Second call — same kid → same derived key → replay.
        var second = await client.PostAsJsonAsync(
            "/internal/v1/fixtures/sign-fixture-derived",
            new SignFixtureInput(kid, []));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadAsStringAsync();
        body.Should().Contain(expectedSig);
        fake.SignDerivedCallCount.Should().Be(1);
    }

    // ── Derived keySource: different kid → different key → different result ───

    [Fact]
    public async Task SignDerivedRoute_DifferentKid_NoCacheHit_FacadeCalledAgain()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput("default")),
            signDerivedResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput("sig")));
        var store = new FakeIdempotencyStore();
        using var host = await BuildSignDerivedHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        // Two calls with different kids → two different derived keys → two façade calls.
        await client.PostAsJsonAsync(
            "/internal/v1/fixtures/sign-fixture-derived",
            new SignFixtureInput("kid-A", []));
        await client.PostAsJsonAsync(
            "/internal/v1/fixtures/sign-fixture-derived",
            new SignFixtureInput("kid-B", []));

        fake.SignDerivedCallCount.Should().Be(2);
        store.StoredKeys.Should().HaveCount(2);
    }

    // ── Header keySource: TTL-expiry → re-executes after clock advances ──────

    [Fact]
    public async Task SignRoute_ExpiredIdempotencyKey_ReExecutes_AfterTtlElapses()
    {
        // A stored result is replayed before TTL expires; after the clock
        // advances past the TTL the entry is a miss and the façade is called again.
        // Uses FakeTimeProvider so the test is deterministic — no ambient
        // DateTimeOffset.UtcNow dependency.
        using var jwt = new TestJwtBuilder();
        var fakeClock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var fake = new FakeSignFixtureSignerFacade(
            signResult: D2Result<SignFixtureOutput?>.Ok(new SignFixtureOutput("sig-ttl")));
        var store = new FakeIdempotencyStore(fakeClock);
        using var host = await BuildSignHostAsync(jwt, fake, store);
        var client = BuildAuthenticatedClient(host, jwt);

        const string idempotencyKey = "idem-ttl-test";

        // First request — cache miss; façade invoked; result stored with 86400s TTL.
        var first = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-ttl", []),
            _IDEMPOTENCY_KEY_HEADER,
            idempotencyKey);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.SignCallCount.Should().Be(1);
        store.StoreCallCount.Should().Be(1);

        // Replay before TTL elapses — same result, façade not called again.
        var replay = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-ttl", []),
            _IDEMPOTENCY_KEY_HEADER,
            idempotencyKey);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.SignCallCount.Should().Be(1, "replay before TTL must not invoke the façade");

        // Advance the clock past the 86400s TTL.
        fakeClock.Advance(TimeSpan.FromSeconds(86401));

        // Request after expiry — expired entry is a miss; façade called again.
        var afterExpiry = await PostWithHeaderAsync(
            client,
            "/internal/v1/fixtures/sign-fixture",
            new SignFixtureInput("kid-ttl", []),
            _IDEMPOTENCY_KEY_HEADER,
            idempotencyKey);
        afterExpiry.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.SignCallCount.Should().Be(2, "expired entry must trigger a new façade invocation");
    }

    // ── Host builders ─────────────────────────────────────────────────────────

    private static async Task<IHost> BuildSignHostAsync(
        TestJwtBuilder jwtBuilder,
        FakeSignFixtureSignerFacade fake,
        FakeIdempotencyStore store)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddD2LocalCache();
                        services.AddSingleton<ITieredCache, FakeTieredCacheStub>();
                        services.AddD2Auth(opts =>
                        {
                            opts.Issuer = new Uri(_ISSUER);
                            opts.Audience = _AUDIENCE;
                        });

                        services.RemoveAll<IJwksProvider>();
                        services.AddSingleton<IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));

                        services.RemoveAll<DcsvIo.D2.Auth.Abstractions.Sessions.ISessionLivenessTracker>();
                        services.AddSingleton<DcsvIo.D2.Auth.Abstractions.Sessions.ISessionLivenessTracker>(
                            new FakeSessionLivenessTracker());

                        services.AddSingleton<ISignFixtureSignerFacade>(fake);
                        services.AddSingleton<D2GeneratedIdempotencyStore>(store);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Auth();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapSignFixtureRoute();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    private static async Task<IHost> BuildSignDerivedHostAsync(
        TestJwtBuilder jwtBuilder,
        FakeSignFixtureSignerFacade fake,
        FakeIdempotencyStore store)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddD2LocalCache();
                        services.AddSingleton<ITieredCache, FakeTieredCacheStub>();
                        services.AddD2Auth(opts =>
                        {
                            opts.Issuer = new Uri(_ISSUER);
                            opts.Audience = _AUDIENCE;
                        });

                        services.RemoveAll<IJwksProvider>();
                        services.AddSingleton<IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));

                        services.RemoveAll<DcsvIo.D2.Auth.Abstractions.Sessions.ISessionLivenessTracker>();
                        services.AddSingleton<DcsvIo.D2.Auth.Abstractions.Sessions.ISessionLivenessTracker>(
                            new FakeSessionLivenessTracker());

                        services.AddSingleton<ISignFixtureSignerFacade>(fake);
                        services.AddSingleton<D2GeneratedIdempotencyStore>(store);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Auth();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapSignFixtureDerivedRoute();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    private static HttpClient BuildAuthenticatedClient(IHost host, TestJwtBuilder jwt)
    {
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SIGN_SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<HttpResponseMessage> PostWithHeaderAsync<T>(
        HttpClient client,
        string requestUri,
        T content,
        string headerName,
        string headerValue)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, requestUri);
        msg.Content = JsonContent.Create(content);
        msg.Headers.TryAddWithoutValidation(headerName, headerValue);
        return await client.SendAsync(msg);
    }
}
