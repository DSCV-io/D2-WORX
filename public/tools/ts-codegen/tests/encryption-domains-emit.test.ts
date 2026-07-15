// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  emitEncryptionDomains,
  type EncryptionDomainsSpec,
  validateEncryptionDomainsSpec,
} from "../src/encryption-domains-emit.js";

const validSpec: EncryptionDomainsSpec = {
  domains: [
    {
      constName: "AUDIT",
      value: "audit",
      mode: "sealed",
      consumerService: "audit",
      doc: "Audit events.",
    },
    {
      constName: "METRICS",
      value: "metrics",
      mode: "symmetric",
      doc: "Symmetric domain.",
    },
    {
      constName: "PLAINTEXT",
      value: "plaintext",
      doc: "Plaintext sentinel.",
    },
  ],
};

describe("validateEncryptionDomainsSpec — happy path", () => {
  it("returns all entries with no diagnostics", () => {
    const v = validateEncryptionDomainsSpec(validSpec);
    expect(v.diagnostics).toEqual([]);
    expect(v.domains).toHaveLength(3);
  });
});

describe("validateEncryptionDomainsSpec — existing fail paths", () => {
  it("flags invalid constName (D2ED004)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [{ constName: "lowerCase", value: "x", doc: "d" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2ED004");
  });

  it("flags empty wire value (D2ED005)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [{ constName: "X", value: "  ", doc: "d" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2ED005");
  });

  it("flags duplicate constName (D2ED002)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [
        { constName: "X", value: "a", doc: "d" },
        { constName: "X", value: "b", doc: "d" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2ED002")).toBe(true);
  });

  it("flags duplicate wire value (D2ED003)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [
        { constName: "X", value: "a", doc: "d" },
        { constName: "Y", value: "a", doc: "d" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2ED003")).toBe(true);
  });
});

describe("validateEncryptionDomainsSpec — mode / consumerService fail paths", () => {
  it("flags an invalid mode value (D2ED006)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [{ constName: "X", value: "x", mode: "asymmetric", doc: "d" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2ED006");
  });

  it("flags sealed without consumerService (D2ED007)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [{ constName: "X", value: "x", mode: "sealed", doc: "d" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2ED007");
  });

  it("flags consumerService without mode (D2ED008)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [
        { constName: "X", value: "x", consumerService: "svc", doc: "d" },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2ED008");
  });

  it("flags consumerService on a symmetric domain (D2ED008)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [
        {
          constName: "X",
          value: "x",
          mode: "symmetric",
          consumerService: "svc",
          doc: "d",
        },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2ED008");
  });

  it("flags a malformed consumerService grammar (D2ED009)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [
        {
          constName: "X",
          value: "x",
          mode: "sealed",
          consumerService: "Bad_Service",
          doc: "d",
        },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2ED009");
  });

  it("flags an over-length consumerService (D2ED009)", () => {
    const v = validateEncryptionDomainsSpec({
      domains: [
        {
          constName: "X",
          value: "x",
          mode: "sealed",
          consumerService: "a".repeat(65),
          doc: "d",
        },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe("D2ED009");
  });
});

describe("emitEncryptionDomains — snapshot pins", () => {
  it("emits the mode + consumer twins with literal values", () => {
    const r = emitEncryptionDomains(validSpec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export const EncryptionDomainModes = {");
    expect(r.source).toContain('"audit": "sealed",');
    expect(r.source).toContain('"metrics": "symmetric",');
    expect(r.source).toContain('"plaintext": "symmetric",');
    expect(r.source).toContain(
      'export type EncryptionDomainMode = "symmetric" | "sealed";',
    );
    expect(r.source).toContain("export const ConsumerServiceByDomain = {");
    expect(r.source).toContain('"audit": "audit",');
    // Symmetric / plaintext domains never appear in the consumer map.
    expect(r.source).not.toContain('"metrics": "metrics"');
    expect(r.source).not.toContain('"plaintext": "plaintext"');
  });

  it("returns empty source on a validation error", () => {
    const r = emitEncryptionDomains({
      domains: [{ constName: "X", value: "x", mode: "sealed", doc: "d" }],
    });
    expect(r.source).toBe("");
    expect(r.diagnostics.some((d) => d.id === "D2ED007")).toBe(true);
  });
});
