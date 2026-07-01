// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Behavioral + structural coverage for ts-rest-client-emitter.ts.
//
// VALIDATION (faithful apiCall double):
//   The real browser substrate (apiCall / apiCallAnon / executeFetch) lives in
//   the DORMANT cross-workspace server/web BFF (the $lib alias resolves only
//   inside SvelteKit; the real wiring is the host-gated BFF composition root). So the emitted REST
//   client is driven against a FAITHFUL test-double of apiCall / apiCallAnon —
//   same signature (`<TData>(path, ApiCallOptions): Promise<D2Result<TData>>`),
//   returning the REAL @d2/result D2Result shape, recording path / method / body /
//   idempotencyKey. The double exercises the exact delegation contract: correct
//   path, verb, body, idempotency-key threading, and success+failure forwarding.
//
//   The emitted .g.ts is @ts-nocheck (the $lib + DTO imports wire up only in the
//   BFF), so the test reconstructs the const from the emitted TEXT (transpile +
//   `new Function` with the double in scope) — driving the ACTUAL committed bytes
//   (the byte-gate pins them) NON-VACUOUSLY.

import { describe, it, expect } from "vitest";
import ts from "typescript";
import { ok, validationFailed, serviceUnavailable } from "@d2/result";
import type { D2Result } from "@d2/result";
import {
  emitTsRestClient,
  type TsRestClientOp,
} from "../src/lib/ts-rest-client-emitter.js";
import type { FieldInfo } from "../src/lib/model-walk.js";

const SIGN_SRC = "contracts/typespec/fixtures/sign-shaped.tsp";

// ---------------------------------------------------------------------------
// Op fixtures
// ---------------------------------------------------------------------------

function field(name: string, csType: string, tsType: string): FieldInfo {
  return {
    name,
    csName: name[0]!.toUpperCase() + name.slice(1),
    csType,
    tsName: name,
    tsType,
    protoType: "string",
    repeated: false,
    optional: false,
    redactReason: undefined,
  };
}

function signRestOp(over: Partial<TsRestClientOp> = {}): TsRestClientOp {
  return {
    opName: "sign",
    routePath: "/internal/v1/sample/sign",
    verb: "POST",
    authIntent: "scoped",
    sourceSpec: SIGN_SRC,
    requestModelName: "SignInput",
    requestFields: [field("kid", "string", "string")],
    responseModelName: "SignOutput",
    idempotencyKeySource: "header",
    ...over,
  };
}

function signDerivedRestOp(): TsRestClientOp {
  return {
    opName: "signDerived",
    routePath: "/internal/v1/sample/sign-derived",
    verb: "POST",
    authIntent: "scoped",
    sourceSpec: SIGN_SRC,
    requestModelName: "SignInput",
    requestFields: [field("kid", "string", "string")],
    responseModelName: "SignOutput",
    idempotencyKeySource: "derived",
  };
}

// ---------------------------------------------------------------------------
// Faithful apiCall / apiCallAnon double — the real signature, real D2Result.
// ---------------------------------------------------------------------------

interface RecordedCall {
  fn: "apiCall" | "apiCallAnon";
  path: string;
  method?: string;
  body?: unknown;
  idempotencyKey?: string;
  signal?: AbortSignal;
  timeout?: number;
}

interface ApiCallDoubleOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  idempotencyKey?: string;
  signal?: AbortSignal;
  timeout?: number;
}

/**
 * Build a faithful apiCall / apiCallAnon double pair. `next` supplies the
 * D2Result each call returns (defaults to ok()). All calls are recorded so the
 * test can assert the emitted delegation (path / verb / body / idempotencyKey).
 */
