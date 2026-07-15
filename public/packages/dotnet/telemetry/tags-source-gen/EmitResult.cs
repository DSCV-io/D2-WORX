// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Result of <see cref="TelemetryTagsEmitter.Emit"/>. Pure data — the Roslyn
/// host transforms the diagnostics into <see cref="Microsoft.CodeAnalysis.Diagnostic"/>
/// instances and writes the generated source to a per-meter <c>.g.cs</c> file.
/// </summary>
/// <param name="GeneratedSource">
/// The generated C# source. Empty when the meter has no closed-enum tags.
/// </param>
/// <param name="HintName">
/// The Roslyn AddSource hint name (e.g. <c>AuthTelemetryTags.g.cs</c>).
/// Empty when no source is emitted.
/// </param>
/// <param name="Diagnostics">Diagnostics emitted during validation + emission.</param>
internal sealed record EmitResult(
    string GeneratedSource,
    string HintName,
    ImmutableArray<EmitDiagnostic> Diagnostics);
