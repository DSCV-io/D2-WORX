// -----------------------------------------------------------------------
// <copyright file="OtelSdkDisabledGate.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Internal;

/// <summary>
/// Internal helper that reads the <c>OTEL_SDK_DISABLED</c> env var and
/// returns whether the OTel SDK setup should short-circuit. The env var
/// is the OpenTelemetry-canonical kill switch — honored by the SDK
/// itself in some scenarios. Centralizing the predicate so
/// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/> and
/// <see cref="WebApplicationTelemetryExtensions.MapD2PrometheusEndpoint"/>
/// observe identical semantics (case-insensitive equality with the
/// literal <c>"true"</c>; any other value — including <c>"1"</c>,
/// <c>"yes"</c>, etc. — is treated as "not disabled").
/// </summary>
internal static class OtelSdkDisabledGate
{
    private const string _ENABLED_TRIGGER_VALUE = "true";

    /// <summary>
    /// Returns <c>true</c> when the
    /// <see cref="D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR"/> env
    /// var is set to <c>"true"</c> (case-insensitive). Returns <c>false</c>
    /// for unset / empty / any other literal value.
    /// </summary>
    /// <returns>
    /// <c>true</c> when callers should short-circuit OTel registration;
    /// otherwise <c>false</c>.
    /// </returns>
    internal static bool IsDisabled()
    {
        var raw = Environment.GetEnvironmentVariable(
            D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR);

        return string.Equals(raw, _ENABLED_TRIGGER_VALUE, StringComparison.OrdinalIgnoreCase);
    }
}