function makeApiDouble(next?: () => D2Result<unknown>): {
  apiCall: <T>(
    path: string,
    opts?: ApiCallDoubleOptions,
  ) => Promise<D2Result<T>>;
  apiCallAnon: <T>(
    path: string,
    opts?: ApiCallDoubleOptions,
  ) => Promise<D2Result<T>>;
  calls: RecordedCall[];
} {
  const calls: RecordedCall[] = [];
  const handler =
    (fn: "apiCall" | "apiCallAnon") =>
    <T>(path: string, opts?: ApiCallDoubleOptions): Promise<D2Result<T>> => {
      calls.push({
        fn,
        path,
        method: opts?.method,
        body: opts?.body,
        idempotencyKey: opts?.idempotencyKey,
        signal: opts?.signal,
        timeout: opts?.timeout,
      });
      return Promise.resolve((next?.() ?? ok()) as D2Result<T>);
    };
  return {
    apiCall: handler("apiCall"),
    apiCallAnon: handler("apiCallAnon"),
    calls,
  };
}

// ---------------------------------------------------------------------------
// Reconstruct the emitted REST const from the emitted TEXT.
// ---------------------------------------------------------------------------

function reconstructConst<T>(
  source: string,
  constName: string,
  scope: Record<string, unknown>,
): T {
  const noImports = source
    .split("\n")
    .filter((l) => !/^\s*import\s/.test(l))
    .join("\n");

  const js = ts.transpileModule(noImports, {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022,
      verbatimModuleSyntax: false,
    },
  }).outputText;

  const body = js.replace(/^export /gm, "");
  const keys = Object.keys(scope);
  const vals = keys.map((k) => scope[k]);
  const make = new Function(...keys, `${body}\nreturn ${constName};`) as (
    ...args: unknown[]
  ) => T;
  return make(...vals);
}

// ===========================================================================
// emitTsRestClient — pure-function structural coverage
// ===========================================================================

describe("emitTsRestClient_Guards", () => {
  it("throws on empty moduleName", () => {
    expect(() => emitTsRestClient("", [signRestOp()])).toThrow(
      "moduleName must not be empty",
    );
  });

  it("returns an empty array for an empty ops list", () => {
    expect(emitTsRestClient("KeyCustodian", [])).toHaveLength(0);
  });
});

