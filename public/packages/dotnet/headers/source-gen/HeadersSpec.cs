// -----------------------------------------------------------------------
// <copyright file="HeadersSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Headers.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/headers/headers.spec.json</c>.
/// </summary>
/// <param name="Headers">Every header entry declared in the spec (in spec order).</param>
internal sealed record HeadersSpec(ImmutableArray<HeaderEntry> Headers);
