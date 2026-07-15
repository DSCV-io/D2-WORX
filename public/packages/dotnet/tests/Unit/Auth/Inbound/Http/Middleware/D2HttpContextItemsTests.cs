// -----------------------------------------------------------------------
// <copyright file="D2HttpContextItemsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Http.Middleware;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions.Http;
using Xunit;

/// <summary>
/// Pins the <see cref="D2HttpContextItems.REQUEST_CONTEXT"/> slot-key value.
/// Downstream pipeline code references this constant by string in some
/// scenarios (logging, diagnostics); a rename here would silently detach
/// readers from the writer.
/// </summary>
public sealed class D2HttpContextItemsTests
{
    [Fact]
    public void RequestContextKey_IsStable()
    {
        D2HttpContextItems.REQUEST_CONTEXT.Should().Be("D2.RequestContext");
    }
}
