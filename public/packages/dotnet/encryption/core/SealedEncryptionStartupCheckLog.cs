// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionStartupCheckLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for <see cref="SealedEncryptionStartupCheck"/>.
/// Emit only the recipient service id — never key material, and never an
/// <see cref="System.Exception"/> parameter.
/// </summary>
internal static partial class SealedEncryptionStartupCheckLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "SealedEncryptionStartupCheck registered but no sealed recipients were " +
            "registered — nothing to verify. The sealed registration-by-service sources " +
            "populate the registry.")]
    public static partial void NoRecipientsRegistered(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Sealed encryption self-test passed for recipient {RecipientServiceId} " +
            "(seal → open round-trip).")]
    public static partial void SelfTestPassed(ILogger logger, string recipientServiceId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Sealed encryption seal-only check passed for recipient " +
            "{RecipientServiceId} (producer host: sealed a sentinel into a well-formed " +
            "frame; no opener registered here to round-trip).")]
    public static partial void SealOnlyVerified(ILogger logger, string recipientServiceId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Sealed encryption recipient {RecipientServiceId} registers only an " +
            "opener — no sealer to synthesize a round-trip frame. Private key material " +
            "was validated at keyring construction (P-256 import + agreement probe).")]
    public static partial void OpenerOnlyRegistered(ILogger logger, string recipientServiceId);
}
