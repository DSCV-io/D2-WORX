// -----------------------------------------------------------------------
// <copyright file="ForwardedJwt.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Attributes;
using DcsvIo.D2.Utilities.Enums;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Redacting wrapper around the raw internal transaction-token (an RS256 JWT
/// bearer string) a hop retains so it can replay the token byte-for-byte on an
/// outbound cross-process gRPC hop. The wrapped value is a live, replayable
/// bearer credential; this type makes it unloggable by construction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why replay verbatim.</b> A hop that rebuilt a token from the claims it
/// parsed would produce a different, unsigned-by-the-issuer token that every
/// downstream hop would reject — so a hop that forwards MUST keep the original
/// bearer bytes. Keeping a live credential in-process is a logging-leak hazard,
/// which this wrapper contains through four reinforcing layers:
/// </para>
/// <list type="number">
///   <item><b>Self-redacting.</b> <see cref="ToString"/> and any default
///     serialization yield <see cref="REDACTION_PLACEHOLDER"/>, never the bytes;
///     the type carries <c>[RedactData]</c> so the Serilog destructuring policy
///     masks the structural (<c>{@x}</c>) capture path; and the raw bytes live
///     in a PRIVATE field, not a public property — the destructuring policy
///     reflects only public instance PROPERTIES, so the bytes are invisible to
///     structural logging even before <c>[RedactData]</c> fires. The explicit
///     <see cref="ToString"/> is required in addition because a plain
///     (<c>{x}</c>) capture calls <see cref="ToString"/> and bypasses the
///     policy entirely.</item>
///   <item><b>Single reveal seam.</b> The raw bytes are reachable only through
///     <see cref="RevealForForwarding"/> — there is no public raw accessor and
///     no implicit/explicit string conversion. The sole production caller is the
///     outbound forwarding credential.</item>
///   <item><b>Enrichment-isolated.</b> The wrapper is held in a dedicated
///     request-scoped holder (<see cref="IForwardedJwtAccessor"/>), never as a
///     property of the request context — the request context is a broadly
///     projected log/telemetry surface, and the credential is structurally
///     excluded from it.</item>
///   <item><b>Never a log parameter.</b> No <c>[LoggerMessage]</c> delegate
///     accepts this type.</item>
/// </list>
/// <para>
/// The guarantee is proven, not asserted — a ToString-redacts test, a
/// field-set-exclusion structural test, a log-capture test across a
/// capture-then-reveal cycle asserting the bytes never surface, and the two
/// scans (sole-reveal-caller, no-log-delegate-parameter).
/// </para>
/// </remarks>
[RedactData(Reason = RedactReason.SecretInformation)]
public readonly struct ForwardedJwt : IEquatable<ForwardedJwt>
{
    /// <summary>
    /// The constant placeholder every log / serialization / string-conversion
    /// path observes in place of the raw bearer bytes.
    /// </summary>
    public const string REDACTION_PLACEHOLDER = "[REDACTED: ForwardedJwt]";

    // Raw bearer bytes — a PRIVATE field, deliberately NOT a public property:
    // the Serilog destructuring policy reflects only public instance properties,
    // so the bytes are invisible to structural logging even before [RedactData]
    // masks the type. The sole read path is RevealForForwarding().
    private readonly string? r_raw;

    private ForwardedJwt(string raw) => r_raw = raw;

    /// <summary>
    /// Gets a value indicating whether this wrapper holds a token (as opposed to
    /// a default / empty <see cref="ForwardedJwt"/>).
    /// </summary>
    public bool HasValue => r_raw is not null;

    /// <summary>Equality operator. See <see cref="Equals(ForwardedJwt)"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both wrap the same token bytes.</returns>
    public static bool operator ==(ForwardedJwt left, ForwardedJwt right) => left.Equals(right);

    /// <summary>Inequality operator. See <see cref="Equals(ForwardedJwt)"/>.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the wrapped token bytes differ.</returns>
    public static bool operator !=(ForwardedJwt left, ForwardedJwt right) => !left.Equals(right);

    /// <summary>
    /// Validates and wraps a raw bearer string. The token is expected to have
    /// ALREADY passed inbound JWT validation at the capture site, so this does
    /// NOT re-check the JWT format — it only rejects a blank credential
    /// (null / empty / whitespace), which is never valid to hold.
    /// </summary>
    /// <param name="rawBearer">
    /// The raw bearer string (the JWT compact serialization without the
    /// <c>Bearer </c> scheme prefix). May be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <c>Ok</c> wrapping the token on success; generic <c>ValidationFailed</c>
    /// when <paramref name="rawBearer"/> is null / empty / whitespace.
    /// </returns>
    public static D2Result<ForwardedJwt> Create(string? rawBearer)
    {
        // ToNullIfEmpty() collapses null/empty/whitespace to null in one call —
        // never hand-roll the null/empty check. NOTE: the trimmed return is
        // deliberately NOT used as the stored value; a validated JWT is held
        // VERBATIM (RevealForForwarding must return the exact bytes the issuer
        // signed), so the trim here is a presence check only.
        if (rawBearer.ToNullIfEmpty() is null)
            return D2Result<ForwardedJwt>.ValidationFailed(category: ErrorCategory.ValidationFailure);

        return D2Result<ForwardedJwt>.Ok(new ForwardedJwt(rawBearer!));
    }

    /// <summary>
    /// THE sole reveal seam — returns the raw bearer bytes verbatim for outbound
    /// forwarding. The intended production caller is the outbound forwarding
    /// credential; a usage-scan test pins that no other production type calls it.
    /// </summary>
    /// <returns>The exact raw bearer bytes the wrapper holds.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called on a default / empty <see cref="ForwardedJwt"/> (one
    /// that holds no token). A reveal with nothing to forward is a pipeline
    /// error, not a silently-empty header.
    /// </exception>
    public string RevealForForwarding() =>
        r_raw ?? throw new InvalidOperationException(
            "RevealForForwarding() was called on an empty ForwardedJwt. A reveal "
            + "with no token to forward indicates a pipeline ordering error — the "
            + "holder must be populated by the inbound auth surface before an "
            + "outbound hop reveals it.");

    /// <summary>
    /// Always returns <see cref="REDACTION_PLACEHOLDER"/> — never the raw bytes.
    /// This is the second mandatory redaction layer: it covers the plain
    /// <c>{x}</c> / string-interpolation log path that the destructuring policy
    /// does not intercept.
    /// </summary>
    /// <returns><see cref="REDACTION_PLACEHOLDER"/>.</returns>
    public override string ToString() => REDACTION_PLACEHOLDER;

    /// <summary>
    /// Structural equality on the wrapped token bytes (ordinal). Two wrappers are
    /// equal when they hold byte-identical tokens; two default wrappers are equal.
    /// </summary>
    /// <param name="other">The other wrapper.</param>
    /// <returns><see langword="true"/> when the wrapped token bytes match.</returns>
    public bool Equals(ForwardedJwt other) =>
        string.Equals(r_raw, other.r_raw, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ForwardedJwt other && Equals(other);

    /// <summary>
    /// Returns a hash of the wrapped token bytes (ordinal), or zero for a default
    /// wrapper. A hash is one-way and non-reversible, so it does not surface the
    /// bytes — unlike <see cref="ToString"/>, which must never echo them.
    /// </summary>
    /// <returns>An ordinal hash of the wrapped bytes, or zero when empty.</returns>
    public override int GetHashCode() =>
        r_raw is null ? 0 : string.GetHashCode(r_raw, StringComparison.Ordinal);
}
