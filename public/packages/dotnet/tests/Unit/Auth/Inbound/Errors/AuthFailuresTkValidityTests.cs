// -----------------------------------------------------------------------
// <copyright file="AuthFailuresTkValidityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Errors;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Auth.Errors;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.Extensions.Configuration;
using Xunit;

/// <summary>
/// Cross-runtime TK-validity RENDER test (.NET half). For EVERY auth
/// error-code spec entry, asserts the ACTUAL wire <see cref="TKMessage"/> the
/// emitted <see cref="AuthFailures"/> factory produces RENDERS to real text —
/// not the raw key — via the <see cref="Translator"/> over
/// <c>contracts/messages</c>. The TS half
/// (<c>error-codes-tk-validity.parity.test.ts</c>) asserts the same invariant
/// on the TS catalog; both runtimes must render the same text. Drives off the
/// ACTUAL factory output (not a re-derived key), so the test guards the real
/// wire path, and is data-driven over the spec so a future added entry is
/// automatically covered.
/// </summary>
public sealed class AuthFailuresTkValidityTests
{
    public static TheoryData<string, string> AuthSpecEntries()
    {
        var json = File.ReadAllText(TestPaths.AuthErrorCodesSpec());
        using var doc = JsonDocument.Parse(json);
        var data = new TheoryData<string, string>();
        foreach (var entry in doc.RootElement.GetProperty("errorCodes").EnumerateArray())
        {
            var code = entry.GetProperty("code").GetString()!;
            var factoryName = entry.GetProperty("factoryName").GetString()!;
            data.Add(code, factoryName);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AuthSpecEntries))]
    public void EveryAuthFailureMessage_RendersToRealText_NotTheRawKey(
        string code,
        string factoryName)
    {
        var translator = new Translator(NewSupportedLocales(), TestPaths.MessagesDirectory());

        var result = InvokeFactory(factoryName);
        result.ErrorCode.Should().Be(code);
        result.Messages.Should().ContainSingle();

        var message = result.Messages[0];
        var rendered = translator.T("en-US", message);

        // Renders to something OTHER than the raw wire key — proves the key
        // actually resolved in the catalog (the cross-runtime render invariant).
        rendered.Should().NotBe(
            message.Key,
            because: $"the '{code}' factory's wire key must resolve to real en-US text, "
                + "not fall through to the raw-key passthrough");
        rendered.Should().NotBeNullOrEmpty();

        // And matches the en-US source text for that key exactly.
        var expected = ExpectedText(message.Key);
        rendered.Should().Be(expected);
    }

    private static D2Result InvokeFactory(string factoryName)
    {
        // The delegating factory carries a single optional `messages` override
        // param; passing null exercises the default-omitted (spec-TK) path so
        // the render assertion drives off the spec's default message.
        var method = typeof(AuthFailures)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == factoryName
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1);
        return (D2Result)method.Invoke(null, [null])!;
    }

    private static string ExpectedText(string key)
    {
        var enUsPath = Path.Combine(TestPaths.MessagesDirectory(), "en-US.json");
        var json = File.ReadAllText(enUsPath);
        var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        catalog.Should().ContainKey(key);
        return catalog[key];
    }

    private static SupportedLocales NewSupportedLocales()
    {
        var dict = new Dictionary<string, string?> { ["PUBLIC_ENABLED_LOCALES:0"] = "en-US" };
        return new SupportedLocales(
            new ConfigurationBuilder().AddInMemoryCollection(dict).Build());
    }
}
