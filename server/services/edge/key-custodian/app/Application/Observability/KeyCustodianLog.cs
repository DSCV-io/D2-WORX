// -----------------------------------------------------------------------
// <copyright file="KeyCustodianLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance <c>[LoggerMessage]</c> source-generated log delegates for the
/// KeyCustodian App layer. EventIds 9500–9529 (Infra takes 9530+).
/// </summary>
/// <remarks>
/// No delegate accepts an <see cref="System.Exception"/> parameter (§3.1 — a
/// logged <c>ex.Message</c> can leak broker URIs / connection strings / raw
/// input). The kid + domain are loggable by design (opaque, non-PII). Key
/// material and the raw compromise reason are NEVER logged.
/// </remarks>
internal static partial class KeyCustodianLog
{
    /// <summary>
    /// Logs that the post-commit rotation announcement failed. The durable
    /// transition already committed; the announce failure is non-fatal (consumers
    /// self-heal via keyring TTL refresh), so the handler still returns success.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The key domain whose announcement failed.</param>
    /// <param name="kid">The kid that was announced.</param>
    /// <param name="errorCode">The error code carried by the failed announce result.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9500,
        Level = LogLevel.Error,
        Message =
            "Key rotation announcement failed for domain {domain} kid {kid} (errorCode {errorCode}); "
            + "the transition is durable and consumers self-heal via keyring TTL refresh.")]
    public static partial void AnnounceFailed(
        ILogger logger, string domain, string kid, string? errorCode);

    /// <summary>
    /// Logs that a key's smoke test failed during activation; the key was not
    /// activated.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="kid">The kid whose smoke test failed.</param>
    /// <param name="keyType">The key type that was tested.</param>
    [LoggerMessage(
        EventId = 9501,
        Level = LogLevel.Warning,
        Message = "Smoke test failed for kid {kid} keyType {keyType}; the key was not activated.")]
    public static partial void SmokeTestFailed(ILogger logger, string kid, string keyType);

    /// <summary>
    /// Logs that a replacement pending key was generated after a compromise.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The key domain.</param>
    /// <param name="kid">The replacement key's kid.</param>
    [LoggerMessage(
        EventId = 9502,
        Level = LogLevel.Information,
        Message = "Replacement pending key {kid} generated for domain {domain} after compromise.")]
    public static partial void ReplacementKeyGenerated(ILogger logger, string domain, string kid);

    /// <summary>
    /// Logs that a rotation completed: the incumbent retired and the successor
    /// activated in one transaction.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The key domain.</param>
    /// <param name="retiringKid">The kid that entered the retiring state.</param>
    /// <param name="activatedKid">The kid that was activated.</param>
    [LoggerMessage(
        EventId = 9503,
        Level = LogLevel.Information,
        Message =
            "Rotation completed for domain {domain}: {retiringKid} retiring, "
            + "{activatedKid} activated.")]
    public static partial void RotationCompleted(
        ILogger logger, string domain, string retiringKid, string activatedKid);

    /// <summary>
    /// Logs that a domain needed bootstrap but its key type was absent from the
    /// <c>RunDueRotations</c> input's <c>BootstrapKeyTypes</c> map. The domain is
    /// skipped without error.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The domain that could not be bootstrapped.</param>
    [LoggerMessage(
        EventId = 9504,
        Level = LogLevel.Warning,
        Message =
            "Domain {domain} needs bootstrap but has no entry in BootstrapKeyTypes; skipping.")]
    public static partial void BootstrapKeyTypeMissing(ILogger logger, string domain);

    /// <summary>
    /// Logs that a per-domain rotation action failed during a <c>RunDueRotations</c>
    /// run. The error is non-fatal to the overall run; other domains continue to be
    /// serviced.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="action">The lifecycle action that failed (e.g. "bootstrap", "rotate").</param>
    /// <param name="domain">The domain for which the action failed.</param>
    /// <param name="errorCode">The error code carried by the failed result.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9505,
        Level = LogLevel.Error,
        Message =
            "RunDueRotations: {action} failed for domain {domain} (errorCode {errorCode}); "
            + "continuing with remaining domains.")]
    public static partial void RotationActionFailed(
        ILogger logger, string action, string domain, string? errorCode);

    /// <summary>
    /// Logs that a record classified by the rotation plan was gone from the store by
    /// the time the handler re-queried it (TOCTOU gap). The domain is counted in
    /// <c>Errors</c> and excluded from the relevant success list.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="action">
    /// The lifecycle action being attempted (e.g. "activate", "generate-successor",
    /// "retire").
    /// </param>
    /// <param name="domain">The domain whose record was not found.</param>
    [LoggerMessage(
        EventId = 9506,
        Level = LogLevel.Warning,
        Message =
            "RunDueRotations: {action} for domain {domain} — record gone from store "
            + "between plan classification and re-query (TOCTOU); counting as error.")]
    public static partial void RecordGoneFromPlan(
        ILogger logger, string action, string domain);
}
