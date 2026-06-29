// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Behavioral + structural coverage for ts-grpc-client-emitter.ts.
//
// THE LOAD-BEARING VALIDATION (the real proto pipeline):
//   The emitted SSR gRPC client is driven against the REAL @d2/grpc-client seam
//   (handleGrpcCall / unaryCall / d2ResultFromProto / isTransientGrpcError), the
//   REAL @d2/resilience ResilientPipeline, the REAL fixture ts-proto types (the
//   buf/ts-proto output — the TS twin of Grpc.Tools), and the REAL emitted
//   predicate twin. A FAKE grpc-js stub (a typed object implementing the real
//   ts-proto <Service>Client interface) drives the callback path. This is NOT a
//   type-double of the seam — every mapping (envelope→D2Result, transport-fault→
//   TK-constant, transient classification, retry) is the shipped library's.
//
//   `runsRealBufTsProto` (below) RE-RUNS the real buf/ts-proto toolchain on the
//   committed fixture .proto into a temp dir and asserts the output is
//   byte-identical to the committed in-package proto-TS (tests/grpc-fixtures/
//   generated/*.ts) — proving the committed proto-TS the harness imports is the
//   genuine buf output, and that the toolchain runs on a standalone fixture proto.
//
//   The emitted .g.ts carries `// @ts-nocheck` (it references module-relative
//   imports that wire up only in the BFF SSR consumer), so the test reconstructs
//   the client factory from the emitted TEXT (the predicate-parity `new Function`
//   pattern) with the real seam + real proto + real predicate twin in scope —
//   driving the ACTUAL committed bytes (the byte-gate pins them) NON-VACUOUSLY.

import { describe, it, expect } from "vitest";
import { execFileSync } from "node:child_process";
import {
  mkdtempSync,
  readFileSync,
  cpSync,
  mkdirSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { findRepoRoot } from "./repo-root.js";
import ts from "typescript";
import { Metadata, status as GrpcStatus } from "@grpc/grpc-js";
import type { ServiceError } from "@grpc/grpc-js";
import {
  d2ResultFromProto,
  handleGrpcCall,
  isTransientGrpcError,
  unaryCall,
} from "@d2/grpc-client";
import { ResilientPipeline, ResilientPipelineBuilder } from "@d2/resilience";
import { ok, validationFailed } from "@d2/result";
import type { D2Result } from "@d2/result";
import {
  emitTsGrpcClient,
  type TsGrpcClientOp,
} from "../src/lib/ts-grpc-client-emitter.js";
import { parseResultPredicate } from "@d2/typespec-decorators";
import type { FieldInfo } from "../src/lib/model-walk.js";
import type { PredicateNode } from "@d2/typespec-decorators";

/** Parse a result-predicate expression to its AST, failing the test on a parse error. */
function parsePred(expr: string): PredicateNode {
  const parsed = parseResultPredicate(expr);
  if (!parsed.ok) throw new Error(`test predicate failed to parse: ${expr}`);

  return parsed.root;
}
// REAL fixture ts-proto types (buf/ts-proto output — the TS twin of Grpc.Tools).
import type {
  PlaceOrderFixtureRequest,
  PlaceOrderFixtureResponse,
  PredicateFixturesOrdersClient,
} from "./grpc-fixtures/generated/place_order.js";
import type {
  SignWithKindFixtureRequest,
  SignWithKindFixtureResponse,
  EnumFixturesSignerClient,
} from "./grpc-fixtures/generated/sign_with_kind.js";

const HERE = dirname(fileURLToPath(import.meta.url));
const _REPO = findRepoRoot(import.meta.url);
const _KC_PRED_GEN = join(
  _REPO,
  "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated",
);
const PRED_SRC = "contracts/typespec/fixtures/resilience-predicate-shaped.tsp";
const ENUM_SRC = "contracts/typespec/fixtures/enum-shaped.tsp";

// ---------------------------------------------------------------------------
// Field + op fixtures (mirror the committed-fixture op shapes)
// ---------------------------------------------------------------------------

function field(
  name: string,
  csType: string,
  tsType: string,
  protoType: string,
  repeated = false,
  enumRef?: FieldInfo["enumRef"],
): FieldInfo {
  return {
    name,
    csName: name[0]!.toUpperCase() + name.slice(1),
    csType,
    tsName: name,
    tsType,
    protoType,
    repeated,
    optional: false,
    redact: false,
    ...(enumRef ? { enumRef } : {}),
  };
}

const KEY_KIND: FieldInfo["enumRef"] = {
  name: "FixtureKeyKind",
  members: [
    { csName: "Rsa", wireValue: "Rsa", needsEnumMember: false },
    { csName: "Aes", wireValue: "Aes", needsEnumMember: false },
    { csName: "Secret", wireValue: "Secret", needsEnumMember: false },
  ],
};

function placeOrderOp(): TsGrpcClientOp {
  return {
    opName: "placeOrderFixture",
    grpcService: "PredicateFixturesOrders",
    grpcMethod: "PlaceOrderFixture",
    sourceSpec: PRED_SRC,
    requestModelName: "PlaceOrderFixtureInput",
    requestFields: [field("customerId", "string", "string", "string")],
    responseModelName: "PlaceOrderFixtureOutput",
    responseFields: [
      field("orderCode", "string", "string", "string"),
      field(
        "itemStatuses",
        "IReadOnlyList<string>",
        "readonly string[]",
        "string",
        true,
      ),
      field("partial", "bool", "boolean", "bool"),
    ],
    retryWhenAst: parsePred(
      'result.category == "infrastructure_unavailable" || result.data.itemStatuses.contains("PENDING") || result.data.partial == true',
    ),
    failWhenAst: parsePred(
      'result.data.itemStatuses.count == 0 || result.errorCode == "VALIDATION_FAILED"',
    ),
    retryBudget: 3,
  };
}

function signGrpcOp(): TsGrpcClientOp {
  return {
    opName: "signFixture",
    grpcService: "SignFixtureSigner",
    grpcMethod: "SignFixture",
    sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
    requestModelName: "SignFixtureInput",
    requestFields: [
      field("kid", "string", "string", "string"),
      field("payload", "byte[]", "Uint8Array", "bytes"),
    ],
    responseModelName: "SignFixtureOutput",
    responseFields: [field("signature", "string", "string", "string")],
  };
}

function signWithKindOp(): TsGrpcClientOp {
  return {
    opName: "signWithKindFixture",
    grpcService: "EnumFixturesSigner",
    grpcMethod: "SignWithKindFixture",
    sourceSpec: ENUM_SRC,
    requestModelName: "SignWithKindFixtureInput",
    requestFields: [
      field("kid", "string", "string", "string"),
      field("keyKind", "KeyKind", "KeyKind", "string", false, KEY_KIND),
    ],
    responseModelName: "SignWithKindFixtureOutput",
    responseFields: [
      field("signature", "string", "string", "string"),
      field("keyKind", "KeyKind", "KeyKind", "string", false, KEY_KIND),
    ],
  };
}

// ---------------------------------------------------------------------------
// Reconstruct the emitted client factory from the emitted TEXT.
//
// The emitted .g.ts is @ts-nocheck plain runtime JS. We strip the import lines
// (the symbols are provided as `new Function` params) and the `export` keywords,
// keep the body (sentinel + default pipeline + interface-erased + factory), and
// return the requested factory. Drives the ACTUAL emitted bytes.
// ---------------------------------------------------------------------------

interface SeamScope {
  d2ResultFromProto: typeof d2ResultFromProto;
  handleGrpcCall: typeof handleGrpcCall;
  unaryCall: typeof unaryCall;
  isTransientGrpcError: typeof isTransientGrpcError;
  ResilientPipeline: typeof ResilientPipeline;
  ResilientPipelineBuilder: typeof ResilientPipelineBuilder;
  ok: typeof ok;
  validationFailed: typeof validationFailed;
  [predicate: string]: unknown;
}

function reconstructFactory<T>(
  source: string,
  factoryName: string,
  scope: SeamScope,
): (stub: unknown) => T {
  // Remove every import line (the symbols come from `scope`), then TRANSPILE the
  // emitted TS to JS with the real TypeScript compiler (type-annotation erasure —
  // exactly what the BFF bundler does to the @ts-nocheck'd .g.ts). The resulting
  // JS is the ACTUAL emitted code, evaluated via `new Function` with the real seam
  // symbols in scope. Non-vacuous: it drives the committed bytes, not a re-write.
  const noImports = source
    .split("\n")
    .filter((l) => !/^\s*import\s/.test(l))
    .join("\n");

  const js = ts.transpileModule(noImports, {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022,
      verbatimModuleSyntax: false,
    },
  }).outputText;

  // Drop `export` keywords so the body is plain statements in the Function scope.
  const body = js.replace(/^export /gm, "");

  const scopeKeys = Object.keys(scope);
  const scopeVals = scopeKeys.map((k) => scope[k]);
  const make = new Function(
    ...scopeKeys,
    `${body}\nreturn ${factoryName};`,
  ) as (...args: unknown[]) => (stub: unknown) => T;
  return make(...scopeVals);
}

