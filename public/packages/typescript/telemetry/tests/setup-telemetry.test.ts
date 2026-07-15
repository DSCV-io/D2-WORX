// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it } from "vitest";
import {
  setupTelemetry,
  type TelemetryHandle,
} from "../src/setup-telemetry.js";

let handle: TelemetryHandle | undefined;

afterEach(async () => {
  if (handle !== undefined) await handle.shutdown();
  handle = undefined;
});

describe("setupTelemetry", () => {
  it("disabled=true short-circuits → no-op handle", async () => {
    handle = setupTelemetry({ serviceName: "svc", disabled: true });
    expect(handle.disabled).toBe(true);
    await expect(handle.shutdown()).resolves.toBeUndefined();
  });

  it("default mode returns a working handle with shutdown", async () => {
    handle = setupTelemetry({ serviceName: "svc" });
    expect(handle.disabled).toBe(false);
    expect(typeof handle.shutdown).toBe("function");
  });

  it("env-override unset uses defaults", () => {
    handle = setupTelemetry({
      serviceName: "svc",
      environment: "test",
    });
    expect(handle.disabled).toBe(false);
  });

  it("explicit endpoint overrides accepted", () => {
    handle = setupTelemetry({
      serviceName: "svc",
      environment: "test",
      otlpTracesEndpoint: "http://localhost:4318/v1/traces",
      otlpMetricsEndpoint: "http://localhost:4318/v1/metrics",
      otlpLogsEndpoint: "http://localhost:4318/v1/logs",
    });
    expect(handle.disabled).toBe(false);
  });
});
