// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import {
  readManifestVersion,
  readNpmVersion,
  readNugetVersion,
  writeManifestVersion,
  writeNpmVersion,
  writeNugetVersion,
} from "../src/manifest-editor.js";

// ---------------------------------------------------------------------------
// Temp dir setup
// ---------------------------------------------------------------------------

let tempDir: string;

beforeEach(() => {
  tempDir = join(tmpdir(), `release-runner-test-${Date.now().toString()}`);
  mkdirSync(tempDir, { recursive: true });
});

afterEach(() => {
  rmSync(tempDir, { recursive: true, force: true });
});

function writeTempFile(name: string, content: string): string {
  const p = join(tempDir, name);
  writeFileSync(p, content, "utf-8");
  return p;
}

// ---------------------------------------------------------------------------
// npm (package.json) — readNpmVersion
// ---------------------------------------------------------------------------

describe("readNpmVersion", () => {
  it("reads the version field from a standard package.json", () => {
    const path = writeTempFile(
      "package.json",
      `{
  "name": "@d2/result",
  "version": "0.1.0",
  "private": true
}`,
    );
    expect(readNpmVersion(path)).toBe("0.1.0");
  });

  it("reads the version field when surrounded by other keys", () => {
    const path = writeTempFile(
      "package.json",
      `{
  "name": "@d2/foo",
  "description": "A package",
  "version": "2.3.4",
  "type": "module"
}`,
    );
    expect(readNpmVersion(path)).toBe("2.3.4");
  });

  it("throws when version field is absent", () => {
    const path = writeTempFile("package.json", `{ "name": "@d2/bar" }`);
    expect(() => readNpmVersion(path)).toThrow(/version/);
  });
});

// ---------------------------------------------------------------------------
// npm — writeNpmVersion
// ---------------------------------------------------------------------------

describe("writeNpmVersion", () => {
  it("updates the version field and preserves surrounding content", () => {
    const original = `{
  "name": "@d2/result",
  "version": "0.1.0",
  "private": true,
  "type": "module"
}`;
    const path = writeTempFile("package.json", original);
    writeNpmVersion(path, "0.2.0");
    const updated = readFileSync(path, "utf-8");
    expect(updated).toContain('"version": "0.2.0"');
    expect(updated).toContain('"name": "@d2/result"');
    expect(updated).toContain('"private": true');
  });

  it("preserves key order and formatting (no JSON re-serialization)", () => {
    const original = `{
  "name": "@d2/foo",
  "version": "1.0.0"
}`;
    const path = writeTempFile("package.json", original);
    writeNpmVersion(path, "1.1.0");
    const updated = readFileSync(path, "utf-8");
    // Verify the original key order is intact (name before version).
    const nameIdx = updated.indexOf('"name"');
    const versionIdx = updated.indexOf('"version"');
    expect(nameIdx).toBeLessThan(versionIdx);
  });

  it("throws when version field is absent", () => {
    const path = writeTempFile("package.json", `{ "name": "@d2/bar" }`);
    expect(() => writeNpmVersion(path, "0.2.0")).toThrow(/version/);
  });

  it("round-trips correctly: read after write returns the new version", () => {
    const path = writeTempFile("package.json", `{"version":"0.1.0"}`);
    writeNpmVersion(path, "0.3.0");
    expect(readNpmVersion(path)).toBe("0.3.0");
  });
});

// ---------------------------------------------------------------------------
// NuGet (.csproj) — readNugetVersion
// ---------------------------------------------------------------------------

describe("readNugetVersion", () => {
  it("reads <Version> from a standard csproj", () => {
    const path = writeTempFile(
      "D2.Shared.Result.csproj",
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Version>0.1.0</Version>
    <PackageId>D2.Shared.Result</PackageId>
  </PropertyGroup>
</Project>`,
    );
    expect(readNugetVersion(path)).toBe("0.1.0");
  });

  it("reads <Version> with indentation", () => {
    const path = writeTempFile(
      "Foo.csproj",
      `<Project>
    <PropertyGroup>
      <Version>2.5.1</Version>
    </PropertyGroup>
</Project>`,
    );
    expect(readNugetVersion(path)).toBe("2.5.1");
  });

  it("throws when <Version> element is absent", () => {
    const path = writeTempFile(
      "Bar.csproj",
      `<Project><PropertyGroup><PackageId>Bar</PackageId></PropertyGroup></Project>`,
    );
    expect(() => readNugetVersion(path)).toThrow(/<Version>/);
  });
});

// ---------------------------------------------------------------------------
// NuGet — writeNugetVersion
// ---------------------------------------------------------------------------

describe("writeNugetVersion", () => {
  it("updates <Version> and preserves surrounding XML", () => {
    const original = `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Version>0.1.0</Version>
    <PackageId>D2.Shared.Result</PackageId>
    <IsPackable>true</IsPackable>
  </PropertyGroup>
</Project>`;
    const path = writeTempFile("D2.Shared.Result.csproj", original);
    writeNugetVersion(path, "0.2.0");
    const updated = readFileSync(path, "utf-8");
    expect(updated).toContain("<Version>0.2.0</Version>");
    expect(updated).toContain("<PackageId>D2.Shared.Result</PackageId>");
    expect(updated).toContain("<IsPackable>true</IsPackable>");
  });

  it("throws when <Version> element is absent", () => {
    const path = writeTempFile(
      "Missing.csproj",
      `<Project><PropertyGroup></PropertyGroup></Project>`,
    );
    expect(() => writeNugetVersion(path, "1.0.0")).toThrow(/<Version>/);
  });

  it("round-trips correctly: read after write returns the new version", () => {
    const path = writeTempFile(
      "Foo.csproj",
      `<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>`,
    );
    writeNugetVersion(path, "1.5.0");
    expect(readNugetVersion(path)).toBe("1.5.0");
  });
});

// ---------------------------------------------------------------------------
// Unified facade — readManifestVersion / writeManifestVersion
// ---------------------------------------------------------------------------

describe("readManifestVersion / writeManifestVersion", () => {
  it("delegates to npm adapter for .json extension", () => {
    const path = writeTempFile("package.json", `{"version":"0.5.0"}`);
    expect(readManifestVersion(path)).toBe("0.5.0");
  });

  it("delegates to nuget adapter for .csproj extension", () => {
    const path = writeTempFile(
      "D2.csproj",
      `<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>`,
    );
    expect(readManifestVersion(path)).toBe("1.0.0");
  });

  it("throws on unknown extension", () => {
    const path = writeTempFile("MANIFEST.txt", "version=1.0.0");
    expect(() => readManifestVersion(path)).toThrow(/extension/);
  });

  it("writeManifestVersion delegates to npm adapter for .json", () => {
    const path = writeTempFile("package.json", `{"version":"0.1.0"}`);
    writeManifestVersion(path, "0.2.0");
    expect(readManifestVersion(path)).toBe("0.2.0");
  });

  it("writeManifestVersion delegates to nuget adapter for .csproj", () => {
    const path = writeTempFile(
      "D2.csproj",
      `<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>`,
    );
    writeManifestVersion(path, "1.0.0");
    expect(readManifestVersion(path)).toBe("1.0.0");
  });

  it("writeManifestVersion throws on unknown extension", () => {
    const path = writeTempFile("file.xml", "<version>1.0.0</version>");
    expect(() => writeManifestVersion(path, "2.0.0")).toThrow(/extension/);
  });
});