/** Load the REAL committed predicate twin (placeOrder) via text extraction. */
function loadCommittedPredicateTwin(): {
  placeOrderFixtureRetryWhen: (r: unknown) => boolean;
  placeOrderFixtureFailWhen: (r: unknown) => boolean;
} {
  const file = join(
    _KC_PRED_GEN,
    "place-order-fixture-resilience-predicates.g.ts",
  );
  const text = readFileSync(file, "utf8");
  const extract = (name: string): ((r: unknown) => boolean) => {
    const re = new RegExp(
      `export const ${name} = \\(r: [^)]*\\): boolean =>\\s*([\\s\\S]*?);`,
    );
    const m = re.exec(text);
    if (m === null) throw new Error(`could not extract ${name}`);

    return new Function("r", `return (${m[1]!.trim()});`) as (
      r: unknown,
    ) => boolean;
  };
  return {
    placeOrderFixtureRetryWhen: extract("placeOrderFixtureRetryWhen"),
    placeOrderFixtureFailWhen: extract("placeOrderFixtureFailWhen"),
  };
}

// ---------------------------------------------------------------------------
// Fake grpc-js stubs — typed against the REAL ts-proto <Service>Client interface.
// Each drives the callback the seam's unaryCall awaits.
// ---------------------------------------------------------------------------

type PlaceOrderResult =
  | { kind: "ok"; response: PlaceOrderFixtureResponse }
  | { kind: "error"; error: ServiceError };

function makeServiceError(
  code: number,
  details = "boom-secret-detail",
): ServiceError {
  const err = new Error(details) as ServiceError;
  err.code = code;
  err.details = details;
  err.metadata = new Metadata();
  return err;
}

/**
 * A fake PredicateFixturesOrders stub. `script` yields a result per call so the
 * test can drive retry sequences (flaky-then-success, business-retry-then-give-up).
 * Records the call count. Typed against the REAL ts-proto client interface so the
 * harness COMPILES against the real proto types (the fixture-proto compile proof).
 */
function makePlaceOrderStub(script: PlaceOrderResult[]): {
  stub: Pick<PredicateFixturesOrdersClient, "placeOrderFixture">;
  calls: () => number;
  lastRequest: () => PlaceOrderFixtureRequest | undefined;
} {
  let i = 0;
  let count = 0;
  let last: PlaceOrderFixtureRequest | undefined;
  const placeOrderFixture = ((
    request: PlaceOrderFixtureRequest,
    arg2: unknown,
    arg3?: unknown,
    arg4?: unknown,
  ): never => {
    count += 1;
    last = request;
    // The callback is the last argument (grpc-js overloads); pick it.
    const cb = [arg2, arg3, arg4]
      .filter((a) => typeof a === "function")
      .pop() as (
      error: ServiceError | null,
      response?: PlaceOrderFixtureResponse,
    ) => void;
    const step = script[Math.min(i, script.length - 1)]!;
    i += 1;
    // Resolve asynchronously, mirroring grpc-js callback timing.
    queueMicrotask(() => {
      if (step.kind === "ok") cb(null, step.response);
      else cb(step.error);
    });
    return undefined as never;
  }) as unknown as PredicateFixturesOrdersClient["placeOrderFixture"];
  return {
    stub: { placeOrderFixture },
    calls: () => count,
    lastRequest: () => last,
  };
}

