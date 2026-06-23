// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Byte-parity gate for the NESTED-MODEL / array-of-MODEL gRPC wire-support
// fixtures. Compiles the committed predicate fixture (resilience-predicate-
// shaped.tsp) through the TypeSpec test-host to obtain the REAL output models,
// re-emits the V2 (placeOrderV2) + depth-3 (deepNest) proto / client / server-
// mapper / keys / DTO fixtures via the REAL emitters, and asserts each is
// BYTE-IDENTICAL to the committed fixture on disk.
//
// Each describe carries a deliberate-drift negative (mutate one token → assert
// NOT byte-identical) so the gate is non-vacuous (§26.5.1). The existing V1
// PredicateFixtures module-level fixtures (IPredicateFixturesGrpcClient /
// PredicateFixturesGrpcClient / …GrpcClientsGenerated) stay byte-identical — they
// are NOT re-emitted here (the separate V2/Deep modules keep V1 a clean regression
// guard); their byte-gates live in predicate-byte-parity.test.ts.

import { describe, it, expect, beforeAll } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import type { Model, Operation, Program } from "@typespec/compiler";
import {
  parseResultPredicate,
  D2_RESILIENCE_RETRY_WHEN_KEY,
  D2_RESILIENCE_FAIL_WHEN_KEY,
} from "@d2/typespec-decorators";
import type { PredicateNode } from "@d2/typespec-decorators";
import {
  walkModel,
  type FieldInfo,
  type NestedModel,
} from "../src/lib/model-walk.js";
import { emitProto } from "../src/lib/proto-emitter.js";
import type { NestedMessageDescriptor } from "../src/lib/proto-emitter.js";
import { emitGrpcService } from "../src/lib/grpc-service-emitter.js";
import {
  emitGrpcClient,
  emitClientKeys,
  type GrpcClientOp,
} from "../src/lib/grpc-client-emitter.js";
import { emitCsharpDtos } from "../src/lib/csharp-dto-emitter.js";
import { emitTsDtos } from "../src/lib/ts-dto-emitter.js";

const CLIENTS_NS = "D2.Edge.Tests.TypeSpecGrpcPredicate.Generated";
const SPEC = "contracts/typespec/fixtures/resilience-predicate-shaped.tsp";

const V2_PROTO_NS = "D2.Services.Protos.PredicateFixturesV2.V1";
const V2_PKG = "d2.predicatefixturesv2.v1";
const DEEP_PROTO_NS = "D2.Services.Protos.PredicateFixturesDeep.V1";
const DEEP_PKG = "d2.predicatefixturesdeep.v1";

const GEN_DIR = join(
  findRepoRoot(import.meta.url),
  "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated",
);
const PROTO_DIR = join(GEN_DIR, "..", "Protos");

function readGen(name: string): string {
  return readFileSync(join(GEN_DIR, name), "utf8").replace(/\r\n/g, "\n");
}
function readProto(name: string): string {
  return readFileSync(join(PROTO_DIR, name), "utf8").replace(/\r\n/g, "\n");
}

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

// ---------------------------------------------------------------------------
// Compile the fixture once; expose the walked field lists + nested models + AST.
// ---------------------------------------------------------------------------

interface Walk {
  fields: readonly FieldInfo[];
  nested: readonly NestedModel[];
}

let v2In: Walk;
let v2Out: Walk;
let v2Retry: PredicateNode;
let v2Fail: PredicateNode;
let deepIn: Walk;
let deepOut: Walk;

/** Dedup nested models for C#/TS DTO emitters (takes NestedModel[]). */
function dedupModels(...ws: Walk[]): readonly NestedModel[] {
  const m = new Map<string, NestedModel>();
  for (const w of ws)
    for (const n of w.nested) if (!m.has(n.name)) m.set(n.name, n);

  return [...m.values()];
}

/** Dedup nested models for emitProto (wraps each in a NestedMessageDescriptor). */
function dedupDescriptors(...ws: Walk[]): readonly NestedMessageDescriptor[] {
  return dedupModels(...ws).map((model) => ({ model }));
}

