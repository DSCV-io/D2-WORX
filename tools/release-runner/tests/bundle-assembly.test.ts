// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Unit tests for the bundle-assembly script's pure helper functions.
//
// Imports buildManifestJson and buildHowToUse directly from the .mjs script.
// The main() call in the script is guarded by an entry-point check
// (import.meta.url === process.argv[1]), so importing the module does not
// trigger the CLI side effects.
//
// Covers:
//   - buildManifestJson: JSON shape, tag/timestamp fields, package mapping,
//     field exclusions, version field.
//   - buildHowToUse: tag in title, nuget/npm counts, code snippets,
//     external-deps caveat, license note.

import { describe, expect, it } from "vitest";
import { resolve } from "node:path";
import { repoRoot } from "./repo-root.js";

// ---------------------------------------------------------------------------
// Dynamic import of the .mjs script
// ---------------------------------------------------------------------------

const scriptPath = resolve(repoRoot, "tools/scripts/assemble-libs-bundle.mjs");

// Dynamic import so vitest resolves the .mjs ESM module at runtime.
// We use a module-level await via top-level await (ESM).
const { buildManifestJson, buildHowToUse } = await import(scriptPath);

// ---------------------------------------------------------------------------
// Shared fixture types
// ---------------------------------------------------------------------------

