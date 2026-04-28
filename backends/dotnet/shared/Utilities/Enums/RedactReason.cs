// -----------------------------------------------------------------------
// <copyright file="RedactReason.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Enums;

/// <summary>
/// Reasons for redacting data from logs or telemetry.
/// </summary>
public enum RedactReason
{
    /// <summary>
    /// No specific reason provided.
    /// </summary>
    Unspecified,

    /// <summary>
    /// Data contains personal information that should be protected.
    /// </summary>
    PersonalInformation,

    /// <summary>
    /// Data contains financial information that should be protected.
    /// </summary>
    FinancialInformation,

    /// <summary>
    /// Data contains secret or sensitive information that should be protected.
    /// </summary>
    SecretInformation,

    /// <summary>
    /// Data is overly verbose and not necessary for logging.
    /// </summary>
    VerboseContent,

    /// <summary>
    /// Other reasons not specified above.
    /// </summary>
    Other,
}