beforeAll(async () => {
  const host = await createTestHost({
    libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
  });
  const tsp = readFileSync(
    join(
      findRepoRoot(import.meta.url),
      "contracts/typespec/fixtures/resilience-predicate-shaped.tsp",
    ),
    "utf8",
  );
  host.addTypeSpecFile("main.tsp", tsp);
  await host.compile("main.tsp", { outputDir: "testing:/out" });

  const program: Program = host.program;
  const fixturesNs = program
    .getGlobalNamespaceType()
    .namespaces.get("D2")
    ?.namespaces.get("Fixtures");

  function walk(name: string): Walk {
    const model = fixturesNs?.models.get(name) as Model | undefined;
    if (model === undefined) throw new Error(`model ${name} not found`);

    const errs: string[] = [];
    const r = walkModel(program, model, (c, msg) => errs.push(`${c}: ${msg}`));
    if (errs.length > 0) throw new Error(`walk ${name}: ${errs.join("; ")}`);

    return { fields: r.fields, nested: r.nestedModels };
  }

  v2In = walk("PlaceOrderV2Input");
  v2Out = walk("PlaceOrderV2Output");
  deepIn = walk("DeepNestInput");
  deepOut = walk("DeepNestOutput");

  const opV2 = fixturesNs?.operations.get("placeOrderV2") as
    | Operation
    | undefined;
  if (opV2 === undefined) throw new Error("placeOrderV2 op not found");

  v2Retry = parsePred(
    program.stateMap(D2_RESILIENCE_RETRY_WHEN_KEY).get(opV2) as string,
  );
  v2Fail = parsePred(
    program.stateMap(D2_RESILIENCE_FAIL_WHEN_KEY).get(opV2) as string,
  );
});

function parsePred(raw: string): PredicateNode {
  const parsed = parseResultPredicate(raw);
  if (!parsed.ok) throw new Error(`fixture predicate failed to parse: ${raw}`);

  return parsed.root;
}

// ---------------------------------------------------------------------------
// Emit helpers (the SAME calls the one-off generator used).
// ---------------------------------------------------------------------------

function v2ClientOp(): GrpcClientOp {
  return {
    opName: "placeOrderV2",
    grpcService: "PredicateFixturesOrdersV2",
    grpcMethod: "PlaceOrderV2",
    protoCsharpNs: V2_PROTO_NS,
    dtoCsharpNs: CLIENTS_NS,
    sourceSpec: SPEC,
    requestModelName: "PlaceOrderV2Input",
    requestFields: v2In.fields,
    responseModelName: "PlaceOrderV2Output",
    responseFields: v2Out.fields,
    retryWhenAst: v2Retry,
    failWhenAst: v2Fail,
  };
}

function deepClientOp(): GrpcClientOp {
  return {
    opName: "deepNest",
    grpcService: "PredicateFixturesGizmosDeep",
    grpcMethod: "DeepNest",
    protoCsharpNs: DEEP_PROTO_NS,
    dtoCsharpNs: CLIENTS_NS,
    sourceSpec: SPEC,
    requestModelName: "DeepNestInput",
    requestFields: deepIn.fields,
    responseModelName: "DeepNestOutput",
    responseFields: deepOut.fields,
  };
}

// ===========================================================================
// V2 — placeOrderV2 (optional nested model + array-of-model)
// ===========================================================================

