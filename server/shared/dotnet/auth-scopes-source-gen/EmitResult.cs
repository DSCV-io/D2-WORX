// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Scopes.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Result of
/// <see cref="ScopesEmitter.Emit(ScopesSpec, IReadOnlyList{string}, IReadOnlyList{string})"/>.
/// Pure data — the Roslyn host transforms the diagnostics into
/// <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances and writes the
/// generated source to <c>Scopes.g.cs</c>.
/// </summary>
/// <param name="GeneratedSource">
/// The generated C# source for the <c>Scopes</c> static partial class.
/// </param>
/// <param name="Diagnostics">Diagnostics emitted during validation + emission.</param>
internal sealed record EmitResult(
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
