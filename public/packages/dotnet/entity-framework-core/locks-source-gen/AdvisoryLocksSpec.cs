// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the advisory-locks spec file.</summary>
/// <param name="Locks">Every advisory-lock entry declared in the spec.</param>
internal sealed record AdvisoryLocksSpec(ImmutableArray<AdvisoryLockEntry> Locks);
