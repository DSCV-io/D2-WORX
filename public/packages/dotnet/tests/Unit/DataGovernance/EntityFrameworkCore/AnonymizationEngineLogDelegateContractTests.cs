// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngineLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using System;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Xunit;

/// <summary>
/// PII-safety contract for the data-governance stack <c>[LoggerMessage]</c>
/// delegates in <see cref="AnonymizationEngineLog"/>. Subject ids are never logged;
/// any exception that reaches the engine (e.g. from a DB driver) may embed
/// connection strings, user input, or other sensitive content in its
/// <c>Message</c> property. Log sinks format the exception via
/// <c>ex.ToString()</c> and persist the resulting string verbatim, so every
/// delegate that handles an exception MUST accept <c>string exType</c> +
/// <c>string firstFrame</c> rather than a raw <see cref="Exception"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnonymizationEngineLogDelegateContractTests
{
    // All 9 delegates on AnonymizationEngineLog (EventIds 9400–9408).
    public static TheoryData<string> AllDelegates => new()
    {
        nameof(AnonymizationEngineLog.SweepStarted),
        nameof(AnonymizationEngineLog.EntityTypeDone),
        nameof(AnonymizationEngineLog.SweepComplete),
        nameof(AnonymizationEngineLog.TierCReachedRuntime),
        nameof(AnonymizationEngineLog.EntityTypeError),
        nameof(AnonymizationEngineLog.TierBConcurrencyRetry),
        nameof(AnonymizationEngineLog.TierBConcurrencyExhausted),
        nameof(AnonymizationEngineLog.TierASetNullMisconfiguration),
        nameof(AnonymizationEngineLog.TierAPlanInvalid),
    };

    [Theory]
    [MemberData(nameof(AllDelegates))]
    public void AnonymizationEngineLogDelegate_DoesNotTakeRawException(string method)
    {
        var info = typeof(AnonymizationEngineLog).GetMethod(
            method, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"[LoggerMessage] delegate '{method}' must exist on AnonymizationEngineLog");

        var hasExceptionParam = info.GetParameters()
            .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType));

        hasExceptionParam.Should().BeFalse(
            $"AnonymizationEngineLog.{method} must not accept a raw Exception — "
            + "log sinks format ex.ToString() verbatim, which can leak ex.Message "
            + "contents (e.g. DB connection strings, user input) into structured logs. "
            + "Use SanitizedExceptionRender.TypeName(ex) + FirstFrame(ex) as separate "
            + "string parameters instead.");
    }
}
