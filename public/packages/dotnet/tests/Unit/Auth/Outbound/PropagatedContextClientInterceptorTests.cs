// -----------------------------------------------------------------------
// <copyright file="PropagatedContextClientInterceptorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Outbound;

using System;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Outbound.Grpc;
using D2.Shared.Context.Abstractions;
using D2.Shared.Headers.Grpc;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using NodaTime;
using Xunit;

/// <summary>
/// Unit matrix for the outbound propagated-context client interceptor: it writes the
/// encoded <c>x-d2-context</c> header when the current request scope has a context with
/// fields, and is a no-op (no header, no throw) when there is no scope or no fields —
/// propagation is opportunistic telemetry.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PropagatedContextClientInterceptorTests
{
    private static readonly Instant sr_t0 = Instant.FromUtc(2026, 6, 30, 12, 0, 0);

    private static readonly Marshaller<string> sr_marshaller = new(
        Encoding.UTF8.GetBytes,
        bytes => Encoding.UTF8.GetString(bytes));

    [Fact]
    public void NullAmbientAccessor_Throws()
    {
        var act = () => new PropagatedContextClientInterceptor(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AsyncUnaryCall_WithRequestContext_WritesEncodedPropagatedHeader()
    {
        var context = new MutableRequestContext
        {
            RequestId = "req-from-A",
            CallPath = [new CallPathEntry("service-a", CallPathKind.Edge, sr_t0.ToDateTimeOffset())],
        };
        var headers = InvokeAndCaptureHeaders(new StubAmbientScope(BuildScope(context)));

        var encoded = headers?.Get(GrpcHeaders.PROPAGATED_CONTEXT)?.Value;
        encoded.Should().NotBeNull("the interceptor writes the propagation header");

        var decoded = PropagatedContextSerializer.TryDecode(encoded);
        decoded.Should().NotBeNull();
        decoded.RequestId.Should().Be("req-from-A");
        decoded.CallPath.Should().ContainSingle();
        decoded.CallPath![0].Id.Should().Be("service-a");
    }

    [Fact]
    public void AsyncUnaryCall_NoAmbientScope_WritesNoHeader()
    {
        var headers = InvokeAndCaptureHeaders(new StubAmbientScope(null));

        (headers?.Get(GrpcHeaders.PROPAGATED_CONTEXT)).Should().BeNull(
            "a system-initiated call with no inbound scope propagates nothing");
    }

    [Fact]
    public void AsyncUnaryCall_EmptyContext_WritesNoHeader()
    {
        // A context with no propagated fields ⇒ HasAnyField is false ⇒ no header.
        var headers = InvokeAndCaptureHeaders(
            new StubAmbientScope(BuildScope(new MutableRequestContext())));

        (headers?.Get(GrpcHeaders.PROPAGATED_CONTEXT)).Should().BeNull(
            "an empty context has nothing to propagate");
    }

    // ---- Remaining call-shape coverage: the interceptor documents that ALL client
    // call shapes route through the single shared WithPropagatedHeader path. Only
    // AsyncUnaryCall was exercised above — prove the rest route through it too. ----

    [Fact]
    public void BlockingUnaryCall_WithRequestContext_WritesEncodedPropagatedHeader()
    {
        var context = new MutableRequestContext { RequestId = "req-from-A" };
        var interceptor = new PropagatedContextClientInterceptor(
            new StubAmbientScope(BuildScope(context)));
        var method = new Method<string, string>(
            MethodType.Unary, "svc", "Echo", sr_marshaller, sr_marshaller);
        var ctx = new ClientInterceptorContext<string, string>(method, "host", default);
        Metadata? captured = null;

        var response = interceptor.BlockingUnaryCall("req", ctx, (_, observed) =>
        {
            captured = observed.Options.Headers;

            return "resp";
        });

        response.Should().Be("resp");
        var encoded = captured?.Get(GrpcHeaders.PROPAGATED_CONTEXT)?.Value;
        encoded.Should().NotBeNull(
            "BlockingUnaryCall must route through the shared WithPropagatedHeader path");
    }

    [Fact]
    public void AsyncClientStreamingCall_WithRequestContext_WritesEncodedPropagatedHeader()
    {
        var context = new MutableRequestContext { RequestId = "req-from-A" };
        var interceptor = new PropagatedContextClientInterceptor(
            new StubAmbientScope(BuildScope(context)));
        var method = new Method<string, string>(
            MethodType.ClientStreaming, "svc", "Echo", sr_marshaller, sr_marshaller);
        var ctx = new ClientInterceptorContext<string, string>(method, "host", default);
        Metadata? captured = null;

        using var call = interceptor.AsyncClientStreamingCall(ctx, observed =>
        {
            captured = observed.Options.Headers;

            return new AsyncClientStreamingCall<string, string>(
                new NoopClientStreamWriter<string>(),
                Task.FromResult("resp"),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        });

        var encoded = captured?.Get(GrpcHeaders.PROPAGATED_CONTEXT)?.Value;
        encoded.Should().NotBeNull(
            "AsyncClientStreamingCall must route through the shared WithPropagatedHeader "
                + "path");
    }

    [Fact]
    public void AsyncServerStreamingCall_WithRequestContext_WritesEncodedPropagatedHeader()
    {
        var context = new MutableRequestContext { RequestId = "req-from-A" };
        var interceptor = new PropagatedContextClientInterceptor(
            new StubAmbientScope(BuildScope(context)));
        var method = new Method<string, string>(
            MethodType.ServerStreaming, "svc", "Echo", sr_marshaller, sr_marshaller);
        var ctx = new ClientInterceptorContext<string, string>(method, "host", default);
        Metadata? captured = null;

        using var call = interceptor.AsyncServerStreamingCall("req", ctx, (_, observed) =>
        {
            captured = observed.Options.Headers;

            return new AsyncServerStreamingCall<string>(
                new EmptyAsyncStreamReader<string>(),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        });

        var encoded = captured?.Get(GrpcHeaders.PROPAGATED_CONTEXT)?.Value;
        encoded.Should().NotBeNull(
            "AsyncServerStreamingCall must route through the shared WithPropagatedHeader "
                + "path");
    }

    [Fact]
    public void AsyncDuplexStreamingCall_WithRequestContext_WritesEncodedPropagatedHeader()
    {
        var context = new MutableRequestContext { RequestId = "req-from-A" };
        var interceptor = new PropagatedContextClientInterceptor(
            new StubAmbientScope(BuildScope(context)));
        var method = new Method<string, string>(
            MethodType.DuplexStreaming, "svc", "Echo", sr_marshaller, sr_marshaller);
        var ctx = new ClientInterceptorContext<string, string>(method, "host", default);
        Metadata? captured = null;

        using var call = interceptor.AsyncDuplexStreamingCall(ctx, observed =>
        {
            captured = observed.Options.Headers;

            return new AsyncDuplexStreamingCall<string, string>(
                new NoopClientStreamWriter<string>(),
                new EmptyAsyncStreamReader<string>(),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        });

        var encoded = captured?.Get(GrpcHeaders.PROPAGATED_CONTEXT)?.Value;
        encoded.Should().NotBeNull(
            "AsyncDuplexStreamingCall must route through the shared WithPropagatedHeader "
                + "path");
    }

    private static IServiceProvider BuildScope(IRequestContext context)
        => new ServiceCollectionStub(context);

    private static Metadata? InvokeAndCaptureHeaders(IAmbientRequestScopeAccessor ambient)
    {
        var interceptor = new PropagatedContextClientInterceptor(ambient);
        Metadata? captured = null;
        var method = new Method<string, string>(
            MethodType.Unary, "svc", "Echo", sr_marshaller, sr_marshaller);
        var ctx = new ClientInterceptorContext<string, string>(method, "host", default);

        using var call = interceptor.AsyncUnaryCall("req", ctx, (_, observed) =>
        {
            captured = observed.Options.Headers;

            return new AsyncUnaryCall<string>(
                Task.FromResult("resp"),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        });

        return captured;
    }

    /// <summary>Ambient-scope stub returning a fixed (or null) provider.</summary>
    private sealed class StubAmbientScope(IServiceProvider? current) : IAmbientRequestScopeAccessor
    {
        public IServiceProvider? Current => current;
    }

    /// <summary>Minimal provider resolving exactly one <see cref="IRequestContext"/>.</summary>
    private sealed class ServiceCollectionStub(IRequestContext context) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IRequestContext) ? context : null;
    }

    /// <summary>No-op <see cref="IClientStreamWriter{T}"/> stand-in.</summary>
    private sealed class NoopClientStreamWriter<T> : IClientStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message) => Task.CompletedTask;

        public Task CompleteAsync() => Task.CompletedTask;
    }

    /// <summary>Empty <see cref="IAsyncStreamReader{T}"/> stand-in.</summary>
    private sealed class EmptyAsyncStreamReader<T> : IAsyncStreamReader<T>
        where T : class
    {
        public T Current => null!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
