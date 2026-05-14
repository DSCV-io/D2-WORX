// -----------------------------------------------------------------------
// <copyright file="LoadResult.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Telemetry.Tags.SourceGen;

/// <summary>
/// Result of <see cref="TelemetrySpecLoader.Load"/>. Either <see cref="Spec"/>
/// is non-null (parse success) or <see cref="Diagnostic"/> is non-null (parse
/// failure). Never both, never neither.
/// </summary>
/// <param name="Spec">The parsed spec on success; null on failure.</param>
/// <param name="Diagnostic">The parse-failure diagnostic; null on success.</param>
internal sealed record LoadResult(TelemetrySpec? Spec, EmitDiagnostic? Diagnostic);
