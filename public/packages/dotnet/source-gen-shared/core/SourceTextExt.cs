// -----------------------------------------------------------------------
// <copyright file="SourceTextExt.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// Shared source-gen text helper. Wired into every source-gen csproj via the
// shared `Compile Include` from `source-gen-shared/core/` — every emitter
// gets the same LF-normalization, so per-generator line-ending drift is
// structurally impossible.
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

/// <summary>
/// Source-text helpers shared by every D² Roslyn emitter.
/// </summary>
internal static class SourceTextExt
{
    /// <summary>
    /// Normalizes every CRLF in a built source string to a bare LF so the
    /// emitted <c>.g.cs</c> is byte-identical regardless of the build host's
    /// OS. <see cref="System.Text.StringBuilder.AppendLine()"/> appends
    /// <see cref="System.Environment.NewLine"/> (CRLF on Windows, LF on
    /// Linux); the repo policy is LF everywhere
    /// (<c>.gitattributes</c> <c>eol=lf</c>, <c>.editorconfig</c>
    /// <c>end_of_line = lf</c>), and the committed <c>EmitCompilerGeneratedFiles</c>
    /// output is LF. Routing every emitter's returned source through this keeps
    /// a Windows rebuild from re-emitting the generated fleet as CRLF (working-
    /// tree-vs-index churn). The two-arg
    /// <see cref="string.Replace(string, string)"/> is used deliberately —
    /// netstandard2.0 has no <c>StringComparison</c> overload.
    /// </summary>
    /// <param name="source">The built source string (may contain CRLF).</param>
    /// <returns>The same source with every CRLF collapsed to a bare LF.</returns>
    public static string LfNormalized(this string source) =>
        source.Replace("\r\n", "\n");
}
