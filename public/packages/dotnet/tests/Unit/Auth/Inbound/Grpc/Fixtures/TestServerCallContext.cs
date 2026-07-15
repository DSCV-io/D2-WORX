// -----------------------------------------------------------------------
// <copyright file="TestServerCallContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Fixtures;

using global::Grpc.Core;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Minimal hand-rolled <see cref="ServerCallContext"/> for unit-test use.
/// Mirrors the BCL <c>Grpc.Core.Testing.TestServerCallContext</c> shape but
/// is in-tree so we don't take a `Grpc.Core.Testing` package dep solely for
/// this. Used as a convenience over wiring a full <c>HttpContext</c>-backed
/// gRPC test host where unit tests don't need the AspNetCore plumbing.
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    /// <summary>
    /// The string key under which <c>Grpc.AspNetCore.Server</c>'s
    /// <c>HttpContextServerCallContextExtensions.GetHttpContext()</c>
    /// extension reads the per-call <see cref="HttpContext"/> from
    /// <see cref="ServerCallContext.UserState"/>.
    /// </summary>
    public const string HTTP_CONTEXT_USER_STATE_KEY = "__HttpContext__";

    public TestServerCallContext(
        Metadata? requestHeaders = null,
        CancellationToken cancellationToken = default,
        string method = "/test.Service/Method",
        string host = "test-host",
        string peer = "test-peer",
        HttpContext? httpContext = null)
    {
        RequestHeadersCore = requestHeaders ?? new Metadata();
        CancellationTokenCore = cancellationToken;
        MethodCore = method;
        HostCore = host;
        PeerCore = peer;
        DeadlineCore = DateTime.UtcNow.AddMinutes(1);
        ResponseTrailersCore = new Metadata();
        UserStateCore = new Dictionary<object, object>();

        if (httpContext is not null)
        {
            UserStateCore[HTTP_CONTEXT_USER_STATE_KEY] = httpContext;
        }
    }

    protected override string MethodCore { get; }

    protected override string HostCore { get; }

    protected override string PeerCore { get; }

    protected override DateTime DeadlineCore { get; }

    protected override Metadata RequestHeadersCore { get; }

    protected override CancellationToken CancellationTokenCore { get; }

    protected override Metadata ResponseTrailersCore { get; }

    protected override Status StatusCore { get; set; }

    protected override WriteOptions? WriteOptionsCore { get; set; }

    protected override AuthContext AuthContextCore { get; } = new(string.Empty, []);

    protected override IDictionary<object, object> UserStateCore { get; }

    protected override ContextPropagationToken CreatePropagationTokenCore(
        ContextPropagationOptions? options) =>
        throw new NotSupportedException(
            "Context propagation is not supported in TestServerCallContext.");

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
        Task.CompletedTask;
}
