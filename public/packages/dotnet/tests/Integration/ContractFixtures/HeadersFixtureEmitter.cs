// -----------------------------------------------------------------------
// <copyright file="HeadersFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.Headers.Common;
using DcsvIo.D2.Headers.Grpc;
using DcsvIo.D2.Headers.Http;
using Xunit;

/// <summary>
/// Emits one fixture per transport-specific header catalog. The fixture
/// data is a sorted <c>{ constName: wireValue }</c> map covering every
/// public string constant on the .NET catalog type. The TS-side parity
/// test asserts the same membership and wire values exist on the
/// matching <c>HttpHeaders</c> / <c>AmqpHeaders</c> / etc. <c>as const</c>
/// objects.
/// </summary>
public sealed class HeadersFixtureEmitter
{
    private const string CATALOG = "headers";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Common()
    {
        var data = EnumerateConstants(typeof(CommonHeaders));
        FixturePathHelpers.WriteFixture(CATALOG, "common", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Http()
    {
        var data = EnumerateConstants(typeof(HttpHeaders));
        FixturePathHelpers.WriteFixture(CATALOG, "http", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Amqp()
    {
        var data = EnumerateConstants(typeof(AmqpHeaders));
        FixturePathHelpers.WriteFixture(CATALOG, "amqp", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Grpc()
    {
        var data = EnumerateConstants(typeof(GrpcHeaders));
        FixturePathHelpers.WriteFixture(CATALOG, "grpc", data);
    }

    /// <summary>
    /// Reflect every <c>public const string</c> on the catalog type and
    /// produce a sorted name → value map. Mirrors the enumeration done
    /// by <c>HeaderCatalogConsistencyTests.EnumerateConstants</c>.
    /// </summary>
    private static SortedDictionary<string, object?> EnumerateConstants(Type type)
    {
        var fields = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .OrderBy(f => f.Name, StringComparer.Ordinal);
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in fields)
            data[f.Name] = (string)f.GetValue(null)!;

        return data;
    }
}
