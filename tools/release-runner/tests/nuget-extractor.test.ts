// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the NuGet extraction helpers (nuget-extractor.ts) — now pure,
// build-free:
//   - parseShippedTxt: header/blank exclusion, trailing-whitespace tolerance.
//   - diffShippedLines: added / removed / rename / no-change line-set diff.
//   - the PublicAPI.* + .release-fingerprint path helpers.
//
// The provider-level git-ref apiDiff + source-based fingerprint composition are
// covered in real-diff-provider.test.ts.

import { join } from "node:path";
import { describe, expect, it } from "vitest";
import {
  diffShippedLines,
  fingerprintBaselinePath,
  parseShippedTxt,
  shippedTxtPath,
  unshippedTxtPath,
} from "../src/nuget-extractor.js";
import type { ApiDiff } from "../src/diff-bump.js";

// ---------------------------------------------------------------------------
// parseShippedTxt
// ---------------------------------------------------------------------------

describe("parseShippedTxt", () => {
  it("excludes the #nullable enable header + blank lines", () => {
    const set = parseShippedTxt("#nullable enable\n\nD2.Foo\nD2.Bar\n");

    expect([...set].sort()).toEqual(["D2.Bar", "D2.Foo"]);
  });

  it("empty / header-only content → empty set", () => {
    expect(parseShippedTxt("#nullable enable\n").size).toBe(0);
    expect(parseShippedTxt("").size).toBe(0);
  });

  it("tolerates trailing whitespace (CRLF stripped via trimEnd)", () => {
    const lf = parseShippedTxt("#nullable enable\nD2.Foo\n");
    const crlf = parseShippedTxt("#nullable enable\r\nD2.Foo\r\n");

    expect([...crlf]).toEqual([...lf]);
  });

  it("deduplicates identical lines (set semantics)", () => {
    const set = parseShippedTxt("#nullable enable\nD2.Foo\nD2.Foo\n");

    expect(set.size).toBe(1);
  });
});

// ---------------------------------------------------------------------------
// diffShippedLines
// ---------------------------------------------------------------------------

describe("diffShippedLines", () => {
  it("identical sets → all false", () => {
    const result = diffShippedLines(
      new Set(["D2.Foo", "D2.Bar"]),
      new Set(["D2.Foo", "D2.Bar"]),
    );

    expect(result).toEqual<ApiDiff>({
      added: false,
      removed: false,
      changed: false,
    });
  });

  it("a new line in head → added", () => {
    const result = diffShippedLines(
      new Set(["D2.Foo"]),
      new Set(["D2.Foo", "D2.Bar"]),
    );

    expect(result.added).toBe(true);
    expect(result.removed).toBe(false);
  });

  it("a line gone from head → removed", () => {
    const result = diffShippedLines(
      new Set(["D2.Foo", "D2.Bar"]),
      new Set(["D2.Foo"]),
    );

    expect(result.removed).toBe(true);
    expect(result.added).toBe(false);
  });

  it("a signature change (old line removed + new line added) → added + removed", () => {
    const result = diffShippedLines(
      new Set(["D2.Foo.Bar() -> int"]),
      new Set(["D2.Foo.Bar() -> string"]),
    );

    expect(result.added).toBe(true);
    expect(result.removed).toBe(true);
    // changed is not separately derivable from line text; left false.
    expect(result.changed).toBe(false);
  });

  it("empty baseline + non-empty head → added only", () => {
    const result = diffShippedLines(new Set(), new Set(["D2.Foo"]));

    expect(result.added).toBe(true);
    expect(result.removed).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Baseline path helpers
// ---------------------------------------------------------------------------

describe("baseline path helpers", () => {
  const csproj = join("/abs", "result", "D2.Shared.Result.csproj");

  it("fingerprintBaselinePath → .release-fingerprint next to the csproj", () => {
    expect(fingerprintBaselinePath(csproj)).toBe(
      join("/abs", "result", ".release-fingerprint"),
    );
  });

  it("shippedTxtPath → PublicAPI.Shipped.txt next to the csproj", () => {
    expect(shippedTxtPath(csproj)).toBe(
      join("/abs", "result", "PublicAPI.Shipped.txt"),
    );
  });

  it("unshippedTxtPath → PublicAPI.Unshipped.txt next to the csproj", () => {
    expect(unshippedTxtPath(csproj)).toBe(
      join("/abs", "result", "PublicAPI.Unshipped.txt"),
    );
  });

  it("handles a Windows-style csproj path", () => {
    const win = "C:\\repo\\result\\D2.Shared.Result.csproj";
    expect(fingerprintBaselinePath(win)).toContain(".release-fingerprint");
    expect(fingerprintBaselinePath(win)).not.toContain(
      "D2.Shared.Result.csproj",
    );
  });
});
