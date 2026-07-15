// -----------------------------------------------------------------------
// <copyright file="RequestContextFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using DcsvIo.D2.AuthContext.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

/// <summary>
/// Emits IRequestContext property-surface fixtures. The fixture
/// <c>data.properties</c> is the sorted union of properties declared on
/// <see cref="IRequestContext"/> AND inherited from
/// <see cref="IAuthContext"/> — verifying the TS-side
/// <c>IRequestContext extends IAuthContext</c> declaration carries the
/// full transitive shape. The fixture <c>data.ownProperties</c> field
/// lists only properties declared directly on IRequestContext for an
/// independent assertion of the extension surface.
/// </summary>
public sealed class RequestContextFixtureEmitter
{
    private const string CATALOG = "request-context";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Minimal()
    {
        const string scenario = "minimal";
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario));
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_FullWithFingerprints()
    {
        const string scenario = "full-with-fingerprints";
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario));
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_WithWhois()
    {
        const string scenario = "with-whois";
        FixturePathHelpers.WriteFixture(CATALOG, scenario, BuildScenario(scenario));
    }

    /// <summary>
    /// Build the canonical fixture payload: union of IRequestContext +
    /// transitive IAuthContext property surface (camelCased, sorted),
    /// plus the IRequestContext-only subset. The TS-side parity test
    /// asserts both lists exactly match the matching TS-side shape.
    /// </summary>
    /// <param name="scenario">Scenario name annotation.</param>
    /// <returns>Fixture payload dictionary.</returns>
    private static SortedDictionary<string, object?> BuildScenario(string scenario)
    {
        var inheritedNames = AllPropertyNames(typeof(IAuthContext));
        var allNames = AllPropertyNames(typeof(IRequestContext));

        var allProperties = allNames
            .Select(CamelCase)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var ownProperties = allNames
            .Where(n => !inheritedNames.Contains(n))
            .Select(CamelCase)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // The envelope already carries the scenario name; here we only emit
        // the parity-tested property surface. The `scenario` argument is
        // retained for symmetry with other emitter signatures (and to
        // provide a place for future per-scenario differentiation).
        _ = scenario;
        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["properties"] = allProperties,
            ["ownProperties"] = ownProperties,
        };
    }

    /// <summary>
    /// Walks the interface inheritance chain and returns the union of
    /// every public property name. Interfaces don't surface
    /// inherited properties via <c>BindingFlags.FlattenHierarchy</c>,
    /// so we walk <see cref="Type.GetInterfaces"/> manually.
    /// </summary>
    /// <param name="type">Interface type to enumerate.</param>
    /// <returns>Set of property names, deduplicated.</returns>
    private static HashSet<string> AllPropertyNames(Type type)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in type.GetProperties())
            result.Add(p.Name);
        foreach (var iface in type.GetInterfaces())
        {
            foreach (var p in iface.GetProperties())
                result.Add(p.Name);
        }

        return result;
    }

    private static string CamelCase(string pascal)
    {
        if (pascal.Length == 0) return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
    }
}
