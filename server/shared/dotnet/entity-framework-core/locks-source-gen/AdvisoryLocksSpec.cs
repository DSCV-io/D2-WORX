// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksSpec.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.AdvisoryLocks.SourceGen;

using System.Collections.Immutable;

/// <summary>Parsed shape of the advisory-locks spec file.</summary>
/// <param name="Locks">Every advisory-lock entry declared in the spec.</param>
internal sealed record AdvisoryLocksSpec(ImmutableArray<AdvisoryLockEntry> Locks);
