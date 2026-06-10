// -----------------------------------------------------------------------
// <copyright file="KeyCustodianOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Options;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Root configuration for the KeyCustodian App layer: the default rotation
/// policy, optional per-domain overrides, and the key-generator sizing defaults.
/// </summary>
/// <remarks>
/// Binds from the <c>KeyCustodian</c> configuration section. The
/// startup-binding / validation wiring lives in the Infra layer; this
/// type is the App-owned options shape.
/// </remarks>
public sealed class KeyCustodianOptions
{
    /// <summary>The configuration section name this options type binds from.</summary>
    public const string SECTION = "KeyCustodian";

    /// <summary>Default RSA modulus size in bits when none is configured (RS256 industry standard).</summary>
    public const int DEFAULT_RSA_KEY_SIZE_BITS = 2048;

    /// <summary>Default opaque-secret length in bytes when none is configured.</summary>
    public const int DEFAULT_SECRET_LENGTH_BYTES = 64;

    /// <summary>Gets or sets the default rotation policy applied to any domain without an override.</summary>
    public RotationPolicyOptions Default { get; set; } = new();

    /// <summary>
    /// Gets the per-domain rotation-policy overrides, keyed by the domain's
    /// normalized value (e.g. <c>"jwks-signing"</c>). A domain absent from this
    /// map uses <see cref="Default"/>.
    /// </summary>
    public Dictionary<string, RotationPolicyOptions> Policies { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the RSA modulus size in bits for generated signing keys.
    /// Defaults to <see cref="DEFAULT_RSA_KEY_SIZE_BITS"/>. Minimum 2048 bits.
    /// </summary>
    [Range(2048, int.MaxValue, ErrorMessage = "RsaKeySizeBits must be at least 2048.")]
    public int RsaKeySizeBits { get; set; } = DEFAULT_RSA_KEY_SIZE_BITS;

    /// <summary>
    /// Gets or sets the length in bytes of generated opaque secret keys.
    /// Defaults to <see cref="DEFAULT_SECRET_LENGTH_BYTES"/>. Minimum 16 bytes.
    /// </summary>
    [Range(16, int.MaxValue, ErrorMessage = "SecretLengthBytes must be at least 16.")]
    public int SecretLengthBytes { get; set; } = DEFAULT_SECRET_LENGTH_BYTES;
}
