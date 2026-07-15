// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionSourceStartupCheckLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for <see cref="SealedEncryptionSourceStartupCheck"/>. Emit
/// only the recipient service id — never key material, and never an
/// <see cref="System.Exception"/> parameter.
/// </summary>
internal static partial class SealedEncryptionSourceStartupCheckLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "SealedEncryptionSourceStartupCheck registered but no sealed recipients were " +
            "registered — nothing to verify.")]
    public static partial void NoRecipientsRegistered(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Sealed encryption recipient {RecipientServiceId} is KeyCustodian-sourced — " +
            "provenance check passed.")]
    public static partial void SourceCheckPassed(ILogger logger, string recipientServiceId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Sealed encryption recipient {RecipientServiceId} is backed by a static / " +
            "unmarked source rather than a KeyCustodian-sourced, rotation-aware keyring. " +
            "Permitted only in a Development host; a non-Development host crashes. Register " +
            "it through a KeyCustodian sealed source (or mark it via MarkD2EncryptionSource).")]
    public static partial void StaticSourceInDevelopment(ILogger logger, string recipientServiceId);
}