describe("byteParity_PlaceOrderV2Proto", () => {
  function emit(): string {
    return emitProto(
      "placeOrderV2",
      "PredicateFixturesOrdersV2",
      "PlaceOrderV2",
      "unary",
      V2_PKG,
      V2_PROTO_NS,
      SPEC,
      "PlaceOrderV2Request",
      v2In.fields,
      undefined,
      "PlaceOrderV2Output",
      v2Out.fields,
      undefined,
      dedupDescriptors(v2In, v2Out),
      (c, m) => {
        throw new Error(`${c}: ${m}`);
      },
    )!.content;
  }

  it("re-emitted V2 proto is byte-identical to the committed fixture", () => {
    expect(emit()).toBe(
      readProto("predicate_fixtures_orders_v2_place_order_v2.g.proto"),
    );
  });

  it("carries repeated PlaceOrderLine + bare PlaceOrderV2Customer + both nested messages (non-vacuity)", () => {
    const p = emit();
    expect(p).toContain("repeated PlaceOrderLine lines = 2;");
    // Nullable nested model → bare message field (NO `optional` keyword, proto3 presence).
    expect(p).toContain("  PlaceOrderV2Customer customer = 3;");
    expect(p).not.toContain("optional PlaceOrderV2Customer");
    expect(p).toContain("message PlaceOrderLine {");
    expect(p).toContain("message PlaceOrderV2Customer {");
  });

  it("deliberate-drift: a mutated nested message name does NOT match", () => {
    const drifted = readProto(
      "predicate_fixtures_orders_v2_place_order_v2.g.proto",
    ).replace("message PlaceOrderLine", "message PlaceOrderLineDRIFTED");
    expect(emit()).not.toBe(drifted);
  });
});

describe("byteParity_PlaceOrderV2Dtos", () => {
  it("re-emitted PlaceOrderV2Input.g.cs is byte-identical to the committed fixture", () => {
    const [inputFile] = emitCsharpDtos(
      "placeOrderV2",
      CLIENTS_NS,
      SPEC,
      v2In.fields,
      v2Out.fields,
      dedupModels(v2In, v2Out),
    );
    expect(inputFile!.content).toBe(readGen("PlaceOrderV2Input.g.cs"));
  });

  it("re-emitted PlaceOrderV2Output.g.cs (with both nested records) is byte-identical", () => {
    const [, outputFile] = emitCsharpDtos(
      "placeOrderV2",
      CLIENTS_NS,
      SPEC,
      v2In.fields,
      v2Out.fields,
      dedupModels(v2In, v2Out),
    );
    expect(outputFile!.content).toBe(readGen("PlaceOrderV2Output.g.cs"));
  });

  it("re-emitted place-order-v2-dto.g.ts is byte-identical to the committed fixture", () => {
    const ts = emitTsDtos(
      "placeOrderV2",
      SPEC,
      v2In.fields,
      v2Out.fields,
      dedupModels(v2In, v2Out),
    );
    expect(ts.content).toBe(readGen("place-order-v2-dto.g.ts"));
  });

  it("deliberate-drift: a mutated nested record does NOT match", () => {
    const drifted = readGen("PlaceOrderV2Output.g.cs").replace(
      "public sealed record PlaceOrderLine(",
      "public sealed record PlaceOrderLineDRIFTED(",
    );
    const [, outputFile] = emitCsharpDtos(
      "placeOrderV2",
      CLIENTS_NS,
      SPEC,
      v2In.fields,
      v2Out.fields,
      dedupModels(v2In, v2Out),
    );
    expect(outputFile!.content).not.toBe(drifted);
  });
});

describe("byteParity_PlaceOrderV2ClientMappers", () => {
  function emit(): string {
    const [, , mappers] = emitGrpcClient(
      "PredicateFixturesV2",
      [v2ClientOp()],
      CLIENTS_NS,
    );

    return mappers!.content;
  }

  it("re-emitted V2 client mappers are byte-identical to the committed fixture", () => {
    expect(emit()).toBe(readGen("PlaceOrderV2ClientMappers.g.cs"));
  });

  it("carries the array-of-model + nullable-nested recursion + both sub-mappers (non-vacuity)", () => {
    const m = emit();
    expect(m).toContain(
      "data.Lines.Select(x => x.ToPlaceOrderLine()).ToList()",
    );
    expect(m).toContain(
      "data.Customer is null ? null : data.Customer.ToPlaceOrderV2Customer()",
    );
    expect(m).toContain(
      "internal global::D2.Services.Protos.PredicateFixturesV2.V1.PlaceOrderLine ToProtoPlaceOrderLine()",
    );
    expect(m).toContain(
      "internal global::D2.Edge.Tests.TypeSpecGrpcPredicate.Generated.PlaceOrderLine ToPlaceOrderLine()",
    );
    expect(m).toContain("using System.Linq;");
  });

  it("deliberate-drift: a mutated sub-mapper name does NOT match", () => {
    const drifted = readGen("PlaceOrderV2ClientMappers.g.cs").replace(
      "ToProtoPlaceOrderLine",
      "ToProtoPlaceOrderLineDRIFTED",
    );
    expect(emit()).not.toBe(drifted);
  });
});

