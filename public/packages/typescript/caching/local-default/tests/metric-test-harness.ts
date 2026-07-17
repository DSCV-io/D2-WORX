// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { metrics } from "@opentelemetry/api";
import {
  AggregationTemporality,
  InMemoryMetricExporter,
  MeterProvider,
  PeriodicExportingMetricReader,
  type ResourceMetrics,
} from "@opentelemetry/sdk-metrics";

import { LOCAL_CACHE_METER_NAME } from "../src/index.js";

/**
 * Registers a MeterProvider with an in-memory exporter so tests can
 * assert exact `d2.cache.local.*` counter values. Teardown shuts the
 * provider down and disables the global metrics API.
 */
export interface MetricTestHarness {
  collect(): Promise<ResourceMetrics>;
  counterValue(name: string): Promise<number>;
  teardown(): Promise<void>;
  readonly reader: PeriodicExportingMetricReader;
  readonly exporter: InMemoryMetricExporter;
  readonly provider: MeterProvider;
}

/**
 * Creates a per-test metric harness bound to {@link LOCAL_CACHE_METER_NAME}.
 */
export function createMetricTestHarness(): MetricTestHarness {
  const exporter = new InMemoryMetricExporter(
    AggregationTemporality.CUMULATIVE,
  );
  // Interval far exceeds any test lifetime - the periodic export never
  // auto-fires; collection is manual via reader.collect().
  const reader = new PeriodicExportingMetricReader({
    exporter,
    exportIntervalMillis: 3_600_000,
  });
  const provider = new MeterProvider({ readers: [reader] });

  metrics.setGlobalMeterProvider(provider);

  return {
    reader,
    exporter,
    provider,
    async collect(): Promise<ResourceMetrics> {
      const result = await reader.collect();

      return result.resourceMetrics;
    },
    async counterValue(name: string): Promise<number> {
      const resourceMetrics = await this.collect();
      let total = 0;

      for (const scope of resourceMetrics.scopeMetrics) {
        if (scope.scope.name !== LOCAL_CACHE_METER_NAME) {
          continue;
        }

        for (const metric of scope.metrics) {
          if (metric.descriptor.name !== name) {
            continue;
          }

          for (const point of metric.dataPoints) {
            const value = point.value;

            if (typeof value === "number") {
              total += value;
            }
          }
        }
      }

      return total;
    },
    async teardown(): Promise<void> {
      await provider.shutdown();
      metrics.disable();
    },
  };
}