describe("emitTsRestClient_FileNameAndShape", () => {
  it("emits one <module>-rest-client.g.ts with the interface + const", () => {
    const [file] = emitTsRestClient("KeyCustodian", [signRestOp()]);
    expect(file!.fileName).toBe("key-custodian-rest-client.g.ts");
    expect(file!.content).toContain(
      "export interface KeyCustodianRestClient {",
    );
    expect(file!.content).toContain(
      "export const keyCustodianRestClient: KeyCustodianRestClient =",
    );
    expect(file!.content).toContain("// @ts-nocheck");
    expect(file!.content).toContain("/* eslint-disable */");
    expect(file!.content.endsWith("\n")).toBe(true);
    expect(file!.content.endsWith("\n\n")).toBe(false);
  });

  it("a scoped op delegates to apiCall; the substrate is imported from the real $lib path", () => {
    const [file] = emitTsRestClient("KeyCustodian", [signRestOp()]);
    expect(file!.content).toContain(
      'import { apiCall } from "$lib/client/rest/gateway-client.js";',
    );
    expect(file!.content).toContain(
      'return apiCall<SignOutput>("/internal/v1/sample/sign"',
    );
    // No apiCallAnon IMPORT (the substrate doc-comment may still mention it).
    expect(file!.content).not.toContain("import { apiCallAnon }");
    expect(file!.content).not.toContain("apiCallAnon<");
  });

  it("a harmless op delegates to apiCallAnon", () => {
    const harmless = signRestOp({
      opName: "ping",
      routePath: "/v1/ping",
      authIntent: "harmless",
      idempotencyKeySource: "none",
    });
    const [file] = emitTsRestClient("KeyCustodian", [harmless]);
    expect(file!.content).toContain(
      'import { apiCallAnon } from "$lib/client/rest/gateway-client.js";',
    );
    expect(file!.content).toContain(
      'return apiCallAnon<SignOutput>("/v1/ping"',
    );
  });

  it("a header-idempotent op threads idempotencyKey; a derived op does NOT", () => {
    const [file] = emitTsRestClient("KeyCustodian", [
      signRestOp(),
      signDerivedRestOp(),
    ]);
    // The header op carries the idempotency-extended options type + threads the key.
    expect(file!.content).toContain("RestCallOptionsWithIdempotency");
    expect(file!.content).toContain("idempotencyKey: opts?.idempotencyKey,");
    // The derived op's method does NOT thread an idempotency key.
    const signDerivedBlock = file!.content.slice(
      file!.content.indexOf("signDerived(input, opts)"),
    );
    expect(signDerivedBlock.slice(0, 200)).not.toContain("idempotencyKey:");
  });

  it("a body verb (POST) sends the input as body; a GET binds a query string", () => {
    const post = signRestOp();
    const get = signRestOp({
      opName: "getStatus",
      routePath: "/v1/status",
      verb: "GET",
      idempotencyKeySource: "none",
    });
    const [file] = emitTsRestClient("KeyCustodian", [post, get]);
    expect(file!.content).toContain("body: input,");
    expect(file!.content).toContain('withQuery("/v1/status", input)');
    expect(file!.content).toContain("function withQuery(");
  });

  it("a shared model across ops imports the DTO type ONCE (deduped)", () => {
    const [file] = emitTsRestClient("KeyCustodian", [
      signRestOp(),
      signDerivedRestOp(),
    ]);
    const importLines = file!.content
      .split("\n")
      .filter((l) => l.includes("SignInput") && l.startsWith("import"));
    expect(importLines).toHaveLength(1);
    expect(importLines[0]).toContain('from "./sign-dto.js"');
  });

  it("withQuery guard uses only undefined check — no null check (DTO fields are ?: T, never T|null)", () => {
    const get = signRestOp({
      opName: "getStatus",
      routePath: "/v1/status",
      verb: "GET",
      idempotencyKeySource: "none",
    });
    const [file] = emitTsRestClient("KeyCustodian", [get]);
    // The guard must filter on undefined only; a null-field check would incorrectly
    // omit fields that happen to hold `null` (which should not arise for ?: T DTOs).
    expect(file!.content).toContain("if (value !== undefined)");
    expect(file!.content).not.toContain("value !== null");
  });

  it("the emitted source carries no phase / deliverable / audit-round identifiers", () => {
    const [file] = emitTsRestClient("KeyCustodian", [signRestOp()]);
    expect(file!.content).not.toMatch(/\bC-6\b|\bStep 9c\b|audit-round/i);
  });
});

// ===========================================================================
// BEHAVIORAL — the emitted REST const over the faithful apiCall double
// ===========================================================================

