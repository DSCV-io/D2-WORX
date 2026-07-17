// -----------------------------------------------------------------------
// <copyright file="SpecEnvelope.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Spec;

using System.Collections.Generic;

/// <summary>
/// Generic envelope wrapping the per-catalog metadata block + the
/// <c>entries</c> array. Every geo spec file deserializes into a
/// <see cref="SpecEnvelope{T}"/> instance — the <typeparamref name="T"/>
/// parameter is bound to the per-catalog DTO type
/// (<see cref="CountrySpec"/>, <see cref="CurrencySpec"/>, etc.).
/// </summary>
/// <typeparam name="T">The per-entry DTO type.</typeparam>
/// <param name="Metadata">The spec's catalog metadata header.</param>
/// <param name="Entries">The catalog entries, in spec-file order.</param>
internal sealed record SpecEnvelope<T>(
    SpecMetadata Metadata,
    IReadOnlyList<T> Entries);
