// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Emitter-output parity: spec ↔ TS emitter ↔ .NET emitter (committed .g.cs).
 *
 * The existing field-constraints.parity.test.ts in contract-tests/ checks
 * RUNTIME-CATALOG equivalence via a .NET reflection fixture (TS catalog
 * values match what .NET reflection reports at test time). That test is
 * intentionally runtime-catalog-only.
 *
 * This test is EMITTER-OUTPUT parity — it verifies that:
 *   1. The TS emitter produces output that contains every name/value
 *      declared in the spec.
 *   2. The committed .NET .g.cs outputs (FieldConstraints.g.cs and
 *      Taxonomy.g.cs) contain every name/value declared in the spec.
 *
 * If either emitter drifts from the spec — e.g., a constant name is
 * renamed in the spec but the .NET generator is not re-run, or the TS
 * emitter has a rendering bug for a specific name pattern — this test
 * fails. The two emitter checks are independent, so a drift in one
 * runtime surfaces as a targeted failure identifying the offending emitter.
 */

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

import {
  emitConstraints,
  emitTaxonomy,
  type FieldConstraintsSpec,
} from "../src/field-constraints-emit.js";

// ---------------------------------------------------------------------------
// Paths
// ---------------------------------------------------------------------------

const here = dirname(fileURLToPath(import.meta.url));
// tests/ is one level below the package root; the package root is
// tools/ts-codegen/, which is 2 levels below the repo root.
const REPO_ROOT = resolve(here, "..", "..", "..");

const SPEC_PATH = resolve(
  REPO_ROOT,
  "contracts",
  "validation",
  "field-constraints.spec.json",
);

const DOTNET_FIELD_CONSTRAINTS_PATH = resolve(
  REPO_ROOT,
  "server",
  "shared",
  "dotnet",
  "validation",
  "abstractions",
  "Generated",
  "D2.Shared.Validation.SourceGen",
  "D2.Shared.Validation.SourceGen.FieldConstraintsGenerator",
  "FieldConstraints.g.cs",
);

const DOTNET_TAXONOMY_PATH = resolve(
  REPO_ROOT,
  "server",
  "shared",
  "dotnet",
  "validation",
  "abstractions",
  "Generated",
  "D2.Shared.Validation.SourceGen",
  "D2.Shared.Validation.SourceGen.FieldConstraintsGenerator",
  "Taxonomy.g.cs",
);

// ---------------------------------------------------------------------------
// Load spec
// ---------------------------------------------------------------------------

const spec = JSON.parse(readFileSync(SPEC_PATH, "utf8")) as FieldConstraintsSpec;

// ---------------------------------------------------------------------------
// TS emitter output — generated fresh from the in-repo spec
// ---------------------------------------------------------------------------

describe("TS emitter output ↔ spec (field-constraints.spec.json)", () => {
  describe("FieldConstraints constants", () => {
    const result = emitConstraints(spec);

    it("emits without error diagnostics", () => {
      const errors = result.diagnostics.filter((d) => d.severity === "error");
      expect(errors).toEqual([]);
    });

    it("emits the FieldConstraints export", () => {
      expect(result.source).toContain("export const FieldConstraints = {");
    });

    for (const entry of spec.constraints) {
      it(`contains constant ${entry.name} = ${entry.value}`, () => {
        // The emitter renders: `NAME: value,` with surrounding whitespace.
        expect(result.source).toContain(`${entry.name}: ${entry.value},`);
      });
    }

    it("constant count matches spec", () => {
      // Count occurrences of `: <number>,` lines inside the const block.
      // We do this by counting how many spec names appear in the output.
      const namesInOutput = spec.constraints.filter((e) =>
        result.source.includes(`${e.name}: ${e.value},`),
      );
      expect(namesInOutput.length).toBe(spec.constraints.length);
    });
  });

  describe("taxonomy enums", () => {
    const result = emitTaxonomy(spec);

    it("emits without error diagnostics", () => {
      const errors = result.diagnostics.filter((d) => d.severity === "error");
      expect(errors).toEqual([]);
    });

    for (const enumEntry of spec.enums) {
      describe(`enum ${enumEntry.name}`, () => {
        it(`exports const ${enumEntry.name}`, () => {
          expect(result.source).toContain(
            `export const ${enumEntry.name} = {`,
          );
        });

        for (const member of enumEntry.members) {
          it(`contains member ${member.name}: "${member.name}"`, () => {
            // The emitter renders: `MemberName: "MemberName",`
            expect(result.source).toContain(
              `${member.name}: "${member.name}",`,
            );
          });
        }

        it(`member count matches spec`, () => {
          const found = enumEntry.members.filter((m) =>
            result.source.includes(`${m.name}: "${m.name}",`),
          );
          expect(found.length).toBe(enumEntry.members.length);
        });
      });
    }
  });
});

