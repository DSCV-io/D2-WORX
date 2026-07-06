// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  createConnection,
  redactAmqpUri,
} from "../src/connection/connection-options.js";

describe("redactAmqpUri — never leaks the broker password", () => {
  it("strips embedded userinfo credentials", () => {
    const redacted = redactAmqpUri(
      "amqps://audit-svc:s3cr3t@rabbitmq.internal:5671/d2",
    );
    expect(redacted).not.toContain("s3cr3t");
    expect(redacted).not.toContain("audit-svc");
    expect(redacted).toContain("rabbitmq.internal");
  });

  it("collapses a malformed URI to its scheme only", () => {
    expect(redactAmqpUri("amqp://:::not a uri")).toBe("amqp://[redacted]");
  });

  it("collapses a scheme-less value to [redacted]", () => {
    expect(redactAmqpUri("garbage")).toBe("[redacted]");
  });
});

describe("createConnection", () => {
  it("builds an auto-reconnecting Connection from the options", () => {
    const connection = createConnection({
      connectionUri: "amqp://guest:guest@127.0.0.1:5672/",
      clientProvidedName: "test-svc",
      retryLowMs: 100,
      retryHighMs: 1000,
      heartbeatSeconds: 30,
    });
    try {
      expect(typeof connection.createConsumer).toBe("function");
      expect(typeof connection.createPublisher).toBe("function");
    } finally {
      // Immediately tear down the background connect loop so the test exits.
      connection.unsafeDestroy();
    }
  });
});
