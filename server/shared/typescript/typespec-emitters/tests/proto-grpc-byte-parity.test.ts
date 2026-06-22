// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Byte-parity gate for the proto + gRPC service-impl emitters.
//
// Each describe block:
//   1. Asserts byte-identical match between re-emitted content and the
//      committed fixture constant.
//   2. Contains a deliberate-drift negative that proves the gate is
//      non-vacuous (§26.5.1 + §1.20): a mutated fixture does NOT match,
//      so real drift would be caught.

import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";
import type { FieldInfo } from "../src/lib/model-walk.js";
import { emitProto } from "../src/lib/proto-emitter.js";
import { emitGrpcService } from "../src/lib/grpc-service-emitter.js";
import type { GrpcDelegationTarget } from "../src/lib/grpc-service-emitter.js";
import {
  emitGrpcClient,
  emitClientKeys,
} from "../src/lib/grpc-client-emitter.js";
import type { GrpcClientOp } from "../src/lib/grpc-client-emitter.js";

// ---------------------------------------------------------------------------
// Committed-file path constants + readFixture helper
// ---------------------------------------------------------------------------

const REPO = findRepoRoot(import.meta.url);

/** Committed home for gRPC service + mapper + client fixtures (TypeSpecGrpc/Generated/). */
const GRPC_HOME = join(
  REPO,
  "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/Generated",
);

/** Committed home for the gRPC .proto fixture (TypeSpecGrpc/Protos/). */
const GRPC_PROTOS = join(
  REPO,
  "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/Protos",
);

/**
 * Read a committed generated file and normalize line endings.
 * Committed generated files are LF; the emitter joins with "\n". Normalize the
 * on-disk read defensively (git working-tree may have CRLF) before comparing.
 */
function readFixture(absPath: string): string {
  return readFileSync(absPath, "utf8").replace(/\r\n/g, "\n");
}

// ---------------------------------------------------------------------------
// Field stubs matching the sign fixture
// ---------------------------------------------------------------------------

function buildSignInputFields(): readonly FieldInfo[] {
  return [
    {
      name: "kid",
      csName: "Kid",
      csType: "string",
      tsName: "kid",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redact: false,
    },
    {
      name: "payload",
      csName: "Payload",
      csType: "byte[]",
      tsName: "payload",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redact: true,
    },
  ];
}

function buildSignOutputFields(): readonly FieldInfo[] {
  return [
    {
      name: "signature",
      csName: "Signature",
      csType: "string",
      tsName: "signature",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redact: false,
    },
  ];
}

const SOURCE = "contracts/typespec/fixtures/sign-shaped.tsp";

// ---------------------------------------------------------------------------
// byteParity_SignProto
// ---------------------------------------------------------------------------

