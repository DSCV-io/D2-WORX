// -----------------------------------------------------------------------
// <copyright file="MtlsLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Mtls;

using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using D2.Shared.AspNetCore.Mtls;
using Xunit;

/// <summary>
/// Pins the no-Exception-parameter contract on <c>MtlsLog</c>'s
/// <c>[LoggerMessage]</c> delegates. A logged exception from a TLS-handshake
/// path can embed certificate subject or subject-alternative-name bytes in
/// <c>ex.Message</c>. Callers pass <c>SanitizedExceptionRender.TypeName(ex)</c>
/// as a separate string instead.
/// </summary>
/// <remarks>
/// Mirrors <c>D2.Shared.Tests.Unit.AuthOutbound.Telemetry.OutboundLogDelegateContractTests</c>.
/// Same enforcement pattern across every <c>[LoggerMessage]</c> surface in the codebase.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class MtlsLogDelegateContractTests
{
    [Fact]
    public void MtlsLog_NoDelegateAcceptsExceptionParameter()
    {
        var mtlsLogType = typeof(SpiffeSanPeerValidator).Assembly
            .GetTypes()
            .Single(t => t.Name == "MtlsLog"
                && t.Namespace == "D2.Shared.AspNetCore.Mtls");

        var leakProneMethods = mtlsLogType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(m => m.Name)
            .ToList();

        leakProneMethods.Should().BeEmpty(
            "MtlsLog delegates must not accept Exception parameters; "
            + "exception messages can leak certificate subject / subject-alternative-name "
            + "bytes from the TLS-handshake path. Use SanitizedExceptionRender.TypeName(ex) "
            + "as a separate string parameter instead. "
            + "Offending delegates: " + string.Join(", ", leakProneMethods));
    }
}
