// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it } from "vitest";
import {
  setupServiceDefaults,
  type ServiceDefaultsHandle,
} from "../src/setup-service-defaults.js";

let handle: ServiceDefaultsHandle | undefined;

afterEach(async () => {
  if (handle !== undefined) await handle.shutdown();
  handle = undefined;
});

describe("setupServiceDefaults", () => {
  it("returns logger + telemetry + shutdown bundle", () => {
    handle = setupServiceDefaults({
      logger: { serviceName: "svc", environment: "test" },
      telemetry: {},
      telemetryDisabled: true,
    });
    expect(typeof handle.logger.info).toBe("function");
    expect(typeof handle.telemetry.shutdown).toBe("function");
    expect(typeof handle.shutdown).toBe("function");
    expect(handle.telemetry.disabled).toBe(true);
  });

  it("propagates telemetryDisabled flag", () => {
    handle = setupServiceDefaults({
      logger: { serviceName: "svc" },
      telemetry: {},
      telemetryDisabled: true,
    });
    expect(handle.telemetry.disabled).toBe(true);
  });

  it("telemetry enabled by default", () => {
    handle = setupServiceDefaults({
      logger: { serviceName: "svc" },
      telemetry: {},
    });
    expect(handle.telemetry.disabled).toBe(false);
  });

  it("shutdown awaits telemetry shutdown", async () => {
    handle = setupServiceDefaults({
      logger: { serviceName: "svc" },
      telemetry: {},
      telemetryDisabled: true,
    });
    await expect(handle.shutdown()).resolves.toBeUndefined();
  });
});
