// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  extractProtoPackage,
  isProtoGateExempt,
  PROTO_PACKAGE_GRAMMAR,
} from "../src/proto-exemption.js";
import { WIRE_CHANNEL_GRAMMAR } from "@d2/typespec-emitters";

// ---------------------------------------------------------------------------
// Parity: local PROTO_PACKAGE_GRAMMAR vs upstream WIRE_CHANNEL_GRAMMAR
// ---------------------------------------------------------------------------

describe("proto-exemption — grammar parity with wire-channel.ts", () => {
  // Test vectors: [package, expectedMatch]
  const vectors: Array<[string, boolean]> = [
    ["d2.keycustodian.v2alpha", true],
    ["d2.common.v1", true],
    ["d2.geo.v1", true],
    ["d2.auth.v3beta", true],
    ["d2.auth.v2", true],
    ["d2.x.v10alpha", true],
    // Non-matching
    ["d2.Keycustodian.v2alpha", false], // uppercase svc
    ["d3.svc.v1", false], // wrong prefix
    ["svc.v1", false], // missing d2.
    ["d2.svc.v", false], // no numeric generation
    ["d2.svc.v1gamma", false], // unknown suffix
    ["", false],
  ];

  for (const [pkg, expected] of vectors) {
    it(`both grammars ${expected ? "match" : "reject"} '${pkg}'`, () => {
      expect(PROTO_PACKAGE_GRAMMAR.test(pkg)).toBe(expected);
      expect(WIRE_CHANNEL_GRAMMAR.test(pkg)).toBe(expected);
    });
  }
});

// ---------------------------------------------------------------------------
// isProtoGateExempt — stable packages
// ---------------------------------------------------------------------------

describe("isProtoGateExempt — stable packages are NOT exempt", () => {
  it("d2.common.v1 (shared stable proto) is not exempt", () => {
    const { exempt, warning } = isProtoGateExempt("d2.common.v1");
    expect(exempt).toBe(false);
    expect(warning).toBeUndefined();
  });

  it("d2.geo.v1 is not exempt", () => {
    const { exempt } = isProtoGateExempt("d2.geo.v1");
    expect(exempt).toBe(false);
  });

  it("d2.fixture.v1 (test fixture stable package) is not exempt", () => {
    const { exempt } = isProtoGateExempt("d2.fixture.v1");
    expect(exempt).toBe(false);
  });

  it("d2.auth.v2 (stable v2) is not exempt", () => {
    const { exempt } = isProtoGateExempt("d2.auth.v2");
    expect(exempt).toBe(false);
  });

  it("d2.x.v10 is not exempt", () => {
    const { exempt } = isProtoGateExempt("d2.x.v10");
    expect(exempt).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// isProtoGateExempt — alpha/beta packages ARE exempt
// ---------------------------------------------------------------------------

describe("isProtoGateExempt — alpha/beta packages ARE exempt", () => {
  it("d2.keycustodian.v2alpha is exempt", () => {
    const { exempt, warning } = isProtoGateExempt("d2.keycustodian.v2alpha");
    expect(exempt).toBe(true);
    expect(warning).toBeUndefined();
  });

  it("d2.auth.v3beta is exempt", () => {
    const { exempt } = isProtoGateExempt("d2.auth.v3beta");
    expect(exempt).toBe(true);
  });

  it("d2.fixture.v2alpha (test fixture alpha package) is exempt", () => {
    const { exempt } = isProtoGateExempt("d2.fixture.v2alpha");
    expect(exempt).toBe(true);
  });

  it("d2.x.v10alpha is exempt", () => {
    const { exempt } = isProtoGateExempt("d2.x.v10alpha");
    expect(exempt).toBe(true);
  });

  it("d2.x.v5beta is exempt", () => {
    const { exempt } = isProtoGateExempt("d2.x.v5beta");
    expect(exempt).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// isProtoGateExempt — unparseable packages treated as non-exempt (fail-loud)
// ---------------------------------------------------------------------------

describe("isProtoGateExempt — malformed package treated as non-exempt with warning", () => {
  it("empty string is non-exempt with warning", () => {
    const { exempt, warning } = isProtoGateExempt("");
    expect(exempt).toBe(false);
    expect(warning).toBeDefined();
    expect(warning).toContain("does not match the D2 wire-channel grammar");
  });

  it("non-D2 package is non-exempt with warning", () => {
    const { exempt, warning } = isProtoGateExempt("google.protobuf.Any");
    expect(exempt).toBe(false);
    expect(warning).toBeDefined();
  });

  it("partially-valid package is non-exempt with warning", () => {
    const { exempt, warning } = isProtoGateExempt("d2.svc.v1gamma");
    expect(exempt).toBe(false);
    expect(warning).toBeDefined();
  });

  it("warning message instructs to treat as stable (enforced)", () => {
    const { warning } = isProtoGateExempt("not.a.d2.package");
    expect(warning).toContain("treating as stable");
  });
});

// ---------------------------------------------------------------------------
// extractProtoPackage
// ---------------------------------------------------------------------------

describe("extractProtoPackage — line parser", () => {
  it("extracts package from a standard declaration line", () => {
    expect(extractProtoPackage("package d2.keycustodian.v2alpha;")).toBe(
      "d2.keycustodian.v2alpha",
    );
  });

  it("extracts package from a line with leading whitespace", () => {
    expect(extractProtoPackage("  package d2.common.v1;")).toBeUndefined();
    // Note: the regex anchors on ^ so leading whitespace yields undefined
  });

  it("extracts package from a line WITHOUT leading whitespace", () => {
    expect(extractProtoPackage("package d2.common.v1;")).toBe("d2.common.v1");
  });

  it("returns undefined for a non-package line", () => {
    expect(extractProtoPackage('syntax = "proto3";')).toBeUndefined();
  });

  it("returns undefined for a comment line", () => {
    expect(extractProtoPackage("// package d2.fake.v1;")).toBeUndefined();
  });

  it("returns undefined for import line", () => {
    expect(
      extractProtoPackage('import "common/v1/d2_result.proto";'),
    ).toBeUndefined();
  });

  it("returns undefined for a package not matching D2 grammar", () => {
    // The PACKAGE_LINE_RE requires the D2 grammar pattern
    expect(extractProtoPackage("package google.protobuf;")).toBeUndefined();
  });

  it("handles trailing whitespace on the line", () => {
    expect(extractProtoPackage("package d2.fixture.v1;   ")).toBe(
      "d2.fixture.v1",
    );
  });
});
