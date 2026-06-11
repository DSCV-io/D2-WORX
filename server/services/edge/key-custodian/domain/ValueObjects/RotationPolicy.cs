// -----------------------------------------------------------------------
// <copyright file="RotationPolicy.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// Immutable value object specifying the timing windows that govern a key's
/// lifecycle: how often it rotates, how long a retiring key overlaps with its
/// successor, and how long a newly-generated key soaks before activation.
/// </summary>
/// <remarks>
/// <b>Invariants.</b> All three durations must be strictly positive, and the
/// cadence must be at least <c>Grace + SmokeSoak</c> long — otherwise a key
/// could be told to rotate before its predecessor finishes retiring, which
/// would break overlap decryption and grace-window JWKS serving.
///
/// <b>Not PII.</b> Policy durations are operational parameters, not personally
/// identifying data. Do NOT apply <c>[RedactData]</c>.
/// </remarks>
public sealed record RotationPolicy
{
    /// <summary>Gets how often a key is rotated (the activation-to-rotation window).</summary>
    public required Duration Cadence { get; init; }

    /// <summary>Gets how long a retiring key remains in service after a new key activates.</summary>
    public required Duration Grace { get; init; }

    /// <summary>Gets how long a generated key must soak before it may be activated.</summary>
    public required Duration SmokeSoak { get; init; }

    /// <summary>
    /// Validates and constructs a <see cref="RotationPolicy"/> from the supplied
    /// duration values.
    /// </summary>
    /// <param name="cadence">How often the key rotates.</param>
    /// <param name="grace">Retiring-to-retired overlap window.</param>
    /// <param name="smokeSoak">Pending-to-active soak window.</param>
    /// <returns>
    /// <c>Ok</c> with the constructed <see cref="RotationPolicy"/> on success;
    /// <c>ValidationFailed</c> carrying
    /// <c>KEYCUSTODIAN_INVALID_ROTATION_POLICY</c> if any duration is
    /// non-positive or if <paramref name="cadence"/> is shorter than
    /// <c>Grace + SmokeSoak</c>.
    /// </returns>
    public static D2Result<RotationPolicy> Create(Duration cadence, Duration grace, Duration smokeSoak)
    {
        if (cadence <= Duration.Zero || grace <= Duration.Zero || smokeSoak <= Duration.Zero)
            return KeyCustodianFailures<RotationPolicy>.InvalidRotationPolicy();

        if (cadence < grace + smokeSoak)
            return KeyCustodianFailures<RotationPolicy>.InvalidRotationPolicy();

        return D2Result<RotationPolicy>.Ok(new RotationPolicy
        {
            Cadence = cadence,
            Grace = grace,
            SmokeSoak = smokeSoak,
        });
    }
}
