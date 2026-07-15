// -----------------------------------------------------------------------
// <copyright file="D2HttpContextItemsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions.Http;
using Xunit;

/// <summary>
/// Pins the <see cref="D2HttpContextItems.REQUEST_CONTEXT"/> slot-key value.
/// Both transport bindings (HTTP middleware and gRPC interceptor) write the
/// validated <c>IRequestContext</c> to <c>HttpContext.Items</c> under this
/// exact string, and the cross-transport scoped <c>IRequestContext</c>
/// resolver lambda (registered identically by <c>AddD2AuthHttp()</c> and
/// <c>AddD2AuthGrpc()</c>) reads from this exact string. A silent rename
/// would detach reader from writer with no compile-time signal — both sides
/// already string-key into the same dictionary slot. The pinning test makes
/// the rename loud.
/// </summary>
public sealed class D2HttpContextItemsTests
{
    [Fact]
    public void RequestContextKey_IsStable()
    {
        D2HttpContextItems.REQUEST_CONTEXT.Should().Be("D2.RequestContext");
    }
}
