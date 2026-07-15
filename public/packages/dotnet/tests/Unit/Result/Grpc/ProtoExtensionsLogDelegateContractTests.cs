// -----------------------------------------------------------------------
// <copyright file="ProtoExtensionsLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result.Grpc;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Result.Grpc;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Pins the no-Exception-parameter contract on <c>ProtoExtensions</c>'s
/// <c>[LoggerMessage]</c> delegates. Exception messages can embed gRPC
/// <c>Status.Detail</c>, broker URIs, JWT contents, and user-input-derived
/// strings — none of which must reach the log pipeline. Callers pass
/// <c>SanitizedExceptionRender.TypeName(ex)</c> + <c>FirstFrame(ex)</c>
/// as separate strings instead.
/// </summary>
/// <remarks>
/// Mirrors <c>AuthLogDelegateContractTests</c> and
/// <c>LoggerMessageDelegateContractTests</c>. Same enforcement pattern
/// across every log surface in the codebase.
/// </remarks>
public sealed class ProtoExtensionsLogDelegateContractTests
{
    [Fact]
    public void ProtoExtensions_NoDelegateAcceptsExceptionParameter()
    {
        // Only [LoggerMessage]-attributed methods are in scope — the contract
        // that no sink parameter accepts a raw Exception. Public non-LoggerMessage
        // methods (e.g. IsTransientGrpcException) are explicitly excluded.
        var leakProneMethods = typeof(ProtoExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttributes(typeof(LoggerMessageAttribute), inherit: false).Length > 0)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(m => m.Name)
            .ToList();

        leakProneMethods.Should().BeEmpty(
            "ProtoExtensions [LoggerMessage] delegates must not accept Exception parameters; "
            + "exception messages can leak gRPC Status.Detail / broker URIs / JWT contents. "
            + "Use SanitizedExceptionRender.TypeName(ex) + FirstFrame(ex) instead. "
            + "Offending delegates: " + string.Join(", ", leakProneMethods));
    }
}
