// -----------------------------------------------------------------------
// <copyright file="IDbExceptionClassifierTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Abstractions;

using System;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using Xunit;

/// <summary>
/// Type-system smoke tests for <see cref="IDbExceptionClassifier"/>. Pin the
/// interface shape — the production base repo handler dispatches by interface,
/// so an accidental signature change (e.g. async-ifying or adding a
/// CancellationToken parameter) would silently break every provider impl.
/// </summary>
public sealed class IDbExceptionClassifierTests
{
    [Fact]
    public void Interface_HasExactlyOneMethod_Classify()
    {
        var methods = typeof(IDbExceptionClassifier).GetMethods(
            BindingFlags.Public | BindingFlags.Instance);

        methods.Should().ContainSingle();
        methods[0].Name.Should().Be("Classify");
    }

    [Fact]
    public void Classify_TakesSingleExceptionParam_ReturnsNullableDbFailureKind()
    {
        var method = typeof(IDbExceptionClassifier).GetMethod("Classify");

        method.Should().NotBeNull();
        method.GetParameters().Should().ContainSingle()
            .Which.ParameterType.Should().Be<Exception>();
        method.ReturnType.Should().Be<DbFailureKind?>();
    }

    [Fact]
    public void CustomImplementation_CanBeWrittenAndUsed()
    {
        // Documents the implementation contract: any class implementing
        // the single-method interface IS a valid classifier — no extra
        // surface area required.
        IDbExceptionClassifier impl = new AlwaysUniqueViolationClassifier();

        var result = impl.Classify(new InvalidOperationException("anything"));

        result.Should().Be(DbFailureKind.UniqueViolation);
    }

    [Fact]
    public void CustomImplementation_CanReturnNullForUnrecognized()
    {
        IDbExceptionClassifier impl = new AlwaysNullClassifier();

        var result = impl.Classify(new InvalidOperationException("anything"));

        result.Should().BeNull();
    }

    private sealed class AlwaysUniqueViolationClassifier : IDbExceptionClassifier
    {
        public DbFailureKind? Classify(Exception exception) => DbFailureKind.UniqueViolation;
    }

    private sealed class AlwaysNullClassifier : IDbExceptionClassifier
    {
        public DbFailureKind? Classify(Exception exception) => null;
    }
}
