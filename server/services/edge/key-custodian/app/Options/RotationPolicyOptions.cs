// -----------------------------------------------------------------------
// <copyright file="RotationPolicyOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Options;

using System;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration-bindable shape of a key-rotation policy. Uses
/// <see cref="TimeSpan"/> for the duration fields so it binds cleanly from
/// <c>IConfiguration</c> (e.g. <c>"30.00:00:00"</c>); the provider converts each
/// to a NodaTime <c>Duration</c> and validates through
/// <c>RotationPolicy.Create</c>.
/// </summary>
public sealed class RotationPolicyOptions
{
    /// <summary>Gets or sets how often a key is rotated (the activation-to-rotation window). Must be positive.</summary>
    [Required]
    public TimeSpan Cadence { get; set; }

    /// <summary>Gets or sets how long a retiring key remains in service after a new key activates. Must be positive.</summary>
    [Required]
    public TimeSpan Grace { get; set; }

    /// <summary>Gets or sets how long a generated key must soak before it may be activated. Must be non-negative.</summary>
    [Required]
    public TimeSpan SmokeSoak { get; set; }
}
