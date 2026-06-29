// -----------------------------------------------------------------------
// <copyright file="EmitResult.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.ProtocolAudiences.SourceGen;

using System.Collections.Immutable;
using D2.Shared.SourceGen;

/// <summary>
/// Result of <see cref="ProtocolAudiencesEmitter.Emit"/>. Pure data — the Roslyn
/// host transforms the diagnostics into
/// <see cref="Microsoft.CodeAnalysis.Diagnostic"/> instances and writes the
/// generated source to <c>WellKnownAudiences.g.cs</c>.
/// </summary>
/// <param name="GeneratedSource">
/// The generated C# source for the <c>WellKnownAudiences</c> static partial class.
/// </param>
/// <param name="Diagnostics">Diagnostics emitted during validation + emission.</param>
internal sealed record EmitResult(
    string GeneratedSource,
    ImmutableArray<EmitDiagnostic> Diagnostics);
