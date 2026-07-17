// -----------------------------------------------------------------------
// <copyright file="LockEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Local.Default;

/// <summary>
/// Internal record for tracking an in-process lock — the holder's
/// caller-supplied identifier plus the absolute expiration time.
/// </summary>
internal sealed record LockEntry(string LockId, DateTimeOffset ExpiresAt);
