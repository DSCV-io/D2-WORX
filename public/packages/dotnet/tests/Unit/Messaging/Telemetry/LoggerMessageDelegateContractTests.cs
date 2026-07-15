// -----------------------------------------------------------------------
// <copyright file="LoggerMessageDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Telemetry;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using Xunit;

/// <summary>
/// PII-safety contract for the messaging-stack <c>[LoggerMessage]</c> log
/// delegates. Several real exceptions in this stack carry sensitive content
/// in their <c>Message</c> property — most notably
/// <c>BrokerUnreachableException</c>, which embeds the AMQP URI password.
/// Sinks (Serilog / OpenTelemetry / etc.) format the exception via
/// <c>ex.ToString()</c> and persist the resulting string verbatim. The
/// delegates that observe these exceptions must therefore take a
/// <c>string exType</c> + <c>string? where</c> instead of <c>Exception</c>.
/// </summary>
public sealed class LoggerMessageDelegateContractTests
{
    public static TheoryData<Type, string> LeakProneLogDelegates => new()
    {
        { typeof(MessagingLog), nameof(MessagingLog.PublishTransientFailure) },
        { typeof(MessagingLog), nameof(MessagingLog.PublishTerminalFailure) },
        { typeof(RabbitMqConnectionLog), nameof(RabbitMqConnectionLog.ReconnectAttemptFailed) },
        { typeof(RabbitMqConnectionLog), nameof(RabbitMqConnectionLog.ConnectionCloseFailed) },
        { typeof(ChannelPoolLog), nameof(ChannelPoolLog.ChannelCloseFailed) },
        { typeof(SubscriberLog), nameof(SubscriberLog.HandlerThrew) },
        { typeof(SubscriberLog), nameof(SubscriberLog.BoundaryFailure) },
        { typeof(SubscriberLog), nameof(SubscriberLog.DlqRepublishFailed) },
        { typeof(SubscriberLog), nameof(SubscriberLog.AckFailed) },
        { typeof(TopologyLog), nameof(TopologyLog.DeclarationFailed) },
    };

    [Theory]
    [MemberData(nameof(LeakProneLogDelegates))]
    public void LeakProneLogDelegate_DoesNotTakeRawException(Type logType, string method)
    {
        var info = logType.GetMethod(
            method, BindingFlags.Public | BindingFlags.Static);
        info.Should().NotBeNull("log delegate must exist on the partial class");

        var hasExceptionParam = info.GetParameters()
            .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType));

        hasExceptionParam.Should().BeFalse(
            $"{logType.Name}.{method} must take string exType, not Exception — "
            + "passing Exception causes log sinks to format ex.ToString() and "
            + "leak ex.Message contents (e.g. AMQP URI password from "
            + "BrokerUnreachableException) into structured logs.");
    }

    [Fact]
    public void HostStartupFaulted_AcceptsExceptionForFaultLogger()
    {
        // HostStartupFaulted is the ContinueWith-only sink for an unobserved
        // background-task fault, and the operator needs the full stack to
        // debug. This fault path never sees user-input-derived exceptions,
        // so the PII guard doesn't apply. Pin the design choice so a future
        // "consistency" refactor doesn't strip it.
        var info = typeof(SubscriberLog).GetMethod(
            nameof(SubscriberLog.HostStartupFaulted),
            BindingFlags.Public | BindingFlags.Static);
        info.Should().NotBeNull();
        info.GetParameters()
            .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType))
            .Should().BeTrue();
    }

    [Fact]
    public void DeclarationFailedFaultSink_AcceptsExceptionForFaultLogger()
    {
        // DeclarationFailedFaultSink is the ContinueWith fault sink for the
        // background topology-declaration task in TopologyHostedService.
        // Same rationale as HostStartupFaulted — operator needs full stack
        // for an unobserved background fault. The INLINE catch path in
        // DefaultTopologyDeclarer uses the sanitized DeclarationFailed
        // delegate (pinned in LeakProneLogDelegates above) instead, since
        // OperationInterruptedException.Message from RabbitMQ.Client can
        // include broker-side text.
        var info = typeof(TopologyLog).GetMethod(
            nameof(TopologyLog.DeclarationFailedFaultSink),
            BindingFlags.Public | BindingFlags.Static);
        info.Should().NotBeNull();
        info.GetParameters()
            .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType))
            .Should().BeTrue();
    }
}
