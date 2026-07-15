// -----------------------------------------------------------------------
// <copyright file="RedactDataAttribute.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Attributes;

using DcsvIo.D2.Utilities.Enums;
using JetBrains.Annotations;

/// <summary>
/// Marker attribute indicating that a type, property, or field should be
/// redacted from logging or other telemetry. Consumed reflectively by the
/// observability layer (Serilog destructuring policy / OTel enrichers).
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class RedactDataAttribute : Attribute
{
    /// <summary>
    /// Gets a custom reason for redaction, if applicable.
    /// </summary>
    [UsedImplicitly]
    public string? CustomReason { get; init; }

    /// <summary>
    /// Gets the reason for redacting the data.
    /// </summary>
    [UsedImplicitly]
    public RedactReason Reason { get; init; }
}
