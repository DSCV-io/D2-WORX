// -----------------------------------------------------------------------
// <copyright file="HttpJwksProviderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Jwks;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Jwks;
using D2.Shared.Auth.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

[Collection("AuthTelemetrySerial")]
public sealed class HttpJwksProviderTests
{
    [Fact]
    public async Task GetKeysAsync_HappyPath_ReturnsKidIndexedSnapshot()
    {
        var key = MakeRsaKey("kid-1");
        var fakeMgr = new FakeConfigurationManager(MakeOidcConfig(key));
        var provider = MakeProvider(fakeMgr);

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeTrue();
        result.Data!.Keys.Should().ContainKey("kid-1");
        result.Data.SourceUri.Should()
            .Be(new Uri("https://edge.internal/.well-known/jwks.json"));
    }

    [Fact]
    public async Task GetKeysAsync_SkipsKeysWithoutKid()
    {
        // Adversarial: an OIDC issuer can technically publish keys without a
        // kid. Validation can't use them; skip them defensively.
        var keyed = MakeRsaKey("kid-1");
        var unkeyed = MakeRsaKey(null);
        var config = MakeOidcConfig(keyed, unkeyed);
        var provider = MakeProvider(new FakeConfigurationManager(config));

        var result = await provider.GetKeysAsync();

        result.Data!.Keys.Should().HaveCount(1);
        result.Data.Keys.Should().ContainKey("kid-1");
    }

    [Fact]
    public async Task GetKeysAsync_ConfigurationManagerThrows_ReturnsServiceUnavailable()
    {
        var fakeMgr = new FakeConfigurationManager(_ =>
            throw new InvalidOperationException("network down"));
        var provider = MakeProvider(fakeMgr);

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be("AUTH_JWKS_UNAVAILABLE");
    }

    [Fact]
    public async Task RefreshAsync_FirstCall_TriggersConfigManagerRefresh()
    {
        var fakeMgr = new FakeConfigurationManager(MakeOidcConfig(MakeRsaKey("kid-1")));
        var provider = MakeProvider(fakeMgr);

        var result = await provider.RefreshAsync();

        result.Success.Should().BeTrue();
        fakeMgr.RefreshRequests.Should().Be(1);
    }

    [Fact]
    public async Task RefreshAsync_WithinCooldown_DoesNotTriggerSecondRefresh()
    {
        // Subsequent RefreshAsync calls within RefreshCooldown (30s default)
        // of the previous successful refresh return Ok without calling
        // RequestRefresh again. Prevents stampedes during sustained validation
        // failures (reactive-refresh-on-unknown-kid).
        var fakeMgr = new FakeConfigurationManager(MakeOidcConfig(MakeRsaKey("kid-1")));
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        var provider = MakeProvider(fakeMgr, clock);

        var first = await provider.RefreshAsync();
        clock.Advance(TimeSpan.FromSeconds(5));
        var second = await provider.RefreshAsync();

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        fakeMgr.RefreshRequests.Should().Be(1, "second call within cooldown should be suppressed");
    }

