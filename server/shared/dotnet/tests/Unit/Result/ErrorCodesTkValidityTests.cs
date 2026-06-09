// -----------------------------------------------------------------------
// <copyright file="ErrorCodesTkValidityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Result;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.Extensions.Configuration;
using Xunit;

/// <summary>
/// TK-validity RENDER test (.NET half) for the GENERIC error-code catalog. For
/// every generic spec entry that ships a constructing factory
/// (<c>factoryShape != none</c>), asserts the ACTUAL wire
/// <see cref="TKMessage"/> the generated <see cref="D2Result"/> factory
/// produces RENDERS to real en-US text — not the raw key — via the
/// <see cref="Translator"/> over <c>contracts/messages</c>. Mirrors the auth
/// <c>AuthFailuresTkValidityTests</c>; the TS half lands in the cross-runtime
/// parity suite. Data-driven over the spec so a future entry is auto-covered.
/// </summary>
public sealed class ErrorCodesTkValidityTests
{
    public static TheoryData<string, string, string> FactoryBearingSpecEntries()
    {
        var json = File.ReadAllText(GenericSpecPath());
        using var doc = JsonDocument.Parse(json);
        var data = new TheoryData<string, string, string>();
        foreach (var entry in doc.RootElement.GetProperty("errorCodes").EnumerateArray())
        {
            var shape = entry.GetProperty("factoryShape").GetString()!;

            // none-shape codes emit no factory — nothing to render.
            if (shape == "none")
                continue;

            var code = entry.GetProperty("code").GetString()!;
            var factoryName = entry.GetProperty("factoryName").GetString()!;
            var userMessageKey = entry.GetProperty("userMessageKey").GetString()!;
            data.Add(code, factoryName, userMessageKey);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(FactoryBearingSpecEntries))]
    public void EveryGenericFactoryMessage_RendersToRealText_NotTheRawKey(
        string code,
        string factoryName,
        string userMessageKey)
    {
        var translator = new Translator(NewSupportedLocales(), MessagesDirectory());

        var result = InvokeFactory(factoryName);
        result.ErrorCode.Should().Be(code);
        result.Messages.Should().ContainSingle();

        var message = result.Messages[0];
        var rendered = translator.T("en-US", message);

        rendered.Should().NotBe(
            message.Key,
            because: $"the '{code}' factory's wire key must resolve to real en-US text, "
                + "not fall through to the raw-key passthrough");
        rendered.Should().NotBeNullOrEmpty();

        var expected = ExpectedText(message.Key);
        rendered.Should().Be(expected);

        // The wire key the factory emits must be the inverse-snake of the spec's
        // userMessageKey symbol path (e.g. TK.Common.Errors.UNKNOWN ->
        // common_errors_UNKNOWN) — pins the code <-> userMessageKey link for the
        // two name-mismatch quirks (UNHANDLED_EXCEPTION/RATE_LIMITED).
        message.Key.Should().Be(SnakeFromSymbolPath(userMessageKey));
    }

    private static D2Result InvokeFactory(string factoryName)
    {
        var method = typeof(D2Result)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == factoryName
                && !m.IsGenericMethodDefinition
                && m.GetParameters().All(p => p.IsOptional));
        var args = method.GetParameters().Select(_ => Type.Missing).ToArray();
        return (D2Result)method.Invoke(null, args)!;
    }

    private static string SnakeFromSymbolPath(string symbolPath)
    {
        // TK.Common.Errors.UNKNOWN -> common_errors_UNKNOWN
        var segments = symbolPath.Split('.');
        var domain = char.ToLowerInvariant(segments[1][0]) + segments[1].Substring(1);
        var category = char.ToLowerInvariant(segments[2][0]) + segments[2].Substring(1);
        return $"{domain}_{category}_{segments[3]}";
    }

    private static string ExpectedText(string key)
    {
        var enUsPath = Path.Combine(MessagesDirectory(), "en-US.json");
        var json = File.ReadAllText(enUsPath);
        var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        catalog.Should().ContainKey(key);
        return catalog[key];
    }

    private static string GenericSpecPath() =>
        Path.Combine(TestPaths.RepoRoot(), "contracts", "error-codes", "error-codes.spec.json");

    private static string MessagesDirectory() => TestPaths.MessagesDirectory();

    private static SupportedLocales NewSupportedLocales()
    {
        var dict = new Dictionary<string, string?> { ["PUBLIC_ENABLED_LOCALES:0"] = "en-US" };
        return new SupportedLocales(
            new ConfigurationBuilder().AddInMemoryCollection(dict).Build());
    }
}
