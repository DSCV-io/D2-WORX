// -----------------------------------------------------------------------
// <copyright file="KeyCustodianInfraLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance <c>[LoggerMessage]</c> source-generated log delegates for the
/// KeyCustodian Infra layer: the root-key vault load, the rotation scheduler, and
/// the post-commit announce path. EventIds 9530+ (the App layer reserves
/// 9500–9529).
/// </summary>
/// <remarks>
/// No delegate accepts an <see cref="System.Exception"/> parameter (§3.1 — a
/// logged <c>ex.Message</c> can leak broker URIs / connection strings / raw key
/// bytes). Exception detail is rendered PII-safely via
/// <c>SanitizedExceptionRender.TypeName</c> / <c>FirstFrame</c> and passed as
/// plain strings. Root-key bytes are NEVER logged — only paths, the successor
/// present/absent signal, and a decoded byte length on a length-mismatch failure.
/// </remarks>
internal static partial class KeyCustodianInfraLog
{
    // =========================================================================
    // Vault — root-key load (9530–9536). Key bytes NEVER logged.
    // =========================================================================

    /// <summary>Logs that the optional successor root key file is present.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="directory">The root-key directory.</param>
    [LoggerMessage(
        EventId = 9530,
        Level = LogLevel.Information,
        Message = "Root successor key (root-next.key) present in {directory}; "
            + "building a two-kid keyring.")]
    public static partial void RootSuccessorKeyPresent(ILogger logger, string directory);

    /// <summary>Logs that the optional successor root key file is absent (steady state).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="directory">The root-key directory.</param>
    [LoggerMessage(
        EventId = 9531,
        Level = LogLevel.Debug,
        Message = "Root successor key (root-next.key) absent in {directory}; "
            + "building a single-kid keyring.")]
    public static partial void RootSuccessorKeyAbsent(ILogger logger, string directory);

    /// <summary>Logs that a required root-key file was not found (fail-fast).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="kid">The kid whose file was missing.</param>
    /// <param name="path">The path that was probed.</param>
    [LoggerMessage(
        EventId = 9532,
        Level = LogLevel.Critical,
        Message = "Root key file for kid {kid} not found at {path}; host cannot start.")]
    public static partial void RootKeyFileMissing(ILogger logger, string kid, string path);

    /// <summary>Logs that a root-key file was empty (fail-fast).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="kid">The kid whose file was empty.</param>
    /// <param name="path">The file path.</param>
    [LoggerMessage(
        EventId = 9533,
        Level = LogLevel.Critical,
        Message = "Root key file for kid {kid} at {path} is empty; host cannot start.")]
    public static partial void RootKeyFileEmpty(ILogger logger, string kid, string path);

    /// <summary>Logs that a root-key file was not valid hex (fail-fast).</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="kid">The kid whose file was malformed.</param>
    /// <param name="path">The file path.</param>
    [LoggerMessage(
        EventId = 9534,
        Level = LogLevel.Critical,
        Message = "Root key file for kid {kid} at {path} is not valid hex; host cannot start.")]
    public static partial void RootKeyFileNotHex(ILogger logger, string kid, string path);

