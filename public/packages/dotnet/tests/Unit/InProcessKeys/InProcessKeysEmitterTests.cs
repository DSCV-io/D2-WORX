// -----------------------------------------------------------------------
// <copyright file="InProcessKeysEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.InProcessKeys;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.InProcessKeys.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic coverage for <see cref="InProcessKeysEmitter"/>: happy-path
/// emission per binding filter, invalid constName diagnostics, unknown
/// binding diagnostics, and empty spec handling.
/// </summary>
public sealed class InProcessKeysEmitterTests
{
    [Fact]
    public void Emit_HappyPathHttpBinding_EmitsConstantWithRightShape()
    {
        var spec = new InProcessKeysSpec(ImmutableArray.Create(
            new KeyEntry(
                ConstName: "REQUEST_CONTEXT",
                Value: "d2.request_context",
                Purpose: "Per-request context slot.",
                Bindings: ImmutableArray.Create("http"))));

        var result = InProcessKeysEmitter.Emit(
            spec,
            InProcessKeysEmitter.BindingFilter.Http,
            targetNamespace: "DcsvIo.D2.Auth.Abstractions.Http",
            className: "D2HttpContextItems",
            visibility: "public");

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class D2HttpContextItems");
        result.GeneratedSource.Should().Contain(
            "public const string REQUEST_CONTEXT = \"d2.request_context\";");
    }

    [Fact]
    public void Emit_InvalidConstName_EmitsInvalidConstNameDiagnostic()
    {
        var spec = new InProcessKeysSpec(ImmutableArray.Create(
            new KeyEntry(
                ConstName: "lower_case_bad",
                Value: "d2.bad",
                Purpose: "Test.",
                Bindings: ImmutableArray.Create("http"))));

        var result = InProcessKeysEmitter.Emit(
            spec,
            InProcessKeysEmitter.BindingFilter.Http,
            targetNamespace: "D2.Test",
            className: "TestKeys",
            visibility: "internal");

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.InvalidConstName);
        result.GeneratedSource.Should().NotContain("lower_case_bad");
    }

    [Fact]
    public void Emit_UnknownBinding_EmitsUnknownBindingDiagnostic()
    {
        var spec = new InProcessKeysSpec(ImmutableArray.Create(
            new KeyEntry(
                ConstName: "BAD_BIND",
                Value: "d2.bad",
                Purpose: "Test.",
                Bindings: ImmutableArray.Create("websocket"))));

        var result = InProcessKeysEmitter.Emit(
            spec,
            InProcessKeysEmitter.BindingFilter.Http,
            targetNamespace: "D2.Test",
            className: "TestKeys",
            visibility: "internal");

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.UnknownBinding);
    }

    [Fact]
    public void Emit_EmptySpec_EmitsEmptyClassWithoutDiagnostics()
    {
        var spec = new InProcessKeysSpec(ImmutableArray<KeyEntry>.Empty);

        var result = InProcessKeysEmitter.Emit(
            spec,
            InProcessKeysEmitter.BindingFilter.Http,
            targetNamespace: "D2.Test",
            className: "TestKeys",
            visibility: "internal");

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("internal static class TestKeys");
    }

    [Fact]
    public void Emit_EntryWithGrpcOnlyBinding_NotEmittedInHttpFilter()
    {
        var spec = new InProcessKeysSpec(ImmutableArray.Create(
            new KeyEntry(
                ConstName: "GRPC_ONLY",
                Value: "d2.grpc",
                Purpose: "Grpc-only key.",
                Bindings: ImmutableArray.Create("grpc"))));

        var result = InProcessKeysEmitter.Emit(
            spec,
            InProcessKeysEmitter.BindingFilter.Http,
            targetNamespace: "D2.Test",
            className: "TestHttp",
            visibility: "internal");

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().NotContain("GRPC_ONLY");
    }
}
