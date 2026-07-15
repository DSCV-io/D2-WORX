// -----------------------------------------------------------------------
// <copyright file="KeyCustodianOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;

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

    /// <summary>Default mTLS root CA validity when none is configured (~10 years).</summary>
    public const string DEFAULT_ROOT_CA_VALIDITY = "3650.00:00:00";

    /// <summary>Default mTLS issuing-intermediate CA validity when none is configured (~1 year).</summary>
    public const string DEFAULT_INTERMEDIATE_CA_VALIDITY = "365.00:00:00";

    /// <summary>Default mTLS workload-leaf validity when none is configured (24 hours).</summary>
    public const string DEFAULT_LEAF_VALIDITY = "1.00:00:00";

    /// <summary>
    /// Gets or sets the token issuer base URL — the Edge external base URL that is
    /// the OIDC <c>issuer</c> and the prefix of the published <c>jwks_uri</c>
    /// (<c>{IssuerBaseUrl}/.well-known/jwks.json</c>) in the OIDC discovery
    /// document. Required and non-empty: an unset value is a misconfiguration that
    /// crashes the host at startup (fail-loud) rather than serving an empty
    /// <c>issuer</c> at request time. No trailing slash is required — the handler
    /// trims one if present.
    /// </summary>
    [Required(ErrorMessage = "IssuerBaseUrl is required (the Edge external base URL).")]
    [MinLength(1, ErrorMessage = "IssuerBaseUrl must not be empty.")]
    public string IssuerBaseUrl { get; set; } = string.Empty;

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

    /// <summary>
    /// Gets or sets how long a generated mTLS root certificate authority is valid
    /// for. Defaults to <see cref="DEFAULT_ROOT_CA_VALIDITY"/> (~10 years). Bound as
    /// a <see cref="TimeSpan"/> for clean <c>IConfiguration</c> binding; converted
    /// to a NodaTime <c>Duration</c> when passed to the certificate-generation rule.
    /// Must be ≥ 1 second.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "10675199.02:48:05.4775807")]
    public TimeSpan RootCaValidity { get; set; } = TimeSpan.Parse(
        DEFAULT_ROOT_CA_VALIDITY, CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets or sets how long a generated mTLS issuing-intermediate certificate
    /// authority is valid for. Defaults to
    /// <see cref="DEFAULT_INTERMEDIATE_CA_VALIDITY"/> (~1 year). Must be ≥ 1 second.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "10675199.02:48:05.4775807")]
    public TimeSpan IntermediateCaValidity { get; set; } = TimeSpan.Parse(
        DEFAULT_INTERMEDIATE_CA_VALIDITY, CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets or sets how long an issued mTLS workload leaf certificate is valid for.
    /// Defaults to <see cref="DEFAULT_LEAF_VALIDITY"/> (24 hours). Short-lived
    /// because revocation is expiry-first. Must be ≥ 1 second.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "10675199.02:48:05.4775807")]
    public TimeSpan LeafValidity { get; set; } = TimeSpan.Parse(
        DEFAULT_LEAF_VALIDITY, CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    /// <remarks>
    /// Applies a defense-in-depth <c>Falsey()</c> check to <see cref="IssuerBaseUrl"/>
    /// (redundant with the <c>[Required]</c> attribute, which already rejects
    /// null / empty / whitespace via its <c>Trim()</c> rule in the
    /// <c>ValidateDataAnnotations</c> path, but load-bearing if that attribute is ever
    /// relaxed or this method is invoked directly) and
    /// validates the nested <see cref="Default"/> policy and every per-domain entry
    /// in <see cref="Policies"/>. Each nested <see cref="RotationPolicyOptions"/>
    /// is validated with <c>validateAllProperties: true</c> so its
    /// <see cref="RangeAttribute"/> constraints are checked. The cross-field
    /// invariant <c>Cadence ≥ Grace + SmokeSoak</c> (mirroring
    /// <c>RotationPolicy.Create</c>) is also checked here so operator
    /// misconfiguration surfaces at host startup rather than at first use.
    /// </remarks>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // [Required] (AllowEmptyStrings defaults to false) already rejects null, empty,
        // and whitespace-only IssuerBaseUrl in the ValidateDataAnnotations startup path:
        // its rule is value.Trim().Length != 0, and Validator.TryValidateObject
        // short-circuits this Validate() the moment that property-level check fails. This
        // explicit Falsey() check is defense-in-depth — it stays load-bearing if [Required]
        // is ever relaxed to AllowEmptyStrings = true, or when Validate() is called
        // directly, so a whitespace IssuerBaseUrl can never boot and serve issuer:"   ".
        if (IssuerBaseUrl.Falsey())
        {
            yield return new ValidationResult(
                "IssuerBaseUrl must not be null, empty, or whitespace-only.",
                [nameof(IssuerBaseUrl)]);
        }

        foreach (var result in ValidatePolicy("Default", Default))
            yield return result;

        foreach (var kvp in Policies)
        {
            foreach (var result in ValidatePolicy($"Policies[\"{kvp.Key}\"]", kvp.Value))
                yield return result;
        }

        // mTLS CA validity nesting: a leaf must outlive-by-less than the
        // intermediate, which must outlive-by-less than the root — otherwise a
        // child certificate could be told to live past the issuer that signed it,
        // which the chain would reject. Each member is independently range-checked
        // above; this is the cross-field ordering invariant.
        if (LeafValidity >= IntermediateCaValidity)
        {
            yield return new ValidationResult(
                $"LeafValidity ({LeafValidity}) must be shorter than IntermediateCaValidity"
                + $" ({IntermediateCaValidity}).",
                [nameof(LeafValidity)]);
        }

        if (IntermediateCaValidity >= RootCaValidity)
        {
            yield return new ValidationResult(
                $"IntermediateCaValidity ({IntermediateCaValidity}) must be shorter than"
                + $" RootCaValidity ({RootCaValidity}).",
                [nameof(IntermediateCaValidity)]);
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
