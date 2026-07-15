// -----------------------------------------------------------------------
// <copyright file="EstablishmentLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Telemetry;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Auth.Grpc.Telemetry;
using D2.Shared.Auth.Http.Telemetry;
using D2.Shared.Context.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// PII-safety + emission contract for the request-context establishment
/// <c>[LoggerMessage]</c> delegates — <c>RequestOriginGrpcLog.CallPathReceived</c>
/// (EventId 4105), <c>RequestOriginEdgeLog.CallPathStarted</c> (4102), and
/// <c>RequestOriginGrpcLog.RequestOriginUnestablishedDenied</c> (4106). Each
/// establishment boundary logs a hop-count summary at Debug; an Exception parameter
/// on any of them could interpolate JWT bytes / request URIs / configured secrets
/// into the log pipeline, so the no-Exception contract is pinned assembly-wide (also
/// covering <c>SystemRequestContextBootstrapLog.SystemContextEstablished</c>, 4103),
/// and each shared delegate's emission (EventId + non-PII args) is asserted directly.
/// </summary>
/// <remarks>
/// Mirrors <c>AuthLogDelegateContractTests</c> (reflection no-Exception contract) and
/// the capability-authority emission tests (log-capture EventId assertions). The
/// assembly-wide scan covers any future delegate added to the auth / context
/// establishment assemblies by construction. The System-bootstrap delegate's own
/// emission is asserted in <c>SystemRequestContextBootstrapLogTests</c> (context area).
/// </remarks>
public sealed class EstablishmentLogDelegateContractTests
{
    [Fact]
    public void EstablishmentLogDelegates_AcceptNoExceptionParameter()
    {
        // A [LoggerMessage] delegate is a static method whose first parameter is an
        // ILogger. Scanning the three establishment-bearing assemblies by that shape
        // pins the no-Exception contract for the known delegates AND any future one.
        Assembly[] assemblies =
        [
            typeof(RequestOriginGrpcLog).Assembly,
            typeof(RequestOriginEdgeLog).Assembly,
            typeof(SystemRequestContextBootstrapLog).Assembly,
        ];

        var logDelegates = assemblies
            .SelectMany(SafeGetTypes)
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(m => m.GetParameters() is { Length: > 0 } ps
                && typeof(ILogger).IsAssignableFrom(ps[0].ParameterType))
            .ToList();

        logDelegates.Select(m => m.Name).Should().Contain(
            ["CallPathReceived", "CallPathStarted", "SystemContextEstablished"],
            "the scan must discover the three establishment log delegates "
            + "(guards against a vacuously-passing empty scan)");

        var leakProne = logDelegates
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        leakProne.Should().BeEmpty(
            "establishment [LoggerMessage] delegates must not accept Exception parameters; "
            + "exception messages can leak JWT bytes / URIs / configured secrets into logs. "
            + "Offending delegates: " + string.Join(", ", leakProne));
    }

    [Fact]
    public void CallPathReceived_EmitsAtEventId4105_WithHopCountAndServiceId()
    {
        var logger = new CapturingLogger();

        logger.CallPathReceived(hopCount: 3, selfServiceId: "edge");

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(
            4105,
            "the cross-process establishment hop logs at 4105 (4101 is the "
            + "AuthEndpointGuardStartupFilter's; the prior 4101 here was a collision)");
        entry.Level.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("edge").And.Contain("3");
    }

    [Fact]
    public void CallPathStarted_EmitsAtEventId4102_WithHopCountAndServiceId()
    {
        var logger = new CapturingLogger();

        logger.CallPathStarted(hopCount: 1, selfServiceId: "edge");

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(4102, "the Edge-inbound establishment start logs at 4102");
        entry.Level.Should().Be(LogLevel.Debug);
        entry.Message.Should().Contain("edge").And.Contain("1");
    }

    [Fact]
    public void CrossProcessPeerIdentityAbsent_EmitsAtEventId4104_AtWarning_WithServiceId()
    {
        var logger = new CapturingLogger();

        logger.CrossProcessPeerIdentityAbsent(selfServiceId: "key-custodian");

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(
            4104, "an absent mTLS peer identity on a cross-process hop logs at 4104");
        entry.Level.Should().Be(
            LogLevel.Warning, "a misconfigured non-mTLS hop is a Warning, not silent");
        entry.Message.Should().Contain("key-custodian");
    }

    [Fact]
    public void RequestOriginUnestablishedDenied_EmitsAtEventId4106_AtWarning()
    {
        var logger = new CapturingLogger();

        logger.RequestOriginUnestablishedDenied();

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(
            4106,
            "platform Unestablished product-gRPC deny logs at 4106");
        entry.Level.Should().Be(
            LogLevel.Warning,
            "fail-closed origin deny is a Warning for operators");
        entry.Message.Should().Contain("AUTH_REQUEST_ORIGIN_UNESTABLISHED");
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Select(t => t!);
        }
    }

    /// <summary>Thread-safe capturing logger for asserting log entries by EventId.</summary>
    private sealed class CapturingLogger : ILogger
    {
        public ConcurrentQueue<(LogLevel Level, EventId EventId, string Message)> Entries { get; }
            = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((logLevel, eventId, formatter(state, exception)));
    }
}
