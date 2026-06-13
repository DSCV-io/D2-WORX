// -----------------------------------------------------------------------
// <copyright file="KeySummary.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// Non-sensitive summary of a managed key returned by the lifecycle command
/// handlers. Carries NO key material — only the kid, domain, type, status, and
/// creation instant.
/// </summary>
/// <remarks>
/// A pure projection over an <see cref="EncryptionKey"/> aggregate. Shared by the
/// generate / activate / retire commands as their output shape, so it lives in
/// the domain as a single source of truth rather than being duplicated per
/// operation.
/// </remarks>
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
