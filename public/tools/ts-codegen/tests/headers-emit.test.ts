// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  catalogClassName,
  catalogsForEntry,
  emitHeadersCatalog,
  entriesForCatalog,
  type HeadersSpec,
  validateHeadersSpec,
} from "../src/headers-emit.js";

const validSpec: HeadersSpec = {
  headers: [
    {
      name: "Idempotency-Key",
      constName: "IDEMPOTENCY_KEY",
      applicability: ["http"],
      convention: "stripe",
      description: "Idempotency.",
    },
    {
      name: "x-d2-context",
      constName: "PROPAGATED_CONTEXT",
      applicability: ["http", "grpc", "amqp"],
      convention: "d2",
      description: "Propagated context.",
    },
    {
      name: "x-proto-type",
      constName: "PROTO_TYPE",
      applicability: ["amqp"],
      convention: "amqp-x",
      description: "Proto type name.",
    },
  ],
};

describe("validateHeadersSpec", () => {
  it("happy path returns all entries with no error diagnostics", () => {
    const v = validateHeadersSpec(validSpec);
    expect(v.entries).toHaveLength(3);
    expect(v.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
  });

  it("flags invalid constName (not UPPER_SNAKE_CASE)", () => {
    const v = validateHeadersSpec({
      headers: [{ ...validSpec.headers[0]!, constName: "lowercase" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2HDR003");
  });

  it("flags empty applicability", () => {
    const v = validateHeadersSpec({
      headers: [{ ...validSpec.headers[0]!, applicability: [] as never }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2HDR005");
  });

  it("flags unknown transport", () => {
    const v = validateHeadersSpec({
      headers: [
        { ...validSpec.headers[0]!, applicability: ["bogus"] as never },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2HDR002");
  });

  it("flags unknown convention as warning (not error)", () => {
    const v = validateHeadersSpec({
      headers: [{ ...validSpec.headers[0]!, convention: "weird" }],
    });
    const warnings = v.diagnostics.filter((d) => d.severity === "warning");
    expect(warnings).toHaveLength(1);
    expect(warnings[0]?.id).toBe("D2HDR006");
    // Entry still valid (warning doesn't block).
    expect(v.entries.some((e) => e.constName === "IDEMPOTENCY_KEY")).toBe(true);
  });

  it("flags duplicate constName within a catalog", () => {
    const v = validateHeadersSpec({
      headers: [
        validSpec.headers[0]!,
        // Same constName, also http-applicable → catalog collision
        { ...validSpec.headers[0]!, name: "Idempotency-Key-2" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2HDR004")).toBe(true);
  });
});

describe("catalogsForEntry / entriesForCatalog", () => {
  it("cross-transport entries appear in Common + each per-transport", () => {
    const xt = validSpec.headers[1]!; // PROPAGATED_CONTEXT (3 transports)
    const cats = catalogsForEntry(xt);
    expect(cats).toContain("common");
    expect(cats).toContain("http");
    expect(cats).toContain("grpc");
    expect(cats).toContain("amqp");
  });

  it("single-transport entries appear ONLY in their per-transport catalog", () => {
    const idem = validSpec.headers[0]!; // IDEMPOTENCY_KEY (http-only)
    const cats = catalogsForEntry(idem);
    expect(cats).toEqual(["http"]);
  });

  it("entriesForCatalog filters by transport correctly", () => {
    const httpOnly = entriesForCatalog(validSpec, "http");
    expect(httpOnly.map((e) => e.constName)).toEqual([
      "IDEMPOTENCY_KEY",
      "PROPAGATED_CONTEXT",
    ]);

    const common = entriesForCatalog(validSpec, "common");
    expect(common.map((e) => e.constName)).toEqual(["PROPAGATED_CONTEXT"]);

    const grpc = entriesForCatalog(validSpec, "grpc");
    expect(grpc.map((e) => e.constName)).toEqual(["PROPAGATED_CONTEXT"]);
  });
});

describe("catalogClassName", () => {
  it("maps each catalog to its TS export class name", () => {
    expect(catalogClassName("common")).toBe("CommonHeaders");
    expect(catalogClassName("http")).toBe("HttpHeaders");
    expect(catalogClassName("amqp")).toBe("AmqpHeaders");
    expect(catalogClassName("grpc")).toBe("GrpcHeaders");
  });
});

describe("emitHeadersCatalog — snapshot pin", () => {
  it("emits HttpHeaders catalog with sorted constants", () => {
    const r = emitHeadersCatalog(validSpec, "http");
    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain('IDEMPOTENCY_KEY: "Idempotency-Key"');
    expect(r.source).toContain('PROPAGATED_CONTEXT: "x-d2-context"');
    expect(r.source).toContain("export const HttpHeaders =");
    expect(r.source).toContain("export type HttpHeaderName");
    expect(r.source).toContain("ALL_HTTP_HEADERS");
  });

  it("emits CommonHeaders catalog with cross-transport subset only", () => {
    const r = emitHeadersCatalog(validSpec, "common");
    expect(r.source).toContain('PROPAGATED_CONTEXT: "x-d2-context"');
    expect(r.source).not.toContain("IDEMPOTENCY_KEY");
    expect(r.source).not.toContain("PROTO_TYPE");
  });

  it("emits AmqpHeaders catalog with amqp-applicable subset", () => {
    const r = emitHeadersCatalog(validSpec, "amqp");
    expect(r.source).toContain('PROPAGATED_CONTEXT: "x-d2-context"');
    expect(r.source).toContain('PROTO_TYPE: "x-proto-type"');
    expect(r.source).not.toContain("IDEMPOTENCY_KEY");
  });

  it("emits GrpcHeaders catalog with grpc-applicable subset", () => {
    const r = emitHeadersCatalog(validSpec, "grpc");
    expect(r.source).toContain('PROPAGATED_CONTEXT: "x-d2-context"');
    expect(r.source).not.toContain("IDEMPOTENCY_KEY");
    expect(r.source).not.toContain("PROTO_TYPE");
  });

  it("returns empty source on validation error", () => {
    const r = emitHeadersCatalog(
      {
        headers: [{ ...validSpec.headers[0]!, constName: "bad-name" }],
      },
      "http",
    );
    expect(r.source).toBe("");
    expect(r.diagnostics.some((d) => d.id === "D2HDR003")).toBe(true);
  });
});
