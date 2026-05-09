// -----------------------------------------------------------------------
// <copyright file="AmqpHeadersTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging;

using AwesomeAssertions;
using D2.Shared.Messaging;
using Xunit;

/// <summary>
/// Pin the canonical header names. These appear on every AMQP message;
/// changing them silently breaks every consumer.
/// </summary>
public sealed class AmqpHeadersTests
{
    [Fact]
    public void ContentType_HasExpectedValue()
        => AmqpHeaders.CONTENT_TYPE.Should().Be("content-type");

    [Fact]
    public void ProtoType_HasExpectedValue()
        => AmqpHeaders.PROTO_TYPE.Should().Be("x-proto-type");

    [Fact]
    public void MessageId_HasExpectedValue()
        => AmqpHeaders.MESSAGE_ID.Should().Be("message-id");

    [Fact]
    public void Timestamp_HasExpectedValue()
        => AmqpHeaders.TIMESTAMP.Should().Be("timestamp");

    [Fact]
    public void Traceparent_HasExpectedValue()
        => AmqpHeaders.TRACEPARENT.Should().Be("traceparent");

    [Fact]
    public void Tracestate_HasExpectedValue()
        => AmqpHeaders.TRACESTATE.Should().Be("tracestate");

    [Fact]
    public void EncryptionKid_HasExpectedValue()
        => AmqpHeaders.ENCRYPTION_KID.Should().Be("x-d2-encryption-kid");

    [Fact]
    public void FailureReason_HasExpectedValue()
        => AmqpHeaders.FAILURE_REASON.Should().Be("x-d2-failure-reason");
}
