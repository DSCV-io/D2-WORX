// -----------------------------------------------------------------------
// <copyright file="RequestOriginEdgeAppBuilderExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http.Establishment;

using AwesomeAssertions;
using D2.Shared.Auth.Http;
using Microsoft.AspNetCore.Builder;
using Xunit;

/// <summary>
/// Null-guard coverage for <c>UseD2RequestOriginEdge()</c> — the
/// <see cref="IApplicationBuilder"/> extension that inserts the Edge-inbound
/// establishment middleware into a request pipeline. The pipeline-position /
/// establishment-behavior matrix itself is proven end-to-end in
/// <see cref="RequestOriginEdgeInboundMiddlewareTests"/>'s TestServer case.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RequestOriginEdgeAppBuilderExtensionsTests
{
    [Fact]
    public void UseD2RequestOriginEdge_NullApp_Throws()
    {
        IApplicationBuilder? app = null;

        var act = () => app!.UseD2RequestOriginEdge();

        act.Should().Throw<ArgumentNullException>();
    }
}