// The SERVER transport mapper for placeOrderV2 is NOT committed as a fixture file:
// the client mapper + the server transport mapper share the SAME namespace and emit the
// SAME per-nested-model sub-mappers (ToProtoPlaceOrderLine / ToPlaceOrderLine …), so
// compiling both in one assembly would collide (CS0121). V1 commits only the client side
// for the same reason. The server-side buildDtoToProto / buildProtoToDto nested recursion is
// proven by grpc-service-emitter.test.ts direct-unit assertions; here we additionally pin
// (re-emit, no committed file) that the server mapper carries the collection-init recursion.
describe("emitGrpcService_PlaceOrderV2_ServerMapperRecursion_NonCommitted", () => {
  function emit(): string {
    const [, mappers] = emitGrpcService(
      "placeOrderV2",
      "PredicateFixturesOrdersV2",
      "PlaceOrderV2",
      V2_PROTO_NS,
      CLIENTS_NS,
      CLIENTS_NS,
      SPEC,
      "PlaceOrderV2Request",
      "PlaceOrderV2Response",
      "PlaceOrderV2Input",
      v2In.fields,
      "PlaceOrderV2Output",
      v2Out.fields,
    );

    return mappers.content;
  }

  it("carries the collection-init array-of-model + nullable-nested + both sub-mappers", () => {
    const m = emit();
    // RepeatedField has no setter → collection-init `Field = { … }` form.
    expect(m).toContain(
      "Lines = { output.Lines.Select(x => x.ToProtoPlaceOrderLine()) },",
    );
    expect(m).toContain(
      "Customer = output.Customer is null ? null : output.Customer.ToProtoPlaceOrderV2Customer(),",
    );
    expect(m).toContain("internal ProtoPlaceOrderLine ToProtoPlaceOrderLine()");
    expect(m).toContain("internal PlaceOrderLine ToPlaceOrderLine()");
    expect(m).toContain("using System.Linq;");
  });
});

describe("byteParity_PlaceOrderV2ModuleFiles", () => {
  function emit(): {
    iface: string;
    impl: string;
    di: string;
    keys: string;
  } {
    const [iface, impl, , di] = emitGrpcClient(
      "PredicateFixturesV2",
      [v2ClientOp()],
      CLIENTS_NS,
    );
    const keys = emitClientKeys("placeOrderV2", CLIENTS_NS, SPEC);

    return {
      iface: iface!.content,
      impl: impl!.content,
      di: di!.content,
      keys: keys.content,
    };
  }

  it("V2 interface is byte-identical", () => {
    expect(emit().iface).toBe(readGen("IPredicateFixturesV2GrpcClient.g.cs"));
  });

  it("V2 impl (predicate-bearing) is byte-identical", () => {
    expect(emit().impl).toBe(readGen("PredicateFixturesV2GrpcClient.g.cs"));
  });

  it("V2 DI ext is byte-identical", () => {
    expect(emit().di).toBe(
      readGen("PredicateFixturesV2GrpcClientsGenerated.g.cs"),
    );
  });

  it("V2 client keys are byte-identical", () => {
    expect(emit().keys).toBe(readGen("PlaceOrderV2ClientKeys.g.cs"));
  });

  it("deliberate-drift: a mutated impl does NOT match", () => {
    const drifted = readGen("PredicateFixturesV2GrpcClient.g.cs").replace(
      "ToPlaceOrderV2Output",
      "ToPlaceOrderV2OutputDRIFTED",
    );
    expect(emit().impl).not.toBe(drifted);
  });
});

