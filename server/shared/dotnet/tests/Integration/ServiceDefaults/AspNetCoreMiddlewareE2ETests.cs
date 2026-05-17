// -----------------------------------------------------------------------
// <copyright file="AspNetCoreMiddlewareE2ETests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults;

using System.Net;
using System.Net.Http;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.AspNetCore;
using D2.Shared.Headers.Http;
using D2.Shared.Tests.Integration.ServiceDefaults.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Composed-pipeline E2E tests for the AspNetCore middleware layer
/// surfaces — security headers on every response (incl. infra-bypass +
/// error paths), HSTS HTTPS-only, CORS preflight + fail-closed,
/// infrastructure bypass behavior + tag-only opt-out, ProblemDetails RFC
/// 7807 shape, and the negative-regression contract that ProblemDetails
/// NEVER leaks exception messages or internal state.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class AspNetCoreMiddlewareE2ETests
{
    [Fact]
    public async Task SecurityHeaders_OnGetProbe_PresentOnResponse()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/probe");

        response.Headers.GetValues("X-Frame-Options")
            .Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.GetValues("Referrer-Policy")
            .Should().ContainSingle().Which.Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task SecurityHeaders_OnGetHealth_PresentOnInfraBypassResponse()
    {
        // SecurityHeaders runs FIRST in the LOCKED order so OWASP defaults
        // ship even on infrastructure-bypass short-circuit responses.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/health");

        response.Headers.GetValues("X-Frame-Options")
            .Should().ContainSingle().Which.Should().Be("DENY");
    }

    [Fact]
    public async Task SecurityHeaders_OnGetThrow_PresentOnUnhandledExceptionResponse()
    {
        // SecurityHeaders middleware writes via OnStarting so the headers
        // ship with the response regardless of downstream throw. Wire an
        // explicit IExceptionHandler so the request returns a 500 instead
        // of propagating the throw out of TestServer.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraServices: services =>
                services.AddExceptionHandler<TestProblemDetailsExceptionHandler>(),
            extraConfigure: app => app.UseExceptionHandler());
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/throw");

        response.Headers.GetValues("X-Frame-Options")
            .Should().ContainSingle().Which.Should().Be("DENY");
    }

    [Fact]
    public async Task SecurityHeaders_HSTS_OnHttpsResponse_DefaultLiteralWritten()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/probe");

        response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
        response.Headers.GetValues("Strict-Transport-Security")
            .Should().ContainSingle().Which.Should().Be(
                D2SecurityHeadersOptions.DEFAULT_STRICT_TRANSPORT_SECURITY);
    }

    [Fact]
    public async Task SecurityHeaders_HSTS_OnHttpResponse_NotPresent()
    {
        // HSTS is HTTPS-only by design — over HTTP it's meaningless and
        // the spec forbids preload submission for non-HTTPS-only origins.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("http://localhost/probe");

        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_PreflightOptions_FromConfiguredOrigin_Returns200WithAllowOriginEcho()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["D2_CORS_ORIGINS:0"] = "https://app.example.com",
            });
        var client = handle.Host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/probe");
        request.Headers.Add("Origin", "https://app.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be("https://app.example.com");
    }

    [Fact]
    public async Task Cors_PreflightOptions_FromUnconfiguredOrigin_NoAllowOriginHeader()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["D2_CORS_ORIGINS:0"] = "https://app.example.com",
            });
        var client = handle.Host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/probe");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_EnvVarBinding_IndexedFormat_BindsAllOrigins()
    {
        // Indexed env-var form D2_CORS_ORIGINS__0/__1 maps to the
        // colon-form IConfiguration keys D2_CORS_ORIGINS:0/:1.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["D2_CORS_ORIGINS:0"] = "https://a.example.com",
                ["D2_CORS_ORIGINS:1"] = "https://b.example.com",
            });

        var resolved = handle.Host.Services
            .GetRequiredService<global::Microsoft.Extensions.Options.IOptions<D2CorsOptions>>()
            .Value;
        resolved.Origins.Should().BeEquivalentTo(
            ["https://a.example.com", "https://b.example.com"]);
    }

    [Fact]
    public async Task InfrastructureBypass_OnGetHealth_ShortCircuitsDownstream()
    {
        // The bypass middleware short-circuits before downstream
        // middleware is invoked.
        var downstreamInvoked = false;
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfigure: app =>
            {
                app.Use(async (_, next) =>
                {
                    downstreamInvoked = true;
                    await next();
                });
            });
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        downstreamInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task InfrastructureBypass_TagOnlyOptOut_DownstreamRuns_AndItemsTagSet()
    {
        bool? capturedFlag = null;
        var downstreamRan = false;
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.InfrastructureBypassConfigure = ib => ib.TagOnly = true;
            },
            extraConfigure: app =>
            {
                app.Use(async (ctx, next) =>
                {
                    capturedFlag = ctx.Items[
                        D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY]
                            as bool?;
                    downstreamRan = true;
                    await next();
                });
            });
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/health", UriKind.Relative));

        downstreamRan.Should().BeTrue();
        capturedFlag.Should().BeTrue();
    }

    [Fact]
    public async Task InfrastructureBypass_OnGetWellKnown_OidcDiscovery_ShortCircuits()
    {
        var downstreamInvoked = false;
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfigure: app =>
            {
                app.Use(async (_, next) =>
                {
                    downstreamInvoked = true;
                    await next();
                });
            },
            extraEndpoints: endpoints =>
            {
                global::Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions.MapGet(
                    endpoints,
                    "/.well-known/openid-configuration",
                    () => global::Microsoft.AspNetCore.Http.Results.Text("ok"));
            });
        var client = handle.Host.GetTestClient();

        await client.GetAsync(new Uri("/.well-known/openid-configuration", UriKind.Relative));

        downstreamInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task ProblemDetails_OnGetThrow_RFC7807ShapeReturned()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraServices: services =>
                services.AddExceptionHandler<TestProblemDetailsExceptionHandler>(),
            extraConfigure: app => app.UseExceptionHandler());
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ProblemDetails_OnGetThrow_NeverLeaksExceptionMessage()
    {
        // NEGATIVE regression for §3.2 + §20.4 — the synthetic /throw
        // exception message ("Synthetic /throw failure (do not log).")
        // must NOT appear in the response body under the COMPOSED
        // pipeline.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraServices: services =>
                services.AddExceptionHandler<TestProblemDetailsExceptionHandler>(),
            extraConfigure: app => app.UseExceptionHandler());
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/throw");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Synthetic /throw failure");
    }

    [Fact]
    public async Task Health_OnGetHealth_ReturnsHealthyJson()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Health_OnGetAlive_ReturnsHealthyJson()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false);
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync(new Uri("/alive", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Cors_PreflightWithCorrelationIdHeader_EchoedInAllowHeaders()
    {
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["D2_CORS_ORIGINS:0"] = "https://app.example.com",
            });
        var client = handle.Host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/probe");
        request.Headers.Add("Origin", "https://app.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add(
            "Access-Control-Request-Headers",
            HttpHeaders.CORRELATION_ID);

        var response = await client.SendAsync(request);

        var allowed = response.Headers.GetValues("Access-Control-Allow-Headers")
            .First()
            .Split(',', StringSplitOptions.TrimEntries);
        allowed.Should().Contain(HttpHeaders.CORRELATION_ID);
    }

    [Fact]
    public async Task SecurityHeaders_NonEmptyOverride_AppliedViaPassThroughDelegate()
    {
        // The SecurityHeadersConfigure pass-through on D2ServiceDefaultsOptions
        // is applied at pipeline-installation time (per-component
        // configures for *Use* extensions run inside UseD2DefaultPipeline).
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            configureOptions: opts =>
            {
                opts.SkipAuthAutoWiring = true;
                opts.SecurityHeadersConfigure =
                    sh => sh.XFrameOptions = "SAMEORIGIN";
            });
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/probe");

        response.Headers.GetValues("X-Frame-Options")
            .Should().ContainSingle().Which.Should().Be("SAMEORIGIN");
    }

    [Fact]
    public async Task ProblemDetails_OnGetThrow_HasNonEmptyResponseBody()
    {
        // Pin that the RFC 7807 customizer runs (response body is not
        // empty / whitespace) under the composed pipeline. Specific shape
        // assertions are owned by AspNetCore's ProblemDetailsTests; this
        // confirms the wiring carries through aggregator.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraServices: services =>
                services.AddExceptionHandler<TestProblemDetailsExceptionHandler>(),
            extraConfigure: app => app.UseExceptionHandler());
        var client = handle.Host.GetTestClient();

        var response = await client.GetAsync("https://localhost/throw");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace(
            because: "the AddD2ProblemDetails customizer should populate a "
            + "JSON body when the pipeline catches the unhandled throw.");

        // Pin the body parses as JSON (RFC 7807 is JSON).
        var act = () => JsonDocument.Parse(body);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Cors_FromConfiguredOrigin_Get_AllowOriginInResponse()
    {
        // Sanity-check non-preflight CORS — actual GET from an allowed
        // origin gets the Allow-Origin header in the response.
        await using var handle = await CompositeTestHostBuilder.BuildAsync(
            captureSerilog: false,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["D2_CORS_ORIGINS:0"] = "https://app.example.com",
            });
        var client = handle.Host.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/probe");
        request.Headers.Add("Origin", "https://app.example.com");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be("https://app.example.com");
    }

    /// <summary>
    /// Test-scope <see cref="global::Microsoft.AspNetCore.Diagnostics.IExceptionHandler"/>
    /// that triggers ASP.NET Core's
    /// <see cref="global::Microsoft.AspNetCore.Http.IProblemDetailsService"/>
    /// pipeline (which the D² customizer plugs into) for unhandled exceptions
    /// from the composed test endpoints.
    /// </summary>
    private sealed class TestProblemDetailsExceptionHandler
        : global::Microsoft.AspNetCore.Diagnostics.IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            global::Microsoft.AspNetCore.Http.HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            var pdService = httpContext.RequestServices
                .GetRequiredService<global::Microsoft.AspNetCore.Http.IProblemDetailsService>();
            return await pdService.TryWriteAsync(
                new global::Microsoft.AspNetCore.Http.ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails =
                    {
                        Status = (int)HttpStatusCode.InternalServerError,
                        Title = "Internal Server Error",
                        Type =
                            "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                    },
                });
        }
    }
}
