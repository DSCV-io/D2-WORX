// -----------------------------------------------------------------------
// <copyright file="KeyCustodianInfraOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Infrastructure-layer configuration for the KeyCustodian module: the root-key
/// directory, the rotation tick cadence, and the per-command database timeout.
/// </summary>
/// <remarks>
/// <para>
/// Binds from the <c>KEYCUSTODIAN_INFRA</c> configuration section (environment
/// variable prefix <c>KEYCUSTODIAN_INFRA__</c>). This infra-owned options shape
/// lives in the Infra layer because every field is an infrastructure concern
/// (filesystem path, scheduler interval, Npgsql command timeout).
/// </para>
/// <para>
/// <see cref="ConnectionString"/> is NOT bound from the section — it is supplied
/// by the host as the <c>connectionString</c> argument to
/// <c>AddD2KeyCustodian</c> (sourced from <c>KEYCUSTODIAN_DATABASE_URL</c>) and
/// post-configured onto this instance so the rotation service and DbContext share
/// one source.
/// </para>
/// </remarks>
public sealed class KeyCustodianInfraOptions
{
    /// <summary>The configuration section name this options type binds from.</summary>
    public const string SECTION = "KEYCUSTODIAN_INFRA";

    /// <summary>The default rotation-check interval (5 minutes).</summary>
    public const string DEFAULT_ROTATION_CHECK_INTERVAL = "00:05:00";

    /// <summary>The default per-command database timeout in seconds.</summary>
    public const int DEFAULT_DB_COMMAND_TIMEOUT_SECONDS = 30;

    /// <summary>
    /// Gets or sets the directory holding the root-key files (<c>root.key</c> and
    /// the optional <c>root-next.key</c>). Required; must be a non-empty path.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string RootKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how often the in-process scheduler checks for due rotations.
    /// Defaults to 5 minutes. Must be at least 1 second.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "10675199.02:48:05.4775807")]
    public TimeSpan RotationCheckInterval { get; set; } =
        TimeSpan.Parse(DEFAULT_ROTATION_CHECK_INTERVAL, CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets or sets the per-command database timeout in seconds. Defaults to 30.
    /// Must be at least 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "DbCommandTimeoutSeconds must be at least 1.")]
    public int DbCommandTimeoutSeconds { get; set; } = DEFAULT_DB_COMMAND_TIMEOUT_SECONDS;

    /// <summary>
    /// Gets or sets the database connection string. Supplied by the host via
    /// <c>AddD2KeyCustodian(configuration, connectionString)</c> — NOT bound from
    /// the <c>KEYCUSTODIAN_INFRA</c> section. Used by the rotation service's
    /// advisory-lock connection (the DbContext is configured separately).
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
