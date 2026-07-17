// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Byte-parity gate for the @d2Resilience predicate emission + the predicate-
// bearing gRPC client. Compiles the committed predicate fixture
// (resilience-predicate-shaped.tsp, op placeOrder) through the TypeSpec
// test-host to obtain the REAL output model, re-emits the predicate files
// (PlaceOrderFixtureResiliencePredicates.g.cs / place-order-fixture-resilience-predicates.g.ts /
// D2GeneratedBusinessRetrySignal.g.cs) + the predicate-bearing client impl + DI
// extension, and asserts each is BYTE-IDENTICAL to the committed fixture on disk.
//
// Each describe carries a deliberate-drift negative (mutate one token → assert NOT
// byte-identical) so the gate is non-vacuous (§26.5.1). The existing sign /
// signWithKind client fixtures are pinned (byte-identical, no predicate) in
// proto-grpc-byte-parity.test.ts + byte-parity.test.ts and are NOT touched here.

import { describe as vitestDescribe, it, expect, beforeAll } from "vitest";
import { shouldRunPrivateProductParity } from "./private-tree.js";

/** Product-home parity � skipped under PUBLIC_ONLY or without private/** tree. */
const describe = shouldRunPrivateProductParity(import.meta.url)
  ? vitestDescribe
  : vitestDescribe.skip;
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
} from "@dcsv-io/d2-typespec-decorators";
import type { PredicateNode } from "@dcsv-io/d2-typespec-decorators";
import {
  emitResultPredicates,
  emitBusinessRetrySignal,
} from "../src/lib/result-predicate-emitter.js";
import {
  emitGrpcClient,
  emitClientKeys,
  type GrpcClientOp,
} from "../src/lib/grpc-client-emitter.js";
import { emitCsharpDtos } from "../src/lib/csharp-dto-emitter.js";
import { emitTsDtos } from "../src/lib/ts-dto-emitter.js";
import { emitProto } from "../src/lib/proto-emitter.js";
import { emitHandlerInterface } from "../src/lib/handler-interface-emitter.js";
import type { FieldInfo } from "../src/lib/model-walk.js";