// ---------------------------------------------------------------------------
// .NET emitter output — committed .g.cs files checked against spec
// ---------------------------------------------------------------------------

describe(".NET committed emitter output ↔ spec (field-constraints.spec.json)", () => {
  describe("FieldConstraints.g.cs constants", () => {
    const dotnetSrc = readFileSync(DOTNET_FIELD_CONSTRAINTS_PATH, "utf8");

    it("is a generated file (sanity-check header)", () => {
      expect(dotnetSrc).toContain("auto-generated");
    });

    for (const entry of spec.constraints) {
      it(`contains public const int ${entry.name} = ${entry.value}`, () => {
        // The .NET emitter renders: `public const int NAME = value;`
        expect(dotnetSrc).toContain(
          `public const int ${entry.name} = ${entry.value};`,
        );
      });
    }

    it("constant count matches spec (no extra constants not in spec)", () => {
      // Each spec constraint generates exactly one `public const int NAME =` line.
      const matches = [...dotnetSrc.matchAll(/public const int (\w+) = /g)].map(
        (m) => m[1],
      );
      const specNames = spec.constraints.map((e) => e.name).sort();
      expect([...matches].sort()).toEqual(specNames);
    });
  });

  describe("Taxonomy.g.cs enums", () => {
    const dotnetSrc = readFileSync(DOTNET_TAXONOMY_PATH, "utf8");

    it("is a generated file (sanity-check header)", () => {
      expect(dotnetSrc).toContain("auto-generated");
    });

    for (const enumEntry of spec.enums) {
      describe(`enum ${enumEntry.name}`, () => {
        it(`declares public enum ${enumEntry.name}`, () => {
          expect(dotnetSrc).toContain(`public enum ${enumEntry.name}`);
        });

        // Extract the brace-matched body for this enum once; both the
        // per-member presence check and the count check run against it so
        // a member name shared across enums (e.g. `Sr` in NamePrefix and
        // NameSuffix) cannot produce a false-positive match from the wrong body.
        const getEnumBody = (): string => {
          const enumStart = dotnetSrc.indexOf(`public enum ${enumEntry.name}`);
          const openBrace = dotnetSrc.indexOf("{", enumStart);
          let depth = 1;
          let pos = openBrace + 1;
          while (pos < dotnetSrc.length && depth > 0) {
            if (dotnetSrc[pos] === "{") depth++;
            else if (dotnetSrc[pos] === "}") depth--;
            pos++;
          }
          return dotnetSrc.slice(openBrace + 1, pos - 1);
        };

        for (const member of enumEntry.members) {
          it(`contains member ${member.name}`, () => {
            // .NET emitter renders members as `    MemberName = N,` or
            // `    MemberName = N` (last member may omit trailing comma).
            // Regex is scoped to the extracted enum body to avoid false
            // positives from identically-named members in sibling enums.
            expect(getEnumBody()).toMatch(
              new RegExp(`\\b${member.name}\\s*=\\s*\\d+`),
            );
          });
        }

        it("member count matches spec", () => {
          const enumBody = getEnumBody();
          // Count `Identifier = N` patterns inside the body.
          const memberMatches = [
            ...enumBody.matchAll(/\b([A-Za-z_][A-Za-z0-9_]*)\s*=\s*\d+/g),
          ];
          expect(memberMatches.length).toBe(enumEntry.members.length);
        });
      });
    }

    it("enum count matches spec (no extra enums not in spec)", () => {
      const matches = [
        ...dotnetSrc.matchAll(/public enum (\w+)/g),
      ].map((m) => m[1]);
      const specNames = spec.enums.map((e) => e.name).sort();
      expect([...matches].sort()).toEqual(specNames);
    });
  });
});
