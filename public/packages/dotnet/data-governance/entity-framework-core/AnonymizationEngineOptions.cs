// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngineOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

/// <summary>
/// Configuration options for <c>AnonymizationEngine</c>. Bind from the
/// <see cref="SECTION_NAME"/> configuration section or supply via
/// <c>IOptions&lt;AnonymizationEngineOptions&gt;</c> in the DI container.
/// </summary>
/// <remarks>
/// The engine validates <see cref="BatchSize"/> on each sweep — if the
/// value is less than 1 the call returns a failure rather than silently
/// looping or truncating. The startup validator (registered by
/// <c>AddD2DataGovernance</c>) validates the EF Core <em>model</em>
/// (entity-type annotations, tier shapes, template tokens) — it does not
/// validate option values such as <see cref="BatchSize"/> or
/// <see cref="MaxConcurrencyRetries"/>. Option-value misconfig is detected
/// at sweep time by the engine itself.
/// </remarks>
public sealed class AnonymizationEngineOptions
{
    /// <summary>
    /// The configuration section name. Environment-variable binding uses
    /// double-underscore separators, e.g. <c>DATA_GOVERNANCE__BATCHSIZE</c>.
    /// </summary>
    public const string SECTION_NAME = "DATA_GOVERNANCE";

    /// <summary>
    /// Gets or sets the maximum number of rows materialized and saved per
    /// Tier-B chunk. Defaults to <c>500</c>. Must be at least 1.
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum number of retries when a Tier-B chunk
    /// encounters a <c>DbUpdateConcurrencyException</c>. Defaults to
    /// <c>3</c>. Must be at least 0 (0 means no retry — fail immediately
    /// on the first concurrency conflict).
    /// </summary>
    public int MaxConcurrencyRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether the startup model validator is skipped.
    /// Defaults to <see langword="false"/> (deny-by-default: the guard runs on every host
    /// start). Set to <see langword="true"/> only for test hosts that intentionally use
    /// incomplete or partial models.
    /// </summary>
    public bool SkipModelValidation { get; set; }
}
