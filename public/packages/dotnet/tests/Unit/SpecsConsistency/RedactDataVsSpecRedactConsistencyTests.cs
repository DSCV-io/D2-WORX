// -----------------------------------------------------------------------
// <copyright file="RedactDataVsSpecRedactConsistencyTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.AuthContext.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Tests.Unit.Auth;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

/// <summary>
/// Cross-spec parity gate ensuring the spec-side <c>"redact": true</c>
/// annotations and the .NET-side <c>[RedactData]</c> attribute placement
/// stay aligned across the IAuthContext + IRequestContext interfaces in
/// BOTH directions: every attribute-bearing property is annotated in the
/// spec, AND every annotated spec property is attribute-bearing on the
/// emitted interface (and on MutableRequestContext, which is what the
/// Serilog destructuring policy actually reflects on at log time). The
/// .NET context-source-gen consumes the spec's <c>redact</c> field at
/// codegen time, so drift in either direction surfaces a real divergence
/// between the spec (single source of truth) and the generated output.
/// </summary>
public sealed class RedactDataVsSpecRedactConsistencyTests
{
    [Fact]
    public void RedactDataAnnotatedProperties_AppearAsRedactTrueInSpec()
    {
        var authCtxSpec = LoadRedactPropertyNames(TestPaths.AuthContextSpec());
        var reqCtxSpec = LoadRedactPropertyNames(TestPaths.RequestContextSpec());

        var authCtxAttributed = CollectRedactDataProperties(typeof(IAuthContext));
        var reqCtxAttributed = CollectRedactDataProperties(typeof(IRequestContext));

        foreach (var prop in authCtxAttributed)
        {
            authCtxSpec.Should().Contain(
                prop,
                because:
                    $"property '{prop}' on IAuthContext is decorated with [RedactData] " +
                    "but does not carry \"redact\": true in IAuthContext.spec.json");
        }

        foreach (var prop in reqCtxAttributed)
        {
            reqCtxSpec.Should().Contain(
                prop,
                because:
                    $"property '{prop}' on IRequestContext is decorated with [RedactData] " +
                    "but does not carry \"redact\": true in IRequestContext.spec.json");
        }
    }

    [Fact]
    public void SpecRedactTrueProperties_HaveRedactDataOnEmittedInterface()
    {
        // Reverse direction: every spec annotation must show up as
        // [RedactData] on the corresponding emitted interface property.
        // GetProperties on an interface returns only directly-declared
        // properties (no walk of base interfaces), so the per-spec scope
        // matches per-interface scope cleanly.
        var authCtxSpec = LoadRedactPropertyNames(TestPaths.AuthContextSpec());
        var reqCtxSpec = LoadRedactPropertyNames(TestPaths.RequestContextSpec());

        var authCtxAttributed = CollectRedactDataProperties(typeof(IAuthContext));
        var reqCtxAttributed = CollectRedactDataProperties(typeof(IRequestContext));

        foreach (var prop in authCtxSpec)
        {
            authCtxAttributed.Should().Contain(
                prop,
                because:
                    $"spec marks '{prop}' as \"redact\": true but [RedactData] is not " +
                    "present on the emitted IAuthContext property — codegen drift.");
        }

        foreach (var prop in reqCtxSpec)
        {
            reqCtxAttributed.Should().Contain(
                prop,
                because:
                    $"spec marks '{prop}' as \"redact\": true but [RedactData] is not " +
                    "present on the emitted IRequestContext property — codegen drift.");
        }
    }

    [Fact]
    public void SpecRedactTrueProperties_ResolveToRealInterfaceProperties()
    {
        var authCtxSpec = LoadRedactPropertyNames(TestPaths.AuthContextSpec());
        var reqCtxSpec = LoadRedactPropertyNames(TestPaths.RequestContextSpec());

        var authCtxProperties = AllPropertyNames(typeof(IAuthContext));
        var reqCtxProperties = AllPropertyNames(typeof(IRequestContext));

        foreach (var prop in authCtxSpec)
        {
            authCtxProperties.Should().Contain(
                prop,
                because:
                    $"spec marks '{prop}' as \"redact\": true but no such property exists " +
                    "on the emitted IAuthContext — orphaned annotation.");
        }

        foreach (var prop in reqCtxSpec)
        {
            reqCtxProperties.Should().Contain(
                prop,
                because:
                    $"spec marks '{prop}' as \"redact\": true but no such property exists " +
                    "on the emitted IRequestContext — orphaned annotation.");
        }
    }

    [Fact]
    public void RedactDataOnMutableConcrete_CoversEverySpecAnnotation()
    {
        // The Serilog destructuring policy reflects on the runtime instance
        // type — typically MutableRequestContext rather than IAuthContext
        // / IRequestContext. Pin that codegen ALSO places [RedactData] on
        // the concrete property, not just the interface, so the policy
        // actually fires at log time. Concrete type carries fields from
        // BOTH spec catalogs; expected = union of spec annotations.
        var authCtxSpec = LoadRedactPropertyNames(TestPaths.AuthContextSpec());
        var reqCtxSpec = LoadRedactPropertyNames(TestPaths.RequestContextSpec());
        var expected = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var n in authCtxSpec)
            expected.Add(n);
        foreach (var n in reqCtxSpec)
            expected.Add(n);

        var concreteAttributed = CollectRedactDataProperties(typeof(MutableRequestContext));

        foreach (var prop in expected)
        {
            concreteAttributed.Should().Contain(
                prop,
                because:
                    $"spec marks '{prop}' as \"redact\": true but [RedactData] is not " +
                    "present on the corresponding MutableRequestContext property — " +
                    "the destructuring policy reflects on the concrete type at log " +
                    "time, so missing the attribute here means PII would leak.");
        }
    }

    [Fact]
    public void AtLeastOneRedactAnnotation_ExistsInTheSpecs()
    {
        // Sanity gate — at least one redact annotation must remain across
        // the spec catalogs. If both lists go empty, somebody removed the
        // PII annotations + nobody noticed; PII would then ship to logs
        // unredacted.
        var authCtxSpec = LoadRedactPropertyNames(TestPaths.AuthContextSpec());
        var reqCtxSpec = LoadRedactPropertyNames(TestPaths.RequestContextSpec());
        (authCtxSpec.Count + reqCtxSpec.Count).Should().BeGreaterThan(
            0,
            because:
                "the spec catalogs are the source of truth for PII redaction " +
                "and at least one annotated field must remain.");
    }

    private static HashSet<string> LoadRedactPropertyNames(string specPath)
    {
        var json = File.ReadAllText(specPath);
        using var doc = JsonDocument.Parse(json);
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        if (!doc.RootElement.TryGetProperty("sections", out var sections))
            return names;
        foreach (var section in sections.EnumerateArray())
        {
            if (!section.TryGetProperty("properties", out var props))
                continue;
            foreach (var prop in props.EnumerateArray())
            {
                if (!prop.TryGetProperty("redact", out var redact))
                    continue;
                if (redact.ValueKind != JsonValueKind.True)
                    continue;
                if (!prop.TryGetProperty("name", out var name))
                    continue;
                var n = name.GetString();
                if (n.Truthy())
                    names.Add(n!);
            }
        }

        return names;
    }

    private static HashSet<string> CollectRedactDataProperties(System.Type type)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (
            var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<RedactDataAttribute>() is null)
                continue;
            names.Add(prop.Name);
        }

        return names;
    }

    private static HashSet<string> AllPropertyNames(System.Type type)
    {
        return new HashSet<string>(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name),
            System.StringComparer.Ordinal);
    }
}
