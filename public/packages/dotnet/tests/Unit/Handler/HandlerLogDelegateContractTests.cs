// -----------------------------------------------------------------------
// <copyright file="HandlerLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Handler;
using Xunit;

/// <summary>
/// Pins the no-Exception-parameter contract on <c>BaseHandlerLog</c>'s
/// <c>[LoggerMessage]</c> delegates. Exception messages can interpolate
/// broker URIs, connection strings, OAuth tokens, presigned URLs, raw
/// user input, and similar PII — none of which must reach the log
/// pipeline. Callers pass <c>SanitizedExceptionRender.TypeName(ex)</c>
/// + <c>FirstFrame(ex)</c> as separate strings instead.
/// </summary>
/// <remarks>
/// Mirrors <c>DcsvIo.D2.Tests.Unit.Auth.Inbound.Telemetry.AuthLogDelegateContractTests</c>
/// and <c>DcsvIo.D2.Tests.Unit.AuthOutbound.Telemetry.OutboundLogDelegateContractTests</c>.
/// Same enforcement pattern across every log surface in the codebase.
/// </remarks>
public sealed class HandlerLogDelegateContractTests
{
    [Fact]
    public void BaseHandlerLog_NoDelegateAcceptsExceptionParameter()
    {
        var baseHandlerLogType = typeof(HandlerTelemetry).Assembly
            .GetTypes()
            .Single(t => t.Name == "BaseHandlerLog" && t.Namespace == "DcsvIo.D2.Handler");

        var leakProneMethods = baseHandlerLogType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(m => m.Name)
            .ToList();

        leakProneMethods.Should().BeEmpty(
            "BaseHandlerLog delegates must not accept Exception parameters; "
            + "exception messages can leak broker URIs, connection strings, OAuth tokens, "
            + "presigned URLs, and raw user input. "
            + "Use SanitizedExceptionRender.TypeName(ex) + FirstFrame(ex) instead. "
            + "Offending delegates: " + string.Join(", ", leakProneMethods));
    }
}
