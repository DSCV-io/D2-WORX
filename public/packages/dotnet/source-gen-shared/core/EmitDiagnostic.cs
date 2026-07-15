// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostic.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// A diagnostic produced by a source-gen's spec loader or emitter.
/// Decoupled from <c>Microsoft.CodeAnalysis.Diagnostic</c> so the loader
/// and emitter are unit-testable without instantiating a Roslyn host.
/// Each generator's <c>Initialize</c> translates these to real Roslyn
/// <c>Diagnostic</c> instances via a per-source-gen
/// <c>ResolveDescriptor(DescriptorId)</c> switch over the source-gen's
/// own <c>DiagnosticIds</c> constants.
/// </summary>
/// <param name="DescriptorId">
/// Matches a per-source-gen <c>DiagnosticIds</c> identifier
/// (e.g. <c>"D2HDR001"</c>, <c>"D2I18N002"</c>, <c>"D2CTX003"</c>).
/// </param>
/// <param name="Args">
/// Arguments to format into the descriptor's <c>messageFormat</c> template.
/// </param>
internal sealed record EmitDiagnostic(string DescriptorId, ImmutableArray<object> Args);
