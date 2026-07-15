// -----------------------------------------------------------------------
// <copyright file="KeyEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.InProcessKeys.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// One in-process slot-key entry parsed from
/// <c>contracts/in-process-keys/keys.spec.json</c>.
/// </summary>
/// <param name="ConstName">Public C# constant identifier — UPPER_SNAKE_CASE.</param>
/// <param name="Value">
/// Wire value of the slot key. Identical across every binding listed in
/// <paramref name="Bindings"/>.
/// </param>
/// <param name="Purpose">Human-readable description of the slot's purpose.</param>
/// <param name="Bindings">
/// Closed enum of in-process bindings the key applies to
/// (<c>http</c> / <c>grpc</c>).
/// </param>
internal sealed record KeyEntry(
    string ConstName,
    string Value,
    string Purpose,
    ImmutableArray<string> Bindings);
