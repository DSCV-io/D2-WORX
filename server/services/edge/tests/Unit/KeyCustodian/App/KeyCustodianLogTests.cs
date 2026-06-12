// -----------------------------------------------------------------------
// <copyright file="KeyCustodianLogTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System;
using System.Linq;
using System.Reflection;
using D2.Edge.KeyCustodian.App.Application.Observability;
using Microsoft.Extensions.Logging;

/// <summary>
/// Contract-pin tests for <see cref="KeyCustodianLog"/> — verifies that no
/// <c>[LoggerMessage]</c>-attributed delegate accepts an <see cref="Exception"/>
/// (or <see cref="Exception"/>-derived) parameter.
/// </summary>
/// <remarks>
/// Per §3.1: a logged <c>ex.Message</c> can leak broker URIs, connection strings,
/// or raw user input. All exception type-name rendering in this layer uses
/// <c>SanitizedExceptionRender.TypeName</c> + <c>FirstFrame</c> separately.
/// </remarks>
public sealed class KeyCustodianLogTests
{
    // Pass method names (strings) — MethodInfo is not xUnit-serializable.
    public static TheoryData<string> LoggerMessageMethodNames()
    {
        var type = typeof(KeyCustodianLog);
        var data = new TheoryData<string>();

        // Discover all static partial methods generated from [LoggerMessage].
        // The source-generator emits the *implementation* as a non-partial method;
        // we walk the declared methods (public + non-public) for those carrying the
        // [LoggerMessage] attribute, which the generator preserves on the delegate.
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
        var type = typeof(KeyCustodianLog);
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
                + "ex.Message can leak broker URIs or raw user input into log sinks "
                + "(§3.1); use SanitizedExceptionRender.TypeName + FirstFrame instead");
    }

    [Fact]
    public void KeyCustodianLog_HasAtLeastOneLoggerMessageDelegate()
    {
        // Guard: if the source generator is ever removed or the class renamed, this
        // fails loudly instead of the theory silently passing on an empty data set.
        var methods = LoggerMessageMethodNames();
        methods.Should().NotBeEmpty(
            because: "KeyCustodianLog must declare at least one [LoggerMessage] delegate");
    }
}
