// -----------------------------------------------------------------------
// <copyright file="KeyCustodianOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Root configuration for the KeyCustodian App layer: the default rotation
/// policy, optional per-domain overrides, and the key-generator sizing defaults.
/// </summary>
/// <remarks>
/// <para>
/// Binds from the <c>KEYCUSTODIAN_APP</c> configuration section (environment
/// variable prefix <c>KEYCUSTODIAN_APP__</c>). The startup-binding / validation
/// wiring lives in the Infra layer; this type is the App-owned options shape.
/// </para>
/// <para>
/// <see cref="IValidatableObject"/> is implemented so that
/// <c>ValidateDataAnnotations()</c> recurses into the nested
/// <see cref="Default"/> policy and every entry in <see cref="Policies"/>.
/// <c>DataAnnotationValidateOptions</c> invokes
/// <c>Validator.TryValidateObject</c>, which calls
/// <c>IValidatableObject.Validate</c> on the root instance; this recursion
/// propagates <see cref="RangeAttribute"/> failures on nested
/// <see cref="RotationPolicyOptions"/> members to the host startup gate.
/// </para>
/// </remarks>
public sealed class KeyCustodianOptions : IValidatableObject
{
    /// <summary>The configuration section name this options type binds from.</summary>
    public const string SECTION = "KEYCUSTODIAN_APP";

    /// <summary>Default RSA modulus size in bits when none is configured (RS256 industry standard).
    /// </summary>
    public const int DEFAULT_RSA_KEY_SIZE_BITS = 2048;

    /// <summary>Default opaque-secret length in bytes when none is configured.</summary>
    public const int DEFAULT_SECRET_LENGTH_BYTES = 64;

    /// <summary>
    /// Gets or sets the default rotation policy applied to any domain without an override.
    /// </summary>
    public RotationPolicyOptions Default { get; set; } = new();

    /// <summary>
    /// Gets the per-domain rotation-policy overrides, keyed by the domain's
    /// value (e.g. <c>"jwks-signing"</c>). A domain absent from this map uses
    /// <see cref="Default"/>. The comparer is <c>OrdinalIgnoreCase</c> because
    /// <c>IConfiguration</c>'s environment-variable provider uppercases keys on
    /// Windows (<c>JWKS-SIGNING</c>) while domain values are lowercase
    /// (<c>jwks-signing</c>). A case-sensitive comparer silently falls through to
    /// <see cref="Default"/> on any Windows deployment.
    /// </summary>
    public Dictionary<string, RotationPolicyOptions> Policies { get; } =
        new(StringComparer.OrdinalIgnoreCase);

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

    /// <inheritdoc/>
    /// <remarks>
    /// Validates the nested <see cref="Default"/> policy and every per-domain
    /// entry in <see cref="Policies"/>. Each nested <see cref="RotationPolicyOptions"/>
    /// is validated with <c>validateAllProperties: true</c> so its
    /// <see cref="RangeAttribute"/> constraints are checked. The cross-field
    /// invariant <c>Cadence ≥ Grace + SmokeSoak</c> (mirroring
    /// <c>RotationPolicy.Create</c>) is also checked here so operator
    /// misconfiguration surfaces at host startup rather than at first use.
    /// </remarks>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in ValidatePolicy("Default", Default))
            yield return result;

        foreach (var kvp in Policies)
        {
            foreach (var result in ValidatePolicy($"Policies[\"{kvp.Key}\"]", kvp.Value))
                yield return result;
        }
    }

    private static IEnumerable<ValidationResult> ValidatePolicy(
        string prefix, RotationPolicyOptions policy)
    {
        var ctx = new ValidationContext(policy);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(policy, ctx, results, validateAllProperties: true))
        {
            foreach (var r in results)
            {
                var members = r.MemberNames
                    .Select(m => $"{prefix}.{m}")
                    .ToList();
                yield return new ValidationResult(
                    $"{prefix}: {r.ErrorMessage}",
                    members.Count > 0 ? members : [$"{prefix}"]);
            }
        }

        // Mirror RotationPolicy.Create cross-field invariant: cadence must cover
        // the full grace + smoke-soak window, otherwise a key would be told to
        // rotate before its predecessor finishes retiring.
        if (policy.Cadence > TimeSpan.Zero &&
            policy.Grace > TimeSpan.Zero &&
            policy.SmokeSoak > TimeSpan.Zero &&
            policy.Cadence < policy.Grace + policy.SmokeSoak)
        {
            yield return new ValidationResult(
                $"{prefix}: Cadence ({policy.Cadence}) must be at least Grace + SmokeSoak" +
                $" ({policy.Grace} + {policy.SmokeSoak} = {policy.Grace + policy.SmokeSoak}).",
                [$"{prefix}.Cadence"]);
        }
    }
}
