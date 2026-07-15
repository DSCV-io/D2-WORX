// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Outcome of a single emitter invocation: a generated source file plus any
/// diagnostics raised during emission.
/// </summary>
/// <param name="HintName">The Roslyn <c>AddSource</c> hint name (e.g.
/// <c>"MqMessages.g.cs"</c>).</param>
/// <param name="GeneratedSource">The emitted C# source string.</param>
/// <param name="Diagnostics">Any diagnostics raised during emission.</param>
internal sealed record EmitResult(
    string HintName,
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
