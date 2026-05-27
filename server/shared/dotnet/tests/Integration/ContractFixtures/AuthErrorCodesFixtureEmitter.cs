// -----------------------------------------------------------------------
// <copyright file="AuthErrorCodesFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using D2.Shared.Auth.Errors;
using Xunit;

/// <summary>
/// Emits the parity fixtures for the <c>AUTH_*</c> error-code catalog
/// reflected off the .NET codegen-emitted static class
/// <see cref="AuthErrorCodes"/>. One fixture (<c>codes.json</c>) maps
/// every constant name to its wire value; a second fixture
/// (<c>http-statuses.json</c>) maps every code to its declared HTTP
/// status. The TS-side parity test asserts byte-equality against the
/// codegen-emitted TS catalog (<c>@d2/auth-abstractions</c>'s
/// <c>AuthErrorCodes</c> object + <c>getAuthErrorHttpStatus</c>).
/// </summary>
public sealed class AuthErrorCodesFixtureEmitter
{
    private const string _CATALOG = "auth-error-codes";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Codes()
    {
        var data = EnumerateConstants(typeof(AuthErrorCodes));
        FixturePathHelpers.WriteFixture(_CATALOG, "codes", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_HttpStatuses()
    {
        // The TS-side getAuthErrorHttpStatus mirror reads this fixture and
        // asserts the per-code → HTTP-status mapping matches byte-for-byte.
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var code in AuthErrorCodes.AllCodes)
            data[code] = AuthErrorCodes.GetHttpStatus(code);

        FixturePathHelpers.WriteFixture(_CATALOG, "http-statuses", data);
    }

    /// <summary>
    /// Reflect every <c>public const string</c> on the catalog type;
    /// produce a sorted map keyed by the field name (e.g.
    /// <c>AUTH_BEARER_MISSING</c> → <c>"AUTH_BEARER_MISSING"</c>) so the
    /// fixture mirrors the TS-side const-map shape one-to-one.
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
