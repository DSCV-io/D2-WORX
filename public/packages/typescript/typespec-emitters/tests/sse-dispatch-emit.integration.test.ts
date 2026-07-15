// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Integration tests for the SSE dispatch emitter via the TypeSpec test-host.
//
// Compiles inline .tsp programs and asserts the in-memory FS contains the
// dispatch-wiring output (proving the $onEmit pushOpsByModule collection + the
// after-walk per-module DI-ext + the once-per-namespace seam fire):
//   1. two PURE @d2ServerPush ops (user + session channels) → both dispatcher
//      pairs, one per-module DI-ext, one shared emit-sink seam, and NO
//      I<Op>Handler for either (a pure-push op is a caller, not a server — the
//      handler is suppressed by isPurePush; the suppression-proof regression).
//   2. a @d2ServerPush op whose output is void → D2TSP008 + NO partial dispatcher.

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

// Match on the FULL basename (path + "/" + fileName) so a shorter file name is
// never a suffix of a longer one — e.g. "OrderShippedFixtureDispatcher.g.cs" is a
// suffix of "IOrderShippedFixtureDispatcher.g.cs", so a bare endsWith would collide.
function getEmittedFile(
  host: Awaited<ReturnType<typeof createTestHost>>,
  fileName: string,
): string | undefined {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return undefined;
  const key = [...stored.keys()].find((k) => k.endsWith(`/${fileName}`));
  return key !== undefined ? stored.get(key) : undefined;
}

// ---------------------------------------------------------------------------
// Two push ops (user + session) → full dispatch layer emitted
// ---------------------------------------------------------------------------

