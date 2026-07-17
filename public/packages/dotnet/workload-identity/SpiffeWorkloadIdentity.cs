// -----------------------------------------------------------------------
// <copyright file="SpiffeWorkloadIdentity.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Spiffe;

using System.Text.RegularExpressions;
using DcsvIo.D2.ErrorCodes.Category;

/// <summary>
/// Strong-typed value object representing a single workload's SPIFFE identity —
/// the subject-alternative-name a leaf certificate carries and a peer validator
/// checks: <c>spiffe://d2.internal/workload/&lt;service&gt;</c>.
/// </summary>
/// <remarks>
/// <b>The single SPIFFE grammar.</b> This is the one place the SPIFFE
/// workload-identity format lives. Two consumers share it: KeyCustodian's
/// issuance value object delegates to this grammar (re-mapping the failure to its
/// own <c>KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY</c> code), and the shared mTLS
/// peer validator feeds a presented certificate's URI SAN to <see cref="Parse"/>.
/// One grammar, two consumers — never two parsers for one format.
///
/// <b>Not PII.</b> A workload identity is a service label such as <c>edge</c> or
/// <c>files</c> — not personally identifying. Do NOT apply <c>[RedactData]</c>
/// to this type.
///
/// <b>Wire-format constants.</b> The trust-domain, scheme, and path-prefix
/// literals are wire/spec-anchored constants — the literal value IS the SPIFFE
/// format. Use these constants instead of raw strings.
///
/// <b>Two construction paths.</b> <see cref="Create"/> validates a bare service
/// identifier (the issuance side); <see cref="Parse"/> validates a full SPIFFE
/// URI extracted from a presented certificate's SAN (the peer-validation side).
/// Both reject anything that is not a well-formed lowercase workload identity
/// inside the <c>d2.internal</c> trust domain with the same generic
/// <c>ValidationFailed</c> failure — a default-deny posture that does not leak
/// which check failed.
///
/// Validation enforces on the service identifier:
/// <list type="bullet">
///   <item>Non-null, non-empty, non-whitespace.</item>
///   <item>Maximum length of <see cref="_SERVICE_ID_MAX"/> characters.</item>
///   <item>Lowercase DNS-label-safe charset: <c>[a-z0-9-]</c> only.</item>
/// </list>
/// </remarks>
public sealed partial record SpiffeWorkloadIdentity
{
    /// <summary>The SPIFFE URI scheme.</summary>
    public const string SCHEME = "spiffe";

    /// <summary>The internal SPIFFE trust domain — equals the internal token audience.</summary>
    public const string TRUST_DOMAIN = "d2.internal";

    /// <summary>The SPIFFE path prefix every D2 workload identity carries.</summary>
    public const string WORKLOAD_PATH_PREFIX = "/workload/";

    private const int _SERVICE_ID_MAX = 64;

    // Bucket 1 — no-backtracking pattern: single anchored character class with no
    // alternation or repetition that could backtrack. Input is length-capped to
    // _SERVICE_ID_MAX before the match so no timeout is required.
    private static readonly Regex sr_serviceIdCharset = ServiceIdCharsetRegex();

    /// <summary>Gets the normalized lowercase service identifier (e.g. <c>edge</c>).</summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Gets the full SPIFFE subject-alternative-name URI emitted onto a leaf
    /// certificate (e.g. <c>spiffe://d2.internal/workload/edge</c>).
    /// </summary>
    public string Uri =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{SCHEME}://{TRUST_DOMAIN}{WORKLOAD_PATH_PREFIX}{ServiceId}");

