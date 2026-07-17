// -----------------------------------------------------------------------
// <copyright file="DlqFailureCausesRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the DlqFailureCauses closed-set value
/// catalog. Spec-driving the cause NAMES (via the DlqFailureMetadata
/// SourceGen) closes the name-level drift surface; this suite closes the
/// value-level drift surface — each enumerated cause is shipped through
/// the production DlqFailureHeaderBuilder emit paths and verified to land
/// in the wire-serialized header byte-for-byte. A refactor breaking any
/// emit path (e.g. dropping DECRYPT_FAILURE wiring on the body-decode
/// boundary) would NOT fail any name-only test, but breaks DLQ ops
/// tooling that filters on the cause value.
/// </summary>
public sealed class DlqFailureCausesRuntimeEmissionTests
{
    [Fact]
    public void HandlerResultFailure_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel dispatches a handler that
        // returns a non-Ok D2Result → DlqFailureHeaderBuilder.FromResult.
        var bytes = DlqFailureHeaderBuilder.FromResult(D2Result.NotFound());
        ReadCauseFromHeader(bytes)
            .Should().Be("HANDLER_RESULT_FAILURE");
    }

    [Fact]
    public void HandlerException_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel catches an unhandled
        // exception from the handler → DlqFailureHeaderBuilder.FromException.
        var bytes = DlqFailureHeaderBuilder.FromException(
            new InvalidOperationException("ignored"));
        ReadCauseFromHeader(bytes)
            .Should().Be("HANDLER_EXCEPTION");
    }

    [Fact]
    public void DecryptFailure_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel's body-decode boundary
        // wraps a decrypt exception → DlqFailureHeaderBuilder.FromBoundary
        // with cause=DECRYPT_FAILURE.
        var bytes = DlqFailureHeaderBuilder.FromBoundary(
            DlqFailureCauses.DECRYPT_FAILURE,
            new InvalidOperationException("bad frame"));
        ReadCauseFromHeader(bytes)
            .Should().Be("DECRYPT_FAILURE");
    }

    [Fact]
    public void DeserializeFailure_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel's body-decode boundary
        // wraps a deserialize exception → DlqFailureHeaderBuilder.FromBoundary
        // with cause=DESERIALIZE_FAILURE.
        var bytes = DlqFailureHeaderBuilder.FromBoundary(
            DlqFailureCauses.DESERIALIZE_FAILURE,
            new JsonException("malformed"));
        ReadCauseFromHeader(bytes)
            .Should().Be("DESERIALIZE_FAILURE");
    }

    [Fact]
    public void RetriesExhausted_CauseValueLandsInSerializedHeader()
    {
        // Production emit path: SubscriberChannel detects x-death attempts
        // exceeded MaxAttempts BEFORE invoking the handler →
        // DlqFailureHeaderBuilder.FromRetriesExhausted.
        var bytes = DlqFailureHeaderBuilder.FromRetriesExhausted(attemptCount: 5);
        ReadCauseFromHeader(bytes)
            .Should().Be("RETRIES_EXHAUSTED");
    }

    [Theory]
    [InlineData("HANDLER_RESULT_FAILURE")]
    [InlineData("HANDLER_EXCEPTION")]
    [InlineData("DECRYPT_FAILURE")]
    [InlineData("DESERIALIZE_FAILURE")]
    [InlineData("RETRIES_EXHAUSTED")]
    public void EveryCatalogValue_HasMatchingConstant(string expectedValue)
    {
        // §21.10 catalog completeness: the 5 closed-set values enumerated
        // in contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json
        // each have a matching DlqFailureCauses constant whose value is
        // the literal wire string. Drift here would break ops dashboards
        // that filter on the cause value.
        var actualValue = expectedValue switch
        {
            "HANDLER_RESULT_FAILURE" => DlqFailureCauses.HANDLER_RESULT_FAILURE,
            "HANDLER_EXCEPTION" => DlqFailureCauses.HANDLER_EXCEPTION,
            "DECRYPT_FAILURE" => DlqFailureCauses.DECRYPT_FAILURE,
            "DESERIALIZE_FAILURE" => DlqFailureCauses.DESERIALIZE_FAILURE,
            "RETRIES_EXHAUSTED" => DlqFailureCauses.RETRIES_EXHAUSTED,
            _ => throw new InvalidOperationException("unmapped"),
        };
        actualValue.Should().Be(expectedValue);
    }

    private static string ReadCauseFromHeader(byte[] bytes)
    {
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("cause").GetString()
            ?? throw new InvalidOperationException("cause was null");
    }
}
