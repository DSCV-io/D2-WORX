// -----------------------------------------------------------------------
// <copyright file="SpecFile.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.ErrorCodes.SourceGen;

/// <summary>
/// Value-equatable record used as the IIncrementalGenerator pipeline boundary.
/// Equatable types at every step boundary are required for incremental
/// caching to work — Roslyn re-runs steps only when input value-equality
/// changes, and a non-equatable boundary type defeats the cache.
/// </summary>
/// <param name="Path">The full path to the spec file (used in diagnostics).</param>
/// <param name="Content">The raw JSON content of the spec file.</param>
internal sealed record SpecFile(string Path, string Content);
