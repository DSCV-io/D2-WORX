// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { LoggerOptions } from "@dcsv-io/d2-logging";
import type { TelemetryOptions } from "@dcsv-io/d2-telemetry";

/**
 * Composed options for {@link setupServiceDefaults}. Mirrors the
 * .NET `D2ServiceDefaultsOptions` shape, with narrower scope (no
 * middleware aggregator, no auth aggregator, no local-cache).
 */
export interface ServiceDefaultsOptions {
  readonly logger: LoggerOptions;
  readonly telemetry: Omit<TelemetryOptions, "serviceName" | "environment">;
  /**
   * If true, disables OTel telemetry (matches `OTEL_SDK_DISABLED` env var
   * semantics). Useful for unit tests + offline dev.
   */
  readonly telemetryDisabled?: boolean;
}
