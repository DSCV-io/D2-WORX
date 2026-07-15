// -----------------------------------------------------------------------
// <copyright file="ToTransportFaultResultTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result.Grpc;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.Result.Grpc;
using global::Grpc.Core;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Pins <see cref="ProtoExtensions.ToTransportFaultResult{TData}"/> — the SHARED
/// <see cref="RpcException"/> → <c>D2Result</c> transport-fault mapping that both
/// <c>HandleAsync</c> and the generated gRPC client use.
/// <para>
/// Regression intent: a transport <see cref="RpcException"/> that falls through a
/// gRPC-agnostic resilience pipeline must map to <c>ServiceUnavailable</c> (503,
/// "downstream unavailable"), NOT <c>UnhandledException</c> (500, "bug in caller's
/// own logic"). 500 would be wrong: the caller is fine, the peer faulted. The
/// generated client captures the thrown <see cref="RpcException"/> and remaps it via
/// THIS method so the code is consistent with the <c>HandleAsync</c> path. Cancelled
/// (gRPC code 1) maps to <c>Canceled</c>.
/// </para>
/// </summary>
public sealed class ToTransportFaultResultTests
{
    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.ResourceExhausted)]
    [InlineData(StatusCode.Aborted)]
    [InlineData(StatusCode.InvalidArgument)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unauthenticated)]
    public void NonCancelled_MapsToServiceUnavailable_Not500(StatusCode code)
    {
        var ex = new RpcException(new Status(code, "transport detail"));

        var result = ex.ToTransportFaultResult<string?>();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.ServiceUnavailable,
            "a transport RpcException is downstream-unavailable (503), never a caller bug (500)");
        result.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public void Cancelled_MapsToCanceled()
    {
        var ex = new RpcException(new Status(StatusCode.Cancelled, string.Empty));

        var result = ex.ToTransportFaultResult<string?>();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "Canceled maps to 400 per the D2Result.Canceled factory");
    }

    [Fact]
    public void TraceId_ThreadsIntoResult()
    {
        const string trace = "trace-xyz-123";
        var ex = new RpcException(new Status(StatusCode.Unavailable, string.Empty));

        var result = ex.ToTransportFaultResult<string?>(traceId: trace);

        result.TraceId.Should().Be(trace);
    }

    [Fact]
    public void NullLogger_DoesNotThrow()
    {
        var ex = new RpcException(new Status(StatusCode.Unavailable, string.Empty));

        var act = () => ex.ToTransportFaultResult<string?>(logger: null);

        act.Should().NotThrow();
    }

    [Fact]
    public void ExceptionMessage_NeverLeaksIntoResultMessages()
    {
        const string sentinel_secret = "BROKER_PASSWORD_secret_should_not_leak_42";
        var ex = new RpcException(new Status(StatusCode.Unavailable, sentinel_secret));

        var result = ex.ToTransportFaultResult<string?>();

        foreach (var msg in result.Messages)
            msg.Key.Should().NotContain(sentinel_secret, "ex.Message must never reach result.Messages");
    }

    [Fact]
    public void LogOutput_DoesNotContainExMessage_ContainsTypeNameAndStatus()
    {
        const string sentinel_secret = "AMQP_secret_token_should_not_log_99";
        var ex = new RpcException(new Status(StatusCode.Internal, sentinel_secret));
        var logger = new CapturingLogger();

        ex.ToTransportFaultResult<string?>(logger, "trace-1");

        logger.LoggedMessages.Should().NotBeEmpty("the transport fault was logged");
        var combined = string.Join(" ", logger.LoggedMessages);
        combined.Should().NotContain(sentinel_secret, "ex.Message must not appear in any log output");
        combined.Should().Contain("RpcException", "the sanitized type name is logged for diagnostics");
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> r_messages = [];

        public IReadOnlyList<string> LoggedMessages => r_messages;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => r_messages.Add(formatter(state, exception));
    }
}
