// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Regression tests for the spec-registry shape guard.
//
// loadScopeNames() and loadAudienceNames() previously cast JSON.parse output
// directly via `as ScopesSpec` / `as AudiencesSpec` — a spec-shape change
// (e.g. key rename from `scopes` to `scope`) produced an opaque TypeError
// from `.map()`. The shape guard now throws a descriptive Error naming the
// file and expected shape.
//
// These tests live in a separate file so that `vi.mock("fs")` is scoped to
// this module only and does not affect the anchor-guard tests in decorators.test.ts
// which read the real spec files.

import { describe, it, expect, vi, afterEach } from "vitest";

// Wraps the 'fs' module so vitest can intercept readFileSync via vi.spyOn.
// Without this, native ESM module namespaces are non-configurable and spyOn fails.
vi.mock("fs");

import {
  loadScopeNames,
  loadAudienceNames,
  loadErrorCodeNames,
  loadErrorCategoryNames,
  _resetSpecRegistryCache,
} from "../src/spec-registry.js";
import * as fsModule from "fs";
import type { Dirent } from "fs";

describe("specRegistry_ShapeGuard_Scopes", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    _resetSpecRegistryCache();
  });

  it("loadScopeNames() throws descriptive Error when scopes.spec.json has wrong shape", () => {
    vi.spyOn(fsModule, "readFileSync").mockReturnValueOnce(
      JSON.stringify({ scope: [{ name: "self.read" }] }), // wrong key: 'scope' not 'scopes'
    );
    expect(() => loadScopeNames()).toThrow(
      "contracts/auth-scopes/scopes.spec.json has unexpected shape",
    );
  });

  it("loadScopeNames() throws descriptive Error when scopes.spec.json is a plain array", () => {
    vi.spyOn(fsModule, "readFileSync").mockReturnValueOnce(
      JSON.stringify([{ name: "self.read" }]), // top-level array instead of object
    );
    expect(() => loadScopeNames()).toThrow(
      "contracts/auth-scopes/scopes.spec.json has unexpected shape",
    );
  });
});

describe("specRegistry_ShapeGuard_Audiences", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    _resetSpecRegistryCache();
  });

  it("loadAudienceNames() throws descriptive Error when audiences.spec.json has wrong shape", () => {
    vi.spyOn(fsModule, "readFileSync").mockReturnValueOnce(
      JSON.stringify({ audience: [{ name: "Files" }] }), // wrong key: 'audience' not 'audiences'
    );
    expect(() => loadAudienceNames()).toThrow(
      "contracts/auth-audiences/audiences.spec.json has unexpected shape",
    );
  });
});

// Build a minimal Dirent-like for the readdirSync mock.
function dir(name: string): Dirent {
  return { name, isDirectory: () => true } as unknown as Dirent;
}

describe("specRegistry_ShapeGuard_ErrorCodes", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    _resetSpecRegistryCache();
  });

  it("loadErrorCodeNames() throws descriptive Error when a spec has the wrong key", () => {
    // First readdir: contracts/ → one *-error-codes dir. Second readdir: the
    // spec files in that dir. Then readFileSync returns a wrong-shaped spec.
    // readdirSync is overloaded; cast the mocked returns to `never` so the
    // overload resolution does not constrain the test-only stub shapes.
    vi.spyOn(fsModule, "readdirSync")
      .mockReturnValueOnce([dir("error-codes")] as never)
      .mockReturnValueOnce(["error-codes.spec.json"] as never);
    vi.spyOn(fsModule, "readFileSync").mockReturnValueOnce(
      JSON.stringify({ errorCode: [{ code: "X" }] }), // wrong key: 'errorCode' not 'errorCodes'
    );
    expect(() => loadErrorCodeNames()).toThrow(
      "contracts/error-codes/error-codes.spec.json has unexpected shape",
    );
  });
});

describe("specRegistry_ShapeGuard_ErrorCategory", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    _resetSpecRegistryCache();
  });

  it("loadErrorCategoryNames() throws descriptive Error when categories key is wrong", () => {
    vi.spyOn(fsModule, "readFileSync").mockReturnValueOnce(
      JSON.stringify({ category: [{ wire: "not_found" }] }), // wrong key: 'category' not 'categories'
    );
    expect(() => loadErrorCategoryNames()).toThrow(
      "contracts/error-category/error-category.spec.json has unexpected shape",
    );
  });

  it("loadErrorCategoryNames() throws when the spec is a plain array", () => {
    vi.spyOn(fsModule, "readFileSync").mockReturnValueOnce(
      JSON.stringify([{ wire: "not_found" }]),
    );
    expect(() => loadErrorCategoryNames()).toThrow(
      "contracts/error-category/error-category.spec.json has unexpected shape",
    );
  });
});