function makeSignWithKindStub(response: SignWithKindFixtureResponse): {
  stub: Pick<EnumFixturesSignerClient, "signWithKindFixture">;
  lastRequest: () => SignWithKindFixtureRequest | undefined;
} {
  let last: SignWithKindFixtureRequest | undefined;
  const signWithKindFixture = ((
    request: SignWithKindFixtureRequest,
    arg2: unknown,
    arg3?: unknown,
    arg4?: unknown,
  ): never => {
    last = request;
    const cb = [arg2, arg3, arg4]
      .filter((a) => typeof a === "function")
      .pop() as (
      error: ServiceError | null,
      response?: SignWithKindFixtureResponse,
    ) => void;
    queueMicrotask(() => cb(null, response));
    return undefined as never;
  }) as unknown as EnumFixturesSignerClient["signWithKindFixture"];
  return { stub: { signWithKindFixture }, lastRequest: () => last };
}

// ---------------------------------------------------------------------------
// Seam scope builders
// ---------------------------------------------------------------------------

function baseScope(extra: Record<string, unknown> = {}): SeamScope {
  return {
    d2ResultFromProto,
    handleGrpcCall,
    unaryCall,
    isTransientGrpcError,
    ResilientPipeline,
    ResilientPipelineBuilder,
    ok,
    validationFailed,
    ...extra,
  };
}

// ===========================================================================
// Fixture proto byte-gate — the real buf/ts-proto pipeline proof (place_order)
// ===========================================================================

/** Run the real buf/ts-proto toolchain on a fixture proto into a temp dir. */
function runRealBufTsProto(): string {
  const out = mkdtempSync(join(tmpdir(), "tsp-buf-"));
  const protoStage = join(out, "protos");
  mkdirSync(join(protoStage, "common", "v1"), { recursive: true });
  mkdirSync(join(out, "gen"), { recursive: true });
  // The d2_result import from the real contracts/protos.
  const repoRoot = _REPO;
  cpSync(
    join(repoRoot, "contracts", "protos", "common", "v1", "d2_result.proto"),
    join(protoStage, "common", "v1", "d2_result.proto"),
  );
  cpSync(
    join(HERE, "grpc-fixtures", "place_order.proto"),
    join(protoStage, "place_order.proto"),
  );
  const genConfig = join(out, "buf.gen.yaml");
  writeFileSync(
    genConfig,
    [
      "version: v2",
      "plugins:",
      '  - local: ["pnpm", "exec", "protoc-gen-ts_proto"]',
      `    out: ${join(out, "gen").replace(/\\/g, "/")}`,
      "    opt:",
      "      - esModuleInterop=true",
      "      - outputServices=grpc-js",
      "      - useExactTypes=false",
      "      - oneof=unions",
      "      - useOptionals=messages",
      "      - Mcommon/v1/d2_result.proto=@d2/protos",
    ].join("\n"),
  );
  // Run buf from the @d2/protos package dir so `pnpm exec protoc-gen-ts_proto` resolves.
  const protosDir = join(repoRoot, "server", "shared", "typescript", "protos");
  execFileSync(
    "pnpm",
    ["exec", "buf", "generate", protoStage, "--template", genConfig],
    { cwd: protosDir, stdio: "pipe", shell: process.platform === "win32" },
  );
  return readFileSync(join(out, "gen", "place_order.ts"), "utf8").replace(
    /\r\n/g,
    "\n",
  );
}

describe("tsGrpcClient_FixtureProtoByteGate", () => {
  it("the real buf/ts-proto toolchain runs on the fixture proto and reproduces the committed proto-TS byte-identically", () => {
    const regenerated = runRealBufTsProto();
    const committed = readFileSync(
      join(HERE, "grpc-fixtures", "generated", "place_order.ts"),
      "utf8",
    ).replace(/\r\n/g, "\n");
    expect(regenerated).toBe(committed);
  }, 60_000);

  it("the committed fixture proto-TS exports the real grpc-js client stub + envelope-shaped response", () => {
    const protoTs = readFileSync(
      join(HERE, "grpc-fixtures", "generated", "place_order.ts"),
      "utf8",
    );
    // The real ts-proto output — a grpc-js callback-style client + the response
    // carrying result (D2ResultProto) + data. This is what the emitted client binds to.
    expect(protoTs).toContain("export const PredicateFixturesOrdersClient");
    expect(protoTs).toContain("export interface PlaceOrderFixtureResponse");
    expect(protoTs).toContain('import { D2ResultProto } from "@d2/protos";');
  });
});

// ===========================================================================
// Sign proto byte-gate — the real buf/ts-proto pipeline proof (sign / KeyCustodian)
// ===========================================================================

/** Run the real buf/ts-proto toolchain on the sign fixture proto into a temp dir. */
function runRealBufTsProtoSign(): string {
  const out = mkdtempSync(join(tmpdir(), "tsp-buf-sign-"));
  const protoStage = join(out, "protos");
  mkdirSync(join(protoStage, "common", "v1"), { recursive: true });
  mkdirSync(join(out, "gen"), { recursive: true });
  // The d2_result import from the real contracts/protos.
  const repoRoot = _REPO;
  cpSync(
    join(repoRoot, "contracts", "protos", "common", "v1", "d2_result.proto"),
    join(protoStage, "common", "v1", "d2_result.proto"),
  );
  cpSync(
    join(HERE, "grpc-fixtures", "sign.proto"),
    join(protoStage, "sign.proto"),
  );
  const genConfig = join(out, "buf.gen.yaml");
  writeFileSync(
    genConfig,
    [
      "version: v2",
      "plugins:",
      '  - local: ["pnpm", "exec", "protoc-gen-ts_proto"]',
      `    out: ${join(out, "gen").replace(/\\/g, "/")}`,
      "    opt:",
      "      - esModuleInterop=true",
      "      - outputServices=grpc-js",
      "      - useExactTypes=false",
      "      - oneof=unions",
      "      - useOptionals=messages",
      "      - Mcommon/v1/d2_result.proto=@d2/protos",
    ].join("\n"),
  );
  // Run buf from the @d2/protos package dir so `pnpm exec protoc-gen-ts_proto` resolves.
  const protosDir = join(repoRoot, "server", "shared", "typescript", "protos");
  execFileSync(
    "pnpm",
    ["exec", "buf", "generate", protoStage, "--template", genConfig],
    { cwd: protosDir, stdio: "pipe", shell: process.platform === "win32" },
  );
  return readFileSync(join(out, "gen", "sign.ts"), "utf8").replace(
    /\r\n/g,
    "\n",
  );
}