// ===========================================================================
// Depth-3 — deepNest (arbitrary nesting: output → widget → parts[])
// ===========================================================================

describe("byteParity_DeepNestDtos", () => {
  function emitCs(): string {
    const files = emitCsharpDtos(
      "deepNest",
      CLIENTS_NS,
      SPEC,
      deepIn.fields,
      deepOut.fields,
      dedupModels(deepIn, deepOut),
    );

    return files.find((f) => f.fileName === "DeepNestOutput.g.cs")!.content;
  }

  it("re-emitted depth-3 output DTO is byte-identical (DeepNestOutput → DeepWidget → DeepPart)", () => {
    expect(emitCs()).toBe(readGen("DeepNestOutput.g.cs"));
  });

  it("re-emitted depth-3 input DTO is byte-identical (DeepNestInput)", () => {
    const files = emitCsharpDtos(
      "deepNest",
      CLIENTS_NS,
      SPEC,
      deepIn.fields,
      deepOut.fields,
      dedupModels(deepIn, deepOut),
    );
    expect(
      files.find((f) => f.fileName === "DeepNestInput.g.cs")!.content,
    ).toBe(readGen("DeepNestInput.g.cs"));
  });

  it("re-emitted depth-3 TS DTO is byte-identical", () => {
    const ts = emitTsDtos(
      "deepNest",
      SPEC,
      deepIn.fields,
      deepOut.fields,
      dedupModels(deepIn, deepOut),
    );
    expect(ts.content).toBe(readGen("deep-nest-dto.g.ts"));
  });

  it("carries all three depth levels as records (non-vacuity)", () => {
    const cs = emitCs();
    expect(cs).toContain("public sealed record DeepWidget(");
    expect(cs).toContain("IReadOnlyList<DeepPart> Parts);");
    expect(cs).toContain("public sealed record DeepPart(");
  });

  it("deliberate-drift: a mutated nested record does NOT match", () => {
    const drifted = readGen("DeepNestOutput.g.cs").replace(
      "DeepPart",
      "DeepPartDRIFTED",
    );
    expect(emitCs()).not.toBe(drifted);
  });
});

describe("byteParity_DeepNestProto", () => {
  function emit(): string {
    return emitProto(
      "deepNest",
      "PredicateFixturesGizmosDeep",
      "DeepNest",
      "unary",
      DEEP_PKG,
      DEEP_PROTO_NS,
      SPEC,
      "DeepNestRequest",
      deepIn.fields,
      undefined,
      "DeepNestOutput",
      deepOut.fields,
      undefined,
      dedupDescriptors(deepIn, deepOut),
      (c, m) => {
        throw new Error(`${c}: ${m}`);
      },
    )!.content;
  }

  it("re-emitted depth-3 proto is byte-identical (all three message levels)", () => {
    expect(emit()).toBe(
      readProto("predicate_fixtures_gizmos_deep_deep_nest.g.proto"),
    );
  });

  it("emits a message at EVERY depth + the array-of-model inside the nested model (non-vacuity)", () => {
    const p = emit();
    expect(p).toContain("message DeepNestOutput {");
    expect(p).toContain("  DeepWidget widget = 2;");
    expect(p).toContain("message DeepWidget {");
    expect(p).toContain("  repeated DeepPart parts = 2;");
    expect(p).toContain("message DeepPart {");
  });

  it("deliberate-drift: a mutated depth-3 message name does NOT match", () => {
    const drifted = readProto(
      "predicate_fixtures_gizmos_deep_deep_nest.g.proto",
    ).replace("message DeepPart", "message DeepPartDRIFTED");
    expect(emit()).not.toBe(drifted);
  });
});

