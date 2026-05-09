// -----------------------------------------------------------------------
// <copyright file="LoadResult.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.SourceGen;

/// <summary>
/// Outcome of a JSON spec parse. Either <paramref name="Spec"/> is non-null
/// (success) or <paramref name="Diagnostic"/> is non-null (failure). Never
/// both, never neither.
/// </summary>
/// <typeparam name="TSpec">The successfully-loaded spec type.</typeparam>
/// <param name="Spec">The parsed spec on success; null on failure.</param>
/// <param name="Diagnostic">The parse-failure diagnostic; null on success.</param>
internal sealed record LoadResult<TSpec>(TSpec? Spec, EmitDiagnostic? Diagnostic)
    where TSpec : class;
