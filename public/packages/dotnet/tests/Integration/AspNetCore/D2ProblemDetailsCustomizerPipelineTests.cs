// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsCustomizerPipelineTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.I18n;
using DcsvIo.D2.ProblemDetails;
using DcsvIo.D2.Result;
using DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests for the D2Result-aware path B handshake:
/// <see cref="HttpContextD2ResultExtensions.SetD2Result"/> →
/// <c>IProblemDetailsService.TryWriteAsync</c> →
/// <c>D2ProblemDetailsCustomizer.Apply</c> → response body.
///
/// Pins the full pipeline composition surface end-to-end through
/// <c>Microsoft.AspNetCore.TestHost</c> so a future change to the wiring
/// (slot key drift, customizer registration order, missing
/// <c>IProblemDetailsService</c> registration, etc.) fails LOUDLY rather
/// than silently degrading to the unhandled-exception path.
/// </summary>
public sealed class D2ProblemDetailsCustomizerPipelineTests
{
    [Fact]
    public async Task SetD2ResultThenProblemDetailsService_PopulatesShapeAFromSpec()
    {
        using var host = await BuildHostWithStashingEndpointAsync(
            path: "/api/not-found",
            statusCode: HttpStatusCode.NotFound,
            errorCode: "WHOIS_NOT_FOUND");

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/not-found");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Type / Title / Status from spec-derived constants (D2Result-aware path).
        root.GetProperty("type").GetString()
            .Should().Be(D2ProblemDetailsKeys.TYPE_URI_PREFIX + "whois-not-found");
        root.GetProperty("title").GetString()
            .Should().Be(D2ProblemDetailsKeys.TitleFor(HttpStatusCode.NotFound));
        root.GetProperty("status").GetInt32().Should().Be(404);

        // Instance set unconditionally — covers the customizer's outer Apply()
        // path regardless of whether a D2Result was stashed.
        root.GetProperty("instance").GetString().Should().Be("GET /api/not-found");

        // Extensions[d2_error_code] / [d2_messages] from D2Result-aware path.
        root.GetProperty(D2ProblemDetailsKeys.EXTENSION_ERROR_CODE).GetString()
            .Should().Be("WHOIS_NOT_FOUND");
        root.TryGetProperty(D2ProblemDetailsKeys.EXTENSION_MESSAGES, out _)
            .Should().BeTrue();

        // Extensions[traceId] / [correlationId] populated unconditionally.
        root.GetProperty(D2ProblemDetailsKeys.EXTENSION_TRACE_ID).GetString()
            .Should().NotBeNullOrEmpty();
        root.GetProperty(D2ProblemDetailsKeys.EXTENSION_CORRELATION_ID).GetString()
            .Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SetD2ResultWithInputErrors_EmitsInputErrorsExtension()
    {
        using var host = await BuildHostWithStashingEndpointAsync(
            path: "/api/validate",
            statusCode: HttpStatusCode.BadRequest,
            errorCode: "VALIDATION_FAILED",
            inputErrors:
            [
                new InputError("email", [TK.Common.Errors.NOT_FOUND]),
            ]);

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/validate");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty(D2ProblemDetailsKeys.EXTENSION_INPUT_ERRORS)
            .ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task SetD2ResultWithoutInputErrors_OmitsInputErrorsExtension()
    {
        using var host = await BuildHostWithStashingEndpointAsync(
            path: "/api/unauthorized",
            statusCode: HttpStatusCode.Unauthorized,
            errorCode: "AUTH_BEARER_MISSING");

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/unauthorized");

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        doc.RootElement.TryGetProperty(
            D2ProblemDetailsKeys.EXTENSION_INPUT_ERRORS, out _)
            .Should().BeFalse();
    }

    [Fact]
    public async Task SetD2ResultThenProblemDetailsService_KebabCasesErrorCodeIntoTypeUri()
    {
        using var host = await BuildHostWithStashingEndpointAsync(
            path: "/api/conflict",
            statusCode: HttpStatusCode.Conflict,
            errorCode: "ENTITY_VERSION_CONFLICT");

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/conflict");

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        // Per-VALUE pin on the kebab-case transformation through the full
        // pipeline (regression on the customizer's KebabCase() helper or
        // a future refactor that drops it).
        doc.RootElement.GetProperty("type").GetString()
            .Should().Be(D2ProblemDetailsKeys.TYPE_URI_PREFIX + "entity-version-conflict");
    }

    private static async Task<global::Microsoft.Extensions.Hosting.IHost>
        BuildHostWithStashingEndpointAsync(
            string path,
            HttpStatusCode statusCode,
            string errorCode,
            IReadOnlyList<InputError>? inputErrors = null)
    {
        return await AspNetCoreTestHostBuilder.BuildAsync(
            extraServices: services =>
            {
                services.AddD2ProblemDetails();
            },
            extraEndpoints: endpoints =>
            {
                endpoints.MapGet(path, async httpContext =>
                {
                    var result = D2Result.Fail(
                        messages: [TK.Common.Errors.NOT_FOUND],
                        inputErrors: inputErrors,
                        errorCode: errorCode,
                        statusCode: statusCode);

                    httpContext.SetD2Result(result);
                    httpContext.Response.StatusCode = (int)statusCode;

                    var problemDetailsService = httpContext.RequestServices
                        .GetRequiredService<IProblemDetailsService>();

                    // Pre-populate Status so IProblemDetailsService accepts the
                    // write; the customizer overwrites Type/Title/Status from
                    // the stashed D2Result regardless of the seed values.
                    await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = httpContext,
                        ProblemDetails =
                        {
                            Status = (int)statusCode,
                        },
                    });
                });
            });
    }
}
