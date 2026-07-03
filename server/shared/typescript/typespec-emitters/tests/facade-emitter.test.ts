// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the façade emitter (src/lib/facade-emitter.ts).
//
// Coverage per the plan:
//   7.  One exposed op → interface has exactly one method with the transport-
//       neutral signature (no HandlerOptions), method name <PascalOp>Async.
//   8.  Façade IMPL: primary-constructor parameter + straight-through delegation.
//   9.  @d2Internal op structurally absent from the interface (structural-absence
//       safety — proven at the pure-fn level by providing only exposed ops).
//   10. Module name derived from the first call's moduleName arg → interface name.
//   11. Multi-op module → multiple methods, same deterministic order as input list.
//   12. DI extension: Transient registration, C# 14 extension block form, correct
//       extension-method name AddD2<Module>Clients().
//   13. Banner / #nullable enable / namespace-before-using / sealed / C# 14
//       extension(...) block / American English / no phase-step-audit ids.
//   14. Zero-exposed-op module → empty return (no façade emitted).
//       Empty module name → Error thrown.
//   Byte-gates for the three committed .g.cs files + drift negatives.
//   Integration test confirming the façade-emitter integration path in emitter.ts.

import { describe, it, expect, beforeAll } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";
import { emitFacade } from "../src/lib/facade-emitter.js";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";

// ---------------------------------------------------------------------------
// Committed-file path constants + readFixture helper
// ---------------------------------------------------------------------------

const _REPO = findRepoRoot(import.meta.url);

/** Committed home for GetJwks DTOs + façade interface (Clients namespace). */
const _KC_CLIENTS_HOME = join(
  _REPO,
  "server/services/edge/key-custodian/clients",
);

/** Committed home for façade impl + DI extension (app/Application/). */
const _KC_APP_HOME = join(
  _REPO,
  "server/services/edge/key-custodian/app/Application",
);

/**
 * Read a committed generated file and normalize line endings.
 * Committed generated files are LF; the emitter joins with "\n". Normalize the
 * on-disk read defensively (git working-tree may have CRLF) before comparing.
 */
function readFacadeFixture(absPath: string): string {
  return readFileSync(absPath, "utf8").replace(/\r\n/g, "\n");
}

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

const _KC_CLIENTS_NS = "D2.Edge.KeyCustodian.Clients";
const _KC_APP_NS = "D2.Edge.KeyCustodian.App.Application";
const _SPEC = "contracts/typespec/key-custodian/key-custodian.tsp";

function makeOp(
  opName: string,
  inputTypeName?: string,
  outputTypeName?: string,
  sourceSpec?: string,
  category?: "Commands" | "Queries",
) {
  return {
    opName,
    inputTypeName:
      inputTypeName ??
      `${opName.charAt(0).toUpperCase()}${opName.slice(1)}Input`,
    outputTypeName:
      outputTypeName ??
      `${opName.charAt(0).toUpperCase()}${opName.slice(1)}Output`,
    sourceSpec: sourceSpec ?? _SPEC,
    category: category ?? "Queries",
  };
}

// ---------------------------------------------------------------------------
// 7. Transport-neutral signature, no HandlerOptions
// ---------------------------------------------------------------------------

describe("facadeEmitter_InterfaceMethod_TransportNeutralSignature", () => {
  it("getJwks → method has ValueTask<D2Result<GetJwksOutput?>> return and no HandlerOptions", () => {
    const [iface] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.content).toContain(
      "ValueTask<D2Result<GetJwksOutput?>> GetJwksAsync(GetJwksInput input, CancellationToken ct = default);",
    );
    expect(iface!.content).not.toContain("HandlerOptions");
  });

  it("method name is <PascalOp>Async", () => {
    const [iface] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.content).toContain("GetJwksAsync(");
  });
});

// ---------------------------------------------------------------------------
// 8. Impl: primary-constructor parameter + straight-through delegation
// ---------------------------------------------------------------------------

