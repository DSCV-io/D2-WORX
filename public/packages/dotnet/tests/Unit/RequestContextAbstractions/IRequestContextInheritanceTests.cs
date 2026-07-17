// -----------------------------------------------------------------------
// <copyright file="IRequestContextInheritanceTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContextAbstractions;

using AwesomeAssertions;
using DcsvIo.D2.AuthContext.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

/// <summary>
/// Inheritance contract — <see cref="IRequestContext"/> MUST extend
/// <see cref="IAuthContext"/>. The whole composition (auth + transport +
/// network + WhoIs) hangs off this; if the spec's <c>extends</c> field were
/// dropped, every handler that takes IRequestContext but reads auth fields
/// would break at runtime.
/// </summary>
public sealed class IRequestContextInheritanceTests
{
    [Fact]
    public void IRequestContext_ExtendsIAuthContext()
    {
        typeof(IAuthContext).IsAssignableFrom(typeof(IRequestContext)).Should().BeTrue();
    }

    [Fact]
    public void IRequestContext_IsAnInterface()
    {
        // Adversarial: the codegen output is meant to be an interface, not a
        // class. A class with the same name would let consumers `new` it,
        // bypassing the MutableRequestContext factory pattern.
        typeof(IRequestContext).IsInterface.Should().BeTrue();
    }
}
