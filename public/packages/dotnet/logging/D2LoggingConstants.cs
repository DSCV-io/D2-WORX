// -----------------------------------------------------------------------
// <copyright file="D2LoggingConstants.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging;

/// <summary>
/// Public constants exposed by <see cref="DcsvIo.D2.Logging"/> — config-key
/// strings and other values consumers may want to reference by symbol rather
/// than by literal.
/// </summary>
public static class D2LoggingConstants
{
    /// <summary>
    /// Configuration key used by
    /// <see cref="LoggingServiceCollectionExtensions.AddD2Logging"/> to source
    /// the service name when <see cref="D2LoggingOptions.ServiceName"/> is
    /// left null. Matches the OpenTelemetry-canonical convention so log +
    /// trace + metric service names stay aligned across the log + trace +
    /// metric pipelines without this lib taking an OpenTelemetry SDK
    /// dependency itself.
    /// </summary>
    public const string OTEL_SERVICE_NAME_CONFIG_KEY = "OTEL_SERVICE_NAME";
}
