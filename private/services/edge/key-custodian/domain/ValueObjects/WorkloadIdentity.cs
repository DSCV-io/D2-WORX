// -----------------------------------------------------------------------
// <copyright file="WorkloadIdentity.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

using D2.Shared.WorkloadIdentity;

/// <summary>
/// KeyCustodian's issuance-side view of a single workload's SPIFFE identity —
/// the subject-alternative-name a leaf certificate carries:
/// <c>spiffe://d2.internal/workload/&lt;service&gt;</c>.
/// </summary>
/// <remarks>
/// <b>Delegates to the shared grammar.</b> The SPIFFE format lives once, in
/// <see cref="SpiffeWorkloadIdentity"/> (<c>D2.Shared.WorkloadIdentity</c>). This
/// type is KeyCustodian's domain wrapper over that grammar: <see cref="Create"/> /
/// <see cref="Parse"/> defer all validation to the shared VO and re-map its
/// generic <c>ValidationFailed</c> to KeyCustodian's domain-specific
/// <c>KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY</c> code so the issuance path keeps
/// its user-facing error code. The shared validator (in
/// <c>D2.Shared.AspNetCore</c>) consumes the same grammar with a generic failure —
/// one grammar, two consumers, never two parsers.
///
/// <b>Not PII.</b> A workload identity is a service label such as <c>edge</c> or
/// <c>files</c> — not personally identifying. Do NOT apply <c>[RedactData]</c>
/// to this type, the same posture as <c>Kid</c> / <c>KeyDomain</c>.
///
/// <b>Wire-format constants.</b> The trust-domain, scheme, and path-prefix
/// literals re-export the shared grammar's constants so existing call sites and
/// tests keep referencing <c>WorkloadIdentity.TRUST_DOMAIN</c> etc.
/// </remarks>
public sealed record WorkloadIdentity
{
    /// <summary>The SPIFFE URI scheme.</summary>
    public const string SCHEME = SpiffeWorkloadIdentity.SCHEME;

    /// <summary>The internal SPIFFE trust domain — equals the internal token audience.</summary>
    public const string TRUST_DOMAIN = SpiffeWorkloadIdentity.TRUST_DOMAIN;

    /// <summary>The SPIFFE path prefix every D2 workload identity carries.</summary>
    public const string WORKLOAD_PATH_PREFIX = SpiffeWorkloadIdentity.WORKLOAD_PATH_PREFIX;

    /// <summary>Gets the normalized lowercase service identifier (e.g. <c>edge</c>).</summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Gets the full SPIFFE subject-alternative-name URI emitted onto a leaf
    /// certificate (e.g. <c>spiffe://d2.internal/workload/edge</c>).
    /// </summary>
    public string Uri => SpiffeWorkloadIdentity.FromTrusted(ServiceId).Uri;

    /// <summary>
    /// Validates and constructs a <see cref="WorkloadIdentity"/> from a raw service
    /// identifier (the issuance side).
    /// </summary>
    /// <param name="serviceId">Raw service identifier (may be null or whitespace).</param>
    /// <returns>
    /// <c>Ok</c> with the validated <see cref="WorkloadIdentity"/> on success;
    /// <c>ValidationFailed</c> carrying
    /// <c>KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY</c> on failure.
    /// </returns>
    public static D2Result<WorkloadIdentity> Create(string? serviceId) =>
        MapShared(SpiffeWorkloadIdentity.Create(serviceId));

    /// <summary>
    /// Validates and constructs a <see cref="WorkloadIdentity"/> from a full SPIFFE
    /// URI extracted from a presented certificate's subject-alternative-name (the
    /// peer-validation side).
    /// </summary>
    /// <param name="uri">The raw SAN URI (may be null, malformed, or foreign).</param>
    /// <returns>
    /// <c>Ok</c> with the parsed <see cref="WorkloadIdentity"/> on success;
    /// <c>ValidationFailed</c> carrying
    /// <c>KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY</c> for any wrong-scheme,
    /// wrong-trust-domain, missing-path, or malformed input (default-deny).
    /// </returns>
    public static D2Result<WorkloadIdentity> Parse(string? uri) =>
        MapShared(SpiffeWorkloadIdentity.Parse(uri));

    /// <summary>
    /// Reconstructs a <see cref="WorkloadIdentity"/> from a trusted, previously-validated
    /// service identifier WITHOUT re-running validation. For store-side rehydration
    /// only — use <see cref="Create"/> / <see cref="Parse"/> for all untrusted input.
    /// </summary>
    /// <param name="serviceId">The stored service identifier.</param>
    /// <returns>A <see cref="WorkloadIdentity"/> whose <see cref="ServiceId"/> is set verbatim.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="serviceId"/> is <see langword="null"/>, empty, or whitespace.
    /// A corrupt stored value with an empty service id is a data-corruption error, not valid input.
    /// </exception>
    public static WorkloadIdentity FromTrusted(string serviceId)
    {
        serviceId.ThrowIfFalsey();

        return new() { ServiceId = serviceId };
    }

    /// <summary>
    /// Re-maps a shared-grammar result to KeyCustodian's domain wrapper, re-stamping
    /// the generic failure as <c>KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY</c> so the
    /// issuance path keeps its user-facing error code.
    /// </summary>
    /// <param name="shared">The shared-grammar validation result.</param>
    /// <returns>The KeyCustodian-domain result.</returns>
    private static D2Result<WorkloadIdentity> MapShared(D2Result<SpiffeWorkloadIdentity> shared)
    {
        if (!shared.Success)
            return KeyCustodianFailures<WorkloadIdentity>.InvalidWorkloadIdentity();

        return D2Result<WorkloadIdentity>.Ok(new WorkloadIdentity { ServiceId = shared.Data!.ServiceId });
    }
}
