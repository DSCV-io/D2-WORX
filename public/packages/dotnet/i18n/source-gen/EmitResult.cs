// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Result of <see cref="TKEmitter.Emit"/>.
/// Pure data — the Roslyn host transforms the diagnostics into
/// <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances and writes the
/// generated source to <c>TK.g.cs</c>.
/// </summary>
/// <param name="GeneratedSource">The generated C# source for the <c>TK</c> static class.</param>
/// <param name="Diagnostics">
/// Diagnostics emitted during decomposition + per-locale coverage analysis.
/// </param>
internal sealed record EmitResult(
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
