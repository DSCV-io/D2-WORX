// -----------------------------------------------------------------------
// <copyright file="LockEntry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Caching.Local.Default;

/// <summary>
/// Internal record for tracking an in-process lock — the holder's
/// caller-supplied identifier plus the absolute expiration time.
/// </summary>
internal sealed record LockEntry(string LockId, DateTimeOffset ExpiresAt);
