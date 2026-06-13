// -----------------------------------------------------------------------
// <copyright file="EncryptionKeyAudit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Entities;

/// <summary>
/// Append-only audit record that captures each lifecycle transition of a
/// managed encryption key.
/// </summary>
/// <remarks>
/// <b>Append-only.</b> All properties are <c>init</c>-only. No mutation methods
/// exist. The App layer creates one entry per transition and persists it
/// alongside the state change in a single <c>SaveChangesAsync</c>.
///
/// <b>PII discipline.</b> The audit record carries the <c>Kid</c> (opaque,
/// loggable), the action, the resulting status, and the timestamp — enough to
/// reconstruct the lifecycle WITHOUT exposing key material or the
/// compromise free-text reason. If a compromise audit requires context, the
/// <see cref="Detail"/> field carries a non-sensitive descriptor such as
/// <c>"operator-initiated"</c> — NEVER the raw compromise reason and NEVER
/// key bytes.
///
/// <b>No <c>byte[]</c> member.</b> This record intentionally has no key-material
/// field. The forensic value lies in the lifecycle timestamps and action sequence,
/// not in replaying the ciphertext.
/// </remarks>
public sealed record EncryptionKeyAudit
{
    /// <summary>Gets the key identifier this entry records.</summary>
    public required Kid Kid { get; init; }

    /// <summary>Gets the lifecycle action that produced this entry.</summary>
    public required KeyAuditAction Action { get; init; }

    /// <summary>Gets the key's status after the transition.</summary>
    public required KeyStatus ResultingStatus { get; init; }

    /// <summary>
    /// Gets the UTC instant at which the transition occurred.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant OccurredAt { get; init; }

    /// <summary>
    /// Gets an optional non-PII breadcrumb. MUST NOT contain key material,
    /// a raw compromise reason, PII, or personally identifying strings.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Creates an <see cref="EncryptionKeyAudit"/> record stamped at the
    /// current clock instant.
    /// </summary>
    /// <param name="kid">The key whose lifecycle is being audited.</param>
    /// <param name="action">The transition that occurred.</param>
    /// <param name="resultingStatus">The key's status after the transition.</param>
    /// <param name="clock">Clock used to stamp <see cref="OccurredAt"/>.</param>
    /// <param name="detail">
    /// Optional non-PII context (e.g. <c>"operator-initiated"</c>).
    /// MUST NOT contain key material or the raw compromise reason.
    /// </param>
    /// <returns>A new, fully-populated <see cref="EncryptionKeyAudit"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="kid"/> or <paramref name="clock"/> is <see langword="null"/>.
    /// </exception>
    public static EncryptionKeyAudit Record(
        Kid kid,
        KeyAuditAction action,
        KeyStatus resultingStatus,
        IClock clock,
        string? detail = null)
    {
        // §5.1a carve-out: reference-type null-guards (domain VO + DI interface).
        // No present-but-falsey concept for these types.
        ArgumentNullException.ThrowIfNull(kid);
        ArgumentNullException.ThrowIfNull(clock);

        return new EncryptionKeyAudit
        {
            Kid = kid,
            Action = action,
            ResultingStatus = resultingStatus,
            OccurredAt = clock.GetCurrentInstant(),
            Detail = detail,
        };
    }
}
