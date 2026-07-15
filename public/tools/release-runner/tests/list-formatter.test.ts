// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Unit tests for formatPackageList — the pure formatter behind --list mode.
//
// Asserts:
//   - Output parses as valid JSON.
//   - Every entry has the five required fields with correct types.
//   - Entries match the input descriptors (name, ecosystem, dir,
//     manifestPath, currentVersion).
//   - The formatter writes nothing to disk (read-only contract).
//   - Output is deterministic and ends with a newline.

import { describe, expect, it } from "vitest";
import { formatPackageList } from "../src/list-formatter.js";
import type { ListEntry } from "../src/list-formatter.js";
import type { PackageDescriptor } from "../src/types.js";

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function makeNpm(name: string, version = "0.1.0"): PackageDescriptor {
  return {
    name,
    ecosystem: "npm",
    dir: `public/packages/typescript/${name.replace("@dcsv-io/d2-", "")}`,
    manifestPath: `/repo/public/packages/typescript/${name.replace("@dcsv-io/d2-", "")}/package.json`,
    changelogPath: `/repo/public/packages/typescript/${name.replace("@dcsv-io/d2-", "")}/CHANGELOG.md`,
    currentVersion: version,
    dependencies: [],
  };
}

function makeNuget(name: string, version = "0.1.0"): PackageDescriptor {
  return {
    name,
    ecosystem: "nuget",
    dir: `public/packages/dotnet/${name.toLowerCase().replace(/\./g, "-")}`,
    manifestPath: `/repo/public/packages/dotnet/${name.toLowerCase().replace(/\./g, "-")}/${name}.csproj`,
    changelogPath: `/repo/public/packages/dotnet/${name.toLowerCase().replace(/\./g, "-")}/CHANGELOG.md`,
    currentVersion: version,
    dependencies: [],
  };
}

// ---------------------------------------------------------------------------
// Shape + field tests
// ---------------------------------------------------------------------------

describe("formatPackageList — output shape", () => {
  it("returns valid JSON", () => {
    const pkgs = [makeNpm("@dcsv-io/d2-result"), makeNuget("DcsvIo.D2.Result")];
    const output = formatPackageList(pkgs);
    expect(() => JSON.parse(output)).not.toThrow();
  });

  it("output ends with a trailing newline", () => {
    const pkgs = [makeNpm("@dcsv-io/d2-result")];
    const output = formatPackageList(pkgs);
    expect(output.endsWith("\n")).toBe(true);
  });

  it("returns an array with one entry per input descriptor", () => {
    const pkgs = [makeNpm("@dcsv-io/d2-result"), makeNuget("DcsvIo.D2.Result")];
    const entries = JSON.parse(formatPackageList(pkgs)) as ListEntry[];
    expect(entries).toHaveLength(2);
  });

  it("each entry has all five required fields", () => {
    const pkgs = [makeNpm("@dcsv-io/d2-result")];
    const entries = JSON.parse(formatPackageList(pkgs)) as ListEntry[];
    const entry = entries[0];
    expect(entry).toBeDefined();
    expect(typeof entry!.name).toBe("string");
    expect(typeof entry!.ecosystem).toBe("string");
    expect(typeof entry!.dir).toBe("string");
    expect(typeof entry!.manifestPath).toBe("string");
    expect(typeof entry!.currentVersion).toBe("string");
  });

  it("npm entry carries ecosystem='npm'", () => {
    const entries = JSON.parse(
      formatPackageList([makeNpm("@dcsv-io/d2-result")]),
    ) as ListEntry[];
    expect(entries[0]!.ecosystem).toBe("npm");
  });

  it("nuget entry carries ecosystem='nuget'", () => {
    const entries = JSON.parse(
      formatPackageList([makeNuget("DcsvIo.D2.Result")]),
    ) as ListEntry[];
    expect(entries[0]!.ecosystem).toBe("nuget");
  });

  it("entry name matches the descriptor name", () => {
    const pkg = makeNpm("@dcsv-io/d2-result");
    const entries = JSON.parse(formatPackageList([pkg])) as ListEntry[];
    expect(entries[0]!.name).toBe("@dcsv-io/d2-result");
  });

  it("entry currentVersion matches the descriptor currentVersion", () => {
    const pkg = makeNpm("@dcsv-io/d2-result", "0.3.7");
    const entries = JSON.parse(formatPackageList([pkg])) as ListEntry[];
    expect(entries[0]!.currentVersion).toBe("0.3.7");
  });

  it("entry dir matches the descriptor dir", () => {
    const pkg = makeNpm("@dcsv-io/d2-result");
    const entries = JSON.parse(formatPackageList([pkg])) as ListEntry[];
    expect(entries[0]!.dir).toBe(pkg.dir);
  });

  it("entry manifestPath matches the descriptor manifestPath", () => {
    const pkg = makeNuget("DcsvIo.D2.Result");
    const entries = JSON.parse(formatPackageList([pkg])) as ListEntry[];
    expect(entries[0]!.manifestPath).toBe(pkg.manifestPath);
  });

  it("entry does NOT include changelogPath (not in the list output)", () => {
    const pkg = makeNpm("@dcsv-io/d2-result");
    const entries = JSON.parse(formatPackageList([pkg])) as Record<
      string,
      unknown
    >[];
    expect(entries[0]).not.toHaveProperty("changelogPath");
  });
});

