// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Integration tests for the `ts-client-output-dirs` production-emission target.
//
// When a @d2ServedBy module is named in the option map, the emitted TS SSR gRPC
// client (<module>-grpc-client.g.ts) AND the TS DTOs of that module's
// @d2GrpcMethod ops are ALSO written to the mapped directory (a service-owned TS
// client package's src/ folder), in addition to the standard emitter output dir.
// The mirror is co-located BY CONCERN (mirroring the .NET client): the gRPC
// client lands in `facade/`, each op's DTO in its `<concern-kebab>/` folder.
// Routing is config-only + @d2Concern-driven — a module NOT in the map is emitted
// only to the standard dir (flat).
//
// These tests drive $onEmit via the TypeSpec test host and assert the in-memory
// FS carries BOTH copies (standard + mirror) for a mapped module, the mirror
// under its concern subfolder, and only the standard copy for an unmapped module.

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";

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

/** All emitted FS keys ending with `suffix` (there may be a standard + a mirror copy). */
function emittedKeysEndingWith(
  host: Awaited<ReturnType<typeof createTestHost>>,
  suffix: string,
): string[] {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return [];
  return [...stored.keys()].filter((k) => k.endsWith(suffix));
}

const BASE_OPTIONS = {
  "csharp-namespace": "D2.Test",
  "csharp-app-namespace-base": "D2.Edge.MintFixtures.App.Application.Handlers",
};

const MINT_TSP = `
  import "@d2/typespec-decorators";
  using D2;
  namespace D2.Fixtures;

  model MintFixtureInput { @d2Field(1) csr: bytes; }
  model MintFixtureOutput { @d2Field(1) certificate: bytes; }

  @d2Command
  @d2ServedBy("MintFixtures")
  @d2Concern("MintFixture")
  @d2GrpcMethod("MintFixturesCa", "MintFixture")
  op mintFixture(input: MintFixtureInput): MintFixtureOutput;
`;

describe("tsClientOutputDirs_MirrorsClientAndDtosForMappedModule", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("a mapped module mirrors its TS gRPC client + gRPC-op DTOs to the configured dir", async () => {
    host.addTypeSpecFile("main.tsp", MINT_TSP);

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          ...BASE_OPTIONS,
          "csharp-clients-namespace": "D2.Edge.MintFixtures.Clients",
          "ts-client-output-dirs": {
            MintFixtures: "server/services/edge/mint/client-ts/src",
          },
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // The TS gRPC client is emitted twice: the standard dir + the mirror dir. The
    // mirror copy lands in the `facade/` concern folder (mirroring the .NET client).
    const clientKeys = emittedKeysEndingWith(
      host,
      "mint-fixtures-grpc-client.g.ts",
    );
    expect(clientKeys.length).toBe(2);
    expect(
      clientKeys.some((k) => k.includes("mint/client-ts/src/facade")),
    ).toBe(true);

    // The gRPC op's DTO is likewise mirrored, routed into its @d2Concern subfolder
    // (concern "MintFixture" → `mint-fixture/`) — the client's concern-relative
    // import (`../mint-fixture/mint-fixture-dto.js`) resolves exactly this file.
    const dtoKeys = emittedKeysEndingWith(host, "mint-fixture-dto.g.ts");
    expect(dtoKeys.length).toBe(2);
    expect(
      dtoKeys.some((k) => k.includes("mint/client-ts/src/mint-fixture")),
    ).toBe(true);
  });
});

describe("tsClientOutputDirs_UnmappedModuleNotMirrored", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("a module absent from the map is emitted only to the standard dir", async () => {
    host.addTypeSpecFile("main.tsp", MINT_TSP);

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          ...BASE_OPTIONS,
          "csharp-clients-namespace": "D2.Edge.MintFixtures.Clients",
          // A DIFFERENT module is mapped — MintFixtures is not, so it is not mirrored.
          "ts-client-output-dirs": {
            SomeOtherModule:
              "server/services/edge/other/client-ts/src/generated",
          },
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // Exactly one copy (the standard dir) — no mirror for the unmapped module.
    const clientKeys = emittedKeysEndingWith(
      host,
      "mint-fixtures-grpc-client.g.ts",
    );
    expect(clientKeys.length).toBe(1);
    expect(clientKeys.some((k) => k.includes("client-ts"))).toBe(false);
  });
});
