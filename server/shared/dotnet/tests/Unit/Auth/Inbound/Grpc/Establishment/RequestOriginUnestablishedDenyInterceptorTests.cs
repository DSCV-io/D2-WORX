// -----------------------------------------------------------------------
// <copyright file="RequestOriginUnestablishedDenyInterceptorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Establishment;

using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Auth.Grpc.Interceptors;
using D2.Shared.Auth.Grpc.Status;
using D2.Shared.Context.Abstractions;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using global::Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GrpcStatusCode = global::Grpc.Core.StatusCode;

/// <summary>
/// Unit matrix for the platform Unestablished-origin deny interceptor.
/// </summary>
/// <remarks>
/// Surface × category (adversarial matrix):
/// <list type="bullet">
///   <item>Unary / ClientStreaming / ServerStreaming / Duplex × Unestablished
///     → deny + <c>AUTH_REQUEST_ORIGIN_UNESTABLISHED</c></item>
///   <item>Unary × missing request context → fail-closed same code</item>
///   <item>Unary / ServerStreaming × CrossProcessHop → continue</item>
///   <item>Unary × Harmless + Unestablished → skip deny</item>
/// </list>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class RequestOriginUnestablishedDenyInterceptorTests
{
    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new RequestOriginUnestablishedDenyInterceptor(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Unary_UnestablishedOrigin_DeniesWithAuthRequestOriginUnestablished()
    {
        var interceptor = MakeInterceptor();
        var ctx = new MutableRequestContext
        {
            IsAuthenticated = true,
            Origin = RequestOrigin.Unestablished,
        };
        var scc = BuildContext(
            ctx,
            MethodScopeMetadata.ForScopes(["internal.audit.ping"], ScopeMatch.Any));

        var act = async () => await interceptor.UnaryServerHandler<string, string>(
            "req", scc, (_, _) => Task.FromResult("resp"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ex.Which.Trailers.GetValue(D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED);
    }

    [Fact]
    public async Task Unary_MissingRequestContext_DeniesFailClosed()
    {
        var interceptor = MakeInterceptor();
        var scc = BuildContext(
            requestContext: null,
            MethodScopeMetadata.ForScopes(["internal.audit.ping"], ScopeMatch.Any));

        var act = async () => await interceptor.UnaryServerHandler<string, string>(
            "req", scc, (_, _) => Task.FromResult("resp"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ex.Which.Trailers.GetValue(D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED);
    }

    [Fact]
    public async Task Unary_CrossProcessHop_Continues()
    {
        var interceptor = MakeInterceptor();
        var ctx = new MutableRequestContext
        {
            IsAuthenticated = true,
            Origin = RequestOrigin.CrossProcessHop,
            ImmediateCaller = "edge",
        };
        var scc = BuildContext(
            ctx,
            MethodScopeMetadata.ForScopes(["internal.audit.ping"], ScopeMatch.Any));
        var continuationCalled = false;

        var result = await interceptor.UnaryServerHandler<string, string>(
            "req",
            scc,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("resp");
            });

        continuationCalled.Should().BeTrue();
        result.Should().Be("resp");
    }

    [Fact]
    public async Task Unary_HarmlessEndpoint_SkipsDenyEvenWhenUnestablished()
    {
        var interceptor = MakeInterceptor();
        var ctx = new MutableRequestContext
        {
            IsAuthenticated = false,
            Origin = RequestOrigin.Unestablished,
        };
        var scc = BuildContext(ctx, MethodScopeMetadata.HarmlessEndpoint);
        var continuationCalled = false;

        await interceptor.UnaryServerHandler<string, string>(
            "req",
            scc,
            (_, _) =>
            {
                continuationCalled = true;
                return Task.FromResult("resp");
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ClientStreaming_Unestablished_DeniesWithAuthRequestOriginUnestablished()
    {
        var interceptor = MakeInterceptor();
        var ctx = new MutableRequestContext { Origin = RequestOrigin.Unestablished };
        var scc = BuildContext(
            ctx,
            MethodScopeMetadata.ForScopes(["s"], ScopeMatch.Any));

        var act = async () => await interceptor.ClientStreamingServerHandler(
            new EmptyAsyncStreamReader<string>(),
            scc,
            (_, _) => Task.FromResult("resp"));

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ex.Which.Trailers.GetValue(D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED);
    }

    [Fact]
    public async Task ServerStreaming_Unestablished_DeniesWithAuthRequestOriginUnestablished()
    {
        var interceptor = MakeInterceptor();
        var ctx = new MutableRequestContext { Origin = RequestOrigin.Unestablished };
        var scc = BuildContext(
            ctx,
            MethodScopeMetadata.ForScopes(["s"], ScopeMatch.Any));

        var act = async () => await interceptor.ServerStreamingServerHandler(
            "req",
            new DiscardingServerStreamWriter<string>(),
            scc,
            (_, _, _) => Task.CompletedTask);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ex.Which.Trailers.GetValue(D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED);
    }

    [Fact]
    public async Task ServerStreaming_CrossProcessHop_Continues()
    {
        var interceptor = MakeInterceptor();
        var ctx = new MutableRequestContext { Origin = RequestOrigin.CrossProcessHop };
        var scc = BuildContext(
            ctx,
            MethodScopeMetadata.ForScopes(["s"], ScopeMatch.Any));
        var continuationCalled = false;

        await interceptor.ServerStreamingServerHandler(
            "req",
            new DiscardingServerStreamWriter<string>(),
            scc,
            (_, _, _) =>
            {
                continuationCalled = true;
                return Task.CompletedTask;
            });

        continuationCalled.Should().BeTrue();
    }

    [Fact]
    public async Task DuplexStreaming_Unestablished_DeniesWithAuthRequestOriginUnestablished()
    {
        var interceptor = MakeInterceptor();
        var ctx = new MutableRequestContext { Origin = RequestOrigin.Unestablished };
        var scc = BuildContext(
            ctx,
            MethodScopeMetadata.ForScopes(["s"], ScopeMatch.Any));

        var act = async () => await interceptor.DuplexStreamingServerHandler(
            new EmptyAsyncStreamReader<string>(),
            new DiscardingServerStreamWriter<string>(),
            scc,
            (_, _, _) => Task.CompletedTask);

        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ex.Which.Trailers.GetValue(D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED);
    }

    private static RequestOriginUnestablishedDenyInterceptor MakeInterceptor() =>
        new(NullLogger<RequestOriginUnestablishedDenyInterceptor>.Instance);

    private static TestServerCallContext BuildContext(
        MutableRequestContext? requestContext,
        MethodScopeMetadata metadata)
    {
        var http = new DefaultHttpContext();
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(metadata),
            "test");
        http.SetEndpoint(endpoint);

        if (requestContext is not null)
        {
            http.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;
        }

        var scc = new TestServerCallContext(httpContext: http);

        if (requestContext is not null)
            scc.UserState[D2GrpcUserStateKeys.REQUEST_CONTEXT] = requestContext;

        return scc;
    }

    private sealed class EmptyAsyncStreamReader<T> : IAsyncStreamReader<T>
        where T : class
    {
        public T Current => null!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class DiscardingServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message) => Task.CompletedTask;
    }
}
