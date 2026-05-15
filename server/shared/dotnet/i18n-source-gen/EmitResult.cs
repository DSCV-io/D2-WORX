// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.I18n.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using D2.Shared.SourceGen;

/// <summary>
/// Result of <see cref="TKEmitter.Emit(string, IReadOnlyDictionary{string, string})"/>.
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
