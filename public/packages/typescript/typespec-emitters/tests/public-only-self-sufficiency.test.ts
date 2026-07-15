// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// §8.8 dual-suite public-only self-sufficiency pin for typespec-emitters.
//
// Product-home byte-parity tests may hard-read monorepo-private paths when the
// private tree is present. Under PUBLIC_ONLY=1 those suites must skip so a
// public clone / public CI lane does not require private/**.

import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  isPublicOnlyMode,
  shouldRunPrivateProductParity,
} from "./private-tree.js";

const testsDir = dirname(fileURLToPath(import.meta.url));

/** Test files that hard-read private product homes (must use the private-tree gate). */
const PRODUCT_HOME_SUITE_MARKERS = [
  "byte-parity.test.ts",
  "facade-emitter.test.ts",
  "openapi-byte-parity.test.ts",
  "predicate-parity.test.ts",
  "predicate-byte-parity.test.ts",
  "proto-grpc-byte-parity.test.ts",
  "ts-client-byte-parity.test.ts",
  "ts-grpc-client-emitter.test.ts",
  "nested-model-grpc-byte-parity.test.ts",
  "sse-dispatch-emitter.test.ts",
  "audit-production-homes-byte-parity.integration.test.ts",
  "keycustodian-wellknown-emit.integration.test.ts",
] as const;

describe("public_only_self_sufficiency_typespec_emitters", () => {
  it("product-home suites import shouldRunPrivateProductParity (static gate pin)", () => {
    for (const name of PRODUCT_HOME_SUITE_MARKERS) {
      const body = readFileSync(join(testsDir, name), "utf8");

      expect(
        body.includes("shouldRunPrivateProductParity"),
        `${name} must gate private product-home reads via shouldRunPrivateProductParity`,
      ).toBe(true);
    }
  });

  it("fixture-only / unit suites remain present under tests/", () => {
    const names = new Set(readdirSync(testsDir));

    // In-package / public-fixture suites that must not require private/**
    expect(names.has("smoke-emit.test.ts")).toBe(true);
    expect(names.has("csharp-dto-emitter.test.ts")).toBe(true);
    expect(names.has("lib.test.ts")).toBe(true);
    expect(names.has("public-only-self-sufficiency.test.ts")).toBe(true);
    expect(names.has("private-tree.ts")).toBe(true);
  });

  it("PUBLIC_ONLY=1 forces shouldRunPrivateProductParity false", () => {
    const prev = process.env.PUBLIC_ONLY;

    try {
      process.env.PUBLIC_ONLY = "1";
      expect(isPublicOnlyMode()).toBe(true);
      expect(shouldRunPrivateProductParity(import.meta.url)).toBe(false);
    } finally {
      if (prev === undefined) delete process.env.PUBLIC_ONLY;
      else process.env.PUBLIC_ONLY = prev;
    }
  });
});
