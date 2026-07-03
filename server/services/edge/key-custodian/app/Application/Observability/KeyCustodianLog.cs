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

    /// <summary>
    /// Logs that a workload-certificate issuance request found no active issuing
    /// intermediate CA. The workload id is loggable (a non-PII service label); no
    /// certificate or key material is logged.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="workloadServiceId">The workload that requested a leaf.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9507,
        Level = LogLevel.Error,
        Message =
            "Workload certificate issuance for {workloadServiceId} found no active issuing CA; "
            + "the mTLS mesh cannot form until an intermediate CA is seeded or rotated in.")]
    public static partial void NoActiveIssuingCa(ILogger logger, string workloadServiceId);

    /// <summary>
    /// Logs that replacement-key generation failed AFTER the compromise durably
    /// committed. The compromised key is already dead; the missing replacement is
    /// non-fatal (the scheduler or an operator provisions one on the next cycle), so
    /// the handler still returns success with a null replacement kid.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The key domain whose replacement generation failed.</param>
    /// <param name="reason">
    /// The failure reason — the build result's error code, or the classified
    /// second-save failure kind. Never an exception message (§3.1).
    /// </param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9508,
        Level = LogLevel.Warning,
        Message =
            "Replacement pending key generation failed for domain {domain} after the compromise "
            + "committed (reason {reason}); the compromised key is already dead — a replacement "
            + "is generated on the next cycle.")]
    public static partial void ReplacementGenerationFailed(
        ILogger logger, string domain, string? reason);

    /// <summary>
    /// Logs that the certificate-authority hierarchy was seeded on startup: the
    /// root + intermediate were loaded from the CA provider and persisted as active
    /// managed keys. No certificate or key material is logged.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="rootKid">The seeded root CA key's kid.</param>
    /// <param name="intermediateKid">The seeded intermediate CA key's kid.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9510,
        Level = LogLevel.Information,
        Message =
            "Certificate-authority hierarchy seeded: root {rootKid} and intermediate "
            + "{intermediateKid} are now active managed keys.")]
    public static partial void CaSeeded(
        ILogger logger, string rootKid, string intermediateKid);

    /// <summary>
    /// Logs that CA seeding found an existing active hierarchy and made no change
    /// (idempotent re-run). No certificate or key material is logged.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(
        EventId = 9511,
        Level = LogLevel.Debug,
        Message =
            "Certificate-authority hierarchy already seeded (active root + intermediate "
            + "present); seeding is a no-op.")]
    public static partial void CaSeedSkippedAlreadyActive(ILogger logger);

    /// <summary>
    /// Logs that the capability authority rejected a request: the named workload is
    /// not authorized for the requested capability on the requested target. The
    /// workload id, capability, and target are all loggable non-PII labels; no key
    /// material is logged and there is no exception parameter (§3.1).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="workloadServiceId">The workload that was denied (a non-PII service label).</param>
    /// <param name="capability">The requested capability (sign / lifecycle / keyring / issuance / ca-cert / seal-encrypt / seal-decrypt).</param>
    /// <param name="target">
    /// The requested target (e.g. the key domain), or the closed-set
    /// <c>AuthorityRejections.Target.NONE</c> marker for a targetless capability
    /// (issuance / ca-cert).
    /// </param>
    [LoggerMessage(
        EventId = 9512,
        Level = LogLevel.Warning,
        Message =
            "Authority rejected: workload {workloadServiceId} not authorized for capability "
            + "{capability} on target {target}.")]
    public static partial void AuthorityRejected(
        ILogger logger, string workloadServiceId, string capability, string target);

    /// <summary>
    /// Logs that a GetKeyring request found no active payload key for the requested
    /// domain and returned 503. The domain is loggable (a non-PII label); no kid and no
    /// key material is logged, and there is no exception parameter (§3.1).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The payload key domain whose keyring has no active key.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9513,
        Level = LogLevel.Warning,
        Message =
            "Keyring fetch for domain {domain} found no active payload key and returned 503; "
            + "payload encryption for the domain is blocked until a key is active.")]
    public static partial void KeyringKeyUnavailable(ILogger logger, string domain);

    /// <summary>
    /// Logs that a CA-certificate fetch found no active root or issuing-intermediate
    /// tier and returned 503. The caller id is loggable (a non-PII service label); no
    /// certificate or key material is logged, and there is no exception parameter
    /// (§3.1).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="callerId">The caller that requested the chain (a non-PII service label).</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9514,
        Level = LogLevel.Warning,
        Message =
            "CA-certificate fetch for caller {callerId} found no active root/intermediate "
            + "tier and returned 503; the chain cannot be distributed until the CA is seeded "
            + "or rotated in.")]
    public static partial void CaCertificateUnavailable(ILogger logger, string callerId);

    /// <summary>
    /// Logs that a workload leaf certificate was issued (the log-side forensic
    /// complement to the durable issuance audit row). The workload id and kid are
    /// loggable non-PII labels; no certificate or key material is logged.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="workloadServiceId">The workload the leaf was issued to (its SAN identity).</param>
    /// <param name="kid">The issuing-intermediate kid that signed the leaf.</param>
    /// <param name="notAfter">The leaf's not-after instant (ISO-8601).</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9515,
        Level = LogLevel.Information,
        Message =
            "Workload leaf certificate issued to {workloadServiceId} by intermediate {kid}; "
            + "valid until {notAfter}.")]
    public static partial void LeafCertificateIssued(
        ILogger logger, string workloadServiceId, string kid, string notAfter);
}