describe("tsRestClient_Behavioral_FaithfulDouble", () => {
  type RestClient = Record<
    string,
    (input: unknown, opts?: unknown) => Promise<D2Result<unknown>>
  >;

  function buildClient(
    ops: TsRestClientOp[],
    next?: () => D2Result<unknown>,
  ): { client: RestClient; calls: RecordedCall[] } {
    const double = makeApiDouble(next);
    const [file] = emitTsRestClient("KeyCustodian", ops);
    const client = reconstructConst<RestClient>(
      file!.content,
      "keyCustodianRestClient",
      { apiCall: double.apiCall, apiCallAnon: double.apiCallAnon },
    );
    return { client, calls: double.calls };
  }

  it("success — forwards the path, POST verb, and the input as body", async () => {
    const { client, calls } = buildClient([signRestOp()], () =>
      ok({ signature: "sig" }),
    );
    const result = await client.sign!({ kid: "k1" });

    expect(result.success).toBe(true);
    expect(result.data).toEqual({ signature: "sig" });
    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({
      fn: "apiCall",
      path: "/internal/v1/sample/sign",
      method: "POST",
      body: { kid: "k1" },
    });
  });

  it("a header-idempotent op threads opts.idempotencyKey to the substrate", async () => {
    const { client, calls } = buildClient([signRestOp()]);
    await client.sign!({ kid: "k1" }, { idempotencyKey: "idem-123" });
    expect(calls[0]!.idempotencyKey).toBe("idem-123");
  });

  it("a derived-keySource op does NOT thread a client idempotency key", async () => {
    const { client, calls } = buildClient([signDerivedRestOp()]);
    await client.signDerived!({ kid: "k1" });
    expect(calls[0]!.idempotencyKey).toBeUndefined();
  });

  it("failure forwarding — a substrate ValidationFailed is returned verbatim (no swallow into ok)", async () => {
    const { client } = buildClient([signRestOp()], () =>
      validationFailed({ messages: [] }),
    );
    const result = await client.sign!({ kid: "" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });

  it("failure forwarding — a substrate ServiceUnavailable is returned verbatim", async () => {
    const { client } = buildClient([signRestOp()], () => serviceUnavailable());
    const result = await client.sign!({ kid: "k" });
    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(503);
  });

  it("a harmless op delegates to apiCallAnon (not apiCall)", async () => {
    const { client, calls } = buildClient([
      signRestOp({
        opName: "ping",
        routePath: "/v1/ping",
        authIntent: "harmless",
        idempotencyKeySource: "none",
      }),
    ]);
    await client.ping!({ kid: "k" });
    expect(calls[0]!.fn).toBe("apiCallAnon");
  });

  it("a GET op binds the input fields as a query string and sends no body", async () => {
    const { client, calls } = buildClient([
      signRestOp({
        opName: "getStatus",
        routePath: "/v1/status",
        verb: "GET",
        idempotencyKeySource: "none",
      }),
    ]);
    await client.getStatus!({ kid: "abc", empty: undefined });
    expect(calls[0]!.method).toBe("GET");
    expect(calls[0]!.body).toBeUndefined();
    expect(calls[0]!.path).toBe("/v1/status?kid=abc");
  });

  it("adversarial — empty / whitespace input is forwarded as-is (the client is a thin forwarder)", async () => {
    const { client, calls } = buildClient([signRestOp()]);
    await client.sign!({ kid: "   " });
    expect(calls[0]!.body).toEqual({ kid: "   " });
  });

  it("the abort signal + timeout are threaded to the substrate", async () => {
    const { client, calls } = buildClient([signRestOp()]);
    const ac = new AbortController();
    await client.sign!({ kid: "k" }, { signal: ac.signal, timeout: 1234 });
    expect(calls[0]!.signal).toBe(ac.signal);
    expect(calls[0]!.timeout).toBe(1234);
  });

  it("tolerant reader — extra fields in the substrate result pass through intact", async () => {
    // The faithful apiCall double returns a real D2Result whose data payload carries
    // an extra field that the SignOutput DTO does not declare.  The emitted REST client
    // is a thin delegator: it calls apiCall<SignOutput>(...) and returns the result
    // verbatim — it does NOT re-parse or reshape the payload.  The extra field
    // therefore survives in the returned D2Result.data without corrupting the known
    // fields.  This pins the tolerance property: the generated client never strips or
    // rejects unknown fields that arrive from a newer server.
    const dataWithExtra = Object.assign(
      ok({ signature: "sig-tr5" }).data as object,
      {
        futureField: "extra-value",
      },
    );
    const { client } = buildClient([signRestOp()], () =>
      ok(dataWithExtra as unknown as { signature: string }),
    );
    const result = await client.sign!({ kid: "k-tr5" });

    expect(result.success).toBe(true);
    // The known field survived.
    expect((result.data as { signature: string }).signature).toBe("sig-tr5");
    // The extra field was forwarded verbatim (the client did not strip it).
    expect((result.data as Record<string, unknown>)["futureField"]).toBe(
      "extra-value",
    );
  });
});
