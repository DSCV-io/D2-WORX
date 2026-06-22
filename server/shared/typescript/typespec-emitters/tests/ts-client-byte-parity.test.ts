// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Byte-parity gate for the emitted TS client .g.ts fixtures (the TS SSR gRPC
// client + the browser REST client). Regenerating the emitter output must
// produce byte-identical content to the committed .g.ts fixtures.
//
// The gate is non-vacuous per §26.5.1 + §1.20: a deliberate-drift case (mutate
// one token of the committed fixture) is asserted to NOT match the regenerated
// output — proving the gate FAILS on real divergence (never a tautology).

import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";
import {
  emitTsGrpcClient,
  type TsGrpcClientOp,
} from "../src/lib/ts-grpc-client-emitter.js";
import {
  emitTsRestClient,
  type TsRestClientOp,
} from "../src/lib/ts-rest-client-emitter.js";
import { parseResultPredicate } from "@d2/typespec-decorators";
import type { PredicateNode } from "@d2/typespec-decorators";
import type { FieldInfo } from "../src/lib/model-walk.js";

/** Parse a result-predicate expression to its AST, failing the test on a parse error. */
function parsePred(expr: string): PredicateNode {
  const parsed = parseResultPredicate(expr);
  if (!parsed.ok) throw new Error(`test predicate failed to parse: ${expr}`);

  return parsed.root;
}

const REPO = findRepoRoot(import.meta.url);
const KC = join(REPO, "server/services/edge/tests/Unit/KeyCustodian");
const PRED_GEN = join(KC, "TypeSpecGrpcPredicate/Generated");
const DTO_GEN = join(KC, "TypeSpecDto/Generated");
const ENUM_GEN = join(KC, "TypeSpecGrpcEnum/Generated");

const PRED_SRC = "contracts/typespec/fixtures/resilience-predicate-shaped.tsp";
const SIGN_SRC = "contracts/typespec/fixtures/sign-shaped.tsp";
const ENUM_SRC = "contracts/typespec/fixtures/enum-shaped.tsp";

function readFixture(absPath: string): string {
  // Committed generated files are LF; the emitter joins with "\n". Normalize the
  // on-disk read (git working-tree may have CRLF) before comparing.
  return readFileSync(absPath, "utf8").replace(/\r\n/g, "\n");
}

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
  name: "KeyKind",
  members: [
    { csName: "Rsa", wireValue: "Rsa", needsEnumMember: false },
    { csName: "Aes", wireValue: "Aes", needsEnumMember: false },
    { csName: "Secret", wireValue: "Secret", needsEnumMember: false },
  ],
};

// ---------------------------------------------------------------------------
// Op fixtures (mirror the committed-fixture op shapes EXACTLY).
// ---------------------------------------------------------------------------

function placeOrderOp(): TsGrpcClientOp {
  return {
    opName: "placeOrder",
    grpcService: "PredicateFixturesOrders",
    grpcMethod: "PlaceOrder",
    sourceSpec: PRED_SRC,
    requestModelName: "PlaceOrderInput",
    requestFields: [field("customerId", "string", "string", "string")],
    responseModelName: "PlaceOrderOutput",
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
    opName: "sign",
    grpcService: "KeyCustodianSigner",
    grpcMethod: "Sign",
    sourceSpec: SIGN_SRC,
    requestModelName: "SignInput",
    requestFields: [
      field("kid", "string", "string", "string"),
      field("payload", "byte[]", "Uint8Array", "bytes"),
    ],
    responseModelName: "SignOutput",
    responseFields: [field("signature", "string", "string", "string")],
  };
}

function signWithKindOp(): TsGrpcClientOp {
  return {
    opName: "signWithKind",
    grpcService: "EnumFixturesSigner",
    grpcMethod: "SignWithKind",
    sourceSpec: ENUM_SRC,
    requestModelName: "SignWithKindInput",
    requestFields: [
      field("kid", "string", "string", "string"),
      field("keyKind", "KeyKind", "KeyKind", "string", false, KEY_KIND),
    ],
    responseModelName: "SignWithKindOutput",
    responseFields: [
      field("signature", "string", "string", "string"),
      field("keyKind", "KeyKind", "KeyKind", "string", false, KEY_KIND),
    ],
  };
}

