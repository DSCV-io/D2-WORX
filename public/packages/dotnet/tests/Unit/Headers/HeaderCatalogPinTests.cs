// -----------------------------------------------------------------------
// <copyright file="HeaderCatalogPinTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Headers;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.Headers.Common;
using DcsvIo.D2.Headers.Grpc;
using DcsvIo.D2.Headers.Http;
using Xunit;

/// <summary>
/// Per-VALUE pin against every emitted header constant across all four
/// per-transport catalogs (Common / Http / Amqp / Grpc). One catalog-spanning
/// pin set anchored on the codegen output — drift between the spec and any
/// emitted catalog surfaces here.
/// </summary>
public sealed class HeaderCatalogPinTests
{
    private const BindingFlags _PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;

    [Theory]
    [InlineData(nameof(CommonHeaders.PROPAGATED_CONTEXT), "x-d2-context")]
    [InlineData(nameof(CommonHeaders.TRACEPARENT), "traceparent")]
    [InlineData(nameof(CommonHeaders.TRACESTATE), "tracestate")]
    public void CommonHeaders_PerValuePin(string field, string expected)
    {
        var actual = (string)typeof(CommonHeaders)
            .GetField(field, _PUBLIC_STATIC)!
            .GetValue(null)!;
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(nameof(HttpHeaders.ACCEPT_LANGUAGE), "Accept-Language")]
    [InlineData(nameof(HttpHeaders.AUTHORIZATION), "Authorization")]
    [InlineData(nameof(HttpHeaders.CLIENT_FINGERPRINT), "X-D2-Client-Fingerprint")]
    [InlineData(nameof(HttpHeaders.D2_CURRENCY), "X-D2-Currency")]
    [InlineData(nameof(HttpHeaders.D2_LOCALE), "X-D2-Locale")]
    [InlineData(nameof(HttpHeaders.D2_TIMEZONE), "X-D2-Timezone")]
    [InlineData(nameof(HttpHeaders.IDEMPOTENCY_KEY), "Idempotency-Key")]
    [InlineData(nameof(HttpHeaders.INTERNAL_TOKEN), "X-D2-Internal-Token")]
    [InlineData(nameof(HttpHeaders.PROPAGATED_CONTEXT), "x-d2-context")]
    [InlineData(nameof(HttpHeaders.TRACEPARENT), "traceparent")]
    [InlineData(nameof(HttpHeaders.TRACESTATE), "tracestate")]
    public void HttpHeaders_PerValuePin(string field, string expected)
    {
        var actual = (string)typeof(HttpHeaders)
            .GetField(field, _PUBLIC_STATIC)!
            .GetValue(null)!;
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(nameof(AmqpHeaders.CONTENT_TYPE), "content-type")]
    [InlineData(nameof(AmqpHeaders.ENCRYPTION_KID), "x-d2-encryption-kid")]
    [InlineData(nameof(AmqpHeaders.FAILURE_REASON), "x-d2-failure-reason")]
    [InlineData(nameof(AmqpHeaders.MESSAGE_ID), "message-id")]
    [InlineData(nameof(AmqpHeaders.PROPAGATED_CONTEXT), "x-d2-context")]
    [InlineData(nameof(AmqpHeaders.PROTO_TYPE), "x-proto-type")]
    [InlineData(nameof(AmqpHeaders.TIMESTAMP), "timestamp")]
    [InlineData(nameof(AmqpHeaders.TRACEPARENT), "traceparent")]
    [InlineData(nameof(AmqpHeaders.TRACESTATE), "tracestate")]
    public void AmqpHeaders_PerValuePin(string field, string expected)
    {
        var actual = (string)typeof(AmqpHeaders)
            .GetField(field, _PUBLIC_STATIC)!
            .GetValue(null)!;
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(nameof(GrpcHeaders.AUTHORIZATION), "Authorization")]
    [InlineData(nameof(GrpcHeaders.PROPAGATED_CONTEXT), "x-d2-context")]
    [InlineData(nameof(GrpcHeaders.TRACEPARENT), "traceparent")]
    [InlineData(nameof(GrpcHeaders.TRACESTATE), "tracestate")]
    public void GrpcHeaders_PerValuePin(string field, string expected)
    {
        var actual = (string)typeof(GrpcHeaders)
            .GetField(field, _PUBLIC_STATIC)!
            .GetValue(null)!;
        actual.Should().Be(expected);
    }
}