describe("sseDispatchEmitIntegration_TwoPushOps_EmitsFullDispatchLayer", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("user + session pure-push ops → both dispatcher pairs + per-module DI-ext + shared seam + NO handler", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model OrderFixtureLine { sku: string; quantity: int32; }
      model OrderShippedFixtureOutput { orderId: string; shippedAt: utcDateTime; lines: OrderFixtureLine[]; }
      model SessionExpiringFixtureOutput { sessionId: string; expiresAt: utcDateTime; }

      @d2Command
      @d2ServedBy("PushFixtures")
      @d2ServerPush("user")
      op orderShippedFixture(): OrderShippedFixtureOutput;

      @d2Command
      @d2ServedBy("PushFixtures")
      @d2ServerPush("session")
      op sessionExpiringFixture(): SessionExpiringFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Edge.Tests.TypeSpecSse.Generated",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // User-channel dispatcher pair.
    const userIface = getEmittedFile(
      host,
      "IOrderShippedFixtureDispatcher.g.cs",
    );
    expect(userIface).toBeDefined();
    expect(userIface).toContain(
      "public interface IOrderShippedFixtureDispatcher",
    );
    const userImpl = getEmittedFile(host, "OrderShippedFixtureDispatcher.g.cs");
    expect(userImpl).toBeDefined();
    expect(userImpl).toContain(
      "new D2GeneratedSseChannelTarget(D2GeneratedSseChannelClass.User, targetId),",
    );
    expect(userImpl).toContain('"orderShippedFixture", payload, ct);');

    // Session-channel dispatcher pair — the Session arm is baked, non-vacuous.
    const sessionImpl = getEmittedFile(
      host,
      "SessionExpiringFixtureDispatcher.g.cs",
    );
    expect(sessionImpl).toBeDefined();
    expect(sessionImpl).toContain(
      "new D2GeneratedSseChannelTarget(D2GeneratedSseChannelClass.Session, targetId),",
    );
    expect(sessionImpl).toContain('"sessionExpiringFixture", payload, ct);');

    // Per-module DI-ext — one AddTransient per op.
    const diExt = getEmittedFile(
      host,
      "PushFixturesSseDispatchersGenerated.g.cs",
    );
    expect(diExt).toBeDefined();
    expect(diExt).toContain(
      "public IServiceCollection AddD2PushFixturesSseDispatchers()",
    );
    expect(diExt).toContain(
      "services.AddTransient<IOrderShippedFixtureDispatcher, OrderShippedFixtureDispatcher>();",
    );
    expect(diExt).toContain(
      "services.AddTransient<ISessionExpiringFixtureDispatcher, SessionExpiringFixtureDispatcher>();",
    );

    // The emit-sink seam — emitted ONCE for the namespace (family: enum + struct + interface).
    const seam = getEmittedFile(host, "D2GeneratedSseEmitSink.g.cs");
    expect(seam).toBeDefined();
    expect(seam).toContain("public enum D2GeneratedSseChannelClass");
    expect(seam).toContain(
      "public readonly record struct D2GeneratedSseChannelTarget(",
    );
    expect(seam).toContain("ValueTask<D2Result> EmitAsync<TPayload>(");

    // The payload DTO carries the temporal field + nested model (walkModel integration).
    const payloadDto = getEmittedFile(host, "OrderShippedFixtureOutput.g.cs");
    expect(payloadDto).toBeDefined();
    expect(payloadDto).toContain("DateTimeOffset ShippedAt");
    expect(payloadDto).toContain("IReadOnlyList<OrderFixtureLine> Lines");
    expect(payloadDto).toContain("public sealed record OrderFixtureLine(");

    // Suppression proof: a pure-push op is a caller, not a request server —
    // it gets ONLY the dispatcher surface, NO I<Op>Handler. The handler is gated
    // out by isPurePush. (A combined push + request-exposure op still gets one;
    // that selective branch is covered in sse-emit.direct.test.ts.)
    expect(getEmittedFile(host, "IOrderShippedHandler.g.cs")).toBeUndefined();
    expect(
      getEmittedFile(host, "ISessionExpiringHandler.g.cs"),
    ).toBeUndefined();

    // Suppression proof: a pure-push op emits only its output payload DTO —
    // the input side is suppressed (dtoInputModel = undefined for isPurePush ops),
    // so no orphan parameterless <Op>Input record is emitted. Net committed set per
    // pure-push op: output DTO + dispatcher pair + (shared) seam + DI; NO input DTO.
    expect(getEmittedFile(host, "OrderShippedInput.g.cs")).toBeUndefined();
    expect(getEmittedFile(host, "SessionExpiringInput.g.cs")).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Void-output push op → D2TSP008 + no partial dispatcher
// ---------------------------------------------------------------------------

describe("sseDispatchEmitIntegration_VoidOutputPush_D2TSP008NoPartial", () => {
  it("a @d2ServerPush op with a void output → D2TSP008 fired, no dispatcher emitted", async () => {
    const badHost = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
    badHost.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model PingInput { id: string; }

      @d2Command
      @d2ServedBy("PushFixtures")
      @d2ServerPush("user")
      op ping(input: PingInput): void;
      `,
    );

    let compileError: unknown = undefined;
    try {
      await badHost.compile("main.tsp", {
        emit: ["@d2/typespec-emitters"],
        options: {
          "@d2/typespec-emitters": {
            "csharp-namespace": "D2.Edge.Tests.TypeSpecSse.Generated",
          },
        },
        outputDir: "testing:/out",
      });
    } catch (err) {
      compileError = err;
    }

    const programErrors = badHost.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    // D2TSP008 (server-push-requires-payload) is an error-severity diagnostic.
    expect(compileError !== undefined || programErrors.length > 0).toBe(true);
    const hasPushPayloadDiag = badHost.program.diagnostics.some((d) =>
      String(d.code).includes("server-push-requires-payload"),
    );
    expect(hasPushPayloadDiag).toBe(true);
    expect(badHost.program.hasError()).toBe(true);

    // No partial dispatcher emitted for the failing op.
    expect(getEmittedFile(badHost, "IPingDispatcher.g.cs")).toBeUndefined();
    expect(getEmittedFile(badHost, "PingDispatcher.g.cs")).toBeUndefined();
  });

  it("a @d2ServerPush op with an empty-record output → D2TSP008 fired, no dispatcher emitted", async () => {
    const badHost = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
    badHost.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model SignalInput { id: string; }
      model SignalOutput {}

      @d2Command
      @d2ServedBy("PushFixtures")
      @d2ServerPush("session")
      op signal(input: SignalInput): SignalOutput;
      `,
    );

    let compileError: unknown = undefined;
    try {
      await badHost.compile("main.tsp", {
        emit: ["@d2/typespec-emitters"],
        options: {
          "@d2/typespec-emitters": {
            "csharp-namespace": "D2.Edge.Tests.TypeSpecSse.Generated",
          },
        },
        outputDir: "testing:/out",
      });
    } catch (err) {
      compileError = err;
    }

    const programErrors = badHost.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(compileError !== undefined || programErrors.length > 0).toBe(true);
    const hasPushPayloadDiag = badHost.program.diagnostics.some((d) =>
      String(d.code).includes("server-push-requires-payload"),
    );
    expect(hasPushPayloadDiag).toBe(true);
    expect(badHost.program.hasError()).toBe(true);

    // No partial dispatcher emitted for the empty-output op.
    expect(getEmittedFile(badHost, "ISignalDispatcher.g.cs")).toBeUndefined();
    expect(getEmittedFile(badHost, "SignalDispatcher.g.cs")).toBeUndefined();
  });
});
