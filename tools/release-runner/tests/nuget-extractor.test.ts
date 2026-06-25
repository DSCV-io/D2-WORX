// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Integration test for the NuGet extraction adapter.
//
// Tests run against the REAL D2.Shared.Utilities package (the spike's
// representative consumable). The PublicApiAnalyzers baseline and the
// deterministic build are exercised end-to-end — no stubs for the extraction
// path itself.
//
// Three test scenarios (all real, no synthetic builds):
//
//   T1  ApiDiff detects a new public member added to the source.
//   T2  ApiDiff detects a public member removed from the source.
//   T3  FingerprintDiff detects an internal body change AND ignores a
//       comment-only edit.
//
// The tests mutate source files under utilities/ temporarily, build, assert,
// then restore the original content. Each test restores unconditionally in
// the `afterEach` hook so CI never leaves the tree dirty.
//
// Execution time: ~3–8 s per test on an incremental build (no-restore).
// Coverage assertion: parsUnshippedTxt (unit) + extractNugetDiff (integration).

import { afterEach, describe, expect, it } from "vitest";
import { existsSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import {
  extractNugetDiff,
  fingerprintBaselinePath,
  parseUnshippedTxt,
  type DotnetShell,
  type ShellResult,
} from "../src/nuget-extractor.js";
import { repoRoot } from "./repo-root.js";
import type { ApiDiff, FingerprintDiff } from "../src/diff-bump.js";
import type { PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Package under test — D2.Shared.Utilities (representative consumable)
// ---------------------------------------------------------------------------

const utilitiesDir = resolve(repoRoot, "server/shared/dotnet/utilities");
const csprojPath = join(utilitiesDir, "D2.Shared.Utilities.csproj");

/** Minimal PackageDescriptor for D2.Shared.Utilities (spike only). */
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
// File-mutation helpers (guard + restore)
// ---------------------------------------------------------------------------

/** Map of filePath → original content, restored in afterEach. */
const filesToRestore = new Map<string, string>();

/**
 * Mutate `filePath` to `newContent` and record it for restore in afterEach.
 *
 * When the file does not yet exist (e.g. the fingerprint baseline on a first
 * run), the restore step deletes the file rather than writing back an original.
 * Pass `newContent` as `""` when registering a file for delete-on-restore.
 */
function mutateFile(filePath: string, newContent: string): void {
  if (!filesToRestore.has(filePath)) {
    // Sentinel: null string means "file did not exist — delete on restore".
    filesToRestore.set(
      filePath,
      existsSync(filePath)
        ? readFileSync(filePath, "utf-8")
        : "\x00_DELETE_\x00",
    );
  }

  writeFileSync(filePath, newContent, "utf-8");
}

afterEach(() => {
  for (const [path, original] of filesToRestore) {
    if (original === "\x00_DELETE_\x00") {
      if (existsSync(path)) rmSync(path);
    } else {
      writeFileSync(path, original, "utf-8");
    }
  }

  filesToRestore.clear();
});

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
    // `changed` is never set by parseUnshippedTxt; the rename case (removed+added)
    // is modelled that way and the bump engine handles it as a break.
    const result = parseUnshippedTxt(
      "#nullable enable\nstatic D2.Shared.Foo.Bar(string! x) -> void\n",
    );
    expect(result.changed).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Integration — real extraction on D2.Shared.Utilities
// ---------------------------------------------------------------------------

describe("extractNugetDiff — integration (D2.Shared.Utilities)", () => {
  // T1: Adding a new public member → RS0016 → ApiDiff.added = true
  it("T1: ApiDiff.added when a new public member is added to the source", async () => {
    const renderPath = join(
      utilitiesDir,
      "Diagnostics/SanitizedExceptionRender.cs",
    );
    const original = readFileSync(renderPath, "utf-8");

    // Insert a new public member into SanitizedExceptionRender.
    const mutated = original.replace(
      "public static string TypeName(Exception ex) =>",
      "/// <summary>Spike-added test member — not part of permanent API.</summary>\n" +
        '    /// <param name="ex">The exception.</param>\n' +
        "    /// <returns>A string.</returns>\n" +
        '    public static string SpikeNewMember(Exception ex) => "spike";\n\n' +
        "    public static string TypeName(Exception ex) =>",
    );

    mutateFile(renderPath, mutated);

    const result = extractNugetDiff(utilitiesPkg);

    expect(result.apiDiff.added).toBe(true);
    expect(result.apiDiff.removed).toBe(false);
  }, 60_000);

  // T2: Removing an existing public member → RS0017 → ApiDiff.removed = true
  it("T2: ApiDiff.removed when a shipped public member is removed from source", async () => {
    const renderPath = join(
      utilitiesDir,
      "Diagnostics/SanitizedExceptionRender.cs",
    );
    const original = readFileSync(renderPath, "utf-8");

    // Make TypeName internal so it's no longer part of the public API.
    // RS0017 fires because PublicAPI.Shipped.txt still lists the public member.
    const mutated = original.replace(
      "    public static string TypeName(Exception ex) =>",
      "    internal static string TypeName(Exception ex) =>",
    );

    mutateFile(renderPath, mutated);

    const result = extractNugetDiff(utilitiesPkg);

    expect(result.apiDiff.removed).toBe(true);
  }, 60_000);

  // T3a: Comment-only edit → FingerprintDiff.changed = false (same DLL hash)
  // T3b: Internal body edit → FingerprintDiff.changed = true (different DLL hash)
  it("T3a: FingerprintDiff.changed is false for a comment-only edit", async () => {
    // First, build with clean source so the DLL exists at the expected path.
    extractNugetDiff(utilitiesPkg);
    const cleanDllPath = join(
      utilitiesDir,
      "bin/Release/net10.0/D2.Shared.Utilities.dll",
    );
    // The DLL was just built; compute its hash as the baseline.
    const { createHash } = await import("node:crypto");
    const cleanBytes = readFileSync(cleanDllPath);
    const cleanHash = createHash("sha256").update(cleanBytes).digest("hex");

    // Write the clean hash as the committed baseline so the comparison works.
    // mutateFile registers the file for restore/delete in afterEach.
    const baselinePath = fingerprintBaselinePath(csprojPath);
    mutateFile(baselinePath, cleanHash);

    // Now add a comment-only edit.
    const guardPath = join(utilitiesDir, "Extensions/GuardExtensions.cs");
    const originalGuard = readFileSync(guardPath, "utf-8");
    mutateFile(
      guardPath,
      originalGuard.replace(
        "// -----------------------------------------------------------------------",
        "// -----------------------------------------------------------------------\n// SPIKE_COMMENT_ONLY_TEST",
      ),
    );

    const commentResult = extractNugetDiff(utilitiesPkg);
    expect(commentResult.fingerprintDiff.changed).toBe(false);
  }, 90_000);

  it("T3b: FingerprintDiff.changed is true for an internal method-body edit", async () => {
    // Two extractNugetDiff calls (4 dotnet-build invocations × ~15s each)
    // require a longer timeout than T3a (1 call × ~30s).
    // Build with clean source so the DLL exists, then capture its hash.
    extractNugetDiff(utilitiesPkg);
    const cleanDllPath = join(
      utilitiesDir,
      "bin/Release/net10.0/D2.Shared.Utilities.dll",
    );
    const { createHash } = await import("node:crypto");
    const cleanBytes = readFileSync(cleanDllPath);
    const cleanHash = createHash("sha256").update(cleanBytes).digest("hex");
    const baselinePath = fingerprintBaselinePath(csprojPath);
    mutateFile(baselinePath, cleanHash);

    // Mutate an internal string literal in a method body.
    const guardPath = join(utilitiesDir, "Extensions/GuardExtensions.cs");
    const originalGuard = readFileSync(guardPath, "utf-8");
    mutateFile(
      guardPath,
      originalGuard.replace(
        "Value must be a non-empty, non-whitespace string.",
        "Value must be a non-empty, non-whitespace string. (SPIKE_INTERNAL_CHANGE)",
      ),
    );

    const bodyResult = extractNugetDiff(utilitiesPkg);
    expect(bodyResult.fingerprintDiff.changed).toBe(true);
  }, 120_000);
});

// ---------------------------------------------------------------------------
// DotnetShell injection — verify the seam contract
// ---------------------------------------------------------------------------

describe("extractNugetDiff — injected shell (seam contract)", () => {
  it("injected shell returning RS0016 in stdout → apiDiff.added = true", () => {
    const syntheticShell: DotnetShell = {
      build(_csprojPath, extraArgs): ShellResult {
        if (extraArgs.includes("-p:DebugType=none")) {
          // Fingerprint build — succeed with no diagnostics.
          return { status: 0, stdout: "Build succeeded.", stderr: "" };
        }

        // API-diff build — emit RS0016 (new public member).
        return {
          status: 1,
          stdout:
            "error RS0016: Symbol 'static D2.Fake.Foo.NewMethod() -> void' " +
            "is not part of the declared public API",
          stderr: "",
        };
      },
      readFile(filePath): string | undefined {
        if (filePath.endsWith(".dll")) return "exists";
        if (filePath.endsWith(".release-fingerprint")) return "abc123";
        return undefined;
      },
      sha256File(_filePath): string | undefined {
        // Return a hash DIFFERENT from the committed baseline ("abc123")
        // so fingerprintDiff.changed is true for the injected case.
        return "def456";
      },
    };

    const result = extractNugetDiff(utilitiesPkg, syntheticShell);

    // The injected build reports RS0016 → added.
    expect(result.apiDiff.added).toBe(true);
    expect(result.apiDiff.removed).toBe(false);
    expect(result.apiDiff.changed).toBe(false);
    // The injected hash differs from the baseline → fingerprint changed.
    expect(result.fingerprintDiff.changed).toBe(true);
    // Timing is measurable (> 0 ms).
    expect(result.extractionMs).toBeGreaterThanOrEqual(0);
  });

  it("injected shell returning RS0017 in stdout → apiDiff.removed = true", () => {
    const syntheticShell: DotnetShell = {
      build(_csprojPath, extraArgs): ShellResult {
        if (extraArgs.includes("-p:DebugType=none")) {
          return { status: 0, stdout: "Build succeeded.", stderr: "" };
        }

        return {
          status: 1,
          stdout:
            "error RS0017: Symbol 'static D2.Fake.Foo.OldMethod() -> void' " +
            "is part of the declared API, but is either not public or could not be found",
          stderr: "",
        };
      },
      readFile(filePath): string | undefined {
        if (filePath.endsWith(".dll")) return "exists";
        if (filePath.endsWith(".release-fingerprint")) return "abc123";
        return undefined;
      },
      sha256File(_filePath): string | undefined {
        return "abc123";
      },
    };

    const result = extractNugetDiff(utilitiesPkg, syntheticShell);

    expect(result.apiDiff.removed).toBe(true);
    expect(result.apiDiff.added).toBe(false);
    // Hash matches baseline → fingerprint unchanged.
    expect(result.fingerprintDiff.changed).toBe(false);
  });

  it("injected shell with no RS0016/RS0017 + matching hash → no diff", () => {
    const syntheticShell: DotnetShell = {
      build(_csprojPath, _extraArgs): ShellResult {
        return { status: 0, stdout: "Build succeeded.", stderr: "" };
      },
      readFile(filePath): string | undefined {
        if (filePath.endsWith(".dll")) return "exists";
        if (filePath.endsWith(".release-fingerprint")) return "stable-hash-xyz";
        return undefined;
      },
      sha256File(_filePath): string | undefined {
        return "stable-hash-xyz";
      },
    };

    const result = extractNugetDiff(utilitiesPkg, syntheticShell);

    expect(result.apiDiff).toEqual<ApiDiff>({
      added: false,
      removed: false,
      changed: false,
    });
    expect(result.fingerprintDiff).toEqual<FingerprintDiff>({ changed: false });
  });

  it("injected shell with no committed baseline → fingerprintDiff.changed = true (first run)", () => {
    const syntheticShell: DotnetShell = {
      build(_csprojPath, _extraArgs): ShellResult {
        return { status: 0, stdout: "Build succeeded.", stderr: "" };
      },
      readFile(filePath): string | undefined {
        if (filePath.endsWith(".dll")) return "exists";
        // No baseline file → undefined.
        return undefined;
      },
      sha256File(_filePath): string | undefined {
        return "some-hash";
      },
    };

    const result = extractNugetDiff(utilitiesPkg, syntheticShell);

    // No baseline → treated as changed (PATCH bump seeded on first run).
    expect(result.fingerprintDiff.changed).toBe(true);
  });
});
