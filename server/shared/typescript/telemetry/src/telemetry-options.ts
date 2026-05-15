// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Options for {@link setupTelemetry}. All endpoint URLs default to env-var
 * resolution (`OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` etc.) when unset.
 *
 * Mirrors `D2.Shared.Telemetry.D2TelemetryOptions` field-for-field where
 * the .NET options surface applies in Node.
 */
export interface TelemetryOptions {
  /** Service name; emitted as `service.name` resource attribute. */
  readonly serviceName: string;
  /** Environment label (e.g. `"prod"`, `"local"`). */
  readonly environment?: string;
  /** Per-signal OTLP endpoints. Per-signal env var overrides these. */
  readonly otlpTracesEndpoint?: string;
  readonly otlpMetricsEndpoint?: string;
  readonly otlpLogsEndpoint?: string;
  /** Additional ActivitySources / meters / instrumentation paths. */
  readonly additionalActivitySources?: readonly string[];
  readonly additionalMeters?: readonly string[];
  readonly instrumentationExcludedPaths?: readonly (string | RegExp)[];
  /**
   * Set to `true` to short-circuit telemetry init — `setupTelemetry`
   * returns no-op providers + a no-op shutdown. Mirrors `OTEL_SDK_DISABLED`
   * env var behavior.
   */
  readonly disabled?: boolean;
}
