// -----------------------------------------------------------------------
// <copyright file="HttpContextRequestContextExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http.Middleware;

using AwesomeAssertions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Http.Middleware;
using D2.Shared.Context.Abstractions;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class HttpContextRequestContextExtensionsTests
{
    [Fact]
    public void GetD2RequestContext_NoMiddlewareRan_ReturnsNull()
    {
        var ctx = new DefaultHttpContext();

        var result = ctx.GetD2RequestContext();

        result.Should().BeNull();
    }

    [Fact]
    public void GetD2RequestContext_WhenMiddlewareSetsSlot_ReturnsPopulatedContext()
    {
        var ctx = new DefaultHttpContext();
        var requestContext = new MutableRequestContext { IsAuthenticated = true };
        ctx.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;

        var result = ctx.GetD2RequestContext();

        result.Should().BeSameAs(requestContext);
    }

    [Fact]
    public void GetD2RequestContext_SlotHoldsWrongType_ReturnsNull()
    {
        // Defensive cast — if anything other than IRequestContext is stashed
        // under the key (e.g. by misbehaving middleware), the typed accessor
        // returns null rather than throwing InvalidCastException.
        var ctx = new DefaultHttpContext();
        ctx.Items[D2HttpContextItems.REQUEST_CONTEXT] = "not-a-request-context";

        var result = ctx.GetD2RequestContext();

        result.Should().BeNull();
    }
}
