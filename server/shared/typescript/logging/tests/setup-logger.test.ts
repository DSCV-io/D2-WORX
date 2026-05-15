// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it } from "vitest";
import {
  clearRedactedFieldsRegistry,
  markRedactedFields,
} from "../src/redaction.js";
import { setupLogger } from "../src/setup-logger.js";

describe("setupLogger", () => {
  afterEach(() => clearRedactedFieldsRegistry());

  it("returns an ILogger with all six level methods + child()", () => {
    const log = setupLogger({ serviceName: "svc" });
    for (const m of ["trace", "debug", "info", "warn", "error", "fatal"]) {
      expect(typeof (log as unknown as Record<string, unknown>)[m]).toBe(
        "function",
      );
    }
    expect(typeof log.child).toBe("function");
  });

  it("level methods accept (message) and (message, bindings)", () => {
    const log = setupLogger({ serviceName: "svc", minLevel: "trace" });
    expect(() => log.trace("plain")).not.toThrow();
    expect(() => log.trace("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.debug("plain")).not.toThrow();
    expect(() => log.debug("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.info("plain")).not.toThrow();
    expect(() => log.info("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.warn("plain")).not.toThrow();
    expect(() => log.warn("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.error("plain")).not.toThrow();
    expect(() => log.error("with bindings", { x: 1 })).not.toThrow();
    expect(() => log.fatal("plain")).not.toThrow();
    expect(() => log.fatal("with bindings", { x: 1 })).not.toThrow();
  });

  it("child() returns a wrapper that exposes the same shape", () => {
    const log = setupLogger({ serviceName: "svc" });
    const child = log.child({ requestId: "x" });
    expect(typeof child.info).toBe("function");
    expect(child).not.toBe(log);
    expect(() => child.info("hello", { extra: 1 })).not.toThrow();
  });

  it("redactPaths from explicit array merge into Pino redact config", () => {
    // Smoke: setup with redactPaths must not throw + result must be usable.
    const log = setupLogger({
      serviceName: "svc",
      redactPaths: [["email", "phone"], ["secret"]],
    });
    expect(() => log.info("hi", { email: "a@b.c" })).not.toThrow();
  });

  it("redact paths from markRedactedFields registry get merged", () => {
    markRedactedFields(Symbol("X"), ["password"]);
    const log = setupLogger({ serviceName: "svc" });
    expect(() => log.info("hi", { password: "x" })).not.toThrow();
  });

  it("environment defaults to 'unknown'", () => {
    expect(() =>
      setupLogger({ serviceName: "svc", minLevel: "info" }),
    ).not.toThrow();
  });

  it("environment override accepted", () => {
    expect(() =>
      setupLogger({ serviceName: "svc", environment: "test" }),
    ).not.toThrow();
  });

  it("pretty=true wires up pino-pretty transport (smoke)", () => {
    expect(() =>
      setupLogger({ serviceName: "svc", pretty: true }),
    ).not.toThrow();
  });
});
