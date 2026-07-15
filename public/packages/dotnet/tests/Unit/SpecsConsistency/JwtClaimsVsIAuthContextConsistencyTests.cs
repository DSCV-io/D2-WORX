// -----------------------------------------------------------------------
// <copyright file="JwtClaimsVsIAuthContextConsistencyTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Tests.Unit.Auth;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

/// <summary>
/// Cross-spec consistency: every <c>claim:</c> annotation in
/// <c>contracts/auth-context/IAuthContext.spec.json</c> must reference an
/// existing entry (by <c>value</c>) in
/// <c>contracts/jwt-claims/jwt-claims.spec.json</c>. Forward direction only —
/// reverse (every jwt-claims entry has a corresponding IAuthContext property)
/// is intentionally not enforced because <c>IAT</c> / <c>EXP</c> /
/// <c>ACT_KIND</c> live outside the top-level IAuthContext properties.
/// </summary>
public sealed class JwtClaimsVsIAuthContextConsistencyTests
{
    [Fact]
    public void EveryAuthContextClaimAnnotation_ReferencesAJwtClaimsSpecEntry()
    {
        var authContextClaims = LoadAuthContextClaimAnnotations();
        var jwtClaimsValues = LoadJwtClaimsSpecValues();

        authContextClaims.Should().NotBeEmpty(
            "IAuthContext spec should declare at least one claim-mapped property");
        jwtClaimsValues.Should().NotBeEmpty(
            "jwt-claims spec should declare at least one claim entry");

        var missing = authContextClaims.Except(jwtClaimsValues).ToList();
        missing.Should().BeEmpty(
            "every IAuthContext claim: annotation must reference a jwt-claims entry " +
            "(by value); missing: " + string.Join(", ", missing));
    }

    [Fact]
    public void JwtClaimsSpec_HasNoDuplicateConstNames()
    {
        var entries = LoadJwtClaimsEntries();
        var constNames = entries.Select(e => e.ConstName).ToList();
        constNames.Should().OnlyHaveUniqueItems(
            "jwt-claims spec must not declare a constName twice");
    }

    [Fact]
    public void JwtClaimsSpec_KindValuesAreInClosedEnum()
    {
        var entries = LoadJwtClaimsEntries();
        var validKinds = new HashSet<string> { "standard", "d2-custom", "inside-act" };
        foreach (var entry in entries)
        {
            validKinds.Should().Contain(
                entry.Kind,
                $"claim '{entry.ConstName}' has kind '{entry.Kind}' outside the closed enum");
        }
    }

    [Fact]
    public void JwtClaimsSpec_ContainsAtLeastEveryStandardOAuthClaim()
    {
        var entries = LoadJwtClaimsEntries();
        var values = entries.Select(e => e.Value).ToHashSet();
        var standardOAuth = new[]
        {
            "sub", "aud", "iat", "exp", "azp", "scope", "act", "client_id",
        };
        foreach (var c in standardOAuth)
        {
            values.Should().Contain(
                c,
                $"jwt-claims spec must include the standard OAuth claim '{c}' " +
                "(consumer code reads it via JwtClaimTypes constants)");
        }
    }

    [Fact]
    public void JwtClaimsSpec_ConstNamesAreUpperSnakeCase()
    {
        var entries = LoadJwtClaimsEntries();
        foreach (var entry in entries)
        {
            // Inline check — UPPER_SNAKE_CASE: starts with [A-Z], rest are
            // [A-Z0-9_]. Avoids the Regex/GeneratedRegex ceremony for a tiny
            // pure-ASCII validator.
            IsUpperSnakeCase(entry.ConstName).Should().BeTrue(
                $"claim constName '{entry.ConstName}' must be UPPER_SNAKE_CASE");
        }
    }

    private static bool IsUpperSnakeCase(string s)
    {
        if (s.Falsey()) return false;
        if (s[0] < 'A' || s[0] > 'Z') return false;
        for (int i = 1; i < s.Length; i++)
        {
            var c = s[i];
            var ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
            if (!ok) return false;
        }

        return true;
    }

    private static HashSet<string> LoadAuthContextClaimAnnotations()
    {
        var path = TestPaths.AuthContextSpec();
        File.Exists(path).Should().BeTrue("spec file must be present at " + path);
        var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new HashSet<string>();
        foreach (var section in doc.RootElement.GetProperty("sections").EnumerateArray())
        {
            foreach (var property in section.GetProperty("properties").EnumerateArray())
            {
                if (property.TryGetProperty("claim", out var claimEl))
                {
                    var claim = claimEl.GetString();
                    if (claim.Truthy()) result.Add(claim!);
                }
            }
        }

        return result;
    }

    private static HashSet<string> LoadJwtClaimsSpecValues() =>
        LoadJwtClaimsEntries().Select(e => e.Value).ToHashSet();

    private static List<JwtClaimEntry> LoadJwtClaimsEntries()
    {
        var path = TestPaths.JwtClaimsSpec();
        File.Exists(path).Should().BeTrue("spec file must be present at " + path);
        var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new List<JwtClaimEntry>();
        foreach (var element in doc.RootElement.GetProperty("claims").EnumerateArray())
        {
            result.Add(new JwtClaimEntry(
                ConstName: element.GetProperty("constName").GetString()!,
                Value: element.GetProperty("value").GetString()!,
                Kind: element.GetProperty("kind").GetString()!));
        }

        return result;
    }

    private sealed record JwtClaimEntry(string ConstName, string Value, string Kind);
}
