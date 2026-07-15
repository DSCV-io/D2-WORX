// -----------------------------------------------------------------------
// <copyright file="JwksKeySetSnapshot.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions.Jwks;

using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Immutable snapshot of a JWKS verify-key set fetched from an OIDC issuer's
/// <c>jwks_uri</c> endpoint. Returned by <see cref="IJwksProvider"/>.
/// </summary>
/// <remarks>
/// The keys are PUBLIC verify keys, not signing keys — non-secret by design,
/// safe to log the count / kid set if helpful (never log key material itself).
/// Snapshots are referentially-equal value objects: a fresh fetch with the
/// same keys + same source produces an equal snapshot, supporting cache
/// short-circuits.
/// <para>
/// <b>Defensive copy on construction</b>: the <see cref="Keys"/> init setter
/// copies the supplied dictionary into an internal read-only wrapper, so a
/// caller mutating their original dictionary post-construction does NOT affect
/// the snapshot. The "immutable snapshot" claim is enforced by structure, not
/// by trust.
/// </para>
/// </remarks>
public sealed record JwksKeySetSnapshot
{
    private readonly IReadOnlyDictionary<string, SecurityKey> r_keys
        = new Dictionary<string, SecurityKey>(0);

    /// <summary>
    /// Gets the verify keys in this snapshot, keyed by their <c>kid</c> (key id)
    /// claim. Empty when the upstream JWKS endpoint returned an empty key set
    /// (operationally a degraded state — every JWT validation against this
    /// snapshot will fail "kid not found", which is the correct outcome).
    /// </summary>
    /// <remarks>
    /// Defensively copied at construction time — mutating the source
    /// dictionary post-construction does not affect this snapshot.
    /// </remarks>
    public required IReadOnlyDictionary<string, SecurityKey> Keys
    {
        get => r_keys;
        init => r_keys = new Dictionary<string, SecurityKey>(value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the wall-clock instant this snapshot was fetched from <see cref="SourceUri"/>.
    /// Used by callers for staleness checks and by telemetry to surface JWKS
    /// freshness on dashboards.
    /// </summary>
    public required DateTimeOffset FetchedAt { get; init; }

    /// <summary>
    /// Gets the JWKS endpoint URL this snapshot was fetched from. Carried for
    /// telemetry / diagnostics (e.g. "JWKS for <c>https://edge.internal/.well-known/jwks.json</c>
    /// last refreshed 4m ago"). Implementations should set this to the
    /// resolved <c>jwks_uri</c> from the OIDC discovery document, not the
    /// configured issuer URL.
    /// </summary>
    public required Uri SourceUri { get; init; }
}
