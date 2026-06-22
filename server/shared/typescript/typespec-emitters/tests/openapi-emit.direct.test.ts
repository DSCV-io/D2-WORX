// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct $onEmit dispatch test for the OpenAPI emitter.
//
// V8 coverage of the OpenAPI dispatch loop in emitter.ts requires calling the
// INSTRUMENTED src $onEmit (the emit-list path runs the built dist, which the
// src/** coverage instrumentation never sees). This file mocks only
// @typespec/compiler's emitFile to capture writes (mirroring the
// route-emit.direct pattern), compiles a real @service fixture through the
// test-host so the genuine getOpenAPI3 / listServices / getAllHttpServices
// seams run, and drives $onEmit against that real program.

import { describe, it, expect, vi } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { HttpTestLibrary } from "@typespec/http/testing";
import { VersioningTestLibrary } from "@typespec/versioning/testing";
import type * as CompilerNs from "@typespec/compiler";
import type { EmitContext } from "@typespec/compiler";

// Files written through the choke-point during $onEmit.
const directOpenApiEmitted: Array<{ path: string; content: string }> = [];

// Mock ONLY emitFile (everything else stays real so getOpenAPI3 + the test-host
// compile work normally).
vi.mock("@typespec/compiler", async (importOriginal) => {
  const original = await importOriginal<typeof CompilerNs>();
  return {
    ...original,
    emitFile: async (
      _program: unknown,
      opts: { path: string; content: string },
    ) => {
      directOpenApiEmitted.push({ path: opts.path, content: opts.content });
    },
  };
});

// Import AFTER the mock registration so $onEmit's emit-file choke-point is mocked.
const { $onEmit } = await import("../src/emitter.js");

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

describe("openApiEmitDirect_OnEmitDispatch", () => {
  it("$onEmit writes the OpenAPI .g.json via the dispatch loop for a @service fixture", async () => {
    directOpenApiEmitted.length = 0;

    const host = await createTestHost({
      libraries: [
        D2DecoratorTestLibrary,
        HttpTestLibrary,
        VersioningTestLibrary,
      ],
    });
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      import "@typespec/http";
      using D2;
      using Http;

      @service(#{ title: "Dispatch Fixtures" })
      namespace DispatchFixtures {
        model PingInput { id: string; }
        model PingOutput { ok: boolean; }

        @d2Query
        @d2ServedBy("DispatchFixtures")
        @d2InProcess
        @route("/v1/dispatch/ping")
        @get
        @d2Harmless
        op ping(input: PingInput): PingOutput;
      }
      `,
    );
    // Compile WITHOUT an emit list — we drive the instrumented src $onEmit below.
    await host.compile("main.tsp");

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    await $onEmit({
      program: host.program,
      emitterOutputDir: "testing:/out",
      options: { "csharp-namespace": "D2.Test" },
    } as unknown as EmitContext);

    // The OpenAPI document was written through the $onEmit dispatch loop.
    const openApiWrite = directOpenApiEmitted.find((w) =>
      w.path.endsWith("dispatch-fixtures.openapi.g.json"),
    );
    expect(openApiWrite).toBeDefined();
    const doc = JSON.parse(openApiWrite!.content) as Record<string, unknown>;
    expect(doc["openapi"]).toBe("3.0.0");
    const pingGet = (
      (doc["paths"] as Record<string, unknown>)["/v1/dispatch/ping"] as Record<
        string,
        unknown
      >
    )["get"] as Record<string, unknown>;
    expect(pingGet["x-d2-scope"]).toEqual({ mode: "harmless" });
    expect(doc["x-d2-generated-by"]).toBeDefined();
  });
});
