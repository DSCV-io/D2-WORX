// -----------------------------------------------------------------------
// <copyright file="ScopesFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// Emits the parity fixture for the OAuth scope catalog reflected off
/// the .NET codegen-emitted static class <see cref="Scopes"/>. The
/// catalog is nested (<c>Scopes.Auth.User.Impersonate.Force</c>) — the
/// emitter recursively walks the nested static classes and collects
/// every <c>public const string</c> leaf into a sorted
/// <c>{ scopePath: wireValue }</c> map. The TS-side parity test
/// flattens the TS nested-tree <c>Scopes</c> object the same way and
/// asserts byte-equality, plus pins <c>ALL_SCOPES</c> against the
/// fixture's sorted wire-value list.
/// </summary>
/// <remarks>
/// Only surfaces the TS side exposes (<c>Scopes</c> nested tree +
/// <c>ALL_SCOPES</c> flat list) get fixtures here. Helper methods on
/// the .NET side (<c>GetActionSensitivity</c>,
/// <c>IsImpersonationBlocked</c>, <c>IsAnonymous</c>, <c>IsKnown</c>,
/// <c>IsGrantedTo</c>, etc.) have no TS counterpart yet — symmetric
/// coverage means no fixture for surfaces the consumer can't reach.
/// </remarks>
public sealed class ScopesFixtureEmitter
{
    private const string _CATALOG = "auth-scopes";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Scopes()
    {
        var data = EnumerateNestedConstants(typeof(Scopes));
        FixturePathHelpers.WriteFixture(_CATALOG, "scopes", data);
    }

    /// <summary>
    /// Recursively walk every nested static class on the catalog type
    /// and collect every <c>public const string</c> leaf — keyed by the
    /// dot-separated path to the const (e.g. <c>Anon.Public.Health</c>),
    /// valued by the const's wire string (e.g. <c>"anon.public.health"</c>).
    /// Ordinal sort by key so the on-disk fixture is stable across
    /// reorderings.
    /// </summary>
    /// <param name="rootType">The catalog root type (e.g. <c>typeof(Scopes)</c>).</param>
    /// <returns>Sorted path → wire-value map.</returns>
    private static SortedDictionary<string, object?> EnumerateNestedConstants(Type rootType)
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        Walk(rootType, prefix: string.Empty, data);
        return data;
    }

    private static void Walk(Type type, string prefix, SortedDictionary<string, object?> data)
    {
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string));
        foreach (var f in fields)
        {
            var key = prefix.Length == 0 ? f.Name : prefix + "." + f.Name;
            data[key] = (string)f.GetValue(null)!;
        }

        var nested = type
            .GetNestedTypes(BindingFlags.Public)
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed); // static class
        foreach (var nt in nested)
        {
            var nextPrefix = prefix.Length == 0 ? nt.Name : prefix + "." + nt.Name;
            Walk(nt, nextPrefix, data);
        }
    }
}