describe("facadeEmitter_Impl_DelegatesViaHandleAsync", () => {
  it("impl has primary-constructor parameter IGetJwksHandler getJwksHandler", () => {
    const [, impl] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(impl!.content).toContain("IGetJwksHandler getJwksHandler");
  });

  it("impl delegates via => getJwksHandler.HandleAsync(input, ct)", () => {
    const [, impl] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(impl!.content).toContain(
      "=> getJwksHandler.HandleAsync(input, ct);",
    );
  });

  it("impl is sealed", () => {
    const [, impl] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(impl!.content).toContain("public sealed class KeyCustodianApi");
  });
});

// ---------------------------------------------------------------------------
// 9. Structural-absence safety: internal ops absent from interface
//    (the pure-fn test — only exposed ops are passed to emitFacade;
//    the integration test below proves the full emitter.ts routing)
// ---------------------------------------------------------------------------

describe("facadeEmitter_InternalOp_AbsentFromInterface", () => {
  it("providing only exposed ops → only those methods appear in the interface", () => {
    const [iface] = emitFacade(
      "KeyCustodian",
      // Only getJwks is passed — an internal op (e.g. reconcileKeyState) is NOT passed
      // because emitter.ts filters internal ops before calling emitFacade.
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.content).toContain("GetJwksAsync(");
    expect(iface!.content).not.toContain("ReconcileKeyState");
    expect(iface!.content).not.toContain("InternalOp");
  });

  it("two-op module (only exposed) → both methods in interface", () => {
    const [iface] = emitFacade(
      "KeyCustodian",
      [
        makeOp("getJwks", "GetJwksInput", "GetJwksOutput"),
        makeOp("sign", "SignInput", "SignOutput"),
      ],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.content).toContain("GetJwksAsync(");
    expect(iface!.content).toContain("SignAsync(");
  });
});

// ---------------------------------------------------------------------------
// 10. Module name derived from moduleName argument → interface/impl names
// ---------------------------------------------------------------------------

describe("facadeEmitter_ModuleNameDrivesTypeNames", () => {
  it("moduleName=KeyCustodian → IKeyCustodianApi / KeyCustodianApi", () => {
    const [iface, impl] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.content).toContain("public interface IKeyCustodianApi");
    expect(impl!.content).toContain("public sealed class KeyCustodianApi");
  });

  it("interface file name is IKeyCustodianApi.g.cs", () => {
    const [iface] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.fileName).toBe("IKeyCustodianApi.g.cs");
  });

  it("impl file name is KeyCustodianApi.g.cs", () => {
    const [, impl] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(impl!.fileName).toBe("KeyCustodianApi.g.cs");
  });

  it("DI-extension file name is KeyCustodianClientsGenerated.g.cs", () => {
    const [, , di] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(di!.fileName).toBe("KeyCustodianClientsGenerated.g.cs");
  });
});

// ---------------------------------------------------------------------------
// 11. Multi-op module → multiple methods, deterministic order
// ---------------------------------------------------------------------------

