// -----------------------------------------------------------------------
// <copyright file="ISessionLivenessTrackerContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Sessions;

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Contract pinning for <see cref="ISessionLivenessTracker"/>. Read-only
/// interface; any addition (e.g. an accidental writer-side method leak)
/// fires this test.
/// </summary>
public sealed class ISessionLivenessTrackerContractTests
{
    [Fact]
    public void IsAliveAsync_HasExpectedShape()
    {
        var method = typeof(ISessionLivenessTracker).GetMethod(
            nameof(ISessionLivenessTracker.IsAliveAsync));

        method.Should().NotBeNull();
        method.ReturnType.Should().Be<ValueTask<D2Result<bool>>>();

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2);

        parameters[0].ParameterType.Should().Be<Guid>();
        parameters[0].Name.Should().Be("sessionId");

        parameters[1].ParameterType.Should().Be<CancellationToken>();
        parameters[1].HasDefaultValue.Should().BeTrue(
            "IsAliveAsync's CancellationToken should default to default(CancellationToken)");
    }

    [Fact]
    public void Interface_IsPublic()
    {
        typeof(ISessionLivenessTracker).IsPublic.Should().BeTrue();
        typeof(ISessionLivenessTracker).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void Interface_HasExactlyOneMethod()
    {
        // Adversarial: pins the read-only surface. If a writer-side method
        // (e.g. SetAsync / RemoveAsync) ever gets accidentally added here,
        // this test fires — Edge owns the writer side internally; this
        // abstractions surface stays read-only by design.
        var methods = typeof(ISessionLivenessTracker).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        methods.Should().HaveCount(1);
    }
}
