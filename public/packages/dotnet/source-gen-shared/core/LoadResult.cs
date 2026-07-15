// -----------------------------------------------------------------------
// <copyright file="LoadResult.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.SourceGen;

/// <summary>
/// Outcome of a JSON spec parse. Either <paramref name="Spec"/> is non-null
/// (success) or <paramref name="Diagnostic"/> is non-null (failure). Never
/// both, never neither. Generic over the per-source-gen spec type so a
/// single shared definition serves every source generator without per-topic
/// duplication.
/// </summary>
/// <typeparam name="TSpec">The successfully-loaded spec type.</typeparam>
/// <param name="Spec">The parsed spec on success; null on failure.</param>
/// <param name="Diagnostic">The parse-failure diagnostic; null on success.</param>
internal sealed record LoadResult<TSpec>(TSpec? Spec, EmitDiagnostic? Diagnostic)
    where TSpec : class;
