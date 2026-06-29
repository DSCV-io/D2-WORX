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
import { emitWireVersionConstant } from "../src/lib/wire-version-emitter.js";
import { emitWireIdentityManifest } from "../src/lib/wire-manifest-emitter.js";
import { parseChannel } from "../src/lib/wire-channel.js";

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

function buildSignFixtureInputFields(): readonly FieldInfo[] {
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
      fieldNumber: 1,
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
      fieldNumber: 2,
    },
  ];
}

function buildSignFixtureOutputFields(): readonly FieldInfo[] {
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
      fieldNumber: 1,
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
      "signFixture",
      "SignFixtureSigner",
      "SignFixture",
      "unary",
      "d2.signfixtures.v2alpha",
      "D2.Services.Protos.SignFixtures.V2Alpha",
      SOURCE,
      "SignFixtureRequest",
      buildSignFixtureInputFields(),
      undefined,
      "SignFixtureOutput", // data message name — wrapper is always <grpcMethod>Response
      buildSignFixtureOutputFields(),
      undefined,
      [],
      () => {},
    );
    expect(result).toBeDefined();
    expect(result!.content).toBe(
      readFixture(
        join(GRPC_PROTOS, "sign_fixture_signer_sign_fixture.g.proto"),
      ),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    // §1.20 non-vacuous guard: corrupt the fixture by one byte — D2ResultProto → DRIFTED.
    const drifted = readFixture(
      join(GRPC_PROTOS, "sign_fixture_signer_sign_fixture.g.proto"),
    ).replace("D2ResultProto", "D2ResultProtoDRIFTED");
    const result = emitProto(
      "signFixture",
      "SignFixtureSigner",
      "SignFixture",
      "unary",
      "d2.signfixtures.v2alpha",
      "D2.Services.Protos.SignFixtures.V2Alpha",
      SOURCE,
      "SignFixtureRequest",
      buildSignFixtureInputFields(),
      undefined,
      "SignFixtureOutput",
      buildSignFixtureOutputFields(),
      undefined,
      [],
      () => {},
    );
    expect(result!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignFixtureSignerService (re-pointed to façade delegation)
// ---------------------------------------------------------------------------

/** Fixture façade delegation target for the sign op (matches the committed .g.cs). */
const SIGN_FACADE_TARGET: GrpcDelegationTarget = {
  kind: "facade",
  typeName: "ISignFixtureSignerFacade",
  methodName: "SignFixtureAsync",
  targetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
};

describe("byteParity_SignFixtureSignerService_FacadeDelegation_CommittedFixtureIdentical", () => {
  it("re-emitted service .g.cs (façade delegation) is byte-identical to the committed fixture", () => {
    const [svc] = emitGrpcService(
      "signFixture",
      "SignFixtureSigner",
      "SignFixture",
      "D2.Services.Protos.SignFixtures.V2Alpha",
      "D2.Edge.Tests.TypeSpecGrpc.Generated",
      "D2.Edge.Tests.TypeSpecDto.Generated",
      SOURCE,
      "SignFixtureRequest",
      "SignFixtureResponse",
      "SignFixtureInput",
      buildSignFixtureInputFields(),
      "SignFixtureOutput",
      buildSignFixtureOutputFields(),
      SIGN_FACADE_TARGET,
    );
    expect(svc.content).toBe(
      readFixture(join(GRPC_HOME, "SignFixtureSignerService.g.cs")),
    );
  });

  it("deliberate-drift detection: handler delegation does NOT match façade fixture", () => {
    // Substituting SignFixtureAsync → HandleAsync would produce a mismatch (non-vacuous gate).
    const drifted = readFixture(
      join(GRPC_HOME, "SignFixtureSignerService.g.cs"),
    ).replace("facade.SignFixtureAsync", "handler.HandleAsync");
    const [svc] = emitGrpcService(
      "signFixture",
      "SignFixtureSigner",
      "SignFixture",
      "D2.Services.Protos.SignFixtures.V2Alpha",
      "D2.Edge.Tests.TypeSpecGrpc.Generated",
      "D2.Edge.Tests.TypeSpecDto.Generated",
      SOURCE,
      "SignFixtureRequest",
      "SignFixtureResponse",
      "SignFixtureInput",
      buildSignFixtureInputFields(),
      "SignFixtureOutput",
      buildSignFixtureOutputFields(),
      SIGN_FACADE_TARGET,
    );
    expect(svc.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignFixtureTransportMappers
// ---------------------------------------------------------------------------

describe("byteParity_SignFixtureTransportMappers_CommittedFixtureIdentical", () => {
  it("re-emitted mapper .g.cs is byte-identical to the committed fixture", () => {
    const [, mapper] = emitGrpcService(
      "signFixture",
      "SignFixtureSigner",
      "SignFixture",
      "D2.Services.Protos.SignFixtures.V2Alpha",
      "D2.Edge.Tests.TypeSpecGrpc.Generated",
      "D2.Edge.Tests.TypeSpecDto.Generated",
      SOURCE,
      "SignFixtureRequest",
      "SignFixtureResponse",
      "SignFixtureInput",
      buildSignFixtureInputFields(),
      "SignFixtureOutput",
      buildSignFixtureOutputFields(),
    );
    expect(mapper.content).toBe(
      readFixture(join(GRPC_HOME, "SignFixtureTransportMappers.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "SignFixtureTransportMappers.g.cs"),
    ).replace(
      "SignFixtureTransportMappers",
      "SignFixtureTransportMappersDRIFTED",
    );
    const [, mapper] = emitGrpcService(
      "signFixture",
      "SignFixtureSigner",
      "SignFixture",
      "D2.Services.Protos.SignFixtures.V2Alpha",
      "D2.Edge.Tests.TypeSpecGrpc.Generated",
      "D2.Edge.Tests.TypeSpecDto.Generated",
      SOURCE,
      "SignFixtureRequest",
      "SignFixtureResponse",
      "SignFixtureInput",
      buildSignFixtureInputFields(),
      "SignFixtureOutput",
      buildSignFixtureOutputFields(),
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
    opName: "signFixture",
    grpcService: "SignFixtureSigner",
    grpcMethod: "SignFixture",
    protoCsharpNs: "D2.Services.Protos.SignFixtures.V2Alpha",
    dtoCsharpNs: CLIENT_DTO_NS,
    sourceSpec: SOURCE,
    requestModelName: "SignFixtureInput",
    requestFields: buildSignFixtureInputFields(),
    responseModelName: "SignFixtureOutput",
    responseFields: buildSignFixtureOutputFields(),
  };
}

// ---------------------------------------------------------------------------
// byteParity_ISignFixtureGrpcClient
// ---------------------------------------------------------------------------

describe("byteParity_ISignFixtureGrpcClient_CommittedFixtureIdentical", () => {
  it("re-emitted interface .g.cs is byte-identical to the committed fixture", () => {
    const [iface] = emitGrpcClient(
      "SignFixture",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(iface!.content).toBe(
      readFixture(join(GRPC_HOME, "ISignFixtureGrpcClient.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "ISignFixtureGrpcClient.g.cs"),
    ).replace("ISignFixtureGrpcClient", "ISignFixtureGrpcClientDRIFTED");
    const [iface] = emitGrpcClient(
      "SignFixture",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(iface!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignFixtureGrpcClient (impl)
// ---------------------------------------------------------------------------

describe("byteParity_SignFixtureGrpcClient_CommittedFixtureIdentical", () => {
  it("re-emitted impl .g.cs is byte-identical to the committed fixture", () => {
    const [, impl] = emitGrpcClient(
      "SignFixture",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(impl!.content).toBe(
      readFixture(join(GRPC_HOME, "SignFixtureGrpcClient.g.cs")),
    );
  });

  it("deliberate-drift detection: removing D2Services.Protos.Common.V1 does NOT match", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "SignFixtureGrpcClient.g.cs"),
    ).replace("using D2.Services.Protos.Common.V1;\n", "");
    const [, impl] = emitGrpcClient(
      "SignFixture",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(impl!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignFixtureClientMappers
// ---------------------------------------------------------------------------

describe("byteParity_SignFixtureClientMappers_CommittedFixtureIdentical", () => {
  it("re-emitted mapper .g.cs is byte-identical to the committed fixture", () => {
    const [, , mapper] = emitGrpcClient(
      "SignFixture",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(mapper!.content).toBe(
      readFixture(join(GRPC_HOME, "SignFixtureClientMappers.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "SignFixtureClientMappers.g.cs"),
    ).replace("SignFixtureClientMappers", "SignFixtureClientMappersDRIFTED");
    const [, , mapper] = emitGrpcClient(
      "SignFixture",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(mapper!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignFixtureGrpcClientsGenerated (DI extension)
// ---------------------------------------------------------------------------

describe("byteParity_SignFixtureGrpcClientsGenerated_CommittedFixtureIdentical", () => {
  it("re-emitted DI-ext .g.cs is byte-identical to the committed fixture", () => {
    const [, , , di] = emitGrpcClient(
      "SignFixture",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(di!.content).toBe(
      readFixture(join(GRPC_HOME, "SignFixtureGrpcClientsGenerated.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "SignFixtureGrpcClientsGenerated.g.cs"),
    ).replace(
      "AddD2SignFixtureGrpcClients",
      "AddD2SignFixtureGrpcClientsDRIFTED",
    );
    const [, , , di] = emitGrpcClient(
      "SignFixture",
      [buildClientSignOp()],
      CLIENTS_NS,
    );
    expect(di!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_SignFixtureClientKeys
// ---------------------------------------------------------------------------

describe("byteParity_SignFixtureClientKeys_CommittedFixtureIdentical", () => {
  it("re-emitted keys .g.cs is byte-identical to the committed fixture", () => {
    const file = emitClientKeys("signFixture", CLIENTS_NS, SOURCE);
    expect(file.content).toBe(
      readFixture(join(GRPC_HOME, "SignFixtureClientKeys.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "SignFixtureClientKeys.g.cs"),
    ).replace("SignFixtureGrpcClientPipeline", "SignGrpcClientPipelineDRIFTED");
    const file = emitClientKeys("signFixture", CLIENTS_NS, SOURCE);
    expect(file.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// byteParity_WireVersionConstant
// ---------------------------------------------------------------------------

describe("byteParity_WireVersionConstant_CommittedFixtureIdentical", () => {
  const WIRE_SOURCE = "contracts/typespec/fixtures/sign-shaped.tsp";
  const WIRE_NS = "D2.Services.Protos.SignFixtures.V2Alpha";
  const channel = parseChannel("d2.signfixtures.v2alpha")!;

  it("re-emitted WireVersion.g.cs is byte-identical to the committed fixture", () => {
    const file = emitWireVersionConstant(WIRE_NS, channel, WIRE_SOURCE);
    expect(file.content).toBe(readFixture(join(GRPC_HOME, "WireVersion.g.cs")));
  });

  it("deliberate-drift detection: mutated channel does NOT match committed fixture", () => {
    const mutatedChannel = parseChannel("d2.sample.v3beta")!;
    const file = emitWireVersionConstant(WIRE_NS, mutatedChannel, WIRE_SOURCE);
    expect(file.content).not.toBe(
      readFixture(join(GRPC_HOME, "WireVersion.g.cs")),
    );
  });
});

// ---------------------------------------------------------------------------
// byteParity_WireIdentityManifest
// ---------------------------------------------------------------------------

describe("byteParity_WireIdentityManifest_CommittedFixtureIdentical", () => {
  const MANIFEST_PROTO_PACKAGE = "d2.signfixtures.v2alpha";
  const MANIFEST_PROTO_CS_NS = "D2.Services.Protos.SignFixtures.V2Alpha";
  const channel = parseChannel(MANIFEST_PROTO_PACKAGE)!;

  it("re-emitted wire-identity.manifest.g.json is byte-identical to the committed fixture", () => {
    const file = emitWireIdentityManifest(
      MANIFEST_PROTO_PACKAGE,
      MANIFEST_PROTO_CS_NS,
      channel,
    );
    expect(file.content).toBe(
      readFixture(join(GRPC_HOME, "wire-identity.manifest.g.json")),
    );
  });

  it("deliberate-drift detection: mutated channel does NOT match committed fixture", () => {
    const mutatedChannel = parseChannel("d2.sample.v3beta")!;
    const file = emitWireIdentityManifest(
      MANIFEST_PROTO_PACKAGE,
      MANIFEST_PROTO_CS_NS,
      mutatedChannel,
    );
    expect(file.content).not.toBe(
      readFixture(join(GRPC_HOME, "wire-identity.manifest.g.json")),
    );
  });
});
