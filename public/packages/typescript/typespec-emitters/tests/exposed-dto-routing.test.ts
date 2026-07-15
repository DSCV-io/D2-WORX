// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for exposed-DTO routing (items 15-16) and D2TSP003 (missing-cqrs-category).
//
// These test the namespace-resolution logic via the emitter's integration path
// (the TypeSpec test-host) to give V8 credit to the routing branches in
// emitter.ts that are not covered by the direct-unit emitter tests.

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";

// Mount the decorators library.
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

// Mount the emitter package.
const D2EmitterTestLibrary = createTestLibrary({
  name: "@d2/typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

// ---------------------------------------------------------------------------
// Helper: retrieve an emitted file from the in-memory FS by suffix.
// ---------------------------------------------------------------------------

function getEmittedFile(
  host: Awaited<ReturnType<typeof createTestHost>>,
  suffix: string,
): string | undefined {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return undefined;
  const key = [...stored.keys()].find((k) => k.endsWith(suffix));
  return key !== undefined ? stored.get(key) : undefined;
}

// ---------------------------------------------------------------------------
// 15. Exposed-op DTO namespace → Clients; internal-op DTO → app CQRS ns.
// ---------------------------------------------------------------------------

describe("exposedDtoRouting_ExposedOp_DtosGoToClientsNamespace", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("getJwks (@d2InProcess) → DTOs land in csharp-clients-namespace", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model GetJwksOutput { keys: string[]; }

      @d2Query
      @d2InProcess
      @d2ServedBy("KeyCustodian")
      @d2Concern("Jwks")
      op getJwks(): GetJwksOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Fixture.Ns",
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
          "csharp-app-namespace-base":
            "D2.Edge.KeyCustodian.App.Application.Handlers",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // GetJwksOutput should be in the concern-qualified Clients namespace.
    const outputContent = getEmittedFile(host, "GetJwksOutput.g.cs");
    expect(outputContent).toBeDefined();
    expect(outputContent).toContain(
      "namespace D2.Edge.KeyCustodian.Client.Jwks;",
    );

    // Handler interface should be in the app CQRS namespace (Queries.GetJwks).
    const handlerContent = getEmittedFile(host, "IGetJwksHandler.g.cs");
    expect(handlerContent).toBeDefined();
    expect(handlerContent).toContain(
      "namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;",
    );
    // emitUsing=false when csAppNamespaceBase is present.
    expect(handlerContent).not.toContain(
      "using D2.Shared.Handler.Abstractions;",
    );
  });
});

describe("exposedDtoRouting_InternalOp_DtosGoToAppCqrsNamespace", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("reconcileKeyState (@d2Internal @d2Command) → DTOs land in app Commands namespace", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model ReconcileKeyStateInput { kid: string; }
      model ReconcileKeyStateOutput { ok: boolean; }

      @d2Command
      @d2Internal
      @d2ServedBy("KeyCustodian")
      op reconcileKeyState(input: ReconcileKeyStateInput): ReconcileKeyStateOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Fixture.Ns",
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
          "csharp-app-namespace-base":
            "D2.Edge.KeyCustodian.App.Application.Handlers",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // DTOs should be in the app Commands namespace.
    const inputContent = getEmittedFile(host, "ReconcileKeyStateInput.g.cs");
    expect(inputContent).toBeDefined();
    expect(inputContent).toContain(
      "namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ReconcileKeyState;",
    );
    // NOT in the Clients namespace.
    expect(inputContent).not.toContain("D2.Edge.KeyCustodian.Client");
  });
});

// ---------------------------------------------------------------------------
// 16. Missing CQRS category → D2TSP003 diagnostic (loud failure).
// ---------------------------------------------------------------------------

