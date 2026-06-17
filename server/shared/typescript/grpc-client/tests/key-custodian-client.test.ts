// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it, vi } from "vitest";
import { HttpKeyCustodianClient } from "../src/key-custodian-client.js";

const VALID_OPTS = {
  tokenEndpoint: "http://localhost:9999/token",
  clientId: "d2.web",
  clientSecret: "secret",
  audience: "d2.edge",
};

function jsonResponse(body: unknown, init: ResponseInit = {}): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    ...init,
    headers: { "Content-Type": "application/json", ...(init.headers ?? {}) },
  });
}

describe("HttpKeyCustodianClient — happy path", () => {
  it("acquires a token from a well-formed response", async () => {
    const fetchImpl = vi.fn(async () =>
      jsonResponse({ access_token: "fake.jwt.sig", expires_in: 900 }),
    );
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(true);
    expect(result.data?.accessToken).toBe("fake.jwt.sig");
    expect(result.data?.audience).toBe("d2.edge");
    expect(result.data?.expiresAtMs).toBeGreaterThan(Date.now());
  });

  it("posts client_credentials grant body", async () => {
    let capturedBody = "";
    const fetchImpl = vi.fn(async (_url, init) => {
      capturedBody = (init as RequestInit | undefined)?.body as string;
      return jsonResponse({ access_token: "x", expires_in: 100 });
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    await client.acquireToken();
    expect(capturedBody).toContain("grant_type=client_credentials");
    expect(capturedBody).toContain("client_id=d2.web");
    expect(capturedBody).toContain("audience=d2.edge");
  });

  it("uses default audience d2.edge when not provided", async () => {
    let capturedBody = "";
    const fetchImpl = vi.fn(async (_url, init) => {
      capturedBody = (init as RequestInit | undefined)?.body as string;
      return jsonResponse({ access_token: "x", expires_in: 100 });
    });
    const optsWithoutAud = { ...VALID_OPTS } as Record<string, unknown>;
    delete optsWithoutAud["audience"];
    const client = new HttpKeyCustodianClient({
      ...(optsWithoutAud as typeof VALID_OPTS),
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    await client.acquireToken();
    expect(capturedBody).toContain("audience=d2.edge");
  });
});

describe("HttpKeyCustodianClient — adversarial responses", () => {
  it("returns failure on HTTP 5xx", async () => {
    const fetchImpl = vi.fn(
      async () => new Response("server down", { status: 503 }),
    );
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure on HTTP 4xx", async () => {
    const fetchImpl = vi.fn(
      async () => new Response("bad creds", { status: 401 }),
    );
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure on non-JSON response", async () => {
    const fetchImpl = vi.fn(
      async () =>
        new Response("not json", {
          status: 200,
          headers: { "Content-Type": "text/plain" },
        }),
    );
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure when access_token is missing", async () => {
    const fetchImpl = vi.fn(async () => jsonResponse({ expires_in: 900 }));
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure when access_token is empty", async () => {
    const fetchImpl = vi.fn(async () =>
      jsonResponse({ access_token: "", expires_in: 900 }),
    );
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure when access_token exceeds 8KB", async () => {
    const big = "a".repeat(10_000);
    const fetchImpl = vi.fn(async () =>
      jsonResponse({ access_token: big, expires_in: 900 }),
    );
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure when expires_in is missing", async () => {
    const fetchImpl = vi.fn(async () => jsonResponse({ access_token: "x" }));
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure when expires_in is negative", async () => {
    const fetchImpl = vi.fn(async () =>
      jsonResponse({ access_token: "x", expires_in: -1 }),
    );
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure when expires_in is non-finite", async () => {
    const fetchImpl = vi.fn(async () =>
      jsonResponse({ access_token: "x", expires_in: "abc" }),
    );
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure when response is JSON null", async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(null));
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure when response is JSON array", async () => {
    const fetchImpl = vi.fn(async () => jsonResponse([1, 2, 3]));
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure on fetch reject (network unreachable)", async () => {
    const fetchImpl = vi.fn(async () => {
      throw new TypeError("fetch failed");
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("returns failure on fetch reject with non-Error throw", async () => {
    const logged: Array<unknown> = [];
    const fakeLogger = {
      trace: () => {},
      debug: () => {},
      info: () => {},
      warn: (_msg: string, b?: unknown) => logged.push(b),
      error: () => {},
      fatal: () => {},
      child: () => fakeLogger,
    };
    const fetchImpl = vi.fn(async () => {
      throw "string thrown";
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
      logger: fakeLogger,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
    const b = logged[0] as Record<string, unknown>;
    expect(b["errorName"]).toBe("unknown");
  });

  it("returns failure on fetch reject with Error throw + logger captures errorName", async () => {
    const logged: Array<unknown> = [];
    const fakeLogger = {
      trace: () => {},
      debug: () => {},
      info: () => {},
      warn: (_msg: string, b?: unknown) => logged.push(b),
      error: () => {},
      fatal: () => {},
      child: () => fakeLogger,
    };
    const fetchImpl = vi.fn(async () => {
      throw new TypeError("network failure");
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
      logger: fakeLogger,
    });
    await client.acquireToken();
    const b = logged[0] as Record<string, unknown>;
    expect(b["errorName"]).toBe("TypeError");
  });
});

describe("HttpKeyCustodianClient — Singleflight dedup", () => {
  it("100 concurrent acquireToken calls trigger ONE upstream fetch", async () => {
    let count = 0;
    const fetchImpl = vi.fn(async () => {
      count++;
      // tiny delay to ensure all 100 callers are queued
      await new Promise((r) => setTimeout(r, 5));
      return jsonResponse({ access_token: "x", expires_in: 900 });
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    const results = await Promise.all(
      Array.from({ length: 100 }, () => client.acquireToken()),
    );
    expect(count).toBe(1);
    expect(results.every((r) => r.success)).toBe(true);
    expect(client.inflightCount).toBe(0);
  });

  it("subsequent (non-overlapping) calls re-fetch", async () => {
    let count = 0;
    const fetchImpl = vi.fn(async () => {
      count++;
      return jsonResponse({ access_token: `x-${count}`, expires_in: 900 });
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
    });
    await client.acquireToken();
    await client.acquireToken();
    expect(count).toBe(2);
  });
});

describe("HttpKeyCustodianClient — defaults", () => {
  it("falls back to global fetch when no fetchImpl provided", () => {
    // Just asserting construction works; we don't actually invoke the network.
    const client = new HttpKeyCustodianClient({
      tokenEndpoint: "http://localhost:1/x",
      clientId: "x",
      clientSecret: "y",
    });
    expect(client).toBeDefined();
  });
});

describe("HttpKeyCustodianClient — input validation", () => {
  it("throws on missing tokenEndpoint", () => {
    expect(
      () =>
        new HttpKeyCustodianClient({
          ...VALID_OPTS,
          tokenEndpoint: "",
        }),
    ).toThrow(TypeError);
  });

  it("throws on missing clientId", () => {
    expect(
      () =>
        new HttpKeyCustodianClient({
          ...VALID_OPTS,
          clientId: "",
        }),
    ).toThrow(TypeError);
  });

  it("throws on missing clientSecret", () => {
    expect(
      () =>
        new HttpKeyCustodianClient({
          ...VALID_OPTS,
          clientSecret: "",
        }),
    ).toThrow(TypeError);
  });
});

describe("HttpKeyCustodianClient — timeout", () => {
  it("aborts after timeoutMs", async () => {
    const fetchImpl = vi.fn(async (_url: string, init?: RequestInit) => {
      return new Promise<Response>((_, reject) => {
        const signal = init?.signal as AbortSignal | undefined;
        signal?.addEventListener("abort", () => reject(new Error("aborted")));
      });
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
      timeoutMs: 50,
    });
    const result = await client.acquireToken();
    expect(result.success).toBe(false);
  });

  it("the resilience TimeoutLayer ABORTS the underlying fetch on timeout", async () => {
    // Proves the migration's whole point: the pipeline's timeout threads its
    // AbortSignal into fetch, so the in-flight request is genuinely canceled
    // (socket released) — not merely abandoned while it keeps running.
    let observedSignal: AbortSignal | undefined;
    const fetchImpl = vi.fn(async (_url: string, init?: RequestInit) => {
      observedSignal = init?.signal as AbortSignal | undefined;
      return new Promise<Response>((_resolve, reject) => {
        observedSignal?.addEventListener("abort", () =>
          reject(Object.assign(new Error("aborted"), { name: "AbortError" })),
        );
      });
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
      timeoutMs: 30,
    });

    const result = await client.acquireToken();

    // fetch received a signal, and the timeout aborted it.
    expect(observedSignal).toBeDefined();
    expect(observedSignal?.aborted).toBe(true);
    // The timeout maps to the same jwksUnavailable failure as the inner paths.
    expect(result.success).toBe(false);
    // The inflight probe returns to 0 (the fetchToken finally ran).
    expect(client.inflightCount).toBe(0);
  });

  it("concurrent acquireToken calls during a timeout still trigger ONE fetch", async () => {
    // Singleflight-in-pipeline dedup survives the timeout migration.
    let count = 0;
    const fetchImpl = vi.fn(async (_url: string, init?: RequestInit) => {
      count++;
      const signal = init?.signal as AbortSignal | undefined;
      return new Promise<Response>((_resolve, reject) => {
        signal?.addEventListener("abort", () =>
          reject(Object.assign(new Error("aborted"), { name: "AbortError" })),
        );
      });
    });
    const client = new HttpKeyCustodianClient({
      ...VALID_OPTS,
      fetchImpl: fetchImpl as unknown as typeof fetch,
      timeoutMs: 25,
    });

    const results = await Promise.all(
      Array.from({ length: 10 }, () => client.acquireToken()),
    );

    expect(count).toBe(1); // deduped despite 10 callers + a timeout
    expect(results.every((r) => !r.success)).toBe(true);
    expect(client.inflightCount).toBe(0);
  });
});