interface ListEntry {
  name: string;
  ecosystem: "npm" | "nuget";
  currentVersion: string;
  dir: string;
  manifestPath: string;
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const SAMPLE_PACKAGES: ListEntry[] = [
  {
    name: "D2.Shared.Result",
    ecosystem: "nuget",
    currentVersion: "0.1.0",
    dir: "server/shared/dotnet/result/core",
    manifestPath:
      "/repo/server/shared/dotnet/result/core/D2.Shared.Result.csproj",
  },
  {
    name: "@d2/result",
    ecosystem: "npm",
    currentVersion: "0.1.0",
    dir: "server/shared/typescript/result",
    manifestPath: "/repo/server/shared/typescript/result/package.json",
  },
];

const SAMPLE_TAG = "libs-2026.06.24";
const SAMPLE_TS = "2026-06-24T12:00:00.000Z";

// ---------------------------------------------------------------------------
// buildManifestJson tests
// ---------------------------------------------------------------------------

describe("buildManifestJson — output shape", () => {
  it("returns valid JSON", () => {
    const output = buildManifestJson(
      SAMPLE_TAG,
      SAMPLE_TS,
      SAMPLE_PACKAGES,
    ) as string;
    expect(() => JSON.parse(output)).not.toThrow();
  });

  it("ends with a trailing newline", () => {
    const output = buildManifestJson(
      SAMPLE_TAG,
      SAMPLE_TS,
      SAMPLE_PACKAGES,
    ) as string;
    expect(output.endsWith("\n")).toBe(true);
  });

  it("top-level wrapper has tag, generatedAt, and packages fields", () => {
    const parsed = JSON.parse(
      buildManifestJson(SAMPLE_TAG, SAMPLE_TS, SAMPLE_PACKAGES) as string,
    ) as { tag: string; generatedAt: string; packages: unknown[] };
    expect(parsed.tag).toBe(SAMPLE_TAG);
    expect(parsed.generatedAt).toBe(SAMPLE_TS);
    expect(Array.isArray(parsed.packages)).toBe(true);
  });

  it("packages array length matches input", () => {
    const parsed = JSON.parse(
      buildManifestJson(SAMPLE_TAG, SAMPLE_TS, SAMPLE_PACKAGES) as string,
    ) as { packages: unknown[] };
    expect(parsed.packages).toHaveLength(SAMPLE_PACKAGES.length);
  });

  it("each package entry has name, ecosystem, version, dir", () => {
    const parsed = JSON.parse(
      buildManifestJson(SAMPLE_TAG, SAMPLE_TS, SAMPLE_PACKAGES) as string,
    ) as {
      packages: {
        name: string;
        ecosystem: string;
        version: string;
        dir: string;
      }[];
    };
    const entry = parsed.packages[0]!;
    expect(typeof entry.name).toBe("string");
    expect(typeof entry.ecosystem).toBe("string");
    expect(typeof entry.version).toBe("string");
    expect(typeof entry.dir).toBe("string");
  });

  it("package version field uses currentVersion from ListEntry", () => {
    const pkgs: ListEntry[] = [
      {
        name: "D2.Shared.Result",
        ecosystem: "nuget",
        currentVersion: "0.3.7",
        dir: "some/dir",
        manifestPath: "/some/path.csproj",
      },
    ];
    const parsed = JSON.parse(
      buildManifestJson(SAMPLE_TAG, SAMPLE_TS, pkgs) as string,
    ) as { packages: { version: string }[] };
    expect(parsed.packages[0]!.version).toBe("0.3.7");
  });

  it("package entry does NOT include manifestPath", () => {
    const parsed = JSON.parse(
      buildManifestJson(SAMPLE_TAG, SAMPLE_TS, SAMPLE_PACKAGES) as string,
    ) as { packages: Record<string, unknown>[] };
    expect(parsed.packages[0]).not.toHaveProperty("manifestPath");
  });

  it("tag field matches input tag", () => {
    const parsed = JSON.parse(
      buildManifestJson(
        "libs-2099.01.01",
        SAMPLE_TS,
        SAMPLE_PACKAGES,
      ) as string,
    ) as { tag: string };
    expect(parsed.tag).toBe("libs-2099.01.01");
  });

  it("generatedAt field matches input timestamp", () => {
    const ts = "2026-06-24T00:00:00.000Z";
    const parsed = JSON.parse(
      buildManifestJson(SAMPLE_TAG, ts, SAMPLE_PACKAGES) as string,
    ) as { generatedAt: string };
    expect(parsed.generatedAt).toBe(ts);
  });
});

// ---------------------------------------------------------------------------
// buildHowToUse tests
// ---------------------------------------------------------------------------

describe("buildHowToUse — content", () => {
  it("contains the tag in the title", () => {
    const output = buildHowToUse(SAMPLE_TAG, SAMPLE_PACKAGES) as string;
    expect(output).toContain(`D2 Library Bundle — ${SAMPLE_TAG}`);
  });

  it("reports correct nuget count", () => {
    const output = buildHowToUse(SAMPLE_TAG, SAMPLE_PACKAGES) as string;
    expect(output).toContain("1 .NET (NuGet) package");
  });

  it("reports correct npm count", () => {
    const output = buildHowToUse(SAMPLE_TAG, SAMPLE_PACKAGES) as string;
    expect(output).toContain("1 TypeScript (npm) package");
  });

  it("includes the nuget.config snippet referencing the tag", () => {
    const output = buildHowToUse(SAMPLE_TAG, SAMPLE_PACKAGES) as string;
    expect(output).toContain(`d2-libs-${SAMPLE_TAG}/nuget`);
  });

  it("includes the pnpm add snippet referencing the tag", () => {
    const output = buildHowToUse(SAMPLE_TAG, SAMPLE_PACKAGES) as string;
    expect(output).toContain(`d2-libs-${SAMPLE_TAG}/npm/`);
  });

  it("includes the external-deps caveat", () => {
    const output = buildHowToUse(SAMPLE_TAG, SAMPLE_PACKAGES) as string;
    expect(output).toContain("external third-party libraries");
    expect(output).toContain("not a fully");
    expect(output).toContain("air-gapped");
  });

  it("includes the PolyForm Strict license note", () => {
    const output = buildHowToUse(SAMPLE_TAG, SAMPLE_PACKAGES) as string;
    expect(output).toContain("PolyForm Strict");
    expect(output).toContain("non-commercial");
  });

  it("includes both .NET and npm sections", () => {
    const output = buildHowToUse(SAMPLE_TAG, SAMPLE_PACKAGES) as string;
    expect(output).toContain("## .NET (NuGet)");
    expect(output).toContain("## npm / TypeScript");
  });

  it("handles all-nuget input without error", () => {
    const nugetOnly: ListEntry[] = [
      {
        name: "D2.Shared.Result",
        ecosystem: "nuget",
        currentVersion: "0.1.0",
        dir: "a",
        manifestPath: "/a.csproj",
      },
      {
        name: "D2.Shared.Utilities",
        ecosystem: "nuget",
        currentVersion: "0.1.0",
        dir: "b",
        manifestPath: "/b.csproj",
      },
    ];
    const output = buildHowToUse(SAMPLE_TAG, nugetOnly) as string;
    expect(output).toContain("2 .NET (NuGet) package");
    expect(output).toContain("0 TypeScript (npm) package");
  });

  it("handles all-npm input without error", () => {
    const npmOnly: ListEntry[] = [
      {
        name: "@d2/result",
        ecosystem: "npm",
        currentVersion: "0.1.0",
        dir: "a",
        manifestPath: "/a/package.json",
      },
    ];
    const output = buildHowToUse(SAMPLE_TAG, npmOnly) as string;
    expect(output).toContain("0 .NET (NuGet) package");
    expect(output).toContain("1 TypeScript (npm) package");
  });
});
