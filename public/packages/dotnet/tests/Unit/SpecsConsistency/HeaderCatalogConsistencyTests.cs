// -----------------------------------------------------------------------
// <copyright file="HeaderCatalogConsistencyTests.cs" company="DCSV">
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
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.Headers.Common;
using DcsvIo.D2.Headers.Grpc;
using DcsvIo.D2.Headers.Http;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Cross-spec consistency: every entry in
/// <c>contracts/headers/headers.spec.json</c> must appear in every per-transport
/// catalog whose applicability covers it, at the IDENTICAL wire value;
/// per-transport catalogs must contain ONLY entries with applicability for
/// that transport; the Common catalog must contain exactly the cross-transport
/// subset (applicability count >= 2). These tests are the single source of
/// proof that codegen across the 4 catalogs is internally consistent.
/// </summary>
public sealed class HeaderCatalogConsistencyTests
{
    [Fact]
    public void EverySpecEntry_AppearsInEveryApplicableCatalog_AtIdenticalWireValue()
    {
        var entries = LoadSpec();

        foreach (var entry in entries)
        {
            foreach (var transport in entry.Applicability)
            {
                var catalogValue = LookupInCatalog(transport, entry.ConstName);
                catalogValue.Should().Be(
                    entry.Name,
                    $"spec entry '{entry.ConstName}' (applicability '{transport}') " +
                    $"must appear in {transport} catalog with wire value '{entry.Name}'");
            }

            if (entry.Applicability.Count >= 2)
            {
                var commonValue = LookupInCatalog("common", entry.ConstName);
                commonValue.Should().Be(
                    entry.Name,
                    $"spec entry '{entry.ConstName}' (cross-transport) must appear in " +
                    $"Common catalog with wire value '{entry.Name}'");
            }
        }
    }

    [Fact]
    public void EveryPerTransportCatalogEntry_HasMatchingSpecApplicability()
    {
        var entries = LoadSpec();
        var byConst = entries.ToDictionary(e => e.ConstName);

        foreach (var (catalog, type) in CatalogTypes())
        {
            foreach (var (constName, value) in EnumerateConstants(type))
            {
                byConst.Should().ContainKey(
                    constName,
                    $"catalog '{catalog}' contains constant '{constName}' " +
                    "with no matching spec entry");
                var entry = byConst[constName];
                if (catalog == "common")
                {
                    entry.Applicability.Count.Should().BeGreaterThanOrEqualTo(
                        2,
                        $"Common catalog should only contain cross-transport entries; " +
                        $"'{constName}' has applicability count {entry.Applicability.Count}");
                }
                else
                {
                    entry.Applicability.Should().Contain(
                        catalog,
                        $"catalog '{catalog}' contains '{constName}' but spec applicability is " +
                        $"[{string.Join(", ", entry.Applicability)}]");
                }

                value.Should().Be(
                    entry.Name,
                    $"catalog '{catalog}' constant '{constName}' wire value '{value}' " +
                    $"diverges from spec wire value '{entry.Name}'");
            }
        }
    }

    [Fact]
    public void CrossTransportEntries_AppearInMultipleCatalogs_AtIdenticalWireValue()
    {
        var entries = LoadSpec();

        foreach (var entry in entries.Where(e => e.Applicability.Count >= 2))
        {
            var values = new List<string>();
            foreach (var transport in entry.Applicability)
                values.Add(LookupInCatalog(transport, entry.ConstName));

            values.Add(LookupInCatalog("common", entry.ConstName));
            values.Distinct().Should().HaveCount(
                1,
                $"cross-transport entry '{entry.ConstName}' must have identical wire value " +
                $"across every applicable catalog; observed {{{string.Join(", ", values)}}}");
        }
    }

    [Fact]
    public void CommonCatalog_ContainsExactlyTheCrossTransportSubset()
    {
        var entries = LoadSpec();
        var expectedCommon = entries
            .Where(e => e.Applicability.Count >= 2)
            .Select(e => e.ConstName)
            .OrderBy(n => n)
            .ToList();

        var actualCommon = EnumerateConstants(typeof(CommonHeaders))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        actualCommon.Should().BeEquivalentTo(
            expectedCommon,
            "Common catalog membership must match cross-transport spec subset exactly");
    }

    private static string LookupInCatalog(string catalog, string constName)
    {
        var type = catalog switch
        {
            "common" => typeof(CommonHeaders),
            "http" => typeof(HttpHeaders),
            "amqp" => typeof(AmqpHeaders),
            "grpc" => typeof(GrpcHeaders),
            _ => throw new System.ArgumentException($"Unknown catalog '{catalog}'"),
        };
        var field = type.GetField(constName, BindingFlags.Public | BindingFlags.Static);
        field.Should().NotBeNull(
            $"catalog '{catalog}' is missing constant '{constName}' that the spec requires");
        return (string)field.GetValue(null)!;
    }

    private static IEnumerable<(string Name, string Value)> EnumerateConstants(System.Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetValue(null)!));
    }

    private static IEnumerable<(string Catalog, System.Type Type)> CatalogTypes()
    {
        yield return ("common", typeof(CommonHeaders));
        yield return ("http", typeof(HttpHeaders));
        yield return ("amqp", typeof(AmqpHeaders));
        yield return ("grpc", typeof(GrpcHeaders));
    }

    private static List<HeaderEntry> LoadSpec()
    {
        var path = TestPaths.HeadersSpec();
        File.Exists(path).Should().BeTrue("spec file must be present at " + path);
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        var result = new List<HeaderEntry>();
        foreach (var element in doc.RootElement.GetProperty("headers").EnumerateArray())
        {
            var name = element.GetProperty("name").GetString()!;
            var constName = element.GetProperty("constName").GetString()!;
            var applicability = element.GetProperty("applicability")
                .EnumerateArray()
                .Select(a => a.GetString()!)
                .ToList();
            result.Add(new HeaderEntry(name, constName, applicability));
        }

        return result;
    }

    private sealed record HeaderEntry(
        string Name,
        string ConstName,
        IReadOnlyList<string> Applicability);
}
