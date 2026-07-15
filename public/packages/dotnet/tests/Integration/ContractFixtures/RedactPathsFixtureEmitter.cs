// -----------------------------------------------------------------------
// <copyright file="RedactPathsFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.AuthContext.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Utilities.Attributes;
using Xunit;

/// <summary>
/// Emits the canonical (sorted) list of PII-bearing properties on
/// <see cref="IAuthContext"/> and <see cref="IRequestContext"/> as
/// determined by the .NET <c>[RedactData]</c> attribute. The TS-side
/// <c>IAuthContextRedactPaths</c> and <c>IRequestContextRedactPaths</c>
/// arrays must enumerate the same set (camelCased), one per redacted
/// property.
/// </summary>
public sealed class RedactPathsFixtureEmitter
{
    private const string CATALOG = "redact-paths";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_AuthContext()
    {
        var paths = RedactedPropertyPaths(typeof(IAuthContext));

        // Wrapped in a record-like shape so the parity test can assert
        // the array deep-equals the TS-side IAuthContextRedactPaths.
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["paths"] = paths,
        };
        FixturePathHelpers.WriteFixture(CATALOG, "auth-context", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_RequestContext()
    {
        // IRequestContext only — exclude inherited IAuthContext properties so
        // the fixture mirrors the TS-side IRequestContextRedactPaths array
        // (which lists request-context-specific paths only; IAuthContext
        // paths live in IAuthContextRedactPaths).
        var paths = RedactedPropertyPathsDeclaredOn(typeof(IRequestContext));
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["paths"] = paths,
        };
        FixturePathHelpers.WriteFixture(CATALOG, "request-context", data);
    }

    /// <summary>
    /// Enumerate every property on the type (including via interface
    /// inheritance) that carries the <c>[RedactData]</c> attribute,
    /// returning the camelCased name in sorted order.
    /// </summary>
    /// <param name="type">Interface or class type to reflect.</param>
    /// <returns>Sorted camelCase property names.</returns>
    private static List<string> RedactedPropertyPaths(Type type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var redacted = new List<string>();
        foreach (var p in EnumerateAllProperties(type))
        {
            if (p.GetCustomAttribute<RedactDataAttribute>() is null) continue;
            if (!seen.Add(p.Name)) continue;
            redacted.Add(CamelCase(p.Name));
        }

        redacted.Sort(StringComparer.Ordinal);
        return redacted;
    }

    /// <summary>
    /// Same as <see cref="RedactedPropertyPaths"/> but excludes
    /// properties inherited from a base interface — IRequestContext's
    /// own properties only.
    /// </summary>
    /// <param name="type">Interface or class type to reflect.</param>
    /// <returns>Sorted camelCase property names declared on the type.</returns>
    private static List<string> RedactedPropertyPathsDeclaredOn(Type type)
    {
        var inheritedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var baseType in type.GetInterfaces())
        {
            foreach (var p in baseType.GetProperties())
                inheritedNames.Add(p.Name);
        }

        return type
            .GetProperties()
            .Where(p => p.GetCustomAttribute<RedactDataAttribute>() is not null)
            .Where(p => !inheritedNames.Contains(p.Name))
            .Select(p => CamelCase(p.Name))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Walks interface inheritance — returns every public property
    /// from the type and its base interfaces.
    /// </summary>
    /// <param name="type">Interface type to enumerate.</param>
    /// <returns>Property infos from the type + inherited interfaces.</returns>
    private static IEnumerable<PropertyInfo> EnumerateAllProperties(Type type)
    {
        foreach (var p in type.GetProperties()) yield return p;
        foreach (var iface in type.GetInterfaces())
        {
            foreach (var p in iface.GetProperties())
                yield return p;
        }
    }

    private static string CamelCase(string pascal)
    {
        if (pascal.Length == 0) return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
    }
}
