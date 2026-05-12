// -----------------------------------------------------------------------
// <copyright file="SanitizedExceptionRenderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Telemetry;

using System;
using System.Reflection;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Pin the PII-safe rendering: type name + first frame ONLY; never
/// <see cref="Exception.Message"/>. Mirrors the auth-outbound test of the
/// same type — duplicated because the helper itself is duplicated (kept
/// auth and auth-outbound dep-graphs independent).
/// </summary>
public sealed class SanitizedExceptionRenderTests
{
    [Fact]
    public void TypeName_ReturnsFullName()
    {
        var ex = new InvalidOperationException("would-be-leaked-message");

        var rendered = InvokeTypeName(ex);

        rendered.Should().Be("System.InvalidOperationException");
        rendered.Should().NotContain("would-be-leaked-message");
    }

    [Fact]
    public void FirstFrame_OnExceptionWithStack_ReturnsMethodAndFile()
    {
        InvalidOperationException captured;
        try
        {
            throw new InvalidOperationException("ignored");
        }
        catch (InvalidOperationException e)
        {
            captured = e;
        }

        var rendered = InvokeFirstFrame(captured);

        // Whatever the exact format ("Type.Method at file:line" or just
        // "Type.Method"), it must NOT carry the exception's Message text.
        rendered.Should().NotContain("ignored");
        rendered.Should().NotBe("<no frame>");
    }

    [Fact]
    public void FirstFrame_OnExceptionWithoutStack_ReturnsSentinel()
    {
        // Exceptions never thrown have no StackTrace.
        var ex = new InvalidOperationException("never-thrown");

        var rendered = InvokeFirstFrame(ex);

        rendered.Should().Be("<no frame>");
        rendered.Should().NotContain("never-thrown");
    }

    private static string InvokeTypeName(Exception ex)
    {
        var t = typeof(D2.Shared.Auth.Errors.AuthErrorCodes).Assembly
            .GetType("D2.Shared.Auth.Telemetry.SanitizedExceptionRender")!;
        return (string)t.GetMethod("TypeName", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [ex])!;
    }

    private static string InvokeFirstFrame(Exception ex)
    {
        var t = typeof(D2.Shared.Auth.Errors.AuthErrorCodes).Assembly
            .GetType("D2.Shared.Auth.Telemetry.SanitizedExceptionRender")!;
        return (string)t.GetMethod("FirstFrame", BindingFlags.Static | BindingFlags.Public)!
            .Invoke(null, [ex])!;
    }
}