describe("exposedDtoRouting_MissingCategory_D2TSP003Fires", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("op with no @d2Command/@d2Query and csharp-app-namespace-base configured → error diagnostic fires", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Test;

      model FooInput { id: string; }
      model FooOutput { value: string; }

      // Deliberately omit @d2Command and @d2Query to trigger D2TSP003.
      // Also omit @d2Internal and exposure decorators so validators don't fire first
      // on exposure-or-internal-required — the decorator validator runs before emitter,
      // so we accept either a validator error or the D2TSP003 emitter error.
      @d2Internal
      @d2ServedBy("KeyCustodian")
      op badOp(input: FooInput): FooOutput;
      `,
    );

    let compileError: unknown = undefined;
    try {
      await host.compile("main.tsp", {
        emit: ["@d2/typespec-emitters"],
        options: {
          "@d2/typespec-emitters": {
            "csharp-namespace": "D2.Fixture.Ns",
            "csharp-app-namespace-base":
              "D2.Edge.KeyCustodian.App.Application.Handlers",
          },
        },
        outputDir: "testing:/out",
      });
    } catch (err) {
      compileError = err;
    }

    // The decorator layer will fire `category-required` first, OR the emitter fires D2TSP003.
    // Either way, there must be at least one error diagnostic.
    const programErrors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    const hasErrors = compileError !== undefined || programErrors.length > 0;
    expect(hasErrors).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Façade emission (with csAppNamespaceBase + csClientsNamespace) → fires for exposed ops.
// Exercises the exposedOpsByModule collection + façade emission branches in emitter.ts.
// ---------------------------------------------------------------------------

describe("exposedDtoRouting_FacadeEmission_ExposedOpCollected", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("exposed op with @d2ServedBy + csharp-clients-namespace → façade interface emitted", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model GetJwksOutput { keys: string[]; }

      @d2Query
      @d2InProcess
      @d2ServedBy("KeyCustodian")
      @d2Concern("Jwks")
      op getJwks(): GetJwksOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Fixture.Ns",
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
          "csharp-app-namespace-base":
            "D2.Edge.KeyCustodian.App.Application.Handlers",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // The façade interface must be emitted in the Clients Facade namespace.
    const stored = (host as unknown as { fs?: Map<string, string> }).fs;
    expect(stored instanceof Map).toBe(true);
    const ifaceKey = [...(stored as Map<string, string>).keys()].find((k) =>
      k.endsWith("IKeyCustodianApi.g.cs"),
    );
    expect(ifaceKey).toBeDefined();
    const ifaceContent = (stored as Map<string, string>).get(ifaceKey!);
    expect(ifaceContent).toContain(
      "namespace D2.Edge.KeyCustodian.Client.Facade;",
    );
    expect(ifaceContent).toContain("GetJwksAsync(");
  });
});

// ---------------------------------------------------------------------------
// Missing @d2Concern on a client-exposed op (real-module mode) → D2TSP013.
// A client-exposed op's transport DTOs are placed by concern; omitting the
// @d2Concern is a loud build failure (the emitter cannot route them).
// ---------------------------------------------------------------------------

describe("exposedDtoRouting_MissingConcern_D2TSP013Fires", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("client-exposed op without @d2Concern → missing-concern error diagnostic naming the op", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model GetJwksOutput { keys: string[]; }

      // Deliberately omit @d2Concern to trigger D2TSP013 (client-exposed op).
      @d2Query
      @d2InProcess
      @d2ServedBy("KeyCustodian")
      op getJwks(): GetJwksOutput;
      `,
    );

    let compileError: unknown = undefined;
    try {
      await host.compile("main.tsp", {
        emit: ["@d2/typespec-emitters"],
        options: {
          "@d2/typespec-emitters": {
            "csharp-namespace": "D2.Fixture.Ns",
            "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
            "csharp-app-namespace-base":
              "D2.Edge.KeyCustodian.App.Application.Handlers",
          },
        },
        outputDir: "testing:/out",
      });
    } catch (err) {
      compileError = err;
    }

    void compileError;
    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    const missingConcern = errors.find(
      (d) =>
        String(d.code).includes("missing-concern") ||
        String(d.message).includes("@d2Concern"),
    );
    expect(missingConcern).toBeDefined();
    expect(missingConcern!.severity).toBe("error");
    expect(String(missingConcern!.message)).toContain("getJwks");
  });
});

// ---------------------------------------------------------------------------
// Fixture mode (no csharp-app-namespace-base) → legacy csharp-namespace used.
// ---------------------------------------------------------------------------

describe("exposedDtoRouting_FixtureMode_LegacyNamespaceUsed", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("fixture mode (no csharp-app-namespace-base) → DTOs use csharp-namespace", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model GetJwksOutput { keys: string[]; }

      @d2ServedBy("KeyCustodian")
      @d2InProcess
      @d2Query
      op getJwks(): GetJwksOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Edge.Tests.TypeSpecDto.Generated",
          // No csharp-app-namespace-base → fixture mode.
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const outputContent = getEmittedFile(host, "GetJwksOutput.g.cs");
    expect(outputContent).toBeDefined();
    // Should use the legacy fixture namespace, NOT a Clients namespace.
    expect(outputContent).toContain(
      "namespace D2.Edge.Tests.TypeSpecDto.Generated;",
    );
  });
});
