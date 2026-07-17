// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it } from "vitest";
import { ChannelCredentials } from "@grpc/grpc-js";
import { closeChannel, getChannel } from "../src/channel.js";

afterEach(async () => {
  // Reset module-level singleton between tests.
  await closeChannel();
  delete process.env["D2_EDGE_GRPC_ENDPOINT"];
});

describe("getChannel — singleton semantics", () => {
  it("returns the same channel instance across calls", async () => {
    const opts = {
      endpoint: "localhost:9443",
      credentials: ChannelCredentials.createInsecure(),
    };
    const c1 = await getChannel(opts);
    const c2 = await getChannel(opts);
    expect(c1).toBe(c2);
  });

  it("dedupes concurrent first calls (no race)", async () => {
    const opts = {
      endpoint: "localhost:9443",
      credentials: ChannelCredentials.createInsecure(),
    };
    // Synchronously fire 3 calls before any awaits — second + third hit the
    // _channelInit !== null branch.
    const p1 = getChannel(opts);
    const p2 = getChannel(opts);
    const p3 = getChannel(opts);
    const [c1, c2, c3] = await Promise.all([p1, p2, p3]);
    expect(c1).toBe(c2);
    expect(c2).toBe(c3);
  });

  it("uses the endpoint passed in opts", async () => {
    const opts = {
      endpoint: "test-endpoint:1234",
      credentials: ChannelCredentials.createInsecure(),
    };
    const c = await getChannel(opts);
    expect(c.getTarget()).toContain("test-endpoint:1234");
  });

  it("falls back to D2_EDGE_GRPC_ENDPOINT env var when opts.endpoint absent", async () => {
    process.env["D2_EDGE_GRPC_ENDPOINT"] = "env-endpoint:5555";
    const c = await getChannel({
      credentials: ChannelCredentials.createInsecure(),
    });
    expect(c.getTarget()).toContain("env-endpoint:5555");
  });

  it("uses insecure credentials when opts.insecure = true", async () => {
    process.env["D2_EDGE_GRPC_ENDPOINT"] = "x:1";
    const c = await getChannel({ insecure: true });
    expect(c).toBeDefined();
  });

  it("rejects when no endpoint resolvable", async () => {
    await expect(
      getChannel({ credentials: ChannelCredentials.createInsecure() }),
    ).rejects.toThrow("D2_EDGE_GRPC_ENDPOINT");
  });

  it("permits reinit after close + endpoint change", async () => {
    const opts1 = {
      endpoint: "first:1",
      credentials: ChannelCredentials.createInsecure(),
    };
    const c1 = await getChannel(opts1);
    expect(c1.getTarget()).toContain("first:1");
    await closeChannel();
    const opts2 = {
      endpoint: "second:2",
      credentials: ChannelCredentials.createInsecure(),
    };
    const c2 = await getChannel(opts2);
    expect(c2.getTarget()).toContain("second:2");
    expect(c1).not.toBe(c2);
  });

  it("defaults to TLS credentials when neither opts nor insecure provided", async () => {
    process.env["D2_EDGE_GRPC_ENDPOINT"] = "tls-host:443";
    const c = await getChannel();
    expect(c).toBeDefined();
  });
});

describe("closeChannel — idempotent shutdown", () => {
  it("safe to call before any getChannel()", async () => {
    await expect(closeChannel()).resolves.toBeUndefined();
  });

  it("safe to call twice", async () => {
    const opts = {
      endpoint: "localhost:9443",
      credentials: ChannelCredentials.createInsecure(),
    };
    await getChannel(opts);
    await closeChannel();
    await expect(closeChannel()).resolves.toBeUndefined();
  });

  it("clears the cached channel so next getChannel re-initializes", async () => {
    const opts = {
      endpoint: "localhost:9443",
      credentials: ChannelCredentials.createInsecure(),
    };
    const c1 = await getChannel(opts);
    await closeChannel();
    const c2 = await getChannel(opts);
    expect(c1).not.toBe(c2);
  });
});