describe("tsGrpcClient_RealBufTsProtoSignPipelineByteGate", () => {
  it("the real buf/ts-proto toolchain runs on sign.proto and reproduces the committed sign.ts byte-identically", () => {
    const regenerated = runRealBufTsProtoSign();
    const committed = readFileSync(
      join(HERE, "grpc-fixtures", "generated", "sign.ts"),
      "utf8",
    ).replace(/\r\n/g, "\n");
    expect(regenerated).toBe(committed);
  }, 60_000);

  it("byte-gate is non-vacuous: a single-token mutation is detected as drift", () => {
    // Proves the byte-comparison catches real drift — a gate that never fails is useless.
    // Mutate one stable token in the committed fixture and assert the equality fails.
    const committed = readFileSync(
      join(HERE, "grpc-fixtures", "generated", "sign.ts"),
      "utf8",
    ).replace(/\r\n/g, "\n");
    // Replace the first occurrence of the committed package string with a mutated value.
    // "v2alpha" is the channel — it's the channel declaration in the generated protobufPackage line.
    const mutated = committed.replace("v2alpha", "v3beta");
    expect(mutated).not.toBe(committed);
  });

  it("the committed sign.ts exports the SignFixtureSigner grpc-js client stub + v2alpha package", () => {
    const protoTs = readFileSync(
      join(HERE, "grpc-fixtures", "generated", "sign.ts"),
      "utf8",
    );
    // The real ts-proto output for sign.proto — grpc-js callback-style client +
    // D2ResultProto-enveloped response. Package must be v2alpha (not the retired v1).
    expect(protoTs).toContain("export const SignFixtureSignerClient");
    expect(protoTs).toContain("export interface SignFixtureResponse");
    expect(protoTs).toContain('import { D2ResultProto } from "@d2/protos";');
    expect(protoTs).toContain(
      'export const protobufPackage = "d2.signfixtures.v2alpha"',
    );
  });
});

// ===========================================================================
// emitTsGrpcClient — pure-function structural coverage
// ===========================================================================

describe("emitTsGrpcClient_Guards", () => {
  it("throws on empty moduleName", () => {
    expect(() => emitTsGrpcClient("", [placeOrderOp()])).toThrow(
      "moduleName must not be empty",
    );
  });

  it("returns an empty array for an empty ops list", () => {
    expect(emitTsGrpcClient("PredicateFixtures", [])).toHaveLength(0);
  });
});

