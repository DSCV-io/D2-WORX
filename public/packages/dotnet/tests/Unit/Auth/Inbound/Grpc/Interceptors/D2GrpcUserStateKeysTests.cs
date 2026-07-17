// -----------------------------------------------------------------------
// <copyright file="D2GrpcUserStateKeysTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Interceptors;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Grpc.Interceptors;
using Xunit;

/// <summary>
/// Pins the <see cref="D2GrpcUserStateKeys.REQUEST_CONTEXT"/> slot-key value.
/// Cross-transport code (HTTP middleware uses the same string-keyed slot
/// value <c>"D2.RequestContext"</c> on <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/>)
/// depends on the value matching exactly so a single shared lookup convention
/// works under both transport bindings — a rename here would silently detach
/// readers from the writer.
/// </summary>
public sealed class D2GrpcUserStateKeysTests
{
    [Fact]
    public void RequestContextKey_IsStable()
    {
        D2GrpcUserStateKeys.REQUEST_CONTEXT.Should().Be("D2.RequestContext");
    }
}
