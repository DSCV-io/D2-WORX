// -----------------------------------------------------------------------
// <copyright file="TkKeyTransform.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System;

/// <summary>
/// Pure inverse of <c>DcsvIo.D2.I18n.SourceGen.KeyDecomposer.Decompose</c>:
/// converts a <c>TK</c> symbol-path reference back to the snake_case key it
/// was decomposed from, so a <c>userMessageKey</c> can be cross-checked
/// against <c>contracts/messages/en-US.json</c> (the message source — NOT the
/// generated <c>TK.g.cs</c>, which would be circular).
/// </summary>
/// <remarks>
/// <para>
/// <c>KeyDecomposer</c> splits a snake key into [domain, category,
/// constant..N], PascalCasing only the FIRST char of the domain + category
/// segments (the constant segments are uppercased + joined). The TK path is
/// <c>TK.&lt;Domain&gt;.&lt;Category&gt;.&lt;CONST&gt;</c>; the inverse
/// lowercases only the first char of <c>&lt;Domain&gt;</c>/<c>&lt;Category&gt;</c>
/// and emits <c>&lt;CONST&gt;</c> verbatim.
/// </para>
/// <para>
/// The canonical <c>userMessageKey</c> regex
/// (<c>^TK(\.[A-Za-z][A-Za-z0-9]*){2}\.[A-Z][A-Z0-9_]*$</c>) guarantees
/// exactly <c>TK</c> + two segments + a SCREAMING constant, so the parse is
/// total for any schema-conforming key.
/// </para>
/// </remarks>
internal static class TkKeyTransform
{
    private const string _PREFIX = "TK.";

    /// <summary>
    /// Inverse-transforms a TK symbol-path reference to its snake_case key
    /// (e.g. <c>TK.Auth.Errors.UNAUTHORIZED</c> →
    /// <c>auth_errors_UNAUTHORIZED</c>). Returns <c>null</c> when the input
    /// does not have the expected <c>TK.&lt;Domain&gt;.&lt;Category&gt;.&lt;CONST&gt;</c>
    /// shape.
    /// </summary>
    /// <param name="tkPath">The TK symbol-path reference.</param>
    /// <returns>The snake_case key, or <c>null</c> when the shape is unexpected.</returns>
    public static string? ToSnakeKey(string? tkPath)
    {
        if (tkPath is null || !tkPath.StartsWith(_PREFIX, StringComparison.Ordinal))
            return null;

        var body = tkPath.Substring(_PREFIX.Length);
        var segments = body.Split('.');
        if (segments.Length != 3)
            return null;

        var domain = segments[0];
        var category = segments[1];
        var constant = segments[2];
        if (domain.Length == 0 || category.Length == 0 || constant.Length == 0)
            return null;

        return LowerFirst(domain) + "_" + LowerFirst(category) + "_" + constant;
    }

    private static string LowerFirst(string segment) =>
        char.ToLowerInvariant(segment[0]) + segment.Substring(1);
}
