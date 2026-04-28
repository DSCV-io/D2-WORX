// -----------------------------------------------------------------------
// <copyright file="RedactDataAttribute.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Attributes;

using D2.Shared.Utilities.Enums;
using JetBrains.Annotations;

/// <summary>
/// Attribute to indicate that certain data should be redacted from logging or other telemetry for
/// specified reasons.
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class RedactDataAttribute : Attribute
{
    /// <summary>
    /// Gets a custom reason for redaction, if applicable.
    /// </summary>
    public string? CustomReason { get; init; }

    /// <summary>
    /// Gets the reason for redacting the data.
    /// </summary>
    [UsedImplicitly]
    public RedactReason Reason { get; init; }
}
