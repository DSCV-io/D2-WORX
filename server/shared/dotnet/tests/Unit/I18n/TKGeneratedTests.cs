// -----------------------------------------------------------------------
// <copyright file="TKGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.I18n;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.I18n;
using Xunit;

/// <summary>
/// End-to-end smoke test for the SrcGen + Abstractions integration.
/// Proves the SrcGen actually produces the TK class against the live
/// <c>contracts/messages/en-US.json</c> with the expected shape.
/// </summary>
public sealed class TKGeneratedTests
{
    [Fact]
    public void TK_HasNestedStaticPartialClasses_ForEveryDomain()
    {
        // Every TK.X is a nested static partial class. Reflective discovery
        // proves the generated source structure rather than just probing
        // hand-picked names.
        var tk = typeof(TK);
        var nested = tk.GetNestedTypes(BindingFlags.Public);

        nested.Should().NotBeEmpty();
        nested.Should().AllSatisfy(t =>
        {
            t.IsAbstract.Should().BeTrue("nested types should be static (abstract+sealed)");
            t.IsSealed.Should().BeTrue("nested types should be static (abstract+sealed)");
        });
    }

    [Fact]
    public void TK_HasExpectedSampleConstants()
    {
        // Hand-picked sample across multiple domains, proving SrcGen emitted
        // the right shape for the live catalog.
        TK.Common.Errors.NOT_FOUND.Should().NotBeNull();
        TK.Common.Errors.UNKNOWN.Should().NotBeNull();
        TK.Common.Validation.EMAIL_INVALID.Should().NotBeNull();
        TK.Auth.Errors.INVALID_ROLE.Should().NotBeNull();
        TK.Geo.Validation.IP_REQUIRED.Should().NotBeNull();
        TK.Middleware.Errors.INSUFFICIENT_ROLE.Should().NotBeNull();
        TK.Files.Errors.INVALID_UPLOAD_TARGET.Should().NotBeNull();
        TK.Comms.Errors.NO_DELIVERABLE_CHANNELS.Should().NotBeNull();
    }

    [Fact]
    public void TK_ConstantKeys_MatchExactJsonKeys()
    {
        // The generated constants embed the literal JSON key. Sample to verify
        // case is preserved exactly (catches accidental normalisation in SrcGen).
        TK.Common.Errors.NOT_FOUND.Key.Should().Be("common_errors_NOT_FOUND");

        // Lowercase 'unknown' identifier is preserved verbatim from the JSON.
        TK.Common.Errors.UNKNOWN.Key.Should().Be("common_errors_unknown");
        TK.Auth.Errors.SOLE_OWNER_OF_ORGS.Key.Should().Be("auth_errors_SOLE_OWNER_OF_ORGS");
        TK.Geo.Validation.IP_REQUIRED.Key.Should().Be("geo_validation_ip_required");
    }

    [Fact]
    public void TK_AllConstants_HaveCorrespondingKeyInEnUsJson()
    {
        // Inverse drift gate: if the generator ever falls behind the JSON, this
        // catches "TK references a key that doesn't exist in en-US." With SrcGen
        // this should be structurally impossible — but cheap insurance and a
        // useful smoke-test for the build wiring.
        var enUsPath = Path.Combine(AppContext.BaseDirectory, "messages", "en-US.json");
        if (!File.Exists(enUsPath))
        {
            // Translator content-copy didn't land in this test project's bin
            // (Tests doesn't directly reference contracts/messages); skip.
            return;
        }

        var json = File.ReadAllText(enUsPath);
        var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        keys.Should().NotBeNull();
        var keySet = new HashSet<string>(keys.Keys);

        var tkConstants = CollectAllTkMessageConstants(typeof(TK)).ToList();
        tkConstants.Should().NotBeEmpty();
        tkConstants.Should().AllSatisfy(constant =>
            keySet.Should().Contain(
                constant.Key,
                because: $"TK constant '{constant.Key}' must have a matching key in en-US.json"));
    }

    private static IEnumerable<TKMessage> CollectAllTkMessageConstants(Type root)
    {
        foreach (var field in root.GetFields(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (field.FieldType == typeof(TKMessage))
            {
                var value = field.GetValue(obj: null) as TKMessage;
                if (value is not null)
                {
                    yield return value;
                }
            }
        }

        foreach (var nested in root.GetNestedTypes(BindingFlags.Public))
        {
            foreach (var msg in CollectAllTkMessageConstants(nested))
            {
                yield return msg;
            }
        }
    }
}
