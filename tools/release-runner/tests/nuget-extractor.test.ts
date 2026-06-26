// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the NuGet extraction adapter (nuget-extractor.ts).
//
// Unit (synthetic shell seam — no real build):
//   - parseUnshippedTxt: added / removed / rename / empty.
//   - extractNugetDiff: ApiDiff mapping from RS0016/RS0017, IL-dump capture,
//     PublicAPI.* content capture, fail-loud paths.
//
// Integration (gated D2_VERSIONING_INTEGRATION=1 — real build + il-fingerprint):
//   - The provider-level proofs (path-independence, build-stability, impl-change
//     detection) live in real-diff-provider.test.ts (the IL-dump is composed
//     into the fingerprint there). This file's integration block exercises the
//     real DotnetShell against D2.Shared.Utilities end-to-end.

import { describe, expect, it } from "vitest";

const RUN_INTEGRATION = process.env["D2_VERSIONING_INTEGRATION"] === "1";

import { join, resolve } from "node:path";
import {
  extractNugetDiff,
  makeRealDotnetShell,
  parseUnshippedTxt,
  type DotnetShell,
  type ShellResult,
} from "../src/nuget-extractor.js";
import { repoRoot } from "./repo-root.js";
import type { ApiDiff } from "../src/diff-bump.js";
import type { PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Package under test — D2.Shared.Utilities (representative consumable)
// ---------------------------------------------------------------------------

const utilitiesDir = resolve(repoRoot, "server/shared/dotnet/utilities");
const csprojPath = join(utilitiesDir, "D2.Shared.Utilities.csproj");

const utilitiesPkg: PackageDescriptor = {
  name: "D2.Shared.Utilities",
  ecosystem: "nuget",
  dir: "server/shared/dotnet/utilities",
  manifestPath: csprojPath,
  changelogPath: join(utilitiesDir, "CHANGELOG.md"),
  currentVersion: "0.1.0",
  dependencies: [],
};

// ---------------------------------------------------------------------------
// parseUnshippedTxt — unit tests (no build required)
// ---------------------------------------------------------------------------

describe("parseUnshippedTxt — unit", () => {
  it("empty Unshipped.txt → no diff", () => {
    const result = parseUnshippedTxt("#nullable enable\n");
    expect(result).toEqual<ApiDiff>({
      added: false,
      removed: false,
      changed: false,
    });
  });

  it("plain API line → added", () => {
    const result = parseUnshippedTxt(
      "#nullable enable\nstatic D2.Shared.Foo.Bar(string! x) -> void\n",
    );
    expect(result).toEqual<ApiDiff>({
      added: true,
      removed: false,
      changed: false,
    });
  });

  it("*REMOVED* line → removed", () => {
    const result = parseUnshippedTxt(
      "#nullable enable\n*REMOVED*static D2.Shared.Foo.OldBar() -> void\n",
    );
    expect(result).toEqual<ApiDiff>({
      added: false,
      removed: true,
      changed: false,
    });
  });

  it("rename (*REMOVED* + plain) → removed + added", () => {
    const result = parseUnshippedTxt(
      "#nullable enable\n" +
        "*REMOVED*static D2.Shared.Foo.OldBar() -> void\n" +
        "static D2.Shared.Foo.NewBar() -> void\n",
    );
    expect(result).toEqual<ApiDiff>({
      added: true,
      removed: true,
      changed: false,
    });
  });

  it("changed flag is always false (not derivable from PublicApiAnalyzers output)", () => {
    const result = parseUnshippedTxt(
      "#nullable enable\nstatic D2.Shared.Foo.Bar(string! x) -> void\n",
    );
    expect(result.changed).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// extractNugetDiff — injected shell (seam contract, no real build)
// ---------------------------------------------------------------------------

/**
 * Build a synthetic shell that returns a chosen API-diff diagnostic + a fixed
 * IL dump + fixed PublicAPI.* content. Asserts the REAL mapping the adapter
 * performs (faithful seam — never a hollow canned return).
 */
function makeShell(opts: {
  rs0016?: boolean;
  rs0017?: boolean;
  ilDump?: string | undefined;
  shipped?: string;
  unshipped?: string;
  dllPresent?: boolean;
}): DotnetShell {
  return {
    build(_csprojPath, extraArgs): ShellResult {
      // The fingerprint (IL-dump) build passes TreatWarningsAsErrors=false and
      // succeeds regardless of API changes; the API-diff build has no such flag
      // and surfaces RS0016/RS0017 with a non-zero exit. Distinguish on the flag.
      const isFingerprintBuild = extraArgs.includes(
        "-p:TreatWarningsAsErrors=false",
      );

      if (isFingerprintBuild) {
        return { status: 0, stdout: "Build succeeded.", stderr: "" };
      }

      const diags: string[] = [];

      if (opts.rs0016)
        diags.push(
          "error RS0016: Symbol 'static D2.Fake.Foo.NewMethod() -> void' is not part of the declared public API",
        );

      if (opts.rs0017)
        diags.push(
          "error RS0017: Symbol 'static D2.Fake.Foo.OldMethod() -> void' is part of the declared API, but could not be found",
        );

      return {
        status: opts.rs0016 || opts.rs0017 ? 1 : 0,
        stdout: diags.join("\n") || "Build succeeded.",
        stderr: "",
      };
    },
    readFile(filePath): string | undefined {
      if (filePath.endsWith(".dll"))
        return (opts.dllPresent ?? true) ? "exists" : undefined;
      if (filePath.endsWith("PublicAPI.Shipped.txt"))
        return opts.shipped ?? "#nullable enable\n";
      if (filePath.endsWith("PublicAPI.Unshipped.txt"))
        return opts.unshipped ?? "#nullable enable\n";
      return undefined;
    },
    ilDump(_dllPath): string | undefined {
      return Object.prototype.hasOwnProperty.call(opts, "ilDump")
        ? opts.ilDump
        : "# il-fingerprint v1\ntype Foo\n";
    },
  };
}

describe("extractNugetDiff — injected shell (seam contract)", () => {
  it("RS0016 in build output → apiDiff.added = true, IL dump + PublicAPI captured", () => {
    const shell = makeShell({
      rs0016: true,
      ilDump: "# il-fingerprint v1\ntype Foo attrs=0x100\n",
      shipped: "#nullable enable\nD2.Foo\n",
      unshipped: "#nullable enable\n",
    });

    const result = extractNugetDiff(utilitiesPkg, shell);

    expect(result.apiDiff).toEqual<ApiDiff>({
      added: true,
      removed: false,
      changed: false,
    });
    expect(result.ilDump).toBe("# il-fingerprint v1\ntype Foo attrs=0x100\n");
    expect(result.shippedTxt).toBe("#nullable enable\nD2.Foo\n");
    expect(result.unshippedTxt).toBe("#nullable enable\n");
    expect(result.extractionMs).toBeGreaterThanOrEqual(0);
  });

  it("RS0017 in build output → apiDiff.removed = true", () => {
    const shell = makeShell({ rs0017: true });
    const result = extractNugetDiff(utilitiesPkg, shell);

    expect(result.apiDiff.removed).toBe(true);
    expect(result.apiDiff.added).toBe(false);
  });

  it("clean build → apiDiff all-false, IL dump returned", () => {
    const shell = makeShell({ ilDump: "# il-fingerprint v1\n" });
    const result = extractNugetDiff(utilitiesPkg, shell);

    expect(result.apiDiff).toEqual<ApiDiff>({
      added: false,
      removed: false,
      changed: false,
    });
    expect(result.ilDump).toBe("# il-fingerprint v1\n");
  });

  it("il-fingerprint failure (undefined dump) → throws fail-loud", () => {
    const shell = makeShell({ ilDump: undefined });

    expect(() => extractNugetDiff(utilitiesPkg, shell)).toThrow(
      /il-fingerprint returned no dump/,
    );
  });

  it("missing built DLL → throws fail-loud", () => {
    const shell = makeShell({ dllPresent: false });

    expect(() => extractNugetDiff(utilitiesPkg, shell)).toThrow(
      /Built DLL not found/,
    );
  });

  it("build failure with no RS0016/RS0017 → throws fail-loud", () => {
    const failingShell: DotnetShell = {
      build(_csprojPath, _extraArgs): ShellResult {
        return { status: 1, stdout: "error CS1002: ; expected", stderr: "" };
      },
      readFile(): string | undefined {
        return undefined;
      },
      ilDump(): string | undefined {
        return "x";
      },
    };

    expect(() => extractNugetDiff(utilitiesPkg, failingShell)).toThrow(
      /dotnet build failed/,
    );
  });

  it("missing PublicAPI.* files → empty-string content (graceful)", () => {
    const shell: DotnetShell = {
      build(_csprojPath, _extraArgs): ShellResult {
        return { status: 0, stdout: "Build succeeded.", stderr: "" };
      },
      readFile(filePath): string | undefined {
        if (filePath.endsWith(".dll")) return "exists";
        // No PublicAPI.* files present.
        return undefined;
      },
      ilDump(): string | undefined {
        return "# il-fingerprint v1\n";
      },
    };

    const result = extractNugetDiff(utilitiesPkg, shell);

    expect(result.shippedTxt).toBe("");
    expect(result.unshippedTxt).toBe("");
  });
});

// ---------------------------------------------------------------------------
// Integration — real extraction on D2.Shared.Utilities (gated)
// Requires D2_VERSIONING_INTEGRATION=1 + a restored .NET project + the built
// il-fingerprint tool. The dedicated integration CI lane provides this.
// ---------------------------------------------------------------------------

describe.skipIf(!RUN_INTEGRATION)(
  "extractNugetDiff — integration (D2.Shared.Utilities)",
  () => {
    it("real build + IL dump → ApiDiff all-false (baseline current), non-empty IL dump", () => {
      const shell = makeRealDotnetShell(repoRoot);
      const result = extractNugetDiff(utilitiesPkg, shell);

      // The committed baseline is current → no API diff.
      expect(result.apiDiff.added).toBe(false);
      expect(result.apiDiff.removed).toBe(false);
      // The IL dump is non-trivial and carries the tool's banner.
      expect(result.ilDump.startsWith("# il-fingerprint v1")).toBe(true);
      expect(result.ilDump.length).toBeGreaterThan(100);
    }, 120_000);
  },
);
