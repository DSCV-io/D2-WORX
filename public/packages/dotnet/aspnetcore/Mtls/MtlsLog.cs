// -----------------------------------------------------------------------
// <copyright file="MtlsLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Mtls;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance <c>[LoggerMessage]</c> source-generated log delegates for the
/// mutual-TLS peer-certificate validator. The only <c>[LoggerMessage]</c> surface
/// in <c>DcsvIo.D2.AspNetCore</c> — a peer-certificate rejection at the TLS
/// handshake is a security event worth a structured, allocation-free record.
/// </summary>
/// <remarks>
/// No delegate accepts an <see cref="System.Exception"/> parameter (§3.1 — a logged
/// <c>ex.Message</c> can leak certificate subject / subject-alternative-name bytes).
/// On the exception path the type name is rendered PII-safely via
/// <c>SanitizedExceptionRender.TypeName</c> and passed as a plain string.
/// Certificate bytes are NEVER logged — only a content-free reason code and the
/// workload identifier (a non-PII service label). EventIds 9560+ (distinct from the
/// KeyCustodian Infra 9530+ range).
/// </remarks>
internal static partial class MtlsLog
{
    /// <summary>
    /// Logs that a presented client certificate was rejected by the default-deny
    /// peer validator. No certificate bytes are logged — only a content-free reason
    /// code and the workload identifier (when one could be parsed).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="reason">A content-free reason code (e.g. <c>chain-not-trusted</c>).</param>
    /// <param name="workload">The parsed workload id, or a sentinel when none was parsed.</param>
    [LoggerMessage(
        EventId = 9560,
        Level = LogLevel.Warning,
        Message = "mTLS peer certificate rejected (reason {reason}, workload {workload}).")]
    public static partial void PeerCertificateRejected(
        ILogger logger, string reason, string workload);

    /// <summary>
    /// Logs that the peer validator caught an exception while validating a presented
    /// certificate (default-deny — the certificate is rejected). The exception is
    /// rendered PII-safely (type name only — never the message).
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="reason">A content-free reason code.</param>
    /// <param name="exceptionType">The PII-safe exception type name.</param>
    [LoggerMessage(
        EventId = 9561,
        Level = LogLevel.Warning,
        Message = "mTLS peer certificate rejected (reason {reason}, exceptionType {exceptionType}).")]
    public static partial void PeerCertificateRejectedOnException(
        ILogger logger, string reason, string exceptionType);
}