const D2DecoratorTestLibrary = createTestLibrary({
  name: "@dcsv-io/d2-typespec-decorators",
  packageRoot: await findTestPackageRoot(
    new URL(
      "../node_modules/@dcsv-io/d2-typespec-decorators/package.json",
      import.meta.url,
    ).href,
  ),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

const D2EmitterTestLibrary = createTestLibrary({
  name: "@dcsv-io/d2-typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

// The committed fixture homes (the byte-pinned snapshots).
const CLIENTS_NS =
  "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated";
const PROTO_NS = "D2.Services.Protos.PredicateFixtures.V1";
const SPEC =
  "public/contracts/typespec/fixtures/resilience-predicate-shaped.tsp";

const FIXTURE_DIR = join(
  findRepoRoot(import.meta.url),
  "private",
  "services",
  "edge",
  "tests",
  "Unit",
  "KeyCustodian",
  "TypeSpecGrpcPredicate",
  "Generated",
);

function readFixture(name: string): string {
  return readFileSync(join(FIXTURE_DIR, name), "utf8").replace(/\r\n/g, "\n");
}

// ---------------------------------------------------------------------------
// Compile the fixture once; expose the model + parsed predicate ASTs.
// ---------------------------------------------------------------------------

let model: Model;
let retryWhen: PredicateNode;
let failWhen: PredicateNode;

// placeOrderV2 — the NESTED-model + array-of-MODEL predicate shape. Its predicate
// twin is emitted STANDALONE (model + AST only; no gRPC client is committed for it,
// a nested-model gRPC response being a tracked transport-mapper limitation), so the
// rich emission is byte-gated here from the same compiled fixture.
let modelV2: Model;
let retryWhenV2: PredicateNode;
let failWhenV2: PredicateNode;

beforeAll(async () => {
  const host = await createTestHost({
    libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
  });
  const tsp = readFileSync(
    join(
      findRepoRoot(import.meta.url),
      "public",
      "contracts",
      "typespec",
      "fixtures",
      "resilience-predicate-shaped.tsp",
    ),
    "utf8",
  );
  host.addTypeSpecFile("main.tsp", tsp);
  await host.compile("main.tsp", { outputDir: "testing:/out" });

  const program: Program = host.program;
  const globalNs = program.getGlobalNamespaceType();
  const fixturesNs = globalNs.namespaces.get("D2")?.namespaces.get("Fixtures");
  const found = fixturesNs?.models.get("PlaceOrderFixtureOutput");
  if (found === undefined) throw new Error("PlaceOrderFixtureOutput not found");

  model = found;

  const op = fixturesNs?.operations.get("placeOrderFixture") as
    | Operation
    | undefined;
  if (op === undefined) throw new Error("placeOrder op not found");

  retryWhen = parsePred(
    program.stateMap(D2_RESILIENCE_RETRY_WHEN_KEY).get(op) as string,
  );
  failWhen = parsePred(
    program.stateMap(D2_RESILIENCE_FAIL_WHEN_KEY).get(op) as string,
  );

  const foundV2 = fixturesNs?.models.get("PlaceOrderV2FixtureOutput");
  if (foundV2 === undefined)
    throw new Error("PlaceOrderV2FixtureOutput not found");

  modelV2 = foundV2;

  const opV2 = fixturesNs?.operations.get("placeOrderV2Fixture") as
    | Operation
    | undefined;
  if (opV2 === undefined) throw new Error("placeOrderV2 op not found");

  retryWhenV2 = parsePred(
    program.stateMap(D2_RESILIENCE_RETRY_WHEN_KEY).get(opV2) as string,
  );
  failWhenV2 = parsePred(
    program.stateMap(D2_RESILIENCE_FAIL_WHEN_KEY).get(opV2) as string,
  );
});

function parsePred(raw: string): PredicateNode {
  const parsed = parseResultPredicate(raw);
  if (!parsed.ok) throw new Error(`fixture predicate failed to parse: ${raw}`);

  return parsed.root;
}

function emitPredicateFiles(): { cs: string; ts: string } {
  const files = emitResultPredicates({
    opName: "placeOrderFixture",
    responseModelName: "PlaceOrderFixtureOutput",
    outputModel: model,
    clientsNs: CLIENTS_NS,
    dtoCsharpNs: CLIENTS_NS,
    sourceSpec: SPEC,
    retryWhen,
    failWhen,
  });
  return {
    cs: files.find((f) => f.fileName.endsWith(".g.cs"))!.content,
    ts: files.find((f) => f.fileName.endsWith(".g.ts"))!.content,
  };
}

function emitPredicateFilesV2(): { cs: string; ts: string } {
  const files = emitResultPredicates({
    opName: "placeOrderV2Fixture",
    responseModelName: "PlaceOrderV2FixtureOutput",
    outputModel: modelV2,
    clientsNs: CLIENTS_NS,
    dtoCsharpNs: CLIENTS_NS,
    sourceSpec: SPEC,
    retryWhen: retryWhenV2,
    failWhen: failWhenV2,
  });
  return {
    cs: files.find((f) => f.fileName.endsWith(".g.cs"))!.content,
    ts: files.find((f) => f.fileName.endsWith(".g.ts"))!.content,
  };
}

function makePlaceOrderClientOp(): GrpcClientOp {
  const strField = (name: string, cs: string, fieldNumber: number) => ({
    name,
    csName: cs,
    csType: "string",
    tsName: name,
    tsType: "string",
    protoType: "string",
    repeated: false,
    optional: false,
    redactReason: undefined,
    fieldNumber,
  });
  return {
    opName: "placeOrderFixture",
    grpcService: "PredicateFixturesOrders",
    grpcMethod: "PlaceOrderFixture",
    protoCsharpNs: PROTO_NS,
    dtoCsharpNs: CLIENTS_NS,
    sourceSpec: SPEC,
    requestModelName: "PlaceOrderFixtureInput",
    requestFields: [strField("customerId", "CustomerId", 1)],
    responseModelName: "PlaceOrderFixtureOutput",
    responseFields: [
      strField("orderCode", "OrderCode", 1),
      {
        name: "itemStatuses",
        csName: "ItemStatuses",
        csType: "IReadOnlyList<string>",
        tsName: "itemStatuses",
        tsType: "readonly string[]",
        protoType: "string",
        repeated: true,
        optional: false,
        redactReason: undefined,
        fieldNumber: 2,
      },
      {
        name: "partial",
        csName: "Partial",
        csType: "bool",
        tsName: "partial",
        tsType: "boolean",
        protoType: "bool",
        repeated: false,
        optional: false,
        redactReason: undefined,
        fieldNumber: 3,
      },
    ],
    retryWhenAst: retryWhen,
    failWhenAst: failWhen,
  };
}

// ---------------------------------------------------------------------------
// F1 — PlaceOrderFixtureResiliencePredicates.g.cs
// ---------------------------------------------------------------------------

describe("byteParity_PlaceOrderFixtureResiliencePredicatesCs", () => {
  it("re-emitted predicate .g.cs is byte-identical to the committed fixture", () => {
    expect(emitPredicateFiles().cs).toBe(
      readFixture("PlaceOrderFixtureResiliencePredicates.g.cs"),
    );
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture(
      "PlaceOrderFixtureResiliencePredicates.g.cs",
    ).replace("SR_RetryWhen", "SR_RetryWhenDRIFTED");
    expect(emitPredicateFiles().cs).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// F2 — place-order-fixture-resilience-predicates.g.ts
// ---------------------------------------------------------------------------

describe("byteParity_PlaceOrderFixtureResiliencePredicatesTs", () => {
  it("re-emitted predicate .g.ts is byte-identical to the committed fixture", () => {
    expect(emitPredicateFiles().ts).toBe(
      readFixture("place-order-fixture-resilience-predicates.g.ts"),
    );
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture(
      "place-order-fixture-resilience-predicates.g.ts",
    ).replace(
      "placeOrderFixtureRetryWhen",
      "placeOrderFixtureRetryWhenDRIFTED",
    );
    expect(emitPredicateFiles().ts).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// F2b — the NESTED-model + array-of-MODEL predicate twin (placeOrderV2)
//
// Byte-gates the rich-shape predicate twin AND pins (non-vacuity) that the
// emitted bodies genuinely carry the deep optional `?.`-chain (Customer?.Tier)
// + the array-of-MODEL quantifier (.Any(l => l.Status) / .some((l) => l.status))
// — the exact constructs the flat placeOrder twin cannot exercise. The twin is
// emitted STANDALONE (model + AST only) — no gRPC client for placeOrderV2 exists.
// ---------------------------------------------------------------------------

describe("byteParity_PlaceOrderV2FixtureResiliencePredicatesCs_Nested", () => {
  it("re-emitted nested/array-of-model predicate .g.cs is byte-identical to the committed fixture", () => {
    expect(emitPredicateFilesV2().cs).toBe(
      readFixture("PlaceOrderV2FixtureResiliencePredicates.g.cs"),
    );
  });

  it("the emitted C# body carries the deep ?.-chain + the LINQ .Any(...) quantifier (non-vacuity)", () => {
    const cs = emitPredicateFilesV2().cs;
    // Nested-optional path → deep null-conditional chain.
    expect(cs).toContain('r.Data?.Customer?.Tier == "TRIAL"');
    // Array-of-MODEL quantifier with an element sub-predicate.
    expect(cs).toContain('r.Data?.Lines?.Any(l => l.Status == "PENDING")');
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture(
      "PlaceOrderV2FixtureResiliencePredicates.g.cs",
    ).replace("Customer?.Tier", "Customer?.TierDRIFTED");
    expect(emitPredicateFilesV2().cs).not.toBe(drifted);
  });
});

describe("byteParity_PlaceOrderV2FixtureResiliencePredicatesTs_Nested", () => {
  it("re-emitted nested/array-of-model predicate .g.ts is byte-identical to the committed fixture", () => {
    expect(emitPredicateFilesV2().ts).toBe(
      readFixture("place-order-v2-fixture-resilience-predicates.g.ts"),
    );
  });

  it("the emitted TS body carries the deep ?.-chain + the .some(...) quantifier (non-vacuity)", () => {
    const ts = emitPredicateFilesV2().ts;
    expect(ts).toContain('r.data?.customer?.tier === "TRIAL"');
    expect(ts).toContain('r.data?.lines?.some((l) => l.status === "PENDING")');
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture(
      "place-order-v2-fixture-resilience-predicates.g.ts",
    ).replace("customer?.tier", "customer?.tierDRIFTED");
    expect(emitPredicateFilesV2().ts).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// F3 — D2GeneratedBusinessRetrySignal.g.cs
// ---------------------------------------------------------------------------

describe("byteParity_D2GeneratedBusinessRetrySignal", () => {
  it("re-emitted sentinel .g.cs is byte-identical to the committed fixture", () => {
    expect(emitBusinessRetrySignal(CLIENTS_NS, SPEC).content).toBe(
      readFixture("D2GeneratedBusinessRetrySignal.g.cs"),
    );
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture("D2GeneratedBusinessRetrySignal.g.cs").replace(
      "D2GeneratedBusinessRetrySignal",
      "D2GeneratedBusinessRetrySignalDRIFTED",
    );
    expect(emitBusinessRetrySignal(CLIENTS_NS, SPEC).content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// F4 — the predicate-bearing client impl + DI extension
// ---------------------------------------------------------------------------

describe("byteParity_PredicateBearingClient", () => {
  it("re-emitted client impl .g.cs is byte-identical to the committed fixture", () => {
    const [, impl] = emitGrpcClient(
      "PredicateFixtures",
      [makePlaceOrderClientOp()],
      CLIENTS_NS,
    );
    expect(impl!.content).toBe(readFixture("PredicateFixturesGrpcClient.g.cs"));
  });

  it("re-emitted client DI-ext .g.cs is byte-identical to the committed fixture", () => {
    const files = emitGrpcClient(
      "PredicateFixtures",
      [makePlaceOrderClientOp()],
      CLIENTS_NS,
    );
    expect(files[3]!.content).toBe(
      readFixture("PredicateFixturesGrpcClientsGenerated.g.cs"),
    );
  });

  it("deliberate-drift detection: a mutated client impl fixture does NOT match", () => {
    const drifted = readFixture("PredicateFixturesGrpcClient.g.cs").replace(
      "D2GeneratedBusinessRetrySignal",
      "WrongSignal",
    );
    const [, impl] = emitGrpcClient(
      "PredicateFixtures",
      [makePlaceOrderClientOp()],
      CLIENTS_NS,
    );
    expect(impl!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// V1 PredicateFixtures completeness byte-gates — the placeOrder DTO pair, the
// TS DTO, the proto, the client interface + mappers + keys. The predicate /
// impl / DI gates above already pin the resilience-bearing members; these close
// the remaining V1 committed `.g.*` files so the whole module is byte-gated.
// The field lists come from the SAME makePlaceOrderClientOp() the impl gate uses.
// ---------------------------------------------------------------------------

const placeOrderReqFields = (): readonly FieldInfo[] =>
  makePlaceOrderClientOp().requestFields;
const placeOrderRespFields = (): readonly FieldInfo[] =>
  makePlaceOrderClientOp().responseFields;

describe("byteParity_PlaceOrderV1Dtos", () => {
  it("re-emitted PlaceOrderFixtureInput.g.cs is byte-identical to the committed fixture", () => {
    const [inputFile] = emitCsharpDtos(
      "placeOrderFixture",
      CLIENTS_NS,
      SPEC,
      placeOrderReqFields(),
      placeOrderRespFields(),
      [],
    );
    expect(inputFile!.content).toBe(readFixture("PlaceOrderFixtureInput.g.cs"));
  });

  it("re-emitted PlaceOrderFixtureOutput.g.cs is byte-identical to the committed fixture", () => {
    const [, outputFile] = emitCsharpDtos(
      "placeOrderFixture",
      CLIENTS_NS,
      SPEC,
      placeOrderReqFields(),
      placeOrderRespFields(),
      [],
    );
    expect(outputFile!.content).toBe(
      readFixture("PlaceOrderFixtureOutput.g.cs"),
    );
  });

  it("re-emitted place-order-fixture-dto.g.ts is byte-identical to the committed fixture", () => {
    const tsFile = emitTsDtos(
      "placeOrderFixture",
      SPEC,
      placeOrderReqFields(),
      placeOrderRespFields(),
      [],
    );
    expect(tsFile.content).toBe(readFixture("place-order-fixture-dto.g.ts"));
  });

  it("deliberate-drift detection: a mutated PlaceOrderFixtureOutput fixture does NOT match", () => {
    const drifted = readFixture("PlaceOrderFixtureOutput.g.cs").replace(
      "string OrderCode",
      "string OrderCodeDRIFTED",
    );
    const [, outputFile] = emitCsharpDtos(
      "placeOrderFixture",
      CLIENTS_NS,
      SPEC,
      placeOrderReqFields(),
      placeOrderRespFields(),
      [],
    );
    expect(outputFile!.content).not.toBe(drifted);
  });
});

describe("byteParity_PlaceOrderV1Proto", () => {
  function emit(): string {
    return emitProto(
      "placeOrderFixture",
      "PredicateFixturesOrders",
      "PlaceOrderFixture",
      "unary",
      "d2.predicatefixtures.v1",
      PROTO_NS,
      SPEC,
      "PlaceOrderFixtureRequest",
      placeOrderReqFields(),
      undefined,
      "PlaceOrderFixtureOutput",
      placeOrderRespFields(),
      undefined,
      [],
      (c, m) => {
        throw new Error(`${c}: ${m}`);
      },
    )!.content;
  }

  it("re-emitted V1 proto is byte-identical to the committed fixture", () => {
    expect(emit()).toBe(
      readFixture(
        "../Protos/predicate_fixtures_orders_place_order_fixture.g.proto",
      ),
    );
  });

  it("deliberate-drift detection: a mutated proto message name does NOT match", () => {
    const drifted = readFixture(
      "../Protos/predicate_fixtures_orders_place_order_fixture.g.proto",
    ).replace(
      "message PlaceOrderFixtureOutput",
      "message PlaceOrderFixtureOutputDRIFTED",
    );
    expect(emit()).not.toBe(drifted);
  });
});

describe("byteParity_PredicateFixturesV1ClientModule", () => {
  it("re-emitted IPredicateFixturesGrpcClient.g.cs (client interface) is byte-identical", () => {
    const [iface] = emitGrpcClient(
      "PredicateFixtures",
      [makePlaceOrderClientOp()],
      CLIENTS_NS,
    );
    expect(iface!.content).toBe(
      readFixture("IPredicateFixturesGrpcClient.g.cs"),
    );
  });

  it("re-emitted PlaceOrderFixtureClientMappers.g.cs is byte-identical", () => {
    const [, , mappers] = emitGrpcClient(
      "PredicateFixtures",
      [makePlaceOrderClientOp()],
      CLIENTS_NS,
    );
    expect(mappers!.content).toBe(
      readFixture("PlaceOrderFixtureClientMappers.g.cs"),
    );
  });

  it("re-emitted PlaceOrderFixtureClientKeys.g.cs is byte-identical", () => {
    const keys = emitClientKeys("placeOrderFixture", CLIENTS_NS, SPEC);
    expect(keys.content).toBe(readFixture("PlaceOrderFixtureClientKeys.g.cs"));
  });

  it("deliberate-drift detection: a mutated client-interface fixture does NOT match", () => {
    const drifted = readFixture("IPredicateFixturesGrpcClient.g.cs").replace(
      "IPredicateFixturesGrpcClient",
      "IPredicateFixturesGrpcClientDRIFTED",
    );
    const [iface] = emitGrpcClient(
      "PredicateFixtures",
      [makePlaceOrderClientOp()],
      CLIENTS_NS,
    );
    expect(iface!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Handler interface byte-gates — predicate fixture namespace
//
// These three ops (placeOrder, placeOrderV2, deepNest) are defined in the
// same resilience-predicate-shaped.tsp fixture. Their handler interfaces live
// in the fixture namespace (CLIENTS_NS), so emitHandlerInterface is called
// with emitUsing=true and no dtoNamespace (handler ns === DTO ns — no
// separate using needed). The committed .g.cs files are byte-gated here,
// not scattered from $onEmit output (which uses real-module namespaces).
// ---------------------------------------------------------------------------

describe("byteParity_PlaceOrderHandlerInterface", () => {
  it("re-emitted IPlaceOrderFixtureHandler.g.cs is byte-identical to the committed fixture", () => {
    const handler = emitHandlerInterface(
      "placeOrderFixture",
      CLIENTS_NS,
      "PlaceOrderFixtureInput",
      "PlaceOrderFixtureOutput",
      true,
      SPEC,
    );

    expect(handler.content).toBe(readFixture("IPlaceOrderFixtureHandler.g.cs"));
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture("IPlaceOrderFixtureHandler.g.cs").replace(
      "IPlaceOrderFixtureHandler",
      "IPlaceOrderFixtureHandlerDRIFTED",
    );
    const handler = emitHandlerInterface(
      "placeOrderFixture",
      CLIENTS_NS,
      "PlaceOrderFixtureInput",
      "PlaceOrderFixtureOutput",
      true,
      SPEC,
    );

    expect(handler.content).not.toBe(drifted);
  });
});

describe("byteParity_PlaceOrderV2HandlerInterface", () => {
  it("re-emitted IPlaceOrderV2FixtureHandler.g.cs is byte-identical to the committed fixture", () => {
    const handler = emitHandlerInterface(
      "placeOrderV2Fixture",
      CLIENTS_NS,
      "PlaceOrderV2FixtureInput",
      "PlaceOrderV2FixtureOutput",
      true,
      SPEC,
    );

    expect(handler.content).toBe(
      readFixture("IPlaceOrderV2FixtureHandler.g.cs"),
    );
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture("IPlaceOrderV2FixtureHandler.g.cs").replace(
      "IPlaceOrderV2FixtureHandler",
      "IPlaceOrderV2FixtureHandlerDRIFTED",
    );
    const handler = emitHandlerInterface(
      "placeOrderV2Fixture",
      CLIENTS_NS,
      "PlaceOrderV2FixtureInput",
      "PlaceOrderV2FixtureOutput",
      true,
      SPEC,
    );

    expect(handler.content).not.toBe(drifted);
  });
});

describe("byteParity_DeepNestHandlerInterface", () => {
  it("re-emitted IDeepNestFixtureHandler.g.cs is byte-identical to the committed fixture", () => {
    const handler = emitHandlerInterface(
      "deepNestFixture",
      CLIENTS_NS,
      "DeepNestFixtureInput",
      "DeepNestFixtureOutput",
      true,
      SPEC,
    );

    expect(handler.content).toBe(readFixture("IDeepNestFixtureHandler.g.cs"));
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture("IDeepNestFixtureHandler.g.cs").replace(
      "IDeepNestFixtureHandler",
      "IDeepNestFixtureHandlerDRIFTED",
    );
    const handler = emitHandlerInterface(
      "deepNestFixture",
      CLIENTS_NS,
      "DeepNestFixtureInput",
      "DeepNestFixtureOutput",
      true,
      SPEC,
    );

    expect(handler.content).not.toBe(drifted);
  });
});
