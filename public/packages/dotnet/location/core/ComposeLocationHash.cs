// -----------------------------------------------------------------------
// <copyright file="ComposeLocationHash.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location;

using System.Security.Cryptography;
using DcsvIo.D2.Location.ValueObjects;

/// <summary>
/// Free composition function that joins up to three location value
/// objects (<see cref="Coordinates"/> + <see cref="StreetAddress"/> +
/// <see cref="AdminLocation"/>) into a single content-addressable hash
/// identifier. All-null input returns <c>null</c> (location absent —
/// not an error); any non-null subset returns a deterministic
/// <c>"v1."</c>-prefixed SHA-256 hex digest.
/// </summary>
public static class ComposeLocationHash
{
    /// <summary>
    /// Composes a single hash identifier from the (optional) three
    /// location components. Returns <c>null</c> when ALL three inputs
    /// are null (location is absent — not an error). Otherwise returns
    /// <c>"v1." + SHA-256(c.HashId | s.HashId | a.HashId)</c>; missing
    /// slots contribute <c>""</c> (positional, never collapsed). Inner
    /// component <c>"v1."</c> prefixes ARE included in the outer hash
    /// input.
    /// </summary>
    /// <remarks>
    /// Returns <c>string?</c> rather than <c>D2Result&lt;string&gt;</c>
    /// — documented §17 carve-out: the operation cannot fail (inputs
    /// are already-validated VOs or null), and all-null is a legitimate
    /// non-error state. Wrapping with <c>D2Result</c> would force
    /// consumers to unwrap an always-<c>Ok</c> result on the data path.
    /// </remarks>
    /// <param name="coordinates">Optional <see cref="Coordinates"/> component.</param>
    /// <param name="streetAddress">Optional <see cref="StreetAddress"/> component.</param>
    /// <param name="adminLocation">Optional <see cref="AdminLocation"/> component.</param>
    /// <returns>The composed hash identifier, or <c>null</c> for all-null input.</returns>
    public static string? Compose(
        Coordinates? coordinates,
        StreetAddress? streetAddress,
        AdminLocation? adminLocation)
    {
        if (coordinates is null && streetAddress is null && adminLocation is null)
            return null;

        var input =
            (coordinates?.HashId ?? string.Empty) + "|" +
            (streetAddress?.HashId ?? string.Empty) + "|" +
            (adminLocation?.HashId ?? string.Empty);

        // BCL static one-shot per §15.8 — no IDisposable instance to manage.
        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return "v1." + Convert.ToHexStringLower(hashBytes);
    }
}
