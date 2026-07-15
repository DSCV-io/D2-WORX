// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Compile-driven tests for walkModel's @encodedName → FieldInfo.jsonName read.
//
// Unlike model-walk.test.ts (mock Program stubs), these compile a REAL TypeSpec
// program so resolveEncodedName(program, prop, "application/json") reads the
// stock @encodedName decorator state. Covers:
//   1. A property carrying @encodedName("application/json","jwks_uri") whose
//      override DIFFERS from the default camelCase wire name → jsonName set.
//   2. A property whose @encodedName override EQUALS the default camelCase wire
//      name → jsonName === undefined (the differs-from-default guard — no
//      attribute, existing generated output stays byte-identical).
//   3. A property with NO @encodedName → jsonName === undefined.
//   4. @encodedName on an array field → jsonName threaded onto the array FieldInfo.

import { describe, it, expect } from "vitest";
import type { Model, Program } from "@typespec/compiler";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { walkModel } from "../src/lib/model-walk.js";
import type { FieldInfo } from "../src/lib/model-walk.js";

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

const D2EmitterTestLibrary = createTestLibrary({
  name: "@d2/typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

/** Compile an inline .tsp model and return its walked FieldInfo list. */
async function walkInlineModel(
  modelBody: string,
): Promise<readonly FieldInfo[]> {
  const host = await createTestHost({
    libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
  });
  host.addTypeSpecFile(
    "main.tsp",
    `
    import "@d2/typespec-decorators";
    using D2;
    namespace D2.WalkTest;

    ${modelBody}
    `,
  );
  await host.compile("main.tsp", { outputDir: "testing:/out" });

  const program: Program = host.program;
  const errors = program.diagnostics.filter((d) => d.severity === "error");
  expect(errors).toHaveLength(0);

  const walkTestNs = program
    .getGlobalNamespaceType()
    .namespaces.get("D2")
    ?.namespaces.get("WalkTest");
  const model = walkTestNs?.models.get("Target") as Model | undefined;
  if (model === undefined) throw new Error("Target model not found");

  const { fields } = walkModel(program, model, () => {});
  return fields;
}

describe("walkModel_EncodedName_DiffersFromDefault_PopulatesJsonName", () => {
  it("@encodedName override differing from the camelCase default → jsonName set", async () => {
    const fields = await walkInlineModel(
      `
      model Target {
        @encodedName("application/json", "jwks_uri")
        jwksUri: string;
      }
      `,
    );

    expect(fields).toHaveLength(1);
    expect(fields[0]!.csName).toBe("JwksUri");
    expect(fields[0]!.jsonName).toBe("jwks_uri");
  });

  it("@encodedName on an array field → jsonName threaded onto the array FieldInfo", async () => {
    const fields = await walkInlineModel(
      `
      model Target {
        @encodedName("application/json", "id_token_signing_alg_values_supported")
        idTokenSigningAlgValuesSupported: string[];
      }
      `,
    );

    expect(fields).toHaveLength(1);
    expect(fields[0]!.repeated).toBe(true);
    expect(fields[0]!.jsonName).toBe("id_token_signing_alg_values_supported");
  });
});

describe("walkModel_EncodedName_EqualsDefaultOrAbsent_JsonNameUndefined", () => {
  it("a property with no @encodedName → jsonName undefined (the differs-from-default guard)", async () => {
    // resolveEncodedName returns the property's own lowerCamel TypeSpec name when
    // there is no @encodedName; that equals the System.Text.Json camelCase default
    // for the PascalCase csName, so resolveJsonName returns undefined → NO
    // [JsonPropertyName] attribute → existing generated output stays byte-identical.
    // (TypeSpec itself rejects an @encodedName whose value restates the member
    // name — encoded-name-conflict — so a redundant explicit override cannot even
    // be authored; this no-override path is the guard's live protection.)
    const fields = await walkInlineModel(
      `
      model Target {
        issuer: string;
      }
      `,
    );

    expect(fields).toHaveLength(1);
    expect(fields[0]!.jsonName).toBeUndefined();
  });

  it("a nested-model property's @encodedName is honored depth-agnostically", async () => {
    const fields = await walkInlineModel(
      `
      model Inner {
        @encodedName("application/json", "wire_field")
        wireField: string;
      }
      model Target {
        inner: Inner;
      }
      `,
    );

    // The Target's single field is the nested Inner; its nested field carries
    // the jsonName (collectNested reuses resolveProperty → depth-agnostic).
    expect(fields).toHaveLength(1);
    const innerFields = fields[0]!.nested?.fields ?? [];
    expect(innerFields).toHaveLength(1);
    expect(innerFields[0]!.jsonName).toBe("wire_field");
  });
});
