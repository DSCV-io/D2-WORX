// -----------------------------------------------------------------------
// <copyright file="KeyringLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Keyring;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance <c>[LoggerMessage]</c> delegates for the keyring runtime.
/// Every delegate carries the domain + an error CODE only — NEVER key material,
/// NEVER an <see cref="System.Exception"/> parameter (rules.md §3.1: an exception's
/// <c>Message</c> can carry secrets / broker URIs / user input).
/// EventIds 9570–9579 — the KeyCustodian Client range, distinct from the App
/// (9500–9529), Infra (9530+), and Mtls (9560+) ranges.
/// </summary>
internal static partial class KeyringLog
{
    /// <summary>Logs a successful keyring refresh (rotation hot-swap applied).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The payload key domain.</param>
    /// <param name="activeKid">The new active kid the keyring swapped to.</param>
    [LoggerMessage(
        EventId = 9570,
        Level = LogLevel.Debug,
        Message = "Keyring refreshed for domain '{domain}' (active kid '{activeKid}').")]
    public static partial void KeyringRefreshSucceeded(
        ILogger logger, string domain, string activeKid);

    /// <summary>
    /// Logs a rotation-refresh failure after the bounded retry budget was exhausted.
    /// The wrapper keeps serving the current keyring; a later rotation event or a
    /// restart re-drives.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The payload key domain.</param>
    /// <param name="errorCode">The KeyCustodian error code from the last failed fetch.</param>
    [LoggerMessage(
        EventId = 9571,
        Level = LogLevel.Warning,
        Message =
            "Keyring refresh for domain '{domain}' failed after exhausting the retry budget "
            + "(error '{errorCode}'); serving the current keyring until the next rotation event.")]
    public static partial void KeyringRefreshFailed(
        ILogger logger, string domain, string errorCode);

    /// <summary>
    /// Logs a fatal startup keyring-fetch failure. The wrapper cannot serve without an
    /// initial keyring, so construction fails loud (the host will crash).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The payload key domain.</param>
    /// <param name="errorCode">The KeyCustodian error code from the failed startup fetch.</param>
    [LoggerMessage(
        EventId = 9572,
        Level = LogLevel.Error,
        Message =
            "Startup keyring fetch for domain '{domain}' failed (error '{errorCode}'); "
            + "the encryption capability cannot be constructed.")]
    public static partial void KeyringStartupFetchFailed(
        ILogger logger, string domain, string errorCode);

    /// <summary>
    /// Logs an isolated rotation-callback failure (consumer isolation) — one callback
    /// threw, sibling callbacks for the same domain still run.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The payload key domain.</param>
    /// <param name="exceptionType">The thrown exception's type name (never its message).</param>
    [LoggerMessage(
        EventId = 9573,
        Level = LogLevel.Warning,
        Message =
            "A rotation callback for domain '{domain}' threw '{exceptionType}'; "
            + "sibling callbacks were unaffected.")]
    public static partial void RotationCallbackFailed(
        ILogger logger, string domain, string exceptionType);
}
