// -----------------------------------------------------------------------
// <copyright file="ServerCallContextRequestContextExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Interceptors;

using AwesomeAssertions;
using D2.Shared.Auth.Grpc.Interceptors;
using D2.Shared.Context.Abstractions;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using Xunit;

public sealed class ServerCallContextRequestContextExtensionsTests
{
    [Fact]
    public void GetD2RequestContext_NoInterceptorRan_ReturnsNull()
    {
        var ctx = new TestServerCallContext();

        var result = ctx.GetD2RequestContext();

        result.Should().BeNull();
    }

    [Fact]
    public void GetD2RequestContext_WhenInterceptorSetsSlot_ReturnsPopulatedContext()
    {
        var ctx = new TestServerCallContext();
        var requestContext = new MutableRequestContext { IsAuthenticated = true };
        ctx.UserState[D2GrpcUserStateKeys.REQUEST_CONTEXT] = requestContext;

        var result = ctx.GetD2RequestContext();

        result.Should().BeSameAs(requestContext);
    }

    [Fact]
    public void GetD2RequestContext_SlotHoldsWrongType_ReturnsNull()
    {
        // Defensive cast — if anything other than IRequestContext is stashed
        // under the key (e.g. by misbehaving code), the typed accessor
        // returns null rather than throwing InvalidCastException.
        var ctx = new TestServerCallContext();
        ctx.UserState[D2GrpcUserStateKeys.REQUEST_CONTEXT] = "not-a-request-context";

        var result = ctx.GetD2RequestContext();

        result.Should().BeNull();
    }
}
