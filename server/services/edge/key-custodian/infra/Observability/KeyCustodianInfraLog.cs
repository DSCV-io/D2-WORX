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
