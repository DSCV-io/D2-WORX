// -----------------------------------------------------------------------
// <copyright file="LocaleFile.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

/// <summary>
/// Value-equatable record used as the IIncrementalGenerator pipeline boundary.
/// Equatable types at every step boundary are required for incremental
/// caching to work — Roslyn re-runs steps only when input value-equality
/// changes, and a non-equatable boundary type defeats the cache.
/// </summary>
/// <param name="Locale">
/// The locale code derived from the JSON filename (e.g. <c>"en-US"</c>).
/// </param>
/// <param name="Content">The raw JSON content.</param>
internal sealed record LocaleFile(string Locale, string Content);
