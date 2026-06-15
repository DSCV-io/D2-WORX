// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
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
  _resetSpecRegistryCache,
} from "../src/spec-registry.js";
import * as fsModule from "fs";

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
