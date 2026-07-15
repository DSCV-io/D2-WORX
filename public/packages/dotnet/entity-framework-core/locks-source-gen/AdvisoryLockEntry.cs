// -----------------------------------------------------------------------
// <copyright file="AdvisoryLockEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

/// <summary>
/// One advisory-lock entry parsed from
/// <c>contracts/advisory-locks/advisory-locks.spec.json</c>.
/// </summary>
/// <param name="ConstName">UPPER_SNAKE_CASE C# constant identifier.</param>
/// <param name="Database">
/// Owning database (lowercase snake_case). The uniqueness check is
/// per-database — the same key in two different databases is legal.
/// </param>
/// <param name="Key">
/// Explicit signed 64-bit integer passed to
/// <c>pg_advisory_lock</c> / <c>pg_try_advisory_lock</c>.
/// </param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record AdvisoryLockEntry(
    string ConstName,
    string Database,
    long Key,
    string Doc);
