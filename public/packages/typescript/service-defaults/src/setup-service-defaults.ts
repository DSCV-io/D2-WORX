// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { type ILogger, setupLogger } from "@dcsv-io/d2-logging";
import { setupTelemetry, type TelemetryHandle } from "@dcsv-io/d2-telemetry";

import type { ServiceDefaultsOptions } from "./options.js";

/**
 * Bundle returned by {@link setupServiceDefaults}. Mirrors the .NET
 * `services.AddD2ServiceDefaults` aggregate so consumers obtain logger +
 * telemetry handle in a single call.
 */
export interface ServiceDefaultsHandle {
  readonly logger: ILogger;
  readonly telemetry: TelemetryHandle;
  readonly shutdown: () => Promise<void>;
}

/**
 * One-call composition of `@dcsv-io/d2-logging` + `@dcsv-io/d2-telemetry` matching
 * .NET service-defaults composition. Narrower scope: no middleware
 * aggregator, no auth aggregator (the BFF is zero-privilege so middleware
 * lives in the SvelteKit hook).
 */
export function setupServiceDefaults(
  opts: ServiceDefaultsOptions,
): ServiceDefaultsHandle {
  const logger = setupLogger(opts.logger);
  const telemetry = setupTelemetry({
    serviceName: opts.logger.serviceName,
    environment: opts.logger.environment,
    ...opts.telemetry,
    ...(opts.telemetryDisabled === true ? { disabled: true } : {}),
  });
  return {
    logger,
    telemetry,
    shutdown: async () => {
      await telemetry.shutdown();
    },
  };
}
