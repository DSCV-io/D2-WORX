// -----------------------------------------------------------------------
// <copyright file="KeySummary.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Models;

using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Keys;
using NodaTime;

/// <summary>
/// Non-sensitive summary of a managed key returned by the lifecycle command
/// handlers. Carries NO key material — only the kid, domain, type, status, and
/// creation instant.
/// </summary>
/// <param name="Kid">The key identifier.</param>
/// <param name="Domain">The key's logical domain.</param>
/// <param name="KeyType">The cryptographic algorithm category.</param>
/// <param name="Status">The key's lifecycle status after the operation.</param>
/// <param name="CreatedAt">The UTC instant the key was generated.</param>
public sealed record KeySummary(
    string Kid,
    string Domain,
    KeyType KeyType,
    KeyStatus Status,
    Instant CreatedAt)
{
    /// <summary>
    /// Projects an <see cref="EncryptionKey"/> aggregate to its summary. Reads
    /// only non-sensitive identity fields — never the material.
    /// </summary>
    /// <param name="key">The aggregate to summarize.</param>
    /// <returns>A new <see cref="KeySummary"/>.</returns>
    public static KeySummary From(EncryptionKey key) =>
        new(key.Kid.Value, key.KeyDomain.Value, key.KeyType, key.Status, key.CreatedAt);
}