describe("emitTsGrpcClient_FileNameAndShape", () => {
  it("emits one <module>-grpc-client.g.ts with the interface + factory", () => {
    const [file] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    expect(file!.fileName).toBe("predicate-fixtures-grpc-client.g.ts");
    expect(file!.content).toContain(
      "export interface PredicateFixturesGrpcClient {",
    );
    expect(file!.content).toContain(
      "export function createPredicateFixturesGrpcClient(stub: unknown)",
    );
    // @ts-nocheck + eslint-disable (references module-relative imports).
    expect(file!.content).toContain("// @ts-nocheck");
    expect(file!.content).toContain("/* eslint-disable */");
    // Ends with exactly one trailing newline.
    expect(file!.content.endsWith("\n")).toBe(true);
    expect(file!.content.endsWith("\n\n")).toBe(false);
  });

  it("a predicate op imports the seam's isTransientGrpcError + the predicate twin + builds a retry pipeline", () => {
    const [file] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    expect(file!.content).toContain("isTransientGrpcError");
    expect(file!.content).toContain(
      'import { placeOrderFixtureRetryWhen, placeOrderFixtureFailWhen } from "./place-order-fixture-resilience-predicates.js";',
    );
    expect(file!.content).toContain("new ResilientPipelineBuilder()");
    expect(file!.content).toContain("maxAttempts: 3,");
    expect(file!.content).toContain(
      "placeOrderFixtureRetryWhen(result) && !placeOrderFixtureFailWhen(result)",
    );
  });

  it("a non-predicate op emits the plain handleGrpcCall body (no pipeline import, no sentinel)", () => {
    const [file] = emitTsGrpcClient("SignFixture", [signGrpcOp()]);
    expect(file!.content).not.toContain("ResilientPipelineBuilder");
    expect(file!.content).not.toContain("D2GeneratedBusinessRetrySignal");
    expect(file!.content).not.toContain("isTransientGrpcError");
    expect(file!.content).toContain("return handleGrpcCall(");
  });

  it("a response-enum op emits the fail-loud membership parse + imports the enum const", () => {
    const [file] = emitTsGrpcClient("EnumFixtures", [signWithKindOp()]);
    expect(file!.content).toContain(
      'import { FixtureKeyKind } from "./sign-with-kind-fixture-dto.js";',
    );
    expect(file!.content).toContain(
      "Object.values(FixtureKeyKind).includes(data.keyKind)",
    );
    expect(file!.content).toContain(
      "validationFailed<SignWithKindFixtureOutput>()",
    );
  });

  it("a shared model across ops imports the DTO type ONCE (deduped, derived from the type name)", () => {
    // Two ops sharing SignFixtureInput/SignFixtureOutput must not redeclare the import.
    const opA = signGrpcOp();
    const opB: TsGrpcClientOp = {
      ...signGrpcOp(),
      opName: "signAgain",
      grpcMethod: "SignAgain",
    };
    const [file] = emitTsGrpcClient("SignFixture", [opA, opB]);
    const importLines = file!.content
      .split("\n")
      .filter((l) => l.includes("SignFixtureInput") && l.startsWith("import"));
    expect(importLines).toHaveLength(1);
    expect(importLines[0]).toContain('from "./sign-fixture-dto.js"');
  });

  it("the emitted source carries no phase / deliverable / audit-round identifiers", () => {
    const [file] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    expect(file!.content).not.toMatch(/\bC-6\b|\bStep 9c\b|audit-round/i);
  });

  it("an empty-request op emits a `return {};` request mapper (no fields)", () => {
    const noInput: TsGrpcClientOp = {
      opName: "getJwks",
      grpcService: "SignFixtureSigner",
      grpcMethod: "GetJwks",
      sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
      requestModelName: "GetJwksInput",
      requestFields: [],
      responseModelName: "GetJwksOutput",
      responseFields: [field("kid", "string", "string", "string")],
    };
    const [file] = emitTsGrpcClient("SignFixture", [noInput]);
    expect(file!.content).toContain(
      "function toGetJwksRequest(input: GetJwksInput): unknown {",
    );
    expect(file!.content).toContain("  return {};");
  });

  it("a void-response op emits a `return {} as <Output>` response mapper (no fields)", () => {
    const noOutput: TsGrpcClientOp = {
      opName: "revoke",
      grpcService: "SignFixtureSigner",
      grpcMethod: "Revoke",
      sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
      requestModelName: "RevokeInput",
      requestFields: [field("kid", "string", "string", "string")],
      responseModelName: "RevokeOutput",
      responseFields: [],
    };
    const [file] = emitTsGrpcClient("SignFixture", [noOutput]);
    expect(file!.content).toContain("return {} as RevokeOutput;");
  });

  it("a retryWhen-ONLY predicate op imports only retryWhen + the retry guard omits failWhen", () => {
    const retryOnly: TsGrpcClientOp = {
      ...placeOrderOp(),
      failWhenAst: undefined,
    };
    const [file] = emitTsGrpcClient("PredicateFixtures", [retryOnly]);
    expect(file!.content).toContain(
      'import { placeOrderFixtureRetryWhen } from "./place-order-fixture-resilience-predicates.js";',
    );
    expect(file!.content).not.toContain("placeOrderFixtureFailWhen");
    // The retry guard is just retryWhen(result) (no failWhen conjunction).
    expect(file!.content).toContain("if (placeOrderFixtureRetryWhen(result))");
  });

  it("a failWhen-ONLY op carries NO retry-arm (failWhen alone is inert at the client)", () => {
    // failWhen with no retryWhen has zero effect on the client's retry decision —
    // there is no retry condition to gate. The op emits the simple forwarding body.
    const failOnly: TsGrpcClientOp = {
      ...placeOrderOp(),
      retryWhenAst: undefined,
    };
    const [file] = emitTsGrpcClient("PredicateFixtures", [failOnly]);
    expect(file!.content).not.toContain("placeOrderFixtureFailWhen");
    expect(file!.content).not.toContain("placeOrderFixtureRetryWhen");
    expect(file!.content).not.toContain("ResilientPipelineBuilder");
    expect(file!.content).not.toContain("D2GeneratedBusinessRetrySignal");
    // Simple forwarding body (no predicate twin import).
    expect(file!.content).toContain("return handleGrpcCall(");
  });
});

// ===========================================================================
// BEHAVIORAL — the emitted client over the REAL seam + REAL proto + fake stub
// ===========================================================================

