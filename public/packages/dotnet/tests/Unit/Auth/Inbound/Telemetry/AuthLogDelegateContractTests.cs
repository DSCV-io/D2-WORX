// -----------------------------------------------------------------------
// <copyright file="AuthLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Telemetry;

using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Pins the no-Exception-parameter contract on <c>AuthLog</c>'s
/// <c>[LoggerMessage]</c> delegates. Exception messages can interpolate
/// JWT bytes / request URIs / response bodies / configured secrets — none
/// of which must reach the log pipeline. Callers pass
/// <c>SanitizedExceptionRender.TypeName(ex)</c> + <c>FirstFrame(ex)</c>
/// as separate strings instead.
/// </summary>
/// <remarks>
/// Mirrors <c>D2.Shared.Tests.Unit.Messaging.Telemetry.LoggerMessageDelegateContractTests</c>.
/// Same enforcement pattern across every log surface in the codebase.
/// </remarks>
public sealed class AuthLogDelegateContractTests
{
    [Fact]
    public void AuthLog_NoDelegateAcceptsExceptionParameter()
    {
        var authLogType = typeof(D2.Shared.Auth.Errors.AuthErrorCodes).Assembly
            .GetTypes()
            .Single(t => t.Name == "AuthLog" && t.Namespace == "D2.Shared.Auth.Telemetry");

        var leakProneMethods = authLogType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(m => m.Name)
            .ToList();

        leakProneMethods.Should().BeEmpty(
            "AuthLog delegates must not accept Exception parameters; "
            + "exception messages can leak JWT bytes / URIs / response bodies. "
            + "Use SanitizedExceptionRender.TypeName(ex) + FirstFrame(ex) instead. "
            + "Offending delegates: " + string.Join(", ", leakProneMethods));
    }
}
