// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Byte-parity gate for the emitted OpenAPI x-d2-* documents. Regenerating the
// emitter output in-process (compile the openapi-shaped fixture through the
// test-host, run emitOpenApiDocuments) must produce byte-identical content to
// the committed .g.json fixtures.
//
// The gate is non-vacuous per §26.5.1 + §1.20: a deliberate-drift case (mutate
// one token of the committed fixture) is asserted to NOT match the regenerated
// output — proving the gate FAILS on real divergence (never a buffer-vs-itself
// tautology).

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { HttpTestLibrary } from "@typespec/http/testing";
import { VersioningTestLibrary } from "@typespec/versioning/testing";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";
import {
  emitOpenApiDocuments,
  type EmittedFile,
} from "../src/lib/openapi-emitter.js";

const D2DecoratorTestLibrary = createTestLibrary({
  name: "@d2/typespec-decorators",
  packageRoot: await findTestPackageRoot(
    new URL(
      "../node_modules/@d2/typespec-decorators/package.json",
      import.meta.url,
    ).href,
  ),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

const REPO = findRepoRoot(import.meta.url);
const FIXTURE_SRC = join(
  REPO,
  "contracts/typespec/fixtures/openapi-shaped.tsp",
);
const OPENAPI_GEN = join(
  REPO,
  "private/services/edge/tests/Unit/KeyCustodian/TypeSpecOpenApi/Generated",
);

/** Committed generated files are LF; normalize the on-disk read before comparing. */
function readFixture(absPath: string): string {
  return readFileSync(absPath, "utf8").replace(/\r\n/g, "\n");
}

/** Compile the openapi-shaped fixture and return the emitted OpenAPI files. */
async function regenerate(): Promise<EmittedFile[]> {
  const host = await createTestHost({
    libraries: [D2DecoratorTestLibrary, HttpTestLibrary, VersioningTestLibrary],
  });
  host.addTypeSpecFile("main.tsp", readFileSync(FIXTURE_SRC, "utf8"));
  await host.compile("main.tsp");

  const errors = host.program.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0)
    throw new Error(
      `fixture compile produced errors: ${errors.map((e) => e.code).join(", ")}`,
    );

  return emitOpenApiDocuments(host.program);
}

describe("openApiByteParity_CommittedFixturesIdentical", () => {
  let files: EmittedFile[];

  beforeAll(async () => {
    files = await regenerate();
  });

  function regenerated(fileName: string): string {
    const file = files.find((f) => f.fileName === fileName);
    if (file === undefined) throw new Error(`not emitted: ${fileName}`);

    return file.content;
  }

  it("regenerated open-api-fixtures.openapi.g.json is byte-identical to the committed fixture", () => {
    expect(regenerated("open-api-fixtures.openapi.g.json")).toBe(
      readFixture(join(OPENAPI_GEN, "open-api-fixtures.openapi.g.json")),
    );
  });

  it("regenerated open-api-versioned-fixtures.1-0.openapi.g.json is byte-identical to the committed fixture", () => {
    expect(regenerated("open-api-versioned-fixtures.1-0.openapi.g.json")).toBe(
      readFixture(
        join(OPENAPI_GEN, "open-api-versioned-fixtures.1-0.openapi.g.json"),
      ),
    );
  });

  it("regenerated open-api-versioned-fixtures.2-0.openapi.g.json is byte-identical to the committed fixture", () => {
    expect(regenerated("open-api-versioned-fixtures.2-0.openapi.g.json")).toBe(
      readFixture(
        join(OPENAPI_GEN, "open-api-versioned-fixtures.2-0.openapi.g.json"),
      ),
    );
  });

  it("deliberate-drift detection: a mutated x-d2-scope fixture does NOT match regenerated output", () => {
    // Mutate the scope value inside x-d2-scope — the gate must catch this.
    const drifted = readFixture(
      join(OPENAPI_GEN, "open-api-fixtures.openapi.g.json"),
    ).replace("self.write", "self.writeDRIFTED");
    expect(regenerated("open-api-fixtures.openapi.g.json")).not.toBe(drifted);
  });

  it("deliberate-drift detection: a mutated version document does NOT match regenerated output", () => {
    const drifted = readFixture(
      join(OPENAPI_GEN, "open-api-versioned-fixtures.2-0.openapi.g.json"),
    ).replace("openApiExportReportFixture", "exportReportDRIFTED");
    expect(
      regenerated("open-api-versioned-fixtures.2-0.openapi.g.json"),
    ).not.toBe(drifted);
  });
});
