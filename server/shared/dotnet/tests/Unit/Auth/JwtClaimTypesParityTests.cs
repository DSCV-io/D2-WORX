// -----------------------------------------------------------------------
// <copyright file="JwtClaimTypesParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using Xunit;

/// <summary>
/// Parity test: every <c>claim:</c> annotation in the IAuthContext spec MUST
/// have a matching <see cref="JwtClaimTypes"/> constant. Drift safety — adding
/// a new claim to the spec without a constant means handlers reading the
/// claim will use a magic string that doesn't survive renaming.
/// </summary>
public sealed class JwtClaimTypesParityTests
{
    [Fact]
    public void EverySpecClaimAnnotation_HasMatchingJwtClaimTypesConstant()
    {
        var spec_path = TestPaths.AuthContextSpec();
        File.Exists(spec_path).Should().BeTrue("spec file must be present at " + spec_path);

        var spec_json = File.ReadAllText(spec_path);
        var spec = JsonDocument.Parse(spec_json);

        // Walk sections[].properties[] and collect every distinct `claim:` value.
        var spec_claims = new HashSet<string>();
        foreach (var section in spec.RootElement.GetProperty("sections").EnumerateArray())
        {
            foreach (var property in section.GetProperty("properties").EnumerateArray())
            {
                if (property.TryGetProperty("claim", out var claim_value))
                {
                    var claim = claim_value.GetString();
                    if (!string.IsNullOrEmpty(claim))
                        spec_claims.Add(claim);
                }
            }
        }

        spec_claims.Should().NotBeEmpty(
            "spec should declare at least one claim-mapped property");

        // Reflect on JwtClaimTypes — collect every const string value.
        var declared_constants = typeof(JwtClaimTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToHashSet();

        // Adversarial: every spec claim MUST appear in the constants set.
        // If this fails, the spec drifted ahead of JwtClaimTypes.
        var missing = spec_claims.Except(declared_constants).ToList();
        missing.Should().BeEmpty(
            "every claim referenced in the spec must have a matching JwtClaimTypes constant; " +
            "missing: " + string.Join(", ", missing));
    }
}