describe("byteParity_DeepNestClientMappers", () => {
  function emit(): string {
    const [, , mappers] = emitGrpcClient(
      "PredicateFixturesDeep",
      [deepClientOp()],
      CLIENTS_NS,
    );

    return mappers!.content;
  }

  it("re-emitted depth-3 client mappers are byte-identical (recursion through all levels)", () => {
    expect(emit()).toBe(readGen("DeepNestClientMappers.g.cs"));
  });

  it("emits a sub-mapper for EVERY nested level + the nested-array recursion (non-vacuity)", () => {
    const m = emit();
    // Top → widget (nullable nested).
    expect(m).toContain(
      "data.Widget is null ? null : data.Widget.ToDeepWidget()",
    );
    // Widget → parts (array-of-model INSIDE a nested model — the depth-N proof).
    expect(m).toContain("source.Parts.Select(x => x.ToProtoDeepPart())");
    expect(m).toContain("source.Parts.Select(x => x.ToDeepPart()).ToList()");
    // A sub-mapper exists for BOTH the depth-2 DeepWidget AND the depth-3 DeepPart.
    expect(m).toContain("ToProtoDeepWidget()");
    expect(m).toContain("ToProtoDeepPart()");
    expect(m).toContain("ToDeepWidget()");
    expect(m).toContain("ToDeepPart()");
  });

  it("deliberate-drift: a mutated depth-3 sub-mapper name does NOT match", () => {
    const drifted = readGen("DeepNestClientMappers.g.cs").replace(
      "ToProtoDeepPart",
      "ToProtoDeepPartDRIFTED",
    );
    expect(emit()).not.toBe(drifted);
  });
});

// The depth-3 SERVER transport mapper is likewise NOT committed (same namespace +
// sub-mapper collision with the client mapper). Re-emit + assert the depth-3 recursion
// (no committed file). The committed proof is the client mapper byte-gate above.
describe("emitGrpcService_DeepNest_ServerMapperRecursion_NonCommitted", () => {
  function emit(): string {
    const [, mappers] = emitGrpcService(
      "deepNest",
      "PredicateFixturesGizmosDeep",
      "DeepNest",
      DEEP_PROTO_NS,
      CLIENTS_NS,
      CLIENTS_NS,
      SPEC,
      "DeepNestRequest",
      "DeepNestResponse",
      "DeepNestInput",
      deepIn.fields,
      "DeepNestOutput",
      deepOut.fields,
    );

    return mappers.content;
  }

  it("recurses every depth level + the nested array-of-model (collection-init)", () => {
    const m = emit();
    expect(m).toContain(
      "Widget = output.Widget is null ? null : output.Widget.ToProtoDeepWidget(),",
    );
    expect(m).toContain(
      "Parts = { source.Parts.Select(x => x.ToProtoDeepPart()) },",
    );
    expect(m).toContain("internal ProtoDeepWidget ToProtoDeepWidget()");
    expect(m).toContain("internal ProtoDeepPart ToProtoDeepPart()");
    expect(m).toContain("internal DeepPart ToDeepPart()");
  });
});

describe("byteParity_DeepNestModuleFiles", () => {
  function emit(): { iface: string; impl: string; di: string; keys: string } {
    const [iface, impl, , di] = emitGrpcClient(
      "PredicateFixturesDeep",
      [deepClientOp()],
      CLIENTS_NS,
    );
    const keys = emitClientKeys("deepNest", CLIENTS_NS, SPEC);

    return {
      iface: iface!.content,
      impl: impl!.content,
      di: di!.content,
      keys: keys.content,
    };
  }

  it("Deep interface / impl / DI / keys are byte-identical", () => {
    const e = emit();
    expect(e.iface).toBe(readGen("IPredicateFixturesDeepGrpcClient.g.cs"));
    expect(e.impl).toBe(readGen("PredicateFixturesDeepGrpcClient.g.cs"));
    expect(e.di).toBe(
      readGen("PredicateFixturesDeepGrpcClientsGenerated.g.cs"),
    );
    expect(e.keys).toBe(readGen("DeepNestClientKeys.g.cs"));
  });

  it("deliberate-drift: a mutated impl does NOT match", () => {
    const drifted = readGen("PredicateFixturesDeepGrpcClient.g.cs").replace(
      "ToDeepNestOutput",
      "ToDeepNestOutputDRIFTED",
    );
    expect(emit().impl).not.toBe(drifted);
  });
});