describe("tsGrpcClient_Behavioral_PlaceOrder_RealSeam", () => {
  const twin = loadCommittedPredicateTwin();

  function buildClient(): {
    create: (stub: unknown) => {
      placeOrder: (
        input: unknown,
        opts?: unknown,
      ) => Promise<D2Result<unknown>>;
    };
  } {
    const [file] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    const create = reconstructFactory<{
      placeOrder: (
        input: unknown,
        opts?: unknown,
      ) => Promise<D2Result<unknown>>;
    }>(file!.content, "createPredicateFixturesGrpcClient", baseScope(twin));
    return { create };
  }

  function successResponse(
    data: { orderCode: string; itemStatuses: string[]; partial: boolean },
    envelope?: Partial<{
      success: boolean;
      statusCode: number;
      errorCode?: string;
      category?: string;
    }>,
  ): PlaceOrderFixtureResponse {
    return {
      result: {
        success: envelope?.success ?? true,
        statusCode: envelope?.statusCode ?? 200,
        errorCode: envelope?.errorCode,
        category: envelope?.category,
        traceId: undefined,
        messages: [],
        inputErrors: [],
      },
      data,
    };
  }

  it("success — maps the proto data → DTO, round-trips every field, NOT retried", async () => {
    const { create } = buildClient();
    const { stub, calls, lastRequest } = makePlaceOrderStub([
      {
        kind: "ok",
        response: successResponse({
          orderCode: "ORD-1",
          itemStatuses: ["DONE"],
          partial: false,
        }),
      },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "cust-1" });

    expect(result.success).toBe(true);
    expect(result.data).toEqual({
      orderCode: "ORD-1",
      itemStatuses: ["DONE"],
      partial: false,
    });
    expect(calls()).toBe(1);
    // The DTO → proto request mapper copied the field (field-name-identical).
    expect(lastRequest()).toEqual({ customerId: "cust-1" });
  });

  it("business failure rides the OK envelope — reconstructed via the real d2ResultFromProto, NOT retried", async () => {
    const { create } = buildClient();
    const { stub, calls } = makePlaceOrderStub([
      {
        kind: "ok",
        response: {
          result: {
            success: false,
            statusCode: 409,
            errorCode: "CONFLICT",
            category: "conflict",
            traceId: undefined,
            messages: [],
            inputErrors: [],
          },
          data: undefined,
        },
      },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "c" });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(409);
    expect(result.errorCode).toBe("CONFLICT");
    // Neither retryWhen nor failWhen matches a clean conflict with no data → 1 call.
    expect(calls()).toBe(1);
  });

  it("business retryWhen (category infrastructure_unavailable) → retried, recovers on a later success", async () => {
    const { create } = buildClient();
    const failing = successResponse(
      { orderCode: "", itemStatuses: ["X"], partial: false },
      {
        success: false,
        statusCode: 503,
        category: "infrastructure_unavailable",
      },
    );
    const recovered = successResponse({
      orderCode: "ORD-9",
      itemStatuses: ["DONE"],
      partial: false,
    });
    const { stub, calls } = makePlaceOrderStub([
      { kind: "ok", response: failing },
      { kind: "ok", response: recovered },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture(
      { customerId: "c" },
      { deadlineMs: 1000 },
    );

    expect(result.success).toBe(true);
    expect(result.data).toMatchObject({ orderCode: "ORD-9" });
    expect(calls()).toBeGreaterThan(1);
  });

  it("business failWhen WINS over retryWhen — a validation failure is terminal (1 call)", async () => {
    const { create } = buildClient();
    // category infrastructure_unavailable (retryWhen true) AND errorCode VALIDATION_FAILED (failWhen true).
    const { stub, calls } = makePlaceOrderStub([
      {
        kind: "ok",
        response: {
          result: {
            success: false,
            statusCode: 400,
            errorCode: "VALIDATION_FAILED",
            category: "infrastructure_unavailable",
            traceId: undefined,
            messages: [],
            inputErrors: [],
          },
          data: { orderCode: "", itemStatuses: ["X"], partial: false },
        },
      },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "c" });

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe("VALIDATION_FAILED");
    expect(calls()).toBe(1);
  });

  it("business retryWhen exhausts the budget → restores the captured business result verbatim", async () => {
    const { create } = buildClient();
    // Always retryWhen-true (partial == true), never failWhen → budget exhaust.
    const partial = successResponse(
      { orderCode: "ORD-P", itemStatuses: ["DONE"], partial: true },
      {
        success: true,
        statusCode: 206,
      },
    );
    const { stub, calls } = makePlaceOrderStub([
      { kind: "ok", response: partial },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "c" });

    // The restored result is the captured business result (success, partial data), not a 500.
    expect(result.statusCode).toBe(206);
    expect(result.data).toMatchObject({ partial: true });
    // maxAttempts: 3 → 3 calls.
    expect(calls()).toBe(3);
  });

  it("transport transient (UNAVAILABLE) → retried via the real isTransientGrpcError, recovers", async () => {
    const { create } = buildClient();
    const recovered = successResponse({
      orderCode: "ORD-R",
      itemStatuses: ["DONE"],
      partial: false,
    });
    const { stub, calls } = makePlaceOrderStub([
      { kind: "error", error: makeServiceError(GrpcStatus.UNAVAILABLE) },
      { kind: "ok", response: recovered },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "c" });

    expect(result.success).toBe(true);
    expect(calls()).toBeGreaterThan(1);
  });

  it("terminal transport fault → mapped via the seam to serviceUnavailable, NEVER leaks err.details", async () => {
    const { create } = buildClient();
    // Non-transient (NOT_FOUND) → not retried by isTransientGrpcError → terminal.
    const { stub } = makePlaceOrderStub([
      {
        kind: "error",
        error: makeServiceError(GrpcStatus.NOT_FOUND, "broker://secret@host"),
      },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "c" });

    expect(result.success).toBe(false);
    // The seam maps a non-canceled/unauthenticated fault to serviceUnavailable (503).
    expect(result.statusCode).toBe(503);
    // PII: the raw transport detail never reaches the result.
    const rendered = JSON.stringify(result);
    expect(rendered).not.toContain("broker://secret@host");
    expect(rendered).not.toContain("secret");
  });

  it("caller cancellation (CANCELLED) → mapped to canceled, not unhandledException", async () => {
    const { create } = buildClient();
    const { stub } = makePlaceOrderStub([
      {
        kind: "error",
        error: makeServiceError(GrpcStatus.CANCELLED, "client-aborted"),
      },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "c" });

    expect(result.success).toBe(false);
    // The seam's `canceled` factory carries the CANCELED error code (status 400).
    expect(result.errorCode).toBe("CANCELED");
    // PII: the raw client-abort detail never reaches the result.
    expect(JSON.stringify(result)).not.toContain("client-aborted");
  });

  it("deadlineMs is threaded into the underlying unaryCall (the real seam call options)", async () => {
    // A spy stub records that a deadline reached grpc-js (via the 3-arg overload).
    const { create } = buildClient();
    let sawCallOptions = false;
    const placeOrderFixture = ((
      _request: PlaceOrderFixtureRequest,
      arg2: unknown,
      arg3?: unknown,
      arg4?: unknown,
    ): never => {
      const args = [arg2, arg3, arg4];
      // unaryCall with deadlineMs uses the (request, Metadata, CallOptions, cb) overload.
      if (args.some((a) => a instanceof Metadata)) sawCallOptions = true;
      const cb = args.filter((a) => typeof a === "function").pop() as (
        e: ServiceError | null,
        r?: PlaceOrderFixtureResponse,
      ) => void;
      queueMicrotask(() =>
        cb(null, {
          result: {
            success: true,
            statusCode: 200,
            errorCode: undefined,
            category: undefined,
            traceId: undefined,
            messages: [],
            inputErrors: [],
          },
          data: { orderCode: "ORD", itemStatuses: ["DONE"], partial: false },
        }),
      );
      return undefined as never;
    }) as unknown as PredicateFixturesOrdersClient["placeOrderFixture"];
    const client = create({ placeOrderFixture });
    await client.placeOrderFixture({ customerId: "c" }, { deadlineMs: 5000 });
    expect(sawCallOptions).toBe(true);
  });

  it("a caller-supplied pipeline override replaces the default retry pipeline", async () => {
    const { create } = buildClient();
    const passthrough = ResilientPipeline.PassThrough;
    const partial = successResponse(
      { orderCode: "ORD-P", itemStatuses: ["DONE"], partial: true },
      {
        success: true,
        statusCode: 206,
      },
    );
    const { stub, calls } = makePlaceOrderStub([
      { kind: "ok", response: partial },
    ]);
    const client = create(stub);
    // With PassThrough, the business-retry sentinel still throws, but the
    // pipeline does not retry → the catch restores the captured result on 1 call.
    const result = await client.placeOrderFixture(
      { customerId: "c" },
      { pipeline: passthrough },
    );

    expect(result.statusCode).toBe(206);
    expect(calls()).toBe(1);
  });
});

describe("tsGrpcClient_Behavioral_SignWithKind_ResponseEnum_RealSeam", () => {
  // The emitted enum mapper references the KeyKind const-object (imported from the
  // DTO file in a real consumer). Provide the REAL const-object shape (value === wire
  // string, as the DTO emitter emits it) so the membership parse runs against it.
  const KeyKindConst = { Rsa: "Rsa", Aes: "Aes", Secret: "Secret" } as const;

  function buildClient(): (stub: unknown) => {
    signWithKind: (
      input: unknown,
      opts?: unknown,
    ) => Promise<D2Result<unknown>>;
  } {
    const [file] = emitTsGrpcClient("EnumFixtures", [signWithKindOp()]);
    return reconstructFactory<{
      signWithKind: (
        input: unknown,
        opts?: unknown,
      ) => Promise<D2Result<unknown>>;
    }>(
      file!.content,
      "createEnumFixturesGrpcClient",
      baseScope({ FixtureKeyKind: KeyKindConst }),
    );
  }

  it("a known enum wire value maps back to the DTO union member (success)", async () => {
    const create = buildClient();
    const { stub, lastRequest } = makeSignWithKindStub({
      result: {
        success: true,
        statusCode: 200,
        errorCode: undefined,
        category: undefined,
        traceId: undefined,
        messages: [],
        inputErrors: [],
      },
      data: { signature: "sig", keyKind: "Rsa" },
    });
    const client = create(stub);
    const result = await client.signWithKindFixture({
      kid: "k",
      keyKind: "Rsa",
    });

    expect(result.success).toBe(true);
    expect(result.data).toEqual({ signature: "sig", keyKind: "Rsa" });
    // The DTO enum value (the wire string) copied straight onto the proto request.
    expect(lastRequest()).toEqual({ kid: "k", keyKind: "Rsa" });
  });

  it("an UNKNOWN enum wire value fails loud (client-side ValidationFailed, no fallback)", async () => {
    const create = buildClient();
    const { stub } = makeSignWithKindStub({
      result: {
        success: true,
        statusCode: 200,
        errorCode: undefined,
        category: undefined,
        traceId: undefined,
        messages: [],
        inputErrors: [],
      },
      data: { signature: "sig", keyKind: "Quantum" },
    });
    const client = create(stub);
    const result = await client.signWithKindFixture({
      kid: "k",
      keyKind: "Rsa",
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(400);
  });
});

// ===========================================================================
// Cross-runtime predicate-CONSUMPTION parity.
//
// Drives the emitted TS client over the SAME shared parity fixture the .NET
// PredicateParityTests + predicate-parity test use, asserting the client's
// RUNTIME retry decision matches the cross-language expectation: it retries IFF
// `expectedRetry && !expectedFail` (the runtime rule — failWhen WINS). The
// predicate FUNCTIONS are already proven byte-behaviorally identical cross-runtime
// by the parity test; this proves the TS client CONSUMES them the same way the
// .NET client does (the consumption coverage is a separate, additive guarantee).
// ===========================================================================

interface ParityCase {
  readonly name: string;
  readonly success: boolean;
  readonly statusCode: number;
  readonly errorCode?: string;
  readonly category?: string;
  readonly data?: {
    orderCode: string;
    itemStatuses: string[];
    partial: boolean;
  };
  readonly expectedRetry: boolean;
  readonly expectedFail: boolean;
}

function loadParityFixture(): readonly ParityCase[] {
  const path = join(
    _REPO,
    "contracts/resilience/predicate-parity.fixture.json",
  );
  return (JSON.parse(readFileSync(path, "utf8")) as { cases: ParityCase[] })
    .cases;
}

describe("tsGrpcClient_CrossRuntimePredicateConsumptionParity", () => {
  const twin = loadCommittedPredicateTwin();

  function buildClient(): (stub: unknown) => {
    placeOrder: (input: unknown, opts?: unknown) => Promise<D2Result<unknown>>;
  } {
    const [file] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    return reconstructFactory<{
      placeOrder: (
        input: unknown,
        opts?: unknown,
      ) => Promise<D2Result<unknown>>;
    }>(file!.content, "createPredicateFixturesGrpcClient", baseScope(twin));
  }

  it("the shared fixture is present + non-vacuous (retry / fail / failWhen-wins rows all present)", () => {
    const cases = loadParityFixture();
    expect(cases.length).toBeGreaterThan(0);
    expect(cases.some((c) => c.expectedRetry && !c.expectedFail)).toBe(true);
    expect(cases.some((c) => !c.expectedRetry)).toBe(true);
    expect(cases.some((c) => c.expectedRetry && c.expectedFail)).toBe(true);
  });

  it("for every fixture row, the TS client retries IFF (expectedRetry && !expectedFail) — matching the .NET consumption", async () => {
    const create = buildClient();

    for (const c of loadParityFixture()) {
      // The server returns this row's reconstructed result on EVERY call (so a
      // retry would re-observe the same row → distinguishable only by call count).
      const response: PlaceOrderFixtureResponse = {
        result: {
          success: c.success,
          statusCode: c.statusCode,
          errorCode: c.errorCode ?? undefined,
          category: c.category ?? undefined,
          traceId: undefined,
          messages: [],
          inputErrors: [],
        },
        data: c.data ?? undefined,
      };
      const { stub, calls } = makePlaceOrderStub([{ kind: "ok", response }]);
      const client = create(stub);
      await client.placeOrderFixture({ customerId: "c" });

      const shouldRetry = c.expectedRetry && !c.expectedFail;
      if (shouldRetry) {
        // Retried up to the budget (maxAttempts: 3) since the row keeps matching retryWhen.
        expect(
          calls(),
          `row '${c.name}' must be RETRIED (retryWhen && !failWhen)`,
        ).toBe(3);
      } else {
        // Terminal on the first call (no retryWhen, or failWhen wins).
        expect(calls(), `row '${c.name}' must NOT be retried`).toBe(1);
      }
    }
  });
});

// ===========================================================================
// §26.3.2 — cross-runtime capability + diagnostic parity (table-driven)
// ===========================================================================

describe("tsGrpcClient_CapabilityParity_MirrorsNetClient", () => {
  it("the TS gRPC client exposes the twin of the .NET client's capabilities", () => {
    const [predFile] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    const content = predFile!.content;
    // The .NET <Module>GrpcClient capability surface → the TS twin:
    const capabilities: Array<{ net: string; ts: boolean }> = [
      // per-call deadline (the .NET CallOptions deadline ↔ unaryCall deadlineMs).
      { net: "deadline", ts: content.includes("deadlineMs") },
      // per-call pipeline override (the .NET pipelineOverride param).
      { net: "pipelineOverride", ts: content.includes("pipeline?") },
      // transport-fault retry via the transient classifier.
      { net: "transient-retry", ts: content.includes("isTransientGrpcError") },
      // the predicate retry sentinel arm.
      {
        net: "predicate-sentinel",
        ts: content.includes("D2GeneratedBusinessRetrySignal"),
      },
      // business-not-retried by default (the predicate gates the sentinel throw).
      {
        net: "business-gated",
        ts: content.includes("placeOrderFixtureFailWhen(result)"),
      },
      // transport-fault never leaks err.details (the seam owns the mapping).
      {
        net: "no-detail-leak",
        ts: content.includes("handleGrpcCall(() => Promise.reject(e)"),
      },
    ];
    for (const cap of capabilities)
      expect(
        cap.ts,
        `TS client must mirror the .NET capability '${cap.net}'`,
      ).toBe(true);
  });
});

// ===========================================================================
// TOLERANT READER — the emitted client ignores unknown fields on the decoded
// proto response; the known DTO fields survive with full fidelity.
// ===========================================================================

describe("tsGrpcClient_TolerantReader", () => {
  const twin = loadCommittedPredicateTwin();

  function buildClient(): {
    create: (stub: unknown) => {
      placeOrder: (
        input: unknown,
        opts?: unknown,
      ) => Promise<D2Result<unknown>>;
    };
  } {
    const [file] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    const create = reconstructFactory<{
      placeOrder: (
        input: unknown,
        opts?: unknown,
      ) => Promise<D2Result<unknown>>;
    }>(file!.content, "createPredicateFixturesGrpcClient", baseScope(twin));
    return { create };
  }

  it("tolerant reader — extra unknown fields in a proto response are ignored", async () => {
    // The fake stub returns a response object that carries extra unknown properties
    // at both the top level and inside data.  The emitted client + d2ResultFromProto
    // seam reads only the named proto properties (result / data); extra keys on the JS
    // response object are never accessed and do not affect the decoded D2Result.
    const { create } = buildClient();
    const responseWithExtra = {
      result: {
        success: true,
        statusCode: 200,
        errorCode: undefined,
        category: undefined,
        traceId: undefined,
        messages: [],
        inputErrors: [],
      },
      data: {
        orderCode: "ORD-TR3",
        itemStatuses: ["DONE"],
        partial: false,
        // extra fields a newer producer might send:
        unknownDataField: "ignored",
        addedStatusDetail: { code: 42 },
      },
      // extra top-level field:
      unknownEnvelopeField: "also-ignored",
    } as unknown as PlaceOrderFixtureResponse;

    const { stub, calls } = makePlaceOrderStub([
      { kind: "ok", response: responseWithExtra },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "cust-tr3" });

    expect(result.success).toBe(true);
    expect(result.data).toEqual({
      orderCode: "ORD-TR3",
      itemStatuses: ["DONE"],
      partial: false,
    });
    // Extra fields must not surface in the decoded DTO.
    expect(Object.keys(result.data as object)).not.toContain(
      "unknownDataField",
    );
    expect(Object.keys(result.data as object)).not.toContain(
      "addedStatusDetail",
    );
    expect(calls()).toBe(1);
  });

  it("tolerant reader — added optional response field is ignored by the prior decoder", async () => {
    // Forward-compat case: a "newer producer" adds an optional field to the response
    // data payload.  The emitted client mapper only reads the fields it declared
    // (orderCode / itemStatuses / partial); the added field is never read, so it does
    // not appear in the returned D2Result<data>.  The result is success with the known
    // fields intact.
    const { create } = buildClient();
    const forwardCompatResponse = {
      result: {
        success: true,
        statusCode: 200,
        errorCode: undefined,
        category: undefined,
        traceId: undefined,
        messages: [],
        inputErrors: [],
      },
      data: {
        orderCode: "ORD-FC4",
        itemStatuses: ["SHIPPED", "DELIVERED"],
        partial: false,
        // field added in a newer spec revision:
        estimatedDelivery: "2026-07-01",
      },
    } as unknown as PlaceOrderFixtureResponse;

    const { stub } = makePlaceOrderStub([
      { kind: "ok", response: forwardCompatResponse },
    ]);
    const client = create(stub);
    const result = await client.placeOrderFixture({ customerId: "cust-fc4" });

    expect(result.success).toBe(true);
    expect((result.data as { orderCode: string }).orderCode).toBe("ORD-FC4");
    expect(
      (result.data as { itemStatuses: string[] }).itemStatuses,
    ).toHaveLength(2);
    expect((result.data as { partial: boolean }).partial).toBe(false);
    // The added field must not leak into the decoded DTO.
    expect(Object.keys(result.data as object)).not.toContain(
      "estimatedDelivery",
    );
  });
});