function signRestOp(): TsRestClientOp {
  return {
    opName: "sign",
    routePath: "/internal/v1/kc/sign",
    verb: "POST",
    authIntent: "scoped",
    sourceSpec: SIGN_SRC,
    requestModelName: "SignInput",
    requestFields: [
      field("kid", "string", "string", "string"),
      field("payload", "byte[]", "Uint8Array", "bytes"),
    ],
    responseModelName: "SignOutput",
    idempotencyKeySource: "header",
  };
}

function signDerivedRestOp(): TsRestClientOp {
  return {
    opName: "signDerived",
    routePath: "/internal/v1/kc/sign-derived",
    verb: "POST",
    authIntent: "scoped",
    sourceSpec: SIGN_SRC,
    requestModelName: "SignInput",
    requestFields: [
      field("kid", "string", "string", "string"),
      field("payload", "byte[]", "Uint8Array", "bytes"),
    ],
    responseModelName: "SignOutput",
    idempotencyKeySource: "derived",
  };
}

// ===========================================================================
// gRPC client byte-gates
// ===========================================================================

describe("tsClientByteParity_PredicateFixturesGrpcClient", () => {
  it("regenerated predicate-fixtures-grpc-client.g.ts is byte-identical to the committed fixture", () => {
    const [file] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    expect(file!.content).toBe(
      readFixture(join(PRED_GEN, "predicate-fixtures-grpc-client.g.ts")),
    );
  });

  it("deliberate-drift detection: a mutated fixture does NOT match regenerated output", () => {
    const drifted = readFixture(
      join(PRED_GEN, "predicate-fixtures-grpc-client.g.ts"),
    ).replace("placeOrderRetryWhen", "placeOrderRetryWhenDRIFTED");
    const [file] = emitTsGrpcClient("PredicateFixtures", [placeOrderOp()]);
    expect(file!.content).not.toBe(drifted);
  });
});

describe("tsClientByteParity_KeyCustodianGrpcClient", () => {
  it("regenerated key-custodian-grpc-client.g.ts is byte-identical to the committed fixture", () => {
    const [file] = emitTsGrpcClient("KeyCustodian", [signGrpcOp()]);
    expect(file!.content).toBe(
      readFixture(join(DTO_GEN, "key-custodian-grpc-client.g.ts")),
    );
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture(
      join(DTO_GEN, "key-custodian-grpc-client.g.ts"),
    ).replace("handleGrpcCall", "handleGrpcCallDRIFTED");
    const [file] = emitTsGrpcClient("KeyCustodian", [signGrpcOp()]);
    expect(file!.content).not.toBe(drifted);
  });
});

describe("tsClientByteParity_EnumFixturesGrpcClient", () => {
  it("regenerated enum-fixtures-grpc-client.g.ts is byte-identical to the committed fixture", () => {
    const [file] = emitTsGrpcClient("EnumFixtures", [signWithKindOp()]);
    expect(file!.content).toBe(
      readFixture(join(ENUM_GEN, "enum-fixtures-grpc-client.g.ts")),
    );
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture(
      join(ENUM_GEN, "enum-fixtures-grpc-client.g.ts"),
    ).replace("validationFailed", "validationFailedDRIFTED");
    const [file] = emitTsGrpcClient("EnumFixtures", [signWithKindOp()]);
    expect(file!.content).not.toBe(drifted);
  });
});

// ===========================================================================
// REST client byte-gate
// ===========================================================================

describe("tsClientByteParity_KeyCustodianRestClient", () => {
  it("regenerated key-custodian-rest-client.g.ts is byte-identical to the committed fixture", () => {
    const [file] = emitTsRestClient("KeyCustodian", [
      signRestOp(),
      signDerivedRestOp(),
    ]);
    expect(file!.content).toBe(
      readFixture(join(DTO_GEN, "key-custodian-rest-client.g.ts")),
    );
  });

  it("deliberate-drift detection: a mutated fixture does NOT match", () => {
    const drifted = readFixture(
      join(DTO_GEN, "key-custodian-rest-client.g.ts"),
    ).replace("apiCall", "apiCallDRIFTED");
    const [file] = emitTsRestClient("KeyCustodian", [
      signRestOp(),
      signDerivedRestOp(),
    ]);
    expect(file!.content).not.toBe(drifted);
  });
});
