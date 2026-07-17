// -----------------------------------------------------------------------
// <copyright file="SyntheticEndpointsConstants.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults.Infrastructure;

/// <summary>
/// Stable string constants emitted by
/// <see cref="SyntheticEndpoints.MapSyntheticEndpoints"/> — separated from
/// the extension class because StyleCop's <c>SA1201</c> ordering rule
/// flags constants alongside the C# 14 <c>extension(T)</c> block-form on
/// the same class. Tests reference these tokens to locate synthetic
/// emissions in captured log / activity / metric snapshots.
/// </summary>
internal static class SyntheticEndpointsConstants
{
    /// <summary>
    /// Logger category used by the synthetic <c>/log-mel-info</c>
    /// endpoint. A stable name so tests can assert on the OTel
    /// log-record exporter's CategoryName property without depending on
    /// a generic-type token.
    /// </summary>
    public const string SYNTHETIC_LOGGER_CATEGORY = "D2.Tests.Synthetic";

    /// <summary>
    /// Marker string emitted by <c>/log-mel-info</c> as the message body
    /// — tests grep the captured log record body / message for this token
    /// to locate the synthetic event without false-positive matches
    /// against incidental log lines.
    /// </summary>
    public const string SYNTHETIC_MEL_BRIDGE_MARKER = "synthetic-mel-bridge-marker";

    /// <summary>
    /// Marker string emitted by <c>/log-redacted</c> as the message
    /// template's leading literal so tests can locate the log event.
    /// </summary>
    public const string SYNTHETIC_REDACTION_MARKER = "synthetic-redaction-probe";
}
