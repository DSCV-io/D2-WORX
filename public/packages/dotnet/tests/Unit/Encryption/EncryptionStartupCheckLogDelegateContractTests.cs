// -----------------------------------------------------------------------
// <copyright file="EncryptionStartupCheckLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Pins the no-Exception-parameter contract on the encryption-core startup-check
/// <c>[LoggerMessage]</c> delegate classes (<c>EncryptionSourceStartupCheckLog</c> and
/// <c>SealedEncryptionStartupCheckLog</c>). A logged exception from a crypto /
/// registration path can embed key-adjacent material or configuration detail in
/// <c>ex.Message</c>; callers pass <c>SanitizedExceptionRender.TypeName(ex)</c> as a
/// separate string parameter instead (rules.md §3.1). Mirrors the enforcement pattern in
/// <c>DcsvIo.D2.Tests.Unit.Mtls.MtlsLogDelegateContractTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EncryptionStartupCheckLogDelegateContractTests
{
    [Theory]
    [InlineData("EncryptionSourceStartupCheckLog")]
    [InlineData("SealedEncryptionStartupCheckLog")]
    public void LogClass_NoDelegateAcceptsExceptionParameter(string logTypeName)
    {
        var logType = typeof(PayloadCryptoKeyring).Assembly
            .GetTypes()
            .Single(t => t.Name == logTypeName
                && t.Namespace == "DcsvIo.D2.Encryption");

        var leakProneMethods = logType
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.GetParameters()
                .Any(p => typeof(Exception).IsAssignableFrom(p.ParameterType)))
            .Select(m => m.Name)
            .ToList();

        leakProneMethods.Should().BeEmpty(
            logTypeName + " [LoggerMessage] delegates must not accept Exception parameters; "
            + "an exception message can leak key-adjacent or registration detail from a "
            + "crypto/startup path. Use SanitizedExceptionRender.TypeName(ex) as a separate "
            + "string parameter instead. Offending delegates: "
            + string.Join(", ", leakProneMethods));
    }
}
