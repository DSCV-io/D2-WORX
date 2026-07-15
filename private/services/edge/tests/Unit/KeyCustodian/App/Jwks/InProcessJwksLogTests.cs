// -----------------------------------------------------------------------
// <copyright file="InProcessJwksLogTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App.Jwks;

using System.Reflection;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Jwks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Contract-pin tests for <see cref="InProcessJwksLog"/> — verifies that no
/// <c>[LoggerMessage]</c>-attributed delegate accepts an <see cref="Exception"/>
/// (or <see cref="Exception"/>-derived) parameter.
/// </summary>
/// <remarks>
/// Per §3.1 / §1.6: a logged <c>ex.Message</c> can leak connection strings or
/// raw user input. Refresh failure paths pass sanitized type + first-frame
/// strings only (see <see cref="InProcessJwksProvider"/>).
/// </remarks>
[Trait("Category", "Unit")]
public sealed class InProcessJwksLogTests
{
    // Pass method names (strings) — MethodInfo is not xUnit-serializable.
    public static TheoryData<string> LoggerMessageMethodNames()
    {
        var type = typeof(InProcessJwksLog);
        var data = new TheoryData<string>();

        foreach (var method in type.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
                data.Add(method.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LoggerMessageMethodNames))]
    public void LoggerMessageDelegate_DoesNotAcceptExceptionParameter(string methodName)
    {
        var type = typeof(InProcessJwksLog);
        var method = type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .First(m => m.Name == methodName
                && m.GetCustomAttribute<LoggerMessageAttribute>() is not null);

        var exceptionParams = method.GetParameters()
            .Where(p => typeof(Exception).IsAssignableFrom(p.ParameterType))
            .ToList();

        exceptionParams.Should().BeEmpty(
            because:
                $"{methodName} must not accept an Exception parameter — "
                + "ex.Message can leak secrets or raw user input into log sinks "
                + "(§3.1); use SanitizedExceptionRender.TypeName + FirstFrame instead");
    }

    [Fact]
    public void InProcessJwksLog_HasAtLeastOneLoggerMessageDelegate()
    {
        var methods = LoggerMessageMethodNames();
        methods.Should().NotBeEmpty(
            because: "InProcessJwksLog must declare at least one [LoggerMessage] delegate");
    }
}
