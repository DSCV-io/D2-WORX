// -----------------------------------------------------------------------
// <copyright file="AuthErrorCodesFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using D2.Shared.Auth.Errors;
using D2.Shared.Result;
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

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_FactoryNames()
    {
        // code → camelCase factory name (the TS-emitted AuthFailures method
        // key). The .NET AuthFailures methods are PascalCase (BearerMissing);
        // the TS emitter lowercases the first char (bearerMissing). The TS
        // parity test asserts each AuthFailures camelCase method matches.
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (code, _, methodName) in EnumerateFailures())
            data[code] = CamelCase(methodName);

        FixturePathHelpers.WriteFixture(_CATALOG, "factory-names", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_UserMessageKeys()
    {
        // code → the ACTUAL wire TKMessage key the .NET AuthFailures factory
        // stamps (the snake key, e.g. auth_errors_UNAUTHORIZED). This is the
        // load-bearing parity fixture: the TS parity test asserts the TS
        // factory's wire key EQUALS it — directly surfacing any symbol-vs-snake
        // drift between the runtimes.
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (code, result, _) in EnumerateFailures())
            data[code] = result.Messages[0].Key;

        FixturePathHelpers.WriteFixture(_CATALOG, "user-message-keys", data);
    }

    /// <summary>
    /// Reflect every non-generic <see cref="AuthFailures"/> factory method,
    /// invoke it with no override, and yield the (code, result, methodName)
    /// triple. Each delegating factory carries a single optional
    /// <c>IReadOnlyList&lt;TKMessage&gt;? messages = null</c> parameter; passing
    /// null exercises the default-omitted (spec-TK) path so the fixture pins the
    /// default wire key. The two 503 factories also expose a generic
    /// <c>&lt;T&gt;</c> overload of the same name + arity — filtered out by the
    /// non-generic predicate so each factory is yielded once.
    /// </summary>
    private static IEnumerable<(string Code, D2Result Result, string MethodName)> EnumerateFailures()
    {
        var methods = typeof(AuthFailures)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => !m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
            .OrderBy(m => m.Name, StringComparer.Ordinal);
        foreach (var method in methods)
        {
            var result = (D2Result)method.Invoke(null, [null])!;
            yield return (result.ErrorCode!, result, method.Name);
        }
    }

    private static string CamelCase(string pascal) =>
        pascal.Length == 0
            ? pascal
            : char.ToLowerInvariant(pascal[0]) + pascal[1..];

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
