// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.JwtClaims.SourceGen;

using System.Collections.Immutable;
using D2.Shared.SourceGen;

/// <summary>
/// Result of <see cref="JwtClaimsEmitter.Emit"/>. Pure data.
/// </summary>
/// <param name="GeneratedSource">The generated C# source.</param>
/// <param name="Diagnostics">Diagnostics emitted during validation + emission.</param>
internal sealed record EmitResult(
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
