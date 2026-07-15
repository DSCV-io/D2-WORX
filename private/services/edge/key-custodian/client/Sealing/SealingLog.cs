// -----------------------------------------------------------------------
// <copyright file="SealingLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance <c>[LoggerMessage]</c> delegates for the sealing runtime — the sealed
/// sibling of <c>KeyringLog</c>. Every delegate carries the <c>seal:&lt;serviceId&gt;</c>
/// domain + an error CODE only — NEVER key material, NEVER an
/// <see cref="System.Exception"/> parameter (rules.md §3.1: an exception's <c>Message</c> can
/// carry secrets / broker URIs / user input). EventIds 9574–9579 — continuing the KeyCustodian
/// Client range (9570–9579; keyring uses 9570–9573).
/// </summary>
internal static partial class SealingLog
{
    /// <summary>Logs a successful seal-keyring refresh (rotation hot-swap applied).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The <c>seal:&lt;serviceId&gt;</c> domain.</param>
    /// <param name="activeKid">The new active kid the keyring swapped to.</param>
    [LoggerMessage(
        EventId = 9574,
        Level = LogLevel.Debug,
        Message = "Seal keyring refreshed for domain '{domain}' (active kid '{activeKid}').")]
    public static partial void SealKeyringRefreshSucceeded(
        ILogger logger, string domain, string activeKid);

    /// <summary>
    /// Logs a rotation-refresh failure after the bounded retry budget was exhausted. The
    /// wrapper keeps serving the current keyring; a later rotation event or a restart re-drives.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The <c>seal:&lt;serviceId&gt;</c> domain.</param>
    /// <param name="errorCode">The KeyCustodian error code from the last failed fetch.</param>
    [LoggerMessage(
        EventId = 9575,
        Level = LogLevel.Warning,
        Message =
            "Seal keyring refresh for domain '{domain}' failed after exhausting the retry budget "
            + "(error '{errorCode}'); serving the current keyring until the next rotation event.")]
    public static partial void SealKeyringRefreshFailed(
        ILogger logger, string domain, string errorCode);

    /// <summary>
    /// Logs a fatal startup seal-keyring-fetch failure for the OPENER (the private keyring
    /// boot fetch). The opener cannot serve without an initial private keyring, so construction
    /// fails loud (the host will crash).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The <c>seal:&lt;serviceId&gt;</c> domain.</param>
    /// <param name="errorCode">The KeyCustodian error code from the failed startup fetch.</param>
    [LoggerMessage(
        EventId = 9576,
        Level = LogLevel.Error,
        Message =
            "Startup seal private-keyring fetch for domain '{domain}' failed (error "
            + "'{errorCode}'); the sealed opener capability cannot be constructed.")]
    public static partial void SealOpenerStartupFetchFailed(
        ILogger logger, string domain, string errorCode);

    /// <summary>
    /// Logs a SEALER lazy public-keyring fetch failure (the producer side fetches lazily on
    /// first seal, never at boot, so a missing recipient does not fail producer startup). The
    /// caller sees a typed publish failure (retryable) — never a plaintext fallback.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The <c>seal:&lt;serviceId&gt;</c> domain.</param>
    /// <param name="errorCode">The KeyCustodian error code from the failed fetch.</param>
    [LoggerMessage(
        EventId = 9577,
        Level = LogLevel.Warning,
        Message =
            "Lazy seal public-keyring fetch for domain '{domain}' failed (error '{errorCode}'); "
            + "the seal is rejected as a retryable publish failure (never a plaintext fallback).")]
    public static partial void SealSealerLazyFetchFailed(
        ILogger logger, string domain, string errorCode);

    /// <summary>
    /// Logs a successful OPENER private-keyring refresh (rotation hot-swap applied). A private
    /// seal keyring has no single "active" kid (it opens frames by the kid the frame declares),
    /// so only the domain is logged.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The <c>seal:&lt;serviceId&gt;</c> domain.</param>
    [LoggerMessage(
        EventId = 9578,
        Level = LogLevel.Debug,
        Message = "Seal private keyring refreshed for domain '{domain}' (rotation hot-swap).")]
    public static partial void SealOpenerKeyringRefreshSucceeded(ILogger logger, string domain);
}
