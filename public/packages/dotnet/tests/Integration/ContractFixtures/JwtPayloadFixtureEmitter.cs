// -----------------------------------------------------------------------
// <copyright file="JwtPayloadFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Emits JWT-payload wire-shape fixtures. The .NET side does not have a
/// typed <c>JwtPayload</c> record (it parses claims via
/// <c>System.Security.Claims.ClaimsPrincipal</c> + the spec-emitted
/// <c>JwtClaimTypes</c> constants), but the wire shape — a JSON object
/// mapping claim names to typed values — is shared. Each fixture
/// scenario lists the spec-defined claim names that are populated for
/// that scenario; the TS-side parity test asserts the
/// codegen-emitted <c>JwtPayload</c> typed interface accepts every
/// listed claim.
/// </summary>
public sealed class JwtPayloadFixtureEmitter
{
    private const string CATALOG = "jwt-payload";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Minimal()
    {
        // Only standard `sub` populated.
        var claims = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sub"] = "00000000-0000-0000-0000-000000000001",
        };
        FixturePathHelpers.WriteFixture(CATALOG, "minimal", BuildScenario("minimal", claims));
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_D2CustomClaimsOnly()
    {
        const string scenario = "d2-custom-claims-only";

        // Synthetic 10-segment fingerprint — opaque blob; value content
        // is not parity-relevant. Concatenated across source lines so no
        // single source line exceeds the 100-char cap.
        const string syntheticFingerprint =
            "v1.aaaaaaaaaaaaaaaa.bbbbbbbbbbbbbbbb.cccccccccccccccc"
            + ".dddddddddddddddd.eeeeeeeeeeeeeeee.ffffffffffffffff"
            + ".0000000000000000.1111111111111111.2222222222222222"
            + ".3333333333333333";

        // Every d2_-prefixed custom claim populated.
        var claims = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sub"] = "00000000-0000-0000-0000-000000000002",
            ["d2_session_id"] = "session-00000002",
            ["d2_username"] = "synthetic.user",
            ["d2_fp"] = syntheticFingerprint,
            ["d2_org_id"] = "00000000-0000-0000-0000-0000000000aa",
            ["d2_org_name"] = "Synthetic Org",
            ["d2_org_type"] = "Customer",
            ["d2_org_role"] = "Owner",
        };
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario, claims));
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_WithActChain()
    {
        const string scenario = "with-act-chain";

        // RFC 8693 act chain — nested object inside `act`.
        var act = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sub"] = "00000000-0000-0000-0000-0000000000bb",
            ["d2_kind"] = "consent",
            ["d2_session_id"] = "imp-session-00000003",
        };
        var claims = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sub"] = "00000000-0000-0000-0000-000000000003",
            ["act"] = act,
        };
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario, claims));
    }

    /// <summary>
    /// Wrap the scenario's claim set with the canonical list of all
    /// spec-defined claim names — the TS-side parity test verifies (a)
    /// every claim in the fixture's <c>claims</c> map appears in the
    /// spec's claim list, and (b) the spec's full claim list matches
    /// the .NET-emitted catalog.
    /// </summary>
    private static SortedDictionary<string, object?> BuildScenario(
        string scenario,
        SortedDictionary<string, object?> claims)
    {
        var specClaimNames = LoadSpecClaimNames();
        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scenario"] = scenario,
            ["specClaimNames"] = specClaimNames,
            ["claims"] = claims,
        };
    }

    private static List<string> LoadSpecClaimNames()
    {
        var path = TestPaths.JwtClaimsSpec();
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        var names = new List<string>();
        foreach (var c in doc.RootElement.GetProperty("claims").EnumerateArray())
            names.Add(c.GetProperty("value").GetString()!);

        return names
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }
}