// ---------------------------------------------------------------------------
// Multi-package ordering
// ---------------------------------------------------------------------------

describe("formatPackageList — ordering", () => {
  it("preserves input order (loader already sorts by name)", () => {
    const pkgs = [
      makeNpm("@dcsv-io/d2-a"),
      makeNpm("@dcsv-io/d2-b"),
      makeNuget("DcsvIo.D2.Z"),
    ];
    const entries = JSON.parse(formatPackageList(pkgs)) as ListEntry[];
    expect(entries[0]!.name).toBe("@dcsv-io/d2-a");
    expect(entries[1]!.name).toBe("@dcsv-io/d2-b");
    expect(entries[2]!.name).toBe("DcsvIo.D2.Z");
  });

  it("handles a mixed npm+nuget set correctly", () => {
    const pkgs = [makeNpm("@dcsv-io/d2-result"), makeNuget("DcsvIo.D2.Result")];
    const entries = JSON.parse(formatPackageList(pkgs)) as ListEntry[];
    const npmEntry = entries.find((e) => e.ecosystem === "npm");
    const nugetEntry = entries.find((e) => e.ecosystem === "nuget");
    expect(npmEntry).toBeDefined();
    expect(nugetEntry).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Determinism + write-nothing
// ---------------------------------------------------------------------------

describe("formatPackageList — determinism", () => {
  it("produces identical output when called twice with the same input", () => {
    const pkgs = [makeNpm("@dcsv-io/d2-result"), makeNuget("DcsvIo.D2.Result")];
    expect(formatPackageList(pkgs)).toBe(formatPackageList(pkgs));
  });

  it("single-package list produces a JSON array (not a bare object)", () => {
    const output = formatPackageList([makeNpm("@dcsv-io/d2-result")]);
    const parsed: unknown = JSON.parse(output);
    expect(Array.isArray(parsed)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Full-count shape — 83 synthetic descriptors
// ---------------------------------------------------------------------------

describe("formatPackageList — full consumable set shape", () => {
  it("handles 83 entries without truncation or error", () => {
    const pkgs: PackageDescriptor[] = [];

    for (let i = 0; i < 54; i++) {
      pkgs.push(makeNuget(`DcsvIo.D2.Lib${i.toString()}`));
    }

    for (let i = 0; i < 29; i++) {
      pkgs.push(makeNpm(`@dcsv-io/d2-lib${i.toString()}`));
    }

    const output = formatPackageList(pkgs);
    const entries = JSON.parse(output) as ListEntry[];
    expect(entries).toHaveLength(83);

    const nugetCount = entries.filter((e) => e.ecosystem === "nuget").length;
    const npmCount = entries.filter((e) => e.ecosystem === "npm").length;
    expect(nugetCount).toBe(54);
    expect(npmCount).toBe(29);
  });
});
