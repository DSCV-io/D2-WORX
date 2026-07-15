// -----------------------------------------------------------------------
// <copyright file="OutboundLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.Telemetry;

using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Pins the no-Exception-parameter contract on <c>OutboundLog</c>'s
/// <c>[LoggerMessage]</c> delegates. Exception messages can interpolate
/// OAuth bearer tokens / token-endpoint URIs / response bodies / configured
/// client secrets — none of which must reach the log pipeline. Callers pass
/// <c>SanitizedExceptionRender.TypeName(ex)</c> + <c>FirstFrame(ex)</c>
/// as separate strings instead.
/// </summary>
/// <remarks>
/// Mirrors <c>D2.Shared.Tests.Unit.Auth.Inbound.Telemetry.AuthLogDelegateContractTests</c>.
/// Same enforcement pattern across every log surface in the codebase.
/// </remarks>
public sealed class OutboundLogDelegateContractTests
{
    [Fact]
    public void OutboundLog_NoDelegateAcceptsExceptionParameter()
    {
        var outboundLogType = typeof(D2.Shared.Auth.Outbound.Telemetry.OutboundTelemetry).Assembly
            .GetTypes()
            .Single(t => t.Name == "OutboundLog"
                && t.Namespace == "D2.Shared.Auth.Outbound.Telemetry");

        var leakProneMethods = outboundLogType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(m => m.Name)
            .ToList();

        leakProneMethods.Should().BeEmpty(
            "OutboundLog delegates must not accept Exception parameters; "
            + "exception messages can leak OAuth bearer tokens / token-endpoint URIs / "
            + "response bodies. Use SanitizedExceptionRender.TypeName(ex) + "
            + "FirstFrame(ex) instead. "
            + "Offending delegates: " + string.Join(", ", leakProneMethods));
    }
}
