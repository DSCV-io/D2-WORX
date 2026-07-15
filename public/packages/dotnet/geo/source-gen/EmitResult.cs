// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Result of a per-emitter run. Pure data — the Roslyn host transforms the
/// diagnostics into <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances
/// and writes the generated source via
/// <c>Microsoft.CodeAnalysis.SourceProductionContext.AddSource</c>.
/// </summary>
/// <param name="HintName">
/// The Roslyn hint name (a stable, unique-per-source identifier ending in
/// <c>.g.cs</c>) under which the generated source is added.
/// </param>
/// <param name="GeneratedSource">The generated C# source.</param>
/// <param name="Diagnostics">Diagnostics emitted during emission.</param>
internal sealed record EmitResult(
    string HintName,
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
