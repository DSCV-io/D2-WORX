// -----------------------------------------------------------------------
// <copyright file="EncryptionStartupCheckLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Encryption;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for <see cref="EncryptionStartupCheck"/>.
/// </summary>
internal static partial class EncryptionStartupCheckLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "EncryptionStartupCheck registered but no IPayloadCrypto keys were registered via AddD2EncryptionFor.")]
    public static partial void NoKeysRegistered(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Encryption self-test passed for service key {ServiceKey}.")]
    public static partial void SelfTestPassed(ILogger logger, string serviceKey);
}
