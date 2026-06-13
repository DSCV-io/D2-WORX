// -----------------------------------------------------------------------
// <copyright file="KeyCustodianInfraLogTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using D2.Edge.KeyCustodian.Infra.Observability;
using Microsoft.Extensions.Logging;

/// <summary>
/// Contract-pin tests for <see cref="KeyCustodianInfraLog"/> — verifies that no
/// <c>[LoggerMessage]</c>-attributed delegate accepts an <see cref="Exception"/>
/// (or <see cref="Exception"/>-derived) parameter.
/// </summary>
/// <remarks>
/// Per §3.1: a logged <c>ex.Message</c> can leak broker URIs, connection strings,
/// or raw key bytes into log sinks. All exception rendering in the Infra layer
/// uses <c>SanitizedExceptionRender.TypeName</c> + <c>FirstFrame</c> separately,
/// passing the results as plain strings.
/// </remarks>
public sealed class KeyCustodianInfraLogTests
{
    // Pass method names (strings) — MethodInfo is not xUnit-serializable.

    /// <summary>
    /// Returns the names of all static methods on
    /// <see cref="KeyCustodianInfraLog"/> that carry a
    /// <see cref="LoggerMessageAttribute"/>.
    /// </summary>
    /// <returns>A <see cref="TheoryData{T}"/> of method name strings.</returns>
    public static TheoryData<string> LoggerMessageMethodNames()
    {
        var type = typeof(KeyCustodianInfraLog);
        var data = new TheoryData<string>();

        // Discover all static methods (public + non-public) bearing [LoggerMessage].
        // The source-generator emits the implementation as a non-partial method and
        // preserves the [LoggerMessage] attribute on it.
        foreach (var method in type.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
                data.Add(method.Name);
        }

        return data;
    }

    /// <summary>
    /// Asserts that each <c>[LoggerMessage]</c> delegate on
    /// <see cref="KeyCustodianInfraLog"/> does not accept an
    /// <see cref="Exception"/> parameter.
    /// </summary>
    /// <param name="methodName">The delegate method name under test.</param>
    [Theory]
    [MemberData(nameof(LoggerMessageMethodNames))]
    public void LoggerMessageDelegate_DoesNotAcceptExceptionParameter(string methodName)
    {
        var type = typeof(KeyCustodianInfraLog);
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
                + "ex.Message can leak broker URIs, connection strings, or raw key "
                + "bytes into log sinks (§3.1); use SanitizedExceptionRender.TypeName "
                + "+ FirstFrame instead");
    }

    /// <summary>
    /// Guards that at least 12 <c>[LoggerMessage]</c> delegates are declared on
    /// <see cref="KeyCustodianInfraLog"/> (EventIds 9530–9551). If the source
    /// generator is removed or the class is renamed this fails loudly rather than
    /// the theory silently passing on an empty data set.
    /// </summary>
    [Fact]
    public void KeyCustodianInfraLog_HasAtLeastTwelveLoggerMessageDelegates()
    {
        var methods = LoggerMessageMethodNames();
        methods.Count.Should().BeGreaterThanOrEqualTo(
            12,
            because:
                "KeyCustodianInfraLog must declare at least 12 [LoggerMessage] "
                + "delegates covering EventIds 9530-9535 (vault), 9540-9544 "
                + "(scheduling), and 9550-9551 (messaging announce)");
    }
}
