// -----------------------------------------------------------------------
// <copyright file="HttpContextRequestContextExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Http.Middleware;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Auth.Http.Middleware;
using DcsvIo.D2.Context.Abstractions;
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
