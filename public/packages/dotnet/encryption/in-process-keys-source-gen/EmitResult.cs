// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.InProcessKeys.SourceGen;

using System.Collections.Immutable;
using D2.Shared.SourceGen;

/// <summary>
/// Result of <see cref="InProcessKeysEmitter.Emit"/>. Pure data — the Roslyn
/// host transforms diagnostics into <see cref="Microsoft.CodeAnalysis.Diagnostic"/>
/// instances and writes the generated source to the corresponding
/// <c>.g.cs</c> file.
/// </summary>
/// <param name="GeneratedSource">The generated C# source.</param>
/// <param name="Diagnostics">Diagnostics emitted during validation + emission.</param>
internal sealed record EmitResult(
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
