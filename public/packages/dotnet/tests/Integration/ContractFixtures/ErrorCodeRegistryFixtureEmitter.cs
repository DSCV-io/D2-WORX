// -----------------------------------------------------------------------
// <copyright file="ErrorCodeRegistryFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System.Collections.Generic;
using System.Linq;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.ErrorCodes.Registry;
using Xunit;

/// <summary>
/// Emits the cross-runtime parity fixture for the merged
/// <see cref="ErrorCodeRegistry"/>. Reflects the built registry —
/// every <see cref="ErrorCodeInfo"/> entry — and writes a deterministic,
/// sorted <c>registry.json</c> fixture to
/// <c>contract-tests/fixtures/error-codes-registry/</c>. The TS-side
/// parity test reads this fixture and asserts the TS
/// <c>errorCodeRegistry</c> exposes an identical map.
/// </summary>
/// <remarks>
/// JSON shape per entry:
/// <code>
/// {
///   "code": "AUTH_BEARER_MISSING",
///   "httpStatus": 401,
///   "category": "validation_failure",
///   "userMessageKeySnake": "auth_errors_UNAUTHORIZED",
///   "userMessageKeyPath": "TK.Auth.Errors.UNAUTHORIZED",
///   "factoryName": "BearerMissing",
///   "factoryShape": "standard",
///   "doc": "...",
///   "domain": "auth"
/// }
/// </code>
/// The <c>category</c> field is the snake_case wire string (e.g.
/// <c>"validation_failure"</c>) produced by
/// <see cref="ErrorCategoryWire.ToWire"/>; the TS parity axis is
/// <c>fixture.category === ts.category</c> (both are the wire string).
/// The <c>userMessageKeySnake</c> field contains the runtime <c>.Key</c>
/// value the .NET <see cref="DcsvIo.D2.I18n.TKMessage"/> carries; the
/// <c>userMessageKeyPath</c> field contains the spec's TK symbol-path
/// reference (derived by inverse-transforming the snake key). The TS
/// parity test asserts BOTH fields match the TS registry's corresponding
/// entry so any symbol-vs-snake drift surfaces immediately.
/// </remarks>
public sealed class ErrorCodeRegistryFixtureEmitter
{
    private const string _CATALOG = "error-codes-registry";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Registry()
    {
        var data = new SortedDictionary<string, object?>(System.StringComparer.Ordinal);

        foreach (var info in ErrorCodeRegistry.All.OrderBy(i => i.Code, System.StringComparer.Ordinal))
        {
            var entry = new SortedDictionary<string, object?>(System.StringComparer.Ordinal)
            {
                ["code"] = info.Code,
                ["httpStatus"] = info.HttpStatus,
                ["category"] = info.Category.ToWire(),
                ["userMessageKeySnake"] = info.UserMessageKey.Key,
                ["userMessageKeyPath"] = SnakeKeyToPath(info.UserMessageKey.Key),
                ["factoryName"] = info.FactoryName,
                ["factoryShape"] = info.FactoryShape,
                ["doc"] = info.Doc,
                ["domain"] = info.Domain,
            };
            data[info.Code] = entry;
        }

        FixturePathHelpers.WriteFixture(_CATALOG, "registry", data);
    }

    /// <summary>
    /// Forward-transforms a snake_case TK key back to its TK symbol-path
    /// reference so the TS parity test can compare both representations.
    /// Mirrors the inverse of <c>TkKeyTransform.ToSnakeKey</c> in the engine.
    /// </summary>
    /// <remarks>
    /// Input: <c>auth_errors_UNAUTHORIZED</c>.
    /// Output: <c>TK.Auth.Errors.UNAUTHORIZED</c>.
    /// </remarks>
    /// <param name="snakeKey">The snake_case TK key (e.g. <c>auth_errors_UNAUTHORIZED</c>).</param>
    /// <returns>The TK symbol-path reference (e.g. <c>TK.Auth.Errors.UNAUTHORIZED</c>).</returns>
    private static string SnakeKeyToPath(string snakeKey)
    {
        // snake key is <domain>_<category>_<CONSTANT...>
        // Split on '_' but only the first two underscores are segment boundaries;
        // the rest of the constant after the second underscore is the SCREAMING part.
        var underscoreIdx1 = snakeKey.IndexOf('_');
        if (underscoreIdx1 < 0)
            return snakeKey;

        var underscoreIdx2 = snakeKey.IndexOf('_', underscoreIdx1 + 1);
        if (underscoreIdx2 < 0)
            return snakeKey;

        var domain = snakeKey.Substring(0, underscoreIdx1);
        var category = snakeKey.Substring(underscoreIdx1 + 1, underscoreIdx2 - underscoreIdx1 - 1);
        var constant = snakeKey.Substring(underscoreIdx2 + 1);

        return "TK." + UpperFirst(domain) + "." + UpperFirst(category) + "." + constant;
    }

    private static string UpperFirst(string s) =>
        s.Length == 0
            ? s
            : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