    /// <summary>
    /// Validates and constructs a <see cref="SpiffeWorkloadIdentity"/> from a raw
    /// service identifier (the issuance side).
    /// </summary>
    /// <param name="serviceId">Raw service identifier (may be null or whitespace).</param>
    /// <returns>
    /// <c>Ok</c> with the validated <see cref="SpiffeWorkloadIdentity"/> on success;
    /// generic <c>ValidationFailed</c> on failure.
    /// </returns>
    public static D2Result<SpiffeWorkloadIdentity> Create(string? serviceId)
    {
        var normalized = serviceId.ToNullIfEmpty()?.Trim().ToLowerInvariant();

        if (normalized is null)
            return Invalid();

        if (normalized.Length > _SERVICE_ID_MAX)
            return Invalid();

        if (!sr_serviceIdCharset.IsMatch(normalized))
            return Invalid();

        return D2Result<SpiffeWorkloadIdentity>.Ok(
            new SpiffeWorkloadIdentity { ServiceId = normalized });
    }

    /// <summary>
    /// Validates and constructs a <see cref="SpiffeWorkloadIdentity"/> from a full
    /// SPIFFE URI extracted from a presented certificate's subject-alternative-name
    /// (the peer-validation side). Asserts the scheme is <c>spiffe</c>, the host is
    /// the <c>d2.internal</c> trust domain, the path begins with <c>/workload/</c>,
    /// and the remaining service identifier passes <see cref="Create"/>.
    /// </summary>
    /// <param name="uri">The raw SAN URI (may be null, malformed, or foreign).</param>
    /// <returns>
    /// <c>Ok</c> with the parsed <see cref="SpiffeWorkloadIdentity"/> on success;
    /// generic <c>ValidationFailed</c> for any wrong-scheme, wrong-trust-domain,
    /// missing-path, or malformed input (default-deny).
    /// </returns>
    public static D2Result<SpiffeWorkloadIdentity> Parse(string? uri)
    {
        var normalized = uri.ToNullIfEmpty();

        if (normalized is null)
            return Invalid();

        if (!System.Uri.TryCreate(normalized, UriKind.Absolute, out var parsed))
            return Invalid();

        if (!string.Equals(parsed.Scheme, SCHEME, StringComparison.Ordinal))
            return Invalid();

        if (!string.Equals(parsed.Host, TRUST_DOMAIN, StringComparison.Ordinal))
            return Invalid();

        if (!parsed.AbsolutePath.StartsWith(WORKLOAD_PATH_PREFIX, StringComparison.Ordinal))
            return Invalid();

        var serviceId = parsed.AbsolutePath[WORKLOAD_PATH_PREFIX.Length..];

        return Create(serviceId);
    }

    /// <summary>
    /// Reconstructs a <see cref="SpiffeWorkloadIdentity"/> from a trusted,
    /// previously-validated service identifier WITHOUT re-running validation. For
    /// store-side rehydration only — use <see cref="Create"/> / <see cref="Parse"/>
    /// for all untrusted input.
    /// </summary>
    /// <param name="serviceId">The stored service identifier.</param>
    /// <returns>
    /// A <see cref="SpiffeWorkloadIdentity"/> whose <see cref="ServiceId"/> is set verbatim.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="serviceId"/> is <see langword="null"/>, empty, or whitespace.
    /// A corrupt stored value with an empty service id is a data-corruption error, not valid input.
    /// </exception>
    public static SpiffeWorkloadIdentity FromTrusted(string serviceId)
    {
        serviceId.ThrowIfFalsey();

        return new() { ServiceId = serviceId };
    }

    /// <summary>
    /// Builds the generic default-deny failure shared by every rejection path.
    /// The shared grammar owns NO domain-specific error code — a malformed
    /// identity is a generic <c>ValidationFailed</c>; a consuming domain (e.g.
    /// KeyCustodian's issuance path) re-maps it to its own code.
    /// </summary>
    private static D2Result<SpiffeWorkloadIdentity> Invalid() =>
        D2Result<SpiffeWorkloadIdentity>.ValidationFailed(
            category: ErrorCategory.ValidationFailure);

    /// <summary>
    /// Lowercase DNS-label-safe charset: lowercase letters, digits, hyphens only.
    /// Bucket 1 — no-backtracking: single anchored character class, no
    /// alternation/repetition that could backtrack; no timeout required.
    /// </summary>
    [GeneratedRegex(@"^[a-z0-9-]+$", RegexOptions.None)]
    private static partial Regex ServiceIdCharsetRegex();
}
