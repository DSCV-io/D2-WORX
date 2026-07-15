// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Result of an emit operation (interface emission OR mutable+envelope emission).
/// Pure data — the Roslyn host transforms the diagnostics into
/// <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances and writes the
/// generated source to the named file.
/// </summary>
/// <param name="HintName">The Roslyn hint name (e.g. <c>"IAuthContext.g.cs"</c>).</param>
/// <param name="GeneratedSource">The generated C# source.</param>
/// <param name="Diagnostics">Diagnostics emitted during the operation.</param>
internal sealed record EmitResult(
    string HintName,
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
