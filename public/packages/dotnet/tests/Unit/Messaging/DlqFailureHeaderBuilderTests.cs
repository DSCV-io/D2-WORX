// -----------------------------------------------------------------------
// <copyright file="DlqFailureHeaderBuilderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Result;
using Xunit;

public sealed class DlqFailureHeaderBuilderTests
{
    [Fact]
    public void FromException_BuildsHandlerExceptionMetadata()
    {
        // PII guard: exception.Message is NOT propagated into the DLQ header —
        // handler-built strings can interpolate user input.
        // ErrorCode carries the exception type (developer-controlled);
        // Detail stays null.
        var ex = new InvalidOperationException("boom");
        var bytes = DlqFailureHeaderBuilder.FromException(
            ex, attemptCount: 3, traceId: "abcdef", nackedBy: "audit");

        var meta = Decode(bytes);
        meta.Cause.Should().Be(DlqFailureCauses.HANDLER_EXCEPTION);
        meta.ErrorCode.Should().Be(typeof(InvalidOperationException).FullName);
        meta.Detail.Should().BeNull("ex.Message must not leak into broker header");
        meta.AttemptCount.Should().Be(3);
        meta.TraceId.Should().Be("abcdef");
        meta.NackedBy.Should().Be("audit");
    }

    [Fact]
    public void FromException_LongMessage_DetailRemainsNull()
    {
        // PII guard: even arbitrarily long ex.Message is dropped, not truncated.
        var longMsg = new string('x', 1_000);
        var ex = new InvalidOperationException(longMsg);
        var bytes = DlqFailureHeaderBuilder.FromException(ex);

        var meta = Decode(bytes);
        meta.Detail.Should().BeNull();
    }

    [Fact]
    public void FromException_NullExArg_Throws()
    {
        var act = () => DlqFailureHeaderBuilder.FromException(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromResult_BuildsHandlerResultFailureMetadata()
    {
        var result = D2Result.NotFound();
        var bytes = DlqFailureHeaderBuilder.FromResult(
            result, attemptCount: 1, traceId: "t-1");

        var meta = Decode(bytes);
        meta.Cause.Should().Be(DlqFailureCauses.HANDLER_RESULT_FAILURE);
        meta.ErrorCode.Should().NotBeNullOrWhiteSpace();
        meta.AttemptCount.Should().Be(1);
        meta.TraceId.Should().Be("t-1");
    }

    [Fact]
    public void FromResult_NullArg_Throws()
    {
        var act = () => DlqFailureHeaderBuilder.FromResult(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromBoundary_DecryptCause_Works()
    {
        // PII guard: same protection for boundary failures — type only, no message.
        var ex = new InvalidOperationException("bad frame");
        var bytes = DlqFailureHeaderBuilder.FromBoundary(
            DlqFailureCauses.DECRYPT_FAILURE, ex, traceId: "trc");

        var meta = Decode(bytes);
        meta.Cause.Should().Be(DlqFailureCauses.DECRYPT_FAILURE);
        meta.ErrorCode.Should().Be(typeof(InvalidOperationException).FullName);
        meta.Detail.Should().BeNull();
        meta.TraceId.Should().Be("trc");
        meta.AttemptCount.Should().Be(0);
    }

    [Fact]
    public void FromBoundary_DeserializeCause_Works()
    {
        var ex = new JsonException("malformed");
        var bytes = DlqFailureHeaderBuilder.FromBoundary(
            DlqFailureCauses.DESERIALIZE_FAILURE, ex);

        var meta = Decode(bytes);
        meta.Cause.Should().Be(DlqFailureCauses.DESERIALIZE_FAILURE);
    }

    [Fact]
    public void FromBoundary_NullCause_Throws()
    {
        var act = () => DlqFailureHeaderBuilder.FromBoundary(
            null!, new InvalidOperationException());
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void FromBoundary_EmptyOrWhitespaceCause_Throws(string cause)
    {
        var act = () => DlqFailureHeaderBuilder.FromBoundary(
            cause, new InvalidOperationException());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromBoundary_NullExArg_Throws()
    {
        var act = () => DlqFailureHeaderBuilder.FromBoundary("X", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromResult_DetailKeyOnly_NeverInterpolatedText()
    {
        // FromResult's Detail joins Message KEYS (translation tokens —
        // developer-controlled constants), never user-input-interpolated
        // strings. The 256-char truncation still applies — it's a safety
        // net for accidentally-long key joins, not a PII guard.
        var result = D2Result.NotFound();
        var bytes = DlqFailureHeaderBuilder.FromResult(result);
        var meta = Decode(bytes);
        meta.Detail.Should().NotBeNull();
        (meta.Detail!.Length <= 256).Should().BeTrue();
    }

    [Fact]
    public void EncodedBytes_AreUtf8Json()
    {
        var bytes = DlqFailureHeaderBuilder.FromException(new InvalidOperationException("hi"));
        var json = Encoding.UTF8.GetString(bytes);
        json.Should().Contain("\"cause\"");
        json.Should().Contain("\"errorCode\"");
    }

    [Fact]
    public void FromRetriesExhausted_SetsCorrectCauseAndAttemptCount()
    {
        var bytes = DlqFailureHeaderBuilder.FromRetriesExhausted(
            attemptCount: 5, traceId: "abcdef", nackedBy: "audit");
        var meta = Decode(bytes);
        meta.Cause.Should().Be(DlqFailureCauses.RETRIES_EXHAUSTED);
        meta.ErrorCode.Should().Be(DlqFailureCauses.RETRIES_EXHAUSTED);
        meta.Detail.Should().BeNull();
        meta.AttemptCount.Should().Be(5);
        meta.TraceId.Should().Be("abcdef");
        meta.NackedBy.Should().Be("audit");
    }

    [Fact]
    public void FromRetriesExhausted_AttemptCountZero_StillValid()
    {
        var bytes = DlqFailureHeaderBuilder.FromRetriesExhausted(attemptCount: 0);
        var meta = Decode(bytes);
        meta.Cause.Should().Be(DlqFailureCauses.RETRIES_EXHAUSTED);
        meta.AttemptCount.Should().Be(0);
    }

    private static DlqFailureMetadata Decode(byte[] bytes)
    {
        return JsonSerializer.Deserialize<DlqFailureMetadata>(
            bytes, MessagingJsonOptions.Options)
            ?? throw new InvalidOperationException("failed to decode header");
    }
}
