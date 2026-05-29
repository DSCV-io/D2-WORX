// -----------------------------------------------------------------------
// <copyright file="InProcessKeysSpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.InProcessKeys.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/in-process-keys/keys.spec.json</c>.
/// </summary>
/// <param name="Keys">Every key entry declared in the spec (in spec order).</param>
internal sealed record InProcessKeysSpec(ImmutableArray<KeyEntry> Keys);
