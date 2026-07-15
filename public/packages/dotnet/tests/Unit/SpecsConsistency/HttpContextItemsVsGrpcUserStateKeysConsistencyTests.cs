// -----------------------------------------------------------------------
// <copyright file="HttpContextItemsVsGrpcUserStateKeysConsistencyTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Cross-binding parity: every entry in
/// <c>contracts/in-process-keys/keys.spec.json</c> with both <c>http</c> and
/// <c>grpc</c> bindings must appear in BOTH catalogs at IDENTICAL wire values.
/// The HTTP catalog (<see cref="D2HttpContextItems"/>) is public; the gRPC
/// catalog (<c>D2.Shared.Auth.Grpc.Interceptors.D2GrpcUserStateKeys</c>) is
/// internal — accessed here via reflection across the InternalsVisibleTo seam.
/// </summary>
public sealed class HttpContextItemsVsGrpcUserStateKeysConsistencyTests
{
    [Fact]
    public void EveryCrossBindingSpecEntry_AppearsInBothCatalogs_AtIdenticalWireValue()
    {
        var entries = LoadSpec();
        var httpType = typeof(D2HttpContextItems);
        var grpcType = ResolveGrpcUserStateKeysType();

        foreach (var entry in entries)
        {
            var hasHttp = entry.Bindings.Contains("http");
            var hasGrpc = entry.Bindings.Contains("grpc");

            if (hasHttp)
            {
                var httpValue = LookupConst(httpType, entry.ConstName);
                httpValue.Should().Be(
                    entry.Value,
                    $"D2HttpContextItems must carry '{entry.ConstName}' = '{entry.Value}'");
            }

            if (hasGrpc)
            {
                var grpcValue = LookupConst(grpcType, entry.ConstName);
                grpcValue.Should().Be(
                    entry.Value,
                    $"D2GrpcUserStateKeys must carry '{entry.ConstName}' = '{entry.Value}'");
            }
        }
    }

    [Fact]
    public void GrpcUserStateKeysType_RemainsInternal()
    {
        var grpcType = ResolveGrpcUserStateKeysType();
        grpcType.IsPublic.Should().BeFalse(
            "D2GrpcUserStateKeys must remain internal (visibility preservation)");
    }

    [Fact]
    public void BothCatalogs_HaveIdenticalConstantSet()
    {
        var entries = LoadSpec();
        var httpExpected = entries
            .Where(e => e.Bindings.Contains("http"))
            .Select(e => e.ConstName)
            .OrderBy(n => n)
            .ToList();
        var grpcExpected = entries
            .Where(e => e.Bindings.Contains("grpc"))
            .Select(e => e.ConstName)
            .OrderBy(n => n)
            .ToList();

        var httpActual = EnumerateConstNames(typeof(D2HttpContextItems))
            .OrderBy(n => n)
            .ToList();
        var grpcActual = EnumerateConstNames(ResolveGrpcUserStateKeysType())
            .OrderBy(n => n)
            .ToList();

        httpActual.Should().BeEquivalentTo(httpExpected);
        grpcActual.Should().BeEquivalentTo(grpcExpected);
    }

    private static string LookupConst(System.Type type, string constName)
    {
        var field = type.GetField(
            constName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        field.Should().NotBeNull(
            $"type '{type.FullName}' is missing constant '{constName}' from spec");
        return (string)field.GetValue(null)!;
    }

    private static IEnumerable<string> EnumerateConstNames(System.Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => f.Name);

    private static System.Type ResolveGrpcUserStateKeysType()
    {
        // Internal type lives in D2.Shared.Auth.Grpc.Interceptors;
        // InternalsVisibleTo "D2.Shared.Tests" lets us reflect against it.
        var asm = Assembly.Load("D2.Shared.Auth.Grpc");
        var t = asm.GetType("D2.Shared.Auth.Grpc.Interceptors.D2GrpcUserStateKeys");
        t.Should().NotBeNull(
            "D2GrpcUserStateKeys must be present in D2.Shared.Auth.Grpc");
        return t;
    }

    private static List<KeyEntry> LoadSpec()
    {
        var path = TestPaths.InProcessKeysSpec();
        File.Exists(path).Should().BeTrue("spec file must be present at " + path);
        var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new List<KeyEntry>();
        foreach (var element in doc.RootElement.GetProperty("keys").EnumerateArray())
        {
            var bindings = element.GetProperty("bindings")
                .EnumerateArray()
                .Select(b => b.GetString()!)
                .ToList();
            result.Add(new KeyEntry(
                ConstName: element.GetProperty("constName").GetString()!,
                Value: element.GetProperty("value").GetString()!,
                Bindings: bindings));
        }

        return result;
    }

    private sealed record KeyEntry(
        string ConstName,
        string Value,
        IReadOnlyList<string> Bindings);
}
