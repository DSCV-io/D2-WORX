// -----------------------------------------------------------------------
// <copyright file="SmokeProof.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

using D2.Edge.KeyCustodian.Domain.Enums;
using NodaTime;
using IClock = D2.Shared.Time.IClock;

/// <summary>
/// Opaque evidence value object that a smoke test passed for a specific
/// <see cref="KeyType"/> at a specific instant.
/// </summary>
/// <remarks>
/// <b>Construction gating.</b> The only way to obtain a <see cref="SmokeProof"/>
/// is via <see cref="ForPassedSmokeTest"/> — the public positional / primary
/// constructor is intentionally absent. The <em>existence</em> of a
/// <c>SmokeProof</c> instance IS the evidence the smoke test passed;
/// there is no <c>bool Passed</c> field that could be fabricated as <c>true</c>.
///
/// The App layer calls <see cref="ForPassedSmokeTest"/> ONLY after its real
/// smoke test returns success. The Domain never executes smoke logic — it
/// receives the proof as a typed argument to
/// <c>PendingKey.Activate(SmokeProof, RotationPolicy, IClock)</c>.
///
/// <b>Cross-type check.</b> <see cref="VerifiedType"/> identifies which
/// <c>KeyType</c> was exercised. <c>PendingKey.Activate</c> asserts
/// <c>proof.VerifiedType == key.KeyType</c> to prevent passing an RSA-signing
/// proof to activate an AES key.
/// </remarks>
public sealed record SmokeProof
{
    // Private constructor — construction gated through ForPassedSmokeTest.
    private SmokeProof()
    {
    }

    /// <summary>Gets the instant at which the smoke test passed.</summary>
    /// <remarks>Cat 2 bare <see cref="Instant"/> (§25.3) — generic UTC timestamp; no wall-clock context to preserve.</remarks>
    public Instant VerifiedAt { get; private init; }

    /// <summary>Gets the key type that was exercised in the smoke test.</summary>
    public KeyType VerifiedType { get; private init; }

    /// <summary>
    /// Creates a <see cref="SmokeProof"/> stamped at the current clock instant.
    /// Call this ONLY after a real smoke test has succeeded.
    /// </summary>
    /// <param name="verifiedType">The key type that was exercised.</param>
    /// <param name="clock">Clock used to stamp the verification instant.</param>
    /// <returns>A <see cref="SmokeProof"/> recording <paramref name="verifiedType"/> and the current instant.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="clock"/> is <see langword="null"/>.
    /// </exception>
    public static SmokeProof ForPassedSmokeTest(KeyType verifiedType, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new SmokeProof
        {
            VerifiedType = verifiedType,
            VerifiedAt = clock.GetCurrentInstant(),
        };
    }
}
