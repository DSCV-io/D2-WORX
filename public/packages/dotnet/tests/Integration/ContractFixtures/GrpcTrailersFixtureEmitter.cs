// -----------------------------------------------------------------------
// <copyright file="GrpcTrailersFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using D2.Shared.Auth.Grpc.Status;
using Xunit;

/// <summary>
/// Emits the gRPC trailers catalog fixture. The .NET-side
/// <see cref="D2GrpcTrailers"/> class enumeration is canonicalized to a
/// sorted <c>{ constName: wireValue }</c> map; the TS-side parity test
/// asserts the same membership + wire values exist on the matching
/// <c>D2GrpcTrailers</c> <c>as const</c> object in <c>@d2/grpc-client</c>.
/// </summary>
public sealed class GrpcTrailersFixtureEmitter
{
    private const string CATALOG = "grpc-trailers";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Trailers()
    {
        var data = EnumerateConstants(typeof(D2GrpcTrailers));
        FixturePathHelpers.WriteFixture(CATALOG, "trailers", data);
    }

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