    [Fact]
    public async Task RefreshAsync_AfterCooldownElapsed_TriggersSecondRefresh()
    {
        var fakeMgr = new FakeConfigurationManager(MakeOidcConfig(MakeRsaKey("kid-1")));
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));
        var provider = MakeProvider(fakeMgr, clock);

        await provider.RefreshAsync();
        clock.Advance(TimeSpan.FromSeconds(45));
        await provider.RefreshAsync();

        fakeMgr.RefreshRequests.Should().Be(2);
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentCallers_DedupViaSingleflight()
    {
        // N concurrent RefreshAsync callers result in 1 call to
        // ConfigurationManager. Singleflight key is "force-refresh" — global
        // per-process.
        var fakeMgr = new FakeConfigurationManager(MakeOidcConfig(MakeRsaKey("kid-1")));

        // Slow the configuration manager so concurrent callers actually overlap.
        fakeMgr.GetConfigurationDelay = TimeSpan.FromMilliseconds(50);
        var provider = MakeProvider(fakeMgr);

        var tasks = new Task[100];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = provider.RefreshAsync().AsTask();
        await Task.WhenAll(tasks);

        fakeMgr.RefreshRequests.Should().Be(
            1,
            "100 concurrent callers should dedup to 1 upstream call");
    }

    [Fact]
    public async Task RefreshAsync_ConfigurationManagerThrows_ReturnsServiceUnavailable()
    {
        var fakeMgr = new FakeConfigurationManager(_ =>
            throw new InvalidOperationException("upstream down"));
        var provider = MakeProvider(fakeMgr);

        var result = await provider.RefreshAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be("AUTH_JWKS_UNAVAILABLE");
    }

    [Fact]
    public void Constructor_NullConfigManager_Throws()
    {
        var act = () => new HttpJwksProvider(
            configManager: null!,
            options: Options.Create(MakeAuthOptions()),
            logger: NullLogger<HttpJwksProvider>.Instance,
            clock: TimeProvider.System);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetKeysAsync_OidcDocMissingJwksUri_ReturnsServiceUnavailable()
    {
        // Adversarial: Edge serves a discovery doc that resolves but lacks
        // jwks_uri. Without defense, the projection step would NRE on
        // new Uri(null). With defense, ServiceUnavailable + log delegate fires.
        var config = new OpenIdConnectConfiguration
        {
            JwksUri = null,
            Issuer = "https://edge.internal",
        };
        config.SigningKeys.Add(MakeRsaKey("kid-1"));
        var provider = MakeProvider(new FakeConfigurationManager(config));

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be("AUTH_JWKS_UNAVAILABLE");
    }

    [Fact]
    public async Task GetKeysAsync_OidcDocEmptyJwksUri_ReturnsServiceUnavailable()
    {
        var config = new OpenIdConnectConfiguration
        {
            JwksUri = string.Empty,
            Issuer = "https://edge.internal",
        };
        config.SigningKeys.Add(MakeRsaKey("kid-1"));
        var provider = MakeProvider(new FakeConfigurationManager(config));

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTH_JWKS_UNAVAILABLE");
    }

    [Fact]
    public async Task GetKeysAsync_HappyPath_RecordsJwksFetchDurationHistogram()
    {
        // The JwksFetchDurationMs histogram is documented but was previously
        // dead — declared but never emitted. Recording asserted via
        // MeterListener interception of the meter source.
        var fakeMgr = new FakeConfigurationManager(MakeOidcConfig(MakeRsaKey("kid-1")));
        var provider = MakeProvider(fakeMgr);
        var measurements =
            new List<(double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == AuthTelemetry.METER_NAME
                && instrument.Name == "d2.auth.jwks.fetch.duration")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            lock (measurements)
            {
                measurements.Add((value, tags.ToArray()));
            }
        });
        listener.Start();

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeTrue();
        measurements.Should().HaveCount(1);
        measurements[0].Value.Should().BeGreaterThanOrEqualTo(0);
        measurements[0].Tags.Should().Contain(t =>
            t.Key == "trigger" && (string)t.Value! == "implicit");
        measurements[0].Tags.Should().Contain(t =>
            t.Key == "outcome" && (string)t.Value! == "success");
    }

    [Fact]
    public async Task RefreshAsync_OnFailure_RecordsHistogramWithReactiveFailure()
    {
        var fakeMgr = new FakeConfigurationManager(_ =>
            throw new InvalidOperationException("upstream down"));
        var provider = MakeProvider(fakeMgr);
        var measurements = new List<IReadOnlyList<KeyValuePair<string, object?>>>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == AuthTelemetry.METER_NAME
                && instrument.Name == "d2.auth.jwks.fetch.duration")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            lock (measurements)
            {
                measurements.Add(tags.ToArray());
            }
        });
        listener.Start();

        var result = await provider.RefreshAsync();

        result.Success.Should().BeFalse();
        measurements.Should().HaveCount(1);
        measurements[0].Should().Contain(t =>
            t.Key == "trigger" && (string)t.Value! == "reactive");
        measurements[0].Should().Contain(t =>
            t.Key == "outcome" && (string)t.Value! == "failure");
    }

    [Fact]
    public async Task GetKeysAsync_JsonExceptionFromUpstream_TagsOutcomeAsParseError()
    {
        // A JsonException in the upstream chain (malformed discovery doc /
        // proxy interference) gets a distinct outcome tag from generic
        // network failures so operators can alert on Edge config bugs vs
        // transient outage.
        var fakeMgr = new FakeConfigurationManager(_ =>
            throw new System.Text.Json.JsonException("malformed discovery doc"));
        var provider = MakeProvider(fakeMgr);
        var measurements = new List<IReadOnlyList<KeyValuePair<string, object?>>>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == AuthTelemetry.METER_NAME
                && instrument.Name == "d2.auth.jwks.fetch.duration")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            lock (measurements)
            {
                measurements.Add(tags.ToArray());
            }
        });
        listener.Start();

        var result = await provider.GetKeysAsync();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTH_JWKS_UNAVAILABLE");
        measurements.Should().HaveCount(1);
        measurements[0].Should().Contain(t =>
            t.Key == "outcome" && (string)t.Value! == "parse_error");
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterFailureThreshold_FastFailsWithCircuitOpenOutcome()
    {
        // After CircuitBreakerFailureThreshold consecutive failures the
        // breaker opens and subsequent calls fast-fail without invoking the
        // upstream. Verify both behaviors: (a) upstream-call count stops
        // climbing past the threshold, (b) outcome tag is `circuit_open` for
        // the post-trip call.
        var fakeMgr = new FakeConfigurationManager(_ =>
            throw new InvalidOperationException("upstream down"));
        var options = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
            Jwks = new JwksProviderOptions(circuitBreakerFailureThreshold: 3),
        };
        var provider = new HttpJwksProvider(
            fakeMgr,
            Options.Create(options),
            NullLogger<HttpJwksProvider>.Instance,
            TimeProvider.System);
        var lastTags = new List<IReadOnlyList<KeyValuePair<string, object?>>>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == AuthTelemetry.METER_NAME
                && instrument.Name == "d2.auth.jwks.fetch.duration")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            lock (lastTags)
            {
                lastTags.Add(tags.ToArray());
            }
        });
        listener.Start();

        // Trip the breaker (3 failures).
        for (var i = 0; i < 3; i++)
            await provider.GetKeysAsync();

        var preTripUpstreamCalls = fakeMgr.UpstreamCallCount;

        // Next call should fast-fail without an upstream call.
        var trippedResult = await provider.GetKeysAsync();

        trippedResult.Success.Should().BeFalse();
        trippedResult.ErrorCode.Should().Be("AUTH_JWKS_UNAVAILABLE");
        fakeMgr.UpstreamCallCount.Should().Be(
            preTripUpstreamCalls,
            "circuit-open call must not invoke the upstream");
        lastTags[^1].Should().Contain(t =>
            t.Key == "outcome" && (string)t.Value! == "circuit_open");
    }

    private static HttpJwksProvider MakeProvider(
        IConfigurationManager<OpenIdConnectConfiguration> configMgr,
        TimeProvider? clock = null)
        => new(
            configMgr,
            Options.Create(MakeAuthOptions()),
            NullLogger<HttpJwksProvider>.Instance,
            clock ?? TimeProvider.System);

    private static AuthOptions MakeAuthOptions() => new()
    {
        Issuer = new Uri("https://edge.internal"),
        Audience = "files",
    };

    private static OpenIdConnectConfiguration MakeOidcConfig(params SecurityKey[] keys)
    {
        var config = new OpenIdConnectConfiguration
        {
            JwksUri = "https://edge.internal/.well-known/jwks.json",
        };
        foreach (var key in keys)
            config.SigningKeys.Add(key);
        return config;
    }

    private static RsaSecurityKey MakeRsaKey(string? kid)
    {
        var rsa = RSA.Create(2048);
        return kid is null
            ? new RsaSecurityKey(rsa)
            : new RsaSecurityKey(rsa) { KeyId = kid };
    }

    /// <summary>
    /// In-memory fake for <see cref="IConfigurationManager{T}"/>. Lets tests
    /// drive the snapshot returned + count RequestRefresh calls + simulate
    /// upstream errors.
    /// </summary>
    private sealed class FakeConfigurationManager
        : IConfigurationManager<OpenIdConnectConfiguration>
    {
        private readonly Func<CancellationToken, OpenIdConnectConfiguration> r_get;
        private int _refreshCount;
        private int _upstreamCallCount;

        public FakeConfigurationManager(OpenIdConnectConfiguration initial)
            : this(_ => initial)
        {
        }

        public FakeConfigurationManager(
            Func<CancellationToken, OpenIdConnectConfiguration> get)
        {
            r_get = get;
        }

        public int RefreshRequests => Volatile.Read(ref _refreshCount);

        public int UpstreamCallCount => Volatile.Read(ref _upstreamCallCount);

        public TimeSpan GetConfigurationDelay { get; set; }

        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _upstreamCallCount);
            if (GetConfigurationDelay > TimeSpan.Zero)
                await Task.Delay(GetConfigurationDelay, ct);
            return r_get(ct);
        }

        public void RequestRefresh() => Interlocked.Increment(ref _refreshCount);
    }
}
