// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { IAuthContextRedactPaths } from "@d2/auth-context-abstractions";
import { IRequestContextRedactPaths } from "@d2/request-context-abstractions";
import { loadFixture } from "../src/index.js";

interface RedactPathsPayload {
  readonly paths: readonly string[];
}

describe("redact-paths parity (.NET [RedactData] ↔ TS RedactPaths arrays)", () => {
  describe("IAuthContext", () => {
    const fixture = loadFixture<RedactPathsPayload>(
      "redact-paths",
      "auth-context",
    );
    const fixturePaths = [...fixture.data.paths].sort();
    const tsPaths = [...IAuthContextRedactPaths].sort();

    it("array membership matches", () => {
      expect(tsPaths).toEqual(fixturePaths);
    });

    // Per-PATH pin: each path asserted individually so any drift names
    // the specific missing / extra path.
    for (const path of fixturePaths) {
      it(`path ${path} present on TS side`, () => {
        expect(tsPaths).toContain(path);
      });
    }

    for (const path of tsPaths) {
      it(`path ${path} present on .NET side`, () => {
        expect(fixturePaths).toContain(path);
      });
    }
  });

  describe("IRequestContext", () => {
    const fixture = loadFixture<RedactPathsPayload>(
      "redact-paths",
      "request-context",
    );
    const fixturePaths = [...fixture.data.paths].sort();
    const tsPaths = [...IRequestContextRedactPaths].sort();

    it("array membership matches", () => {
      expect(tsPaths).toEqual(fixturePaths);
    });

    for (const path of fixturePaths) {
      it(`path ${path} present on TS side`, () => {
        expect(tsPaths).toContain(path);
      });
    }

    for (const path of tsPaths) {
      it(`path ${path} present on .NET side`, () => {
        expect(fixturePaths).toContain(path);
      });
    }
  });
});
