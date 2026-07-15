// -----------------------------------------------------------------------
// <copyright file="HandleAsyncTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result.Grpc;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.Result;
using DcsvIo.D2.Result.Grpc;
using global::D2.Services.Protos.Common.V1;
using global::Grpc.Core;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Tests for <c>HandleAsync</c> — the resilience wrapper around
/// <see cref="AsyncUnaryCall{TResponse}"/>. Covers success re-materialization,
/// <see cref="RpcException"/> fail-open, generic-exception fail-open, and the
/// v1-bug-fix regression: exception messages must NEVER appear in the returned
/// result messages or log output.
/// </summary>
public sealed class HandleAsyncTests
{
    // ── Success path ──────────────────────────────────────────────────────

    [Fact]
    public async Task Success_ReMaterializes_D2Result()
    {
        var response = MakeResponse(D2Result.Ok());
        response.Data = "the-data";
        var call = MakeCall(response);

        var result = await call.HandleAsync(r => r.Result!, r => r.Data);

        result.Success.Should().BeTrue();
        result.Data.Should().Be("the-data");
    }

    [Fact]
    public async Task Success_NotFound_ReMaterializes_CategoryAndStatus()
    {
        var response = MakeResponse(D2Result.NotFound());
        var call = MakeCall(response);

        var result = await call.HandleAsync(r => r.Result!, r => r.Data);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.Category.Should().Be(ErrorCategory.NotFound);
    }

    // ── RpcException → ServiceUnavailable ────────────────────────────────