describe("byteParity_SignProto_CommittedFixtureIdentical", () => {
  it("re-emitted .proto is byte-identical to the committed fixture", () => {
    const result = emitProto(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      "unary",
      "d2.keycustodian.v1",
      "D2.Services.Protos.KeyCustodian.V1",
      SOURCE,
      "SignRequest",
      buildSignInputFields(),
      "SignOutput", // data message name — wrapper is always <grpcMethod>Response
      buildSignOutputFields(),
      [],
      () => {},
    );
    expect(result).toBeDefined();
    expect(result!.content).toBe(
      readFixture(join(GRPC_PROTOS, "key_custodian_signer_sign.g.proto")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    // §1.20 non-vacuous guard: corrupt the fixture by one byte — D2ResultProto → DRIFTED.
    const drifted = readFixture(
      join(GRPC_PROTOS, "key_custodian_signer_sign.g.proto"),
    ).replace("D2ResultProto", "D2ResultProtoDRIFTED");
    const result = emitProto(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      "unary",
      "d2.keycustodian.v1",
      "D2.Services.Protos.KeyCustodian.V1",
      SOURCE,
      "SignRequest",
      buildSignInputFields(),
      "SignOutput",
      buildSignOutputFields(),
      [],
      () => {},
    );
    expect(result!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_KeyCustodianSignerService (re-pointed to façade delegation)
// ---------------------------------------------------------------------------

/** Fixture façade delegation target for the sign op (matches the committed .g.cs). */
const SIGN_FACADE_TARGET: GrpcDelegationTarget = {
  kind: "facade",
  typeName: "IKeyCustodianSignerFacade",
  methodName: "SignAsync",
  targetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
};

describe("byteParity_KeyCustodianSignerService_FacadeDelegation_CommittedFixtureIdentical", () => {
  it("re-emitted service .g.cs (façade delegation) is byte-identical to the committed fixture", () => {
    const [svc] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      "D2.Services.Protos.KeyCustodian.V1",
      "D2.Edge.Tests.TypeSpecGrpc.Generated",
      "D2.Edge.Tests.TypeSpecDto.Generated",
      SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      buildSignInputFields(),
      "SignOutput",
      buildSignOutputFields(),
      SIGN_FACADE_TARGET,
    );
    expect(svc.content).toBe(
      readFixture(join(GRPC_HOME, "KeyCustodianSignerService.g.cs")),
    );
  });

  it("deliberate-drift detection: handler delegation does NOT match façade fixture", () => {
    // Substituting SignAsync → HandleAsync would produce a mismatch (non-vacuous gate).
    const drifted = readFixture(
      join(GRPC_HOME, "KeyCustodianSignerService.g.cs"),
    ).replace("facade.SignAsync", "handler.HandleAsync");
    const [svc] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      "D2.Services.Protos.KeyCustodian.V1",
      "D2.Edge.Tests.TypeSpecGrpc.Generated",
      "D2.Edge.Tests.TypeSpecDto.Generated",
      SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      buildSignInputFields(),
      "SignOutput",
      buildSignOutputFields(),
      SIGN_FACADE_TARGET,
    );
    expect(svc.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignTransportMappers
// ---------------------------------------------------------------------------

describe("byteParity_SignTransportMappers_CommittedFixtureIdentical", () => {
  it("re-emitted mapper .g.cs is byte-identical to the committed fixture", () => {
    const [, mapper] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      "D2.Services.Protos.KeyCustodian.V1",
      "D2.Edge.Tests.TypeSpecGrpc.Generated",
      "D2.Edge.Tests.TypeSpecDto.Generated",
      SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      buildSignInputFields(),
      "SignOutput",
      buildSignOutputFields(),
    );
    expect(mapper.content).toBe(
      readFixture(join(GRPC_HOME, "SignTransportMappers.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "SignTransportMappers.g.cs"),
    ).replace("SignTransportMappers", "SignTransportMappersDRIFTED");
    const [, mapper] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      "D2.Services.Protos.KeyCustodian.V1",
      "D2.Edge.Tests.TypeSpecGrpc.Generated",
      "D2.Edge.Tests.TypeSpecDto.Generated",
      SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      buildSignInputFields(),
      "SignOutput",
      buildSignOutputFields(),
    );
    expect(mapper.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Client-fixture byte-parity gates (emitGrpcClient + emitClientKeys)
// ---------------------------------------------------------------------------

// The committed client fixtures live in the test project's gRPC namespace
// (D2.Edge.Tests.TypeSpecGrpc.Generated) and reuse the existing test-project DTOs
// (D2.Edge.Tests.TypeSpecDto.Generated) — the same layout as the service fixtures above.
// The DTO namespace differs from the client namespace, so the emitter aliases the DTO
// types (global:: rooted) to disambiguate <Op>Output from the same-named proto data message.
const CLIENTS_NS = "D2.Edge.Tests.TypeSpecGrpc.Generated";
const CLIENT_DTO_NS = "D2.Edge.Tests.TypeSpecDto.Generated";

function buildClientSignOp(): GrpcClientOp {
  return {
    opName: "sign",
    grpcService: "KeyCustodianSigner",
    grpcMethod: "Sign",
    protoCsharpNs: "D2.Services.Protos.KeyCustodian.V1",
    dtoCsharpNs: CLIENT_DTO_NS,
    sourceSpec: SOURCE,
    requestModelName: "SignInput",
    requestFields: buildSignInputFields(),
    responseModelName: "SignOutput",
    responseFields: buildSignOutputFields(),
  };
}

// ---------------------------------------------------------------------------
// byteParity_IKeyCustodianGrpcClient
// ---------------------------------------------------------------------------

describe("byteParity_IKeyCustodianGrpcClient_CommittedFixtureIdentical", () => {
  it("re-emitted interface .g.cs is byte-identical to the committed fixture", () => {
    const [iface] = emitGrpcClient(
      "KeyCustodian",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(iface!.content).toBe(
      readFixture(join(GRPC_HOME, "IKeyCustodianGrpcClient.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "IKeyCustodianGrpcClient.g.cs"),
    ).replace("IKeyCustodianGrpcClient", "IKeyCustodianGrpcClientDRIFTED");
    const [iface] = emitGrpcClient(
      "KeyCustodian",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(iface!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_KeyCustodianGrpcClient (impl)
// ---------------------------------------------------------------------------

describe("byteParity_KeyCustodianGrpcClient_CommittedFixtureIdentical", () => {
  it("re-emitted impl .g.cs is byte-identical to the committed fixture", () => {
    const [, impl] = emitGrpcClient(
      "KeyCustodian",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(impl!.content).toBe(
      readFixture(join(GRPC_HOME, "KeyCustodianGrpcClient.g.cs")),
    );
  });

  it("deliberate-drift detection: removing D2Services.Protos.Common.V1 does NOT match", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "KeyCustodianGrpcClient.g.cs"),
    ).replace("using D2.Services.Protos.Common.V1;\n", "");
    const [, impl] = emitGrpcClient(
      "KeyCustodian",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(impl!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignClientMappers
// ---------------------------------------------------------------------------

describe("byteParity_SignClientMappers_CommittedFixtureIdentical", () => {
  it("re-emitted mapper .g.cs is byte-identical to the committed fixture", () => {
    const [, , mapper] = emitGrpcClient(
      "KeyCustodian",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(mapper!.content).toBe(
      readFixture(join(GRPC_HOME, "SignClientMappers.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "SignClientMappers.g.cs"),
    ).replace("SignClientMappers", "SignClientMappersDRIFTED");
    const [, , mapper] = emitGrpcClient(
      "KeyCustodian",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(mapper!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_KeyCustodianGrpcClientsGenerated (DI extension)
// ---------------------------------------------------------------------------

describe("byteParity_KeyCustodianGrpcClientsGenerated_CommittedFixtureIdentical", () => {
  it("re-emitted DI-ext .g.cs is byte-identical to the committed fixture", () => {
    const [, , , di] = emitGrpcClient(
      "KeyCustodian",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(di!.content).toBe(
      readFixture(join(GRPC_HOME, "KeyCustodianGrpcClientsGenerated.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "KeyCustodianGrpcClientsGenerated.g.cs"),
    ).replace(
      "AddD2KeyCustodianGrpcClients",
      "AddD2KeyCustodianGrpcClientsDRIFTED",
    );
    const [, , , di] = emitGrpcClient(
      "KeyCustodian",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(di!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignClientKeys
// ---------------------------------------------------------------------------

describe("byteParity_SignClientKeys_CommittedFixtureIdentical", () => {
  it("re-emitted keys .g.cs is byte-identical to the committed fixture", () => {
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.content).toBe(
      readFixture(join(GRPC_HOME, "SignClientKeys.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(join(GRPC_HOME, "SignClientKeys.g.cs")).replace(
      "SignGrpcClientPipeline",
      "SignGrpcClientPipelineDRIFTED",
    );
    const file = emitClientKeys("sign", CLIENTS_NS, SOURCE);
    expect(file.content).not.toBe(drifted);
  });
});