    /// <summary>
    /// Logs that a root-key file decoded to the wrong byte length (fail-fast).
    /// The decoded length is a count, never content.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="kid">The kid whose file was the wrong length.</param>
    /// <param name="path">The file path.</param>
    /// <param name="actualBytes">The decoded byte count.</param>
    /// <param name="expectedBytes">The required byte count.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9535,
        Level = LogLevel.Critical,
        Message = "Root key file for kid {kid} at {path} decoded to {actualBytes} bytes; "
            + "expected {expectedBytes}; host cannot start.")]
    public static partial void RootKeyFileWrongLength(
        ILogger logger, string kid, string path, int actualBytes, int expectedBytes);

    /// <summary>
    /// Logs that a required CA chain file was not found (fail-fast). Certificate /
    /// key bytes are NEVER logged — only the missing file's path.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="path">The path that was probed.</param>
    [LoggerMessage(
        EventId = 9536,
        Level = LogLevel.Critical,
        Message = "CA chain file not found at {path}; host cannot start.")]
    public static partial void CaFileMissing(ILogger logger, string path);

    /// <summary>
    /// Logs that the loaded CA chain failed to parse or validate (malformed PEM,
    /// wrong curve, or an intermediate that does not chain to the root). Fail-fast.
    /// Certificate / key bytes are NEVER logged — only the directory + reason.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="directory">The CA directory.</param>
    /// <param name="reason">A sanitized, content-free reason.</param>
    [LoggerMessage(
        EventId = 9537,
        Level = LogLevel.Critical,
        Message = "CA chain in {directory} is invalid ({reason}); host cannot start.")]
    public static partial void CaChainInvalid(ILogger logger, string directory, string reason);

    /// <summary>
    /// Logs that a loaded CA certificate is outside its validity window (expired or
    /// not yet valid). Fail-fast. No certificate bytes are logged.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="directory">The CA directory.</param>
    /// <param name="tier">Which tier failed (<c>root</c> or <c>intermediate</c>).</param>
    [LoggerMessage(
        EventId = 9538,
        Level = LogLevel.Critical,
        Message = "CA {tier} certificate in {directory} is outside its validity window; "
            + "host cannot start.")]
    public static partial void CaCertExpired(ILogger logger, string directory, string tier);

    // =========================================================================
    // Scheduling — rotation service (9540–9546).
    // =========================================================================

    /// <summary>Logs that the rotation scheduler started with its tick interval.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="interval">The configured rotation-check interval.</param>
    [LoggerMessage(
        EventId = 9540,
        Level = LogLevel.Information,
        Message = "Key rotation service started; checking every {interval}.")]
    public static partial void RotationServiceStarted(ILogger logger, TimeSpan interval);

    /// <summary>
    /// Logs that a rotation tick skipped because another instance holds the lock.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(
        EventId = 9541,
        Level = LogLevel.Debug,
        Message = "Rotation tick skipped; another instance holds the rotation advisory lock.")]
    public static partial void RotationTickSkippedLockHeld(ILogger logger);

    /// <summary>
    /// Logs that a complete rotation run finished, with the per-action tallies.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="bootstrapped">Count of bootstrapped domains.</param>
    /// <param name="activated">Count of activated domains.</param>
    /// <param name="rotated">Count of rotated domains.</param>
    /// <param name="successorsGenerated">Count of successor-generated domains.</param>
    /// <param name="retired">Count of retired domains.</param>
    /// <param name="skipped">Count of skipped domains.</param>
    /// <param name="errors">Count of per-domain failures.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9542,
        Level = LogLevel.Information,
        Message = "Rotation run completed: bootstrapped={bootstrapped} activated={activated} "
            + "rotated={rotated} successorsGenerated={successorsGenerated} retired={retired} "
            + "skipped={skipped} errors={errors}.")]
    public static partial void RotationRunCompleted(
        ILogger logger,
        int bootstrapped,
        int activated,
        int rotated,
        int successorsGenerated,
        int retired,
        int skipped,
        int errors);

    /// <summary>Logs that the rotation run handler returned a failure result.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="errorCode">The error code from the failed run.</param>
    [LoggerMessage(
        EventId = 9543,
        Level = LogLevel.Error,
        Message = "Rotation run failed (errorCode {errorCode}); will retry on the next tick.")]
    public static partial void RotationRunFailed(ILogger logger, string? errorCode);

    /// <summary>
    /// Logs that a rotation tick threw. The exception is rendered PII-safely
    /// (type + first frame only — never the message).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exceptionType">The PII-safe exception type name.</param>
    /// <param name="firstFrame">The PII-safe first stack frame.</param>
    [LoggerMessage(
        EventId = 9544,
        Level = LogLevel.Error,
        Message = "Rotation tick threw {exceptionType} at {firstFrame}; "
            + "will retry on the next tick.")]
    public static partial void RotationTickFailed(
        ILogger logger, string exceptionType, string firstFrame);

    /// <summary>
    /// Logs that CA seeding skipped because another instance holds the seed lock.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(
        EventId = 9545,
        Level = LogLevel.Debug,
        Message = "CA seeding skipped; another instance holds the CA-seed advisory lock.")]
    public static partial void CaSeedSkippedLockHeld(ILogger logger);

    /// <summary>
    /// Logs that the startup CA-seeding run failed (the host survives — issuance
    /// fails loud later if no CA was seeded). The exception is rendered PII-safely.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exceptionType">The PII-safe exception type name.</param>
    /// <param name="firstFrame">The PII-safe first stack frame.</param>
    [LoggerMessage(
        EventId = 9546,
        Level = LogLevel.Error,
        Message = "CA seeding threw {exceptionType} at {firstFrame}; "
            + "the mTLS mesh cannot form until a CA is seeded.")]
    public static partial void CaSeedFailed(
        ILogger logger, string exceptionType, string firstFrame);

    /// <summary>
    /// Logs that the CA-seeding command returned a failure result (non-throw). The
    /// host survives; issuance fails loud later if no CA was seeded.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="errorCode">The error code from the failed seed.</param>
    [LoggerMessage(
        EventId = 9547,
        Level = LogLevel.Error,
        Message = "CA seeding returned a failure (errorCode {errorCode}); "
            + "the mTLS mesh cannot form until a CA is seeded.")]
    public static partial void CaSeedRunFailed(ILogger logger, string? errorCode);

    /// <summary>
    /// Logs that the mTLS CA chain could not be loaded from the configured directory
    /// (missing files, malformed PEM, wrong curve, invalid chain, or expired
    /// certificate). The host continues in a degraded posture: workload-certificate
    /// issuance returns a 503 until a valid CA chain is installed and the seeder
    /// re-runs. The operator must run <c>gen-dev-keys.sh</c> to supply the four CA
    /// files (ca-root.crt, ca-root.key, ca-intermediate.crt, ca-intermediate.key).
    /// No certificate or key bytes are logged — only the error category.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="category">A sanitized, content-free description of the failure category.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9548,
        Level = LogLevel.Warning,
        Message =
            "mTLS CA chain could not be loaded ({category}); the host is starting in a degraded "
            + "posture — workload-certificate issuance will return 503 until a valid CA chain is "
            + "installed and the CA seeder re-runs. Run gen-dev-keys.sh to supply the CA files.")]
    public static partial void CaLoadDegraded(ILogger logger, string category);

    // =========================================================================
    // Messaging — post-commit announce (9550–9551).
    // =========================================================================

    /// <summary>
    /// Logs that the post-commit announce publish returned a failure result
    /// (non-fatal — consumers self-heal via keyring TTL refresh).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The announced key domain.</param>
    /// <param name="kid">The announced kid.</param>
    /// <param name="urgent">Whether the announce was urgent (compromise).</param>
    [LoggerMessage(
        EventId = 9550,
        Level = LogLevel.Error,
        Message = "Key rotation announce failed to publish for domain {domain} kid {kid} "
            + "(urgent {urgent}); consumers self-heal via keyring TTL refresh.")]
    public static partial void AnnouncePublishFailed(
        ILogger logger, string domain, string kid, bool urgent);

    /// <summary>
    /// Logs that the post-commit announce threw (caught + downgraded to a failure
    /// result — the durable transition already committed). The exception is
    /// rendered PII-safely.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="domain">The announced key domain.</param>
    /// <param name="kid">The announced kid.</param>
    /// <param name="urgent">Whether the announce was urgent (compromise).</param>
    /// <param name="exceptionType">The PII-safe exception type name.</param>
    /// <param name="firstFrame">The PII-safe first stack frame.</param>
    // long log template — cannot wrap
    [LoggerMessage(
        EventId = 9551,
        Level = LogLevel.Error,
        Message = "Key rotation announce threw {exceptionType} at {firstFrame} for domain "
            + "{domain} kid {kid} (urgent {urgent}); transition is durable, consumers self-heal.")]
    public static partial void AnnounceThrew(
        ILogger logger,
        string domain,
        string kid,
        bool urgent,
        string exceptionType,
        string firstFrame);
}