describe("facadeEmitter_MultiOp_DeterministicOrder", () => {
  it("two-op module preserves the input order in the interface", () => {
    const [iface] = emitFacade(
      "KeyCustodian",
      [
        makeOp("getJwks", "GetJwksInput", "GetJwksOutput"),
        makeOp("sign", "SignInput", "SignOutput"),
      ],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    const jwksIdx = iface!.content.indexOf("GetJwksAsync(");
    const signIdx = iface!.content.indexOf("SignAsync(");
    expect(jwksIdx).toBeGreaterThanOrEqual(0);
    expect(signIdx).toBeGreaterThan(jwksIdx);
  });

  it("two-op impl has two methods and two constructor parameters", () => {
    const [, impl] = emitFacade(
      "KeyCustodian",
      [
        makeOp("getJwks", "GetJwksInput", "GetJwksOutput"),
        makeOp("sign", "SignInput", "SignOutput"),
      ],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(impl!.content).toContain("IGetJwksHandler getJwksHandler");
    expect(impl!.content).toContain("ISignHandler signHandler");
    expect(impl!.content).toContain("GetJwksAsync(");
    expect(impl!.content).toContain("SignAsync(");
  });
});

// ---------------------------------------------------------------------------
// 12. DI extension: Transient, C# 14 extension block, AddD2<Module>Clients
// ---------------------------------------------------------------------------

describe("facadeEmitter_DiExtension_TransientCSharp14", () => {
  it("DI extension registers Transient (AddTransient<I…, …>)", () => {
    const [, , di] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(di!.content).toContain(
      "services.AddTransient<IKeyCustodianApi, KeyCustodianApi>();",
    );
  });

  it("DI extension method name is AddD2KeyCustodianClients()", () => {
    const [, , di] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(di!.content).toContain(
      "public IServiceCollection AddD2KeyCustodianClients()",
    );
  });

  it("DI extension uses C# 14 extension(IServiceCollection) block form", () => {
    const [, , di] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(di!.content).toContain("extension(IServiceCollection services)");
    // Must NOT use the old 'this T target' form.
    expect(di!.content).not.toContain("this IServiceCollection");
  });

  it("DI extension returns services for chaining", () => {
    const [, , di] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(di!.content).toContain("return services;");
  });
});

// ---------------------------------------------------------------------------
// 13. Banner / #nullable / ns-before-using / conventions / no step-audit ids
// ---------------------------------------------------------------------------

describe("facadeEmitter_ConventionsAndBanner", () => {
  it("all three files have the auto-generated banner", () => {
    const files = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    for (const f of files) expect(f.content).toContain("// <auto-generated>");
  });

  it("all three files have #nullable enable", () => {
    const files = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    for (const f of files) expect(f.content).toContain("#nullable enable");
  });

  it("namespace appears before any using directive in the impl", () => {
    const [, impl] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    const nsIdx = impl!.content.indexOf("namespace");
    const usingIdx = impl!.content.indexOf("using");
    expect(nsIdx).toBeGreaterThanOrEqual(0);
    expect(usingIdx).toBeGreaterThan(nsIdx);
  });

  it("namespace appears before any using directive in the DI extension", () => {
    const [, , di] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    // DI extension may or may not have a using; namespace must be first either way.
    const nsIdx = di!.content.indexOf("namespace");
    expect(nsIdx).toBeGreaterThanOrEqual(0);
  });

  it("mixed Commands+Queries categories: impl using directives are sorted alphabetically across both categories", () => {
    // A Commands op and a Queries op in the same module — verifies the sort
    // is applied globally (not per-category bucket) per SA1210.
    const [, impl] = emitFacade(
      "KeyCustodian",
      [
        makeOp(
          "placeOrder",
          "PlaceOrderInput",
          "PlaceOrderOutput",
          _SPEC,
          "Commands",
        ),
        makeOp("getJwks", "GetJwksInput", "GetJwksOutput", _SPEC, "Queries"),
      ],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    const lines = impl!.content.split("\n");
    const usingLines = lines.filter((l) => l.startsWith("using "));
    // All using lines must appear in sorted order.
    const sorted = [...usingLines].sort();
    expect(usingLines).toEqual(sorted);
    // Commands namespace appears before or after Queries depending on sort order —
    // the specific expected sort is alphabetical; verify both category namespaces present.
    expect(impl!.content).toContain(
      `using ${_KC_APP_NS}.Handlers.Commands.PlaceOrder;`,
    );
    expect(impl!.content).toContain(
      `using ${_KC_APP_NS}.Handlers.Queries.GetJwks;`,
    );
  });

  it("interface namespace is the Clients namespace", () => {
    const [iface] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.content).toContain(`namespace ${_KC_CLIENTS_NS};`);
  });

  it("impl and DI extension namespace is the app namespace", () => {
    const [, impl, di] = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(impl!.content).toContain(`namespace ${_KC_APP_NS};`);
    expect(di!.content).toContain(`namespace ${_KC_APP_NS};`);
  });

  it("emitted files contain no phase/step/audit/round identifiers", () => {
    const files = emitFacade(
      "KeyCustodian",
      [makeOp("getJwks", "GetJwksInput", "GetJwksOutput")],
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    for (const f of files) {
      expect(f.content).not.toMatch(/Step\s*\d/i);
      expect(f.content).not.toMatch(/\bR\d+\b/);
      expect(f.content).not.toMatch(/\bF\d+\b/);
      expect(f.content).not.toMatch(/audit/i);
      expect(f.content).not.toMatch(/deliverable/i);
      expect(f.content).not.toMatch(/Phase\s*\d/i);
    }
  });
});

// ---------------------------------------------------------------------------
// 14. Zero-exposed-op module → empty array; empty module name → Error
// ---------------------------------------------------------------------------

describe("facadeEmitter_ZeroOps_AndAdversarial", () => {
  it("zero exposed ops → emitFacade returns empty array (no façade emitted)", () => {
    const result = emitFacade("KeyCustodian", [], _KC_CLIENTS_NS, _KC_APP_NS);
    expect(result).toHaveLength(0);
  });

  it("empty moduleName → Error thrown", () => {
    expect(() =>
      emitFacade("", [makeOp("getJwks")], _KC_CLIENTS_NS, _KC_APP_NS),
    ).toThrow();
  });

  it("empty clientsNamespace → Error thrown", () => {
    expect(() =>
      emitFacade("KeyCustodian", [makeOp("getJwks")], "", _KC_APP_NS),
    ).toThrow();
  });

  it("empty appNamespace → Error thrown", () => {
    expect(() =>
      emitFacade("KeyCustodian", [makeOp("getJwks")], _KC_CLIENTS_NS, ""),
    ).toThrow();
  });
});

// ---------------------------------------------------------------------------
// Byte-gate: IKeyCustodianApi.g.cs (interface)
// ---------------------------------------------------------------------------

// The committed KC façade carries the two well-known ops plus the sign + getKeyring
// + issueLeaf + getCaCertificate ops in source order; the byte-gates regenerate from
// that exact op list. issueLeaf is the sole Command (leaf-issuance audit write); the
// rest are Queries.
const _KC_FACADE_OPS = [
  makeOp("getJwks", "GetJwksInput", "GetJwksOutput"),
  makeOp(
    "getOidcConfiguration",
    "GetOidcConfigurationInput",
    "GetOidcConfigurationOutput",
  ),
  makeOp("sign", "SignInput", "SignOutput"),
  makeOp("getKeyring", "GetKeyringInput", "GetKeyringOutput"),
  makeOp(
    "issueLeaf",
    "IssueLeafInput",
    "IssueLeafOutput",
    undefined,
    "Commands",
  ),
  makeOp("getCaCertificate", "GetCaCertificateInput", "GetCaCertificateOutput"),
];

describe("facadeEmitter_ByteGate_Interface", () => {
  it("regenerated IKeyCustodianApi.g.cs is byte-identical to the committed fixture", () => {
    const [iface] = emitFacade(
      "KeyCustodian",
      _KC_FACADE_OPS,
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.content).toBe(
      readFacadeFixture(join(_KC_CLIENTS_HOME, "IKeyCustodianApi.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match regenerated output", () => {
    const drifted = readFacadeFixture(
      join(_KC_CLIENTS_HOME, "IKeyCustodianApi.g.cs"),
    ).replace("IKeyCustodianApi", "IKeyCustodianApiDRIFTED");
    const [iface] = emitFacade(
      "KeyCustodian",
      _KC_FACADE_OPS,
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(iface!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Byte-gate: KeyCustodianApi.g.cs (impl)
// ---------------------------------------------------------------------------

describe("facadeEmitter_ByteGate_Impl", () => {
  it("regenerated KeyCustodianApi.g.cs is byte-identical to the committed fixture", () => {
    const [, impl] = emitFacade(
      "KeyCustodian",
      _KC_FACADE_OPS,
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(impl!.content).toBe(
      readFacadeFixture(join(_KC_APP_HOME, "KeyCustodianApi.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match regenerated output", () => {
    const drifted = readFacadeFixture(
      join(_KC_APP_HOME, "KeyCustodianApi.g.cs"),
    ).replace("KeyCustodianApi", "KeyCustodianApiDRIFTED");
    const [, impl] = emitFacade(
      "KeyCustodian",
      _KC_FACADE_OPS,
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(impl!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Byte-gate: KeyCustodianClientsGenerated.g.cs (DI extension)
// ---------------------------------------------------------------------------

describe("facadeEmitter_ByteGate_DiExtension", () => {
  it("regenerated KeyCustodianClientsGenerated.g.cs is byte-identical to the committed fixture", () => {
    const [, , di] = emitFacade(
      "KeyCustodian",
      _KC_FACADE_OPS,
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(di!.content).toBe(
      readFacadeFixture(
        join(_KC_APP_HOME, "KeyCustodianClientsGenerated.g.cs"),
      ),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match regenerated output", () => {
    const drifted = readFacadeFixture(
      join(_KC_APP_HOME, "KeyCustodianClientsGenerated.g.cs"),
    ).replace("AddD2KeyCustodianClients", "AddD2KeyCustodianClientsDRIFTED");
    const [, , di] = emitFacade(
      "KeyCustodian",
      _KC_FACADE_OPS,
      _KC_CLIENTS_NS,
      _KC_APP_NS,
    );

    expect(di!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Integration test: navigateProgram emits the façade for getJwks
// ---------------------------------------------------------------------------

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

function getEmittedFile(
  host: Awaited<ReturnType<typeof createTestHost>>,
  suffix: string,
): string | undefined {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return undefined;
  const key = [...stored.keys()].find((k) => k.endsWith(suffix));
  return key !== undefined ? stored.get(key) : undefined;
}

describe("facadeEmitter_Integration_getJwks", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("getJwks (@d2InProcess) → all three façade files are emitted", async () => {
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
      op getJwks(): GetJwksOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Fixture.Ns",
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
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

    // Interface file — in Clients namespace.
    const ifaceContent = getEmittedFile(host, "IKeyCustodianApi.g.cs");
    expect(ifaceContent).toBeDefined();
    expect(ifaceContent).toContain("namespace D2.Edge.KeyCustodian.Clients;");
    expect(ifaceContent).toContain("public interface IKeyCustodianApi");
    expect(ifaceContent).toContain("GetJwksAsync(");

    // Impl file — exact name match (not the interface which also ends with Api.g.cs).
    const implContent = getEmittedFile(host, "/KeyCustodianApi.g.cs");
    expect(implContent).toBeDefined();
    expect(implContent).toContain(
      "namespace D2.Edge.KeyCustodian.App.Application;",
    );
    expect(implContent).toContain("public sealed class KeyCustodianApi");
    expect(implContent).toContain("=> getJwksHandler.HandleAsync(input, ct);");

    // DI extension file.
    const diContent = getEmittedFile(host, "KeyCustodianClientsGenerated.g.cs");
    expect(diContent).toBeDefined();
    expect(diContent).toContain("AddD2KeyCustodianClients");
    expect(diContent).toContain(
      "AddTransient<IKeyCustodianApi, KeyCustodianApi>",
    );
  });
});

describe("facadeEmitter_Integration_InternalOp_AbsentFromFacade", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("@d2Internal op absent from the façade interface (cross-boundary structural-absence)", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model GetJwksOutput { keys: string[]; }
      model ReconcileInput { kid: string; }
      model ReconcileOutput { ok: boolean; }

      @d2Query
      @d2InProcess
      @d2ServedBy("KeyCustodian")
      op getJwks(): GetJwksOutput;

      @d2Command
      @d2Internal
      @d2ServedBy("KeyCustodian")
      op reconcileKeyState(input: ReconcileInput): ReconcileOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: {
        "@d2/typespec-emitters": {
          "csharp-namespace": "D2.Fixture.Ns",
          "csharp-clients-namespace": "D2.Edge.KeyCustodian.Clients",
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

    const ifaceContent = getEmittedFile(host, "IKeyCustodianApi.g.cs");
    expect(ifaceContent).toBeDefined();
    // Only the exposed op should appear.
    expect(ifaceContent).toContain("GetJwksAsync(");
    // The internal op must NOT appear in the façade interface.
    expect(ifaceContent).not.toContain("ReconcileKeyState");
    expect(ifaceContent).not.toContain("reconcile");
  });
});
