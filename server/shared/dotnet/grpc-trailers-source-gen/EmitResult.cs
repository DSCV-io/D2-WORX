// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Grpc.Trailers.SourceGen;

using System.Collections.Immutable;
using D2.Shared.SourceGen;

/// <summary>
/// Result of <see cref="GrpcTrailersEmitter.Emit"/>. Pure data — the Roslyn
/// host transforms the diagnostics into
/// <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances and writes the
/// generated source to <c>D2GrpcTrailers.g.cs</c>.
/// </summary>
/// <param name="GeneratedSource">The generated C# source.</param>
/// <param name="Diagnostics">Diagnostics emitted during validation + emission.</param>
internal sealed record EmitResult(
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
