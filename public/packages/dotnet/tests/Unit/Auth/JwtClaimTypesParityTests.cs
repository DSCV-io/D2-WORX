// -----------------------------------------------------------------------
// <copyright file="JwtClaimTypesParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Utilities.Extensions;
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
        var specPath = TestPaths.AuthContextSpec();
        File.Exists(specPath).Should().BeTrue("spec file must be present at " + specPath);

        var specJson = File.ReadAllText(specPath);
        var spec = JsonDocument.Parse(specJson);

        // Walk sections[].properties[] and collect every distinct `claim:` value.
        var specClaims = new HashSet<string>();
        foreach (var section in spec.RootElement.GetProperty("sections").EnumerateArray())
        {
            foreach (var property in section.GetProperty("properties").EnumerateArray())
            {
                if (property.TryGetProperty("claim", out var claimValue))
                {
                    var claim = claimValue.GetString();
                    if (claim.Truthy())
                        specClaims.Add(claim!);
                }
            }
        }

        specClaims.Should().NotBeEmpty(
            "spec should declare at least one claim-mapped property");

        // Reflect on JwtClaimTypes — collect every const string value.
        var declaredConstants = typeof(JwtClaimTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToHashSet();

        // Adversarial: every spec claim MUST appear in the constants set.
        // If this fails, the spec drifted ahead of JwtClaimTypes.
        var missing = specClaims.Except(declaredConstants).ToList();
        missing.Should().BeEmpty(
            "every claim referenced in the spec must have a matching JwtClaimTypes constant; " +
            "missing: " + string.Join(", ", missing));
    }
}