    [Theory]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.ResourceExhausted)]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.PermissionDenied)]
    public async Task RpcException_NonCancelled_Returns_ServiceUnavailable(StatusCode code)
    {
        var rpcEx = new RpcException(new Status(code, "transport detail"));
        var call = MakeFailedCall(rpcEx);

        var result = await call.HandleAsync(r => r.Result!, r => r.Data);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    // ── RpcException Cancelled → Canceled ────────────────────────────────

    [Fact]
    public async Task RpcException_Cancelled_Returns_Canceled()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Cancelled, string.Empty));
        var call = MakeFailedCall(rpcEx);

        var result = await call.HandleAsync(r => r.Result!, r => r.Data);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Canceled maps to 400 per D2Result.Canceled factory");
    }

    // ── OperationCanceledException → Canceled ─────────────────────────────

    [Fact]
    public async Task OperationCanceledException_Returns_Canceled()
    {
        // FIX-1 regression: raw OCE (never reached the gRPC layer as RpcException)
        // must map to Canceled, not UnhandledException (500).
        const string sentinel_secret = "CANCEL_SENTINEL_should_not_leak_abc123";
        var oce = new OperationCanceledException(sentinel_secret);
        var call = MakeFailedCall(oce);
        var logger = new CapturingLogger();

        var result = await call.HandleAsync(r => r.Result!, r => r.Data, logger, traceId: "trace-oce");

        // Status
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Canceled maps to 400 per D2Result.Canceled factory");
        result.Category.Should().Be(ErrorCategory.ValidationFailure, "Canceled category is ValidationFailure");

        // TraceId threads through
        result.TraceId.Should().Be("trace-oce");

        // ex.Message (the sentinel) must NOT appear in messages or logs
        foreach (var msg in result.Messages.Select(m => m.Key))
            msg.Should().NotContain(sentinel_secret, "ex.Message must never reach result.Messages");

        var combined = string.Join(" ", logger.LoggedMessages);
        combined.Should().NotContain(sentinel_secret, "ex.Message must not appear in any log output");
        combined.Should().Contain("OperationCanceledException", "type name is logged for diagnostics");
    }

    // ── Generic exception → UnhandledException ────────────────────────────

    [Fact]
    public async Task GenericException_Returns_UnhandledException()
    {
        var call = MakeFailedCall(new InvalidOperationException("generic failure"));

        var result = await call.HandleAsync(r => r.Result!, r => r.Data);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── v1-BUG-FIX REGRESSION: ex.Message must never leak ────────────────
    // v1's [LoggerMessage] accepted Exception which could render ex.Message
    // (containing broker URIs, passwords, JWT contents) into user-facing messages
    // and logs. v2 must keep user-facing messages as TK constants only and log
    // only sanitized diagnostics (type name + first frame + status code).

    [Fact]
    public async Task RpcException_Message_NeverAppearsInResultMessages()
    {
        const string sentinel_secret = "SECRET_BROKER_PASSWORD_xyz123";
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, sentinel_secret));
        var call = MakeFailedCall(rpcEx);

        var result = await call.HandleAsync(r => r.Result!, r => r.Data);

        var allMessages = result.Messages.Select(m => m.Key).ToList();
        foreach (var msg in allMessages)
            msg.Should().NotContain(sentinel_secret, "ex.Message must never reach result.Messages");

        result.Messages.Should().NotBeEmpty("ServiceUnavailable carries its TK message");
        result.Messages[0].Key.Should().NotBeNullOrEmpty("factory TK key is the user-facing message");
    }

    [Fact]
    public async Task GenericException_Message_NeverAppearsInResultMessages()
    {
        const string sentinel_secret = "SECRET_CONN_STRING_password=abc";
        var call = MakeFailedCall(new InvalidOperationException(sentinel_secret));

        var result = await call.HandleAsync(r => r.Result!, r => r.Data);

        foreach (var msg in result.Messages.Select(m => m.Key))
            msg.Should().NotContain(sentinel_secret, "ex.Message must never reach result.Messages");
    }

    [Fact]
    public async Task RpcException_LogOutput_DoesNotContain_ExMessage_ContainsTypeName()
    {
        const string sentinel_secret = "AMQP_PASSWORD_secret456";
        var rpcEx = new RpcException(new Status(StatusCode.Internal, sentinel_secret));
        var call = MakeFailedCall(rpcEx);
        var logger = new CapturingLogger();

        await call.HandleAsync(r => r.Result!, r => r.Data, logger, "trace-1");

        logger.LoggedMessages.Should().NotBeEmpty("transport failure was logged");
        var combined = string.Join(" ", logger.LoggedMessages);
        combined.Should().NotContain(sentinel_secret, "ex.Message must not appear in any log output");
        combined.Should().Contain("RpcException", "type name is logged for diagnostics");
    }

    [Fact]
    public async Task GenericException_LogOutput_DoesNotContain_ExMessage_ContainsTypeName()
    {
        const string sentinel_secret = "JWT_SECRET_xyz789";
        var call = MakeFailedCall(new InvalidOperationException(sentinel_secret));
        var logger = new CapturingLogger();

        await call.HandleAsync(r => r.Result!, r => r.Data, logger);

        var combined = string.Join(" ", logger.LoggedMessages);
        combined.Should().NotContain(sentinel_secret);
        combined.Should().Contain("InvalidOperationException");
    }

    // ── No logger does not throw ──────────────────────────────────────────

    [Fact]
    public async Task NullLogger_RpcException_DoesNotThrow()
    {
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, string.Empty));
        var call = MakeFailedCall(rpcEx);

        var act = async () => await call.HandleAsync(r => r.Result!, r => r.Data, logger: null);
        await act.Should().NotThrowAsync();
    }

    // ── TraceId threads into fail-open results ────────────────────────────

    [Fact]
    public async Task RpcException_TraceId_AppearsInFailResult()
    {
        const string trace = "abc123";
        var rpcEx = new RpcException(new Status(StatusCode.Unavailable, string.Empty));
        var call = MakeFailedCall(rpcEx);

        var result = await call.HandleAsync(r => r.Result!, r => r.Data, traceId: trace);

        result.TraceId.Should().Be(trace);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static AsyncUnaryCall<FakeResponse> MakeCall(FakeResponse response) =>
        new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });

    private static AsyncUnaryCall<FakeResponse> MakeFailedCall(Exception ex) =>
        new(
            Task.FromException<FakeResponse>(ex),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.Internal, string.Empty),
            () => new Metadata(),
            () => { });

    private static FakeResponse MakeResponse(D2Result source) =>
        new() { Result = source.ToProto() };

    private sealed class FakeResponse
    {
        public D2ResultProto? Result { get; set; }

        public string? Data { get; set; }
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
