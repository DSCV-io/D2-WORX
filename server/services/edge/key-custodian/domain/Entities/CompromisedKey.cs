// -----------------------------------------------------------------------
// <copyright file="CompromisedKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Entities;

using System.Globalization;
using System.Text;

/// <summary>
/// A managed encryption key that has been marked compromised. This is a terminal
/// state — no further lifecycle transitions are permitted.
/// </summary>
/// <remarks>
/// <b>Terminal state.</b> <c>CompromisedKey</c> exposes NO transition methods.
/// Attempting to call any transition on a <c>CompromisedKey</c> does not compile.
///
/// <b>Material retention.</b> Encrypted key material is retained for forensics —
/// investigators need the material to determine what was signed or encrypted with
/// the compromised key.
///
/// <b>PII / <see cref="Reason"/>.</b> The free-text compromise reason can carry
/// operator-entered sensitive context (e.g. a person's name or internal system).
/// It is therefore marked <c>[RedactData(PersonalInformation)]</c> and is
/// length-capped at <see cref="REASON_MAX"/> characters to bound log line size.
/// The audit record carries a non-sensitive descriptor instead of the raw reason.
///
/// <b>Timestamps.</b> Only <see cref="CompromisedAt"/> is added versus the base
/// — there is no <c>ActivatedAt</c>/<c>RetiringAt</c> because a pending key
/// can be compromised before activation. The predecessor's lifecycle timestamps
/// live in <c>EncryptionKeyAudit</c>.
/// </remarks>
public sealed record CompromisedKey : EncryptionKey
{
    /// <summary>Maximum length of the <see cref="Reason"/> string.</summary>
    public const int REASON_MAX = 512;

    /// <inheritdoc/>
    public override KeyStatus Status => KeyStatus.Compromised;

    /// <summary>
    /// Gets the UTC instant at which this key was marked compromised.
    /// </summary>
    /// <remarks>
    /// Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp;
    /// no wall-clock context to preserve.
    /// </remarks>
    public required Instant CompromisedAt { get; init; }

    /// <summary>
    /// Gets the operator-supplied reason for marking this key compromised.
    /// </summary>
    /// <remarks>
    /// Redacted from telemetry — may contain personally identifying context.
    /// Length-capped at <see cref="REASON_MAX"/> characters at construction.
    /// </remarks>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string Reason { get; init; }

    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"CompromisedKey {{ Kid = {Kid}, KeyDomain = {KeyDomain}, KeyType = {KeyType},"
            + $" CompromisedAt = {CompromisedAt}, Reason = [REDACTED:PersonalInformation] }}");

    /// <summary>
    /// Overrides auto-generated <c>PrintMembers</c> to prevent the operator-supplied
    /// compromise reason from appearing in record equality or debug output.
    /// </summary>
    /// <param name="builder">
    /// The builder the record <c>ToString</c> machinery appends member text to.
    /// </param>
    /// <returns><see langword="true"/> — at least one member was appended.</returns>
    protected override bool PrintMembers(StringBuilder builder)
    {
        builder.Append(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Kid = {Kid}, KeyDomain = {KeyDomain}, KeyType = {KeyType},"
                + $" CompromisedAt = {CompromisedAt}, Reason = [REDACTED:PersonalInformation]"));
        return true;
    }
}
