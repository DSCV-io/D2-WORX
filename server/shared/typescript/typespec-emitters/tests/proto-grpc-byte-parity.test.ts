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
import type { FieldInfo, NestedModel } from "../src/lib/model-walk.js";
import { emitProto } from "../src/lib/proto-emitter.js";
import type { NestedMessageDescriptor } from "../src/lib/proto-emitter.js";
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
      redactReason: undefined,
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
      redactReason: "SecretInformation",
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
      redactReason: undefined,
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

// ---------------------------------------------------------------------------
// Real KeyCustodian sign — the FIRST non-fixture gRPC wire surface. The
// server-side artifacts (proto + service + transport mappers + wire identity) are
// committed + byte-pinned here (proto package d2.keycustodian.v2alpha; the service
// delegates to IKeyCustodianApi.SignAsync). The cross-process gRPC CLIENT is
// deferred (it lives in the production D2.Edge.KeyCustodian.Clients namespace and
// needs the host composition + the clients project to become gRPC-aware).
// ---------------------------------------------------------------------------

const KC_SOURCE = "contracts/typespec/key-custodian/key-custodian.tsp";
const KC_PROTO_CS_NS = "D2.Services.Protos.KeyCustodian.V2Alpha";
const KC_GRPC_NS = "D2.Edge.Tests.TypeSpecGrpc.Generated";
const KC_DTO_NS = "D2.Edge.KeyCustodian.Clients";
const KC_WIRE_HOME = join(GRPC_HOME, "KeyCustodian");

function buildKcSignInputFields(): readonly FieldInfo[] {
  return [
    {
      name: "keyDomain",
      csName: "KeyDomain",
      csType: "string",
      tsName: "keyDomain",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 1,
    },
    {
      name: "signingInput",
      csName: "SigningInput",
      csType: "byte[]",
      tsName: "signingInput",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redactReason: "SecretInformation",
      fieldNumber: 2,
    },
  ];
}

function buildKcSignOutputFields(): readonly FieldInfo[] {
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
      redactReason: undefined,
      fieldNumber: 1,
    },
    {
      name: "kid",
      csName: "Kid",
      csType: "string",
      tsName: "kid",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 2,
    },
  ];
}

/** Real-KC façade delegation target (matches the committed KeyCustodianSignerService.g.cs). */
const KC_SIGN_FACADE_TARGET: GrpcDelegationTarget = {
  kind: "facade",
  typeName: "IKeyCustodianApi",
  methodName: "SignAsync",
  targetNamespace: "D2.Edge.KeyCustodian.Clients",
};

describe("byteParity_KcSignProto_CommittedFixtureIdentical", () => {
  it("re-emitted real KC .proto is byte-identical to the committed fixture", () => {
    const result = emitProto(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      "unary",
      "d2.keycustodian.v2alpha",
      KC_PROTO_CS_NS,
      KC_SOURCE,
      "SignRequest",
      buildKcSignInputFields(),
      undefined,
      "SignOutput",
      buildKcSignOutputFields(),
      undefined,
      [],
      () => {},
    );
    expect(result).toBeDefined();
    expect(result!.content).toBe(
      readFixture(join(GRPC_PROTOS, "key_custodian_signer_sign.g.proto")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_PROTOS, "key_custodian_signer_sign.g.proto"),
    ).replace("D2ResultProto", "D2ResultProtoDRIFTED");
    const result = emitProto(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      "unary",
      "d2.keycustodian.v2alpha",
      KC_PROTO_CS_NS,
      KC_SOURCE,
      "SignRequest",
      buildKcSignInputFields(),
      undefined,
      "SignOutput",
      buildKcSignOutputFields(),
      undefined,
      [],
      () => {},
    );
    expect(result!.content).not.toBe(drifted);
  });
});

describe("byteParity_KeyCustodianSignerService_FacadeDelegation_CommittedFixtureIdentical", () => {
  it("re-emitted real KC service .g.cs (façade delegation) is byte-identical to the committed fixture", () => {
    const [svc] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      buildKcSignInputFields(),
      "SignOutput",
      buildKcSignOutputFields(),
      KC_SIGN_FACADE_TARGET,
    );
    expect(svc.content).toBe(
      readFixture(join(GRPC_HOME, "KeyCustodianSignerService.g.cs")),
    );
  });

  it("deliberate-drift detection: handler delegation does NOT match façade fixture", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "KeyCustodianSignerService.g.cs"),
    ).replace("facade.SignAsync", "handler.HandleAsync");
    const [svc] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      buildKcSignInputFields(),
      "SignOutput",
      buildKcSignOutputFields(),
      KC_SIGN_FACADE_TARGET,
    );
    expect(svc.content).not.toBe(drifted);
  });
});

describe("byteParity_KcSignTransportMappers_CommittedFixtureIdentical", () => {
  it("re-emitted real KC mapper .g.cs is byte-identical to the committed fixture", () => {
    const [, mapper] = emitGrpcService(
      "sign",
      "KeyCustodianSigner",
      "Sign",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      buildKcSignInputFields(),
      "SignOutput",
      buildKcSignOutputFields(),
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
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "SignRequest",
      "SignResponse",
      "SignInput",
      buildKcSignInputFields(),
      "SignOutput",
      buildKcSignOutputFields(),
    );
    expect(mapper.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Real KeyCustodian KEYRING gRPC wire surface — the getKeyring op (a DISTINCT
// service KeyCustodianKeyring on the same package). The output carries a nested
// KeyringEntry model (kid + keyBytes) so this exercises the array-of-model proto +
// the nested sub-mapper recursion. The service delegates to
// IKeyCustodianApi.GetKeyringAsync; the cross-process gRPC CLIENT is deferred (same
// as the sign op). keyBytes redaction is a DTO concern (byte-gated in
// byte-parity.test.ts + reflection-pinned in the .NET GrpcKeyringServiceTests); the
// proto / mappers carry no redaction concept.
// ---------------------------------------------------------------------------

const KC_KEYRING_ENTRY_MODEL: NestedModel = {
  name: "KeyringEntry",
  fields: [
    {
      name: "kid",
      csName: "Kid",
      csType: "string",
      tsName: "kid",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 1,
    },
    {
      name: "keyBytes",
      csName: "KeyBytes",
      csType: "byte[]",
      tsName: "keyBytes",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redactReason: "SecretInformation",
      fieldNumber: 2,
    },
  ],
};

function buildKcGetKeyringInputFields(): readonly FieldInfo[] {
  return [
    {
      name: "keyDomain",
      csName: "KeyDomain",
      csType: "string",
      tsName: "keyDomain",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 1,
    },
  ];
}

function buildKcGetKeyringOutputFields(): readonly FieldInfo[] {
  return [
    {
      name: "activeKid",
      csName: "ActiveKid",
      csType: "string",
      tsName: "activeKid",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 1,
    },
    {
      name: "entries",
      csName: "Entries",
      csType: "IReadOnlyList<KeyringEntry>",
      tsName: "entries",
      tsType: "readonly KeyringEntry[]",
      protoType: undefined,
      repeated: true,
      optional: false,
      redactReason: undefined,
      fieldNumber: 2,
      nested: KC_KEYRING_ENTRY_MODEL,
    },
    {
      name: "aadContext",
      csName: "AadContext",
      csType: "byte[]",
      tsName: "aadContext",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 3,
    },
  ];
}

/** Real-KC keyring façade delegation target (matches the committed KeyCustodianKeyringService.g.cs). */
const KC_KEYRING_FACADE_TARGET: GrpcDelegationTarget = {
  kind: "facade",
  typeName: "IKeyCustodianApi",
  methodName: "GetKeyringAsync",
  targetNamespace: "D2.Edge.KeyCustodian.Clients",
};

describe("byteParity_KcKeyringProto_CommittedFixtureIdentical", () => {
  function emit(): string {
    return emitProto(
      "getKeyring",
      "KeyCustodianKeyring",
      "GetKeyring",
      "unary",
      "d2.keycustodian.v2alpha",
      KC_PROTO_CS_NS,
      KC_SOURCE,
      "GetKeyringRequest",
      buildKcGetKeyringInputFields(),
      undefined,
      "GetKeyringOutput",
      buildKcGetKeyringOutputFields(),
      undefined,
      [{ model: KC_KEYRING_ENTRY_MODEL } as NestedMessageDescriptor],
      (c, m) => {
        throw new Error(`${c}: ${m}`);
      },
    )!.content;
  }

  it("re-emitted real KC keyring .proto is byte-identical to the committed fixture", () => {
    // Non-vacuity: the array-of-model + nested message must be present.
    const p = emit();
    expect(p).toContain("repeated KeyringEntry entries = 2;");
    expect(p).toContain("message KeyringEntry {");
    expect(p).toBe(
      readFixture(
        join(GRPC_PROTOS, "key_custodian_keyring_get_keyring.g.proto"),
      ),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_PROTOS, "key_custodian_keyring_get_keyring.g.proto"),
    ).replace("message KeyringEntry", "message KeyringEntryDRIFTED");
    expect(emit()).not.toBe(drifted);
  });
});

describe("byteParity_KeyCustodianKeyringService_FacadeDelegation_CommittedFixtureIdentical", () => {
  function emitService(): string {
    const [svc] = emitGrpcService(
      "getKeyring",
      "KeyCustodianKeyring",
      "GetKeyring",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "GetKeyringRequest",
      "GetKeyringResponse",
      "GetKeyringInput",
      buildKcGetKeyringInputFields(),
      "GetKeyringOutput",
      buildKcGetKeyringOutputFields(),
      KC_KEYRING_FACADE_TARGET,
    );

    return svc.content;
  }

  it("re-emitted real KC keyring service .g.cs (façade delegation) is byte-identical", () => {
    expect(emitService()).toBe(
      readFixture(join(GRPC_HOME, "KeyCustodianKeyringService.g.cs")),
    );
  });

  it("deliberate-drift detection: handler delegation does NOT match façade fixture", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "KeyCustodianKeyringService.g.cs"),
    ).replace("facade.GetKeyringAsync", "handler.HandleAsync");
    expect(emitService()).not.toBe(drifted);
  });
});

describe("byteParity_GetKeyringTransportMappers_CommittedFixtureIdentical", () => {
  function emitMapper(): string {
    const [, mapper] = emitGrpcService(
      "getKeyring",
      "KeyCustodianKeyring",
      "GetKeyring",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "GetKeyringRequest",
      "GetKeyringResponse",
      "GetKeyringInput",
      buildKcGetKeyringInputFields(),
      "GetKeyringOutput",
      buildKcGetKeyringOutputFields(),
    );

    return mapper.content;
  }

  it("re-emitted real KC keyring transport mappers .g.cs (with nested sub-mappers) is byte-identical", () => {
    const m = emitMapper();
    // Non-vacuity: the nested KeyringEntry sub-mappers + array recursion are present.
    expect(m).toContain("ToProtoKeyringEntry()");
    expect(m).toContain("ToKeyringEntry()");
    expect(m).toBe(
      readFixture(join(GRPC_HOME, "GetKeyringTransportMappers.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "GetKeyringTransportMappers.g.cs"),
    ).replace(
      "GetKeyringTransportMappers",
      "GetKeyringTransportMappersDRIFTED",
    );
    expect(emitMapper()).not.toBe(drifted);
  });
});

describe("byteParity_KcWireVersionConstant_CommittedFixtureIdentical", () => {
  const channel = parseChannel("d2.keycustodian.v2alpha")!;

  it("re-emitted real KC WireVersion.g.cs is byte-identical to the committed fixture", () => {
    const file = emitWireVersionConstant(KC_PROTO_CS_NS, channel, KC_SOURCE);
    expect(file.content).toBe(
      readFixture(join(KC_WIRE_HOME, "WireVersion.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated channel does NOT match committed fixture", () => {
    const mutated = parseChannel("d2.sample.v3beta")!;
    const file = emitWireVersionConstant(KC_PROTO_CS_NS, mutated, KC_SOURCE);
    expect(file.content).not.toBe(
      readFixture(join(KC_WIRE_HOME, "WireVersion.g.cs")),
    );
  });
});

describe("byteParity_KcWireIdentityManifest_CommittedFixtureIdentical", () => {
  const channel = parseChannel("d2.keycustodian.v2alpha")!;

  it("re-emitted real KC wire-identity.manifest.g.json is byte-identical to the committed fixture", () => {
    const file = emitWireIdentityManifest(
      "d2.keycustodian.v2alpha",
      KC_PROTO_CS_NS,
      channel,
    );
    expect(file.content).toBe(
      readFixture(join(KC_WIRE_HOME, "wire-identity.manifest.g.json")),
    );
  });

  it("deliberate-drift detection: mutated channel does NOT match committed fixture", () => {
    const mutated = parseChannel("d2.sample.v3beta")!;
    const file = emitWireIdentityManifest(
      "d2.keycustodian.v2alpha",
      KC_PROTO_CS_NS,
      mutated,
    );
    expect(file.content).not.toBe(
      readFixture(join(KC_WIRE_HOME, "wire-identity.manifest.g.json")),
    );
  });
});

// ---------------------------------------------------------------------------
// Real KeyCustodian CERTIFICATE-AUTHORITY issuance gRPC wire surface — the
// issueLeaf op (its own service KeyCustodianCertificateAuthority; wire method
// IssueWorkloadCertificate — one gRPC service per op so each service carries
// its own transport scope policy). CSR bytes in, leaf + issuing-intermediate
// DER + validity window out — ALL-PUBLIC material (the leaf private key never
// crosses the wire; no redaction anywhere on this op). The two DateTimeOffset
// output fields exercise the temporal outbound arm (ISO-8601 "O" → proto
// string). The service delegates to IKeyCustodianApi.IssueLeafAsync; the
// cross-process gRPC CLIENT is deferred (same as the sign / keyring ops).
// ---------------------------------------------------------------------------

function buildKcIssueLeafInputFields(): readonly FieldInfo[] {
  return [
    {
      name: "csrDer",
      csName: "CsrDer",
      csType: "byte[]",
      tsName: "csrDer",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 1,
    },
  ];
}

function buildKcIssueLeafOutputFields(): readonly FieldInfo[] {
  return [
    {
      name: "certificateDer",
      csName: "CertificateDer",
      csType: "byte[]",
      tsName: "certificateDer",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 1,
    },
    {
      name: "issuerCertificateDer",
      csName: "IssuerCertificateDer",
      csType: "byte[]",
      tsName: "issuerCertificateDer",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 2,
    },
    {
      name: "notBefore",
      csName: "NotBefore",
      csType: "DateTimeOffset",
      tsName: "notBefore",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 3,
    },
    {
      name: "notAfter",
      csName: "NotAfter",
      csType: "DateTimeOffset",
      tsName: "notAfter",
      tsType: "string",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 4,
    },
  ];
}

/** Real-KC issuance façade delegation target (matches the committed KeyCustodianCertificateAuthorityService.g.cs). */
const KC_ISSUE_FACADE_TARGET: GrpcDelegationTarget = {
  kind: "facade",
  typeName: "IKeyCustodianApi",
  methodName: "IssueLeafAsync",
  targetNamespace: "D2.Edge.KeyCustodian.Clients",
};

describe("byteParity_KcIssueLeafProto_CommittedFixtureIdentical", () => {
  function emit(): string {
    return emitProto(
      "issueLeaf",
      "KeyCustodianCertificateAuthority",
      "IssueWorkloadCertificate",
      "unary",
      "d2.keycustodian.v2alpha",
      KC_PROTO_CS_NS,
      KC_SOURCE,
      "IssueWorkloadCertificateRequest",
      buildKcIssueLeafInputFields(),
      undefined,
      "IssueLeafOutput",
      buildKcIssueLeafOutputFields(),
      undefined,
      [],
      (c, m) => {
        throw new Error(`${c}: ${m}`);
      },
    )!.content;
  }

  it("re-emitted real KC issuance .proto is byte-identical to the committed fixture", () => {
    // Non-vacuity: the temporal fields ride the wire as proto strings.
    const p = emit();
    expect(p).toContain("string not_before = 3;");
    expect(p).toContain("string not_after = 4;");
    expect(p).toBe(
      readFixture(
        join(
          GRPC_PROTOS,
          "key_custodian_certificate_authority_issue_workload_certificate.g.proto",
        ),
      ),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(
        GRPC_PROTOS,
        "key_custodian_certificate_authority_issue_workload_certificate.g.proto",
      ),
    ).replace("bytes csr_der", "bytes csr_der_drifted");
    expect(emit()).not.toBe(drifted);
  });
});

describe("byteParity_KeyCustodianCertificateAuthorityService_FacadeDelegation_CommittedFixtureIdentical", () => {
  function emitService(): string {
    const [svc] = emitGrpcService(
      "issueLeaf",
      "KeyCustodianCertificateAuthority",
      "IssueWorkloadCertificate",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "IssueWorkloadCertificateRequest",
      "IssueWorkloadCertificateResponse",
      "IssueLeafInput",
      buildKcIssueLeafInputFields(),
      "IssueLeafOutput",
      buildKcIssueLeafOutputFields(),
      KC_ISSUE_FACADE_TARGET,
    );

    return svc.content;
  }

  it("re-emitted real KC issuance service .g.cs (façade delegation) is byte-identical", () => {
    expect(emitService()).toBe(
      readFixture(
        join(GRPC_HOME, "KeyCustodianCertificateAuthorityService.g.cs"),
      ),
    );
  });

  it("deliberate-drift detection: handler delegation does NOT match façade fixture", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "KeyCustodianCertificateAuthorityService.g.cs"),
    ).replace("facade.IssueLeafAsync", "handler.HandleAsync");
    expect(emitService()).not.toBe(drifted);
  });
});

describe("byteParity_IssueLeafTransportMappers_CommittedFixtureIdentical", () => {
  function emitMapper(): string {
    const [, mapper] = emitGrpcService(
      "issueLeaf",
      "KeyCustodianCertificateAuthority",
      "IssueWorkloadCertificate",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "IssueWorkloadCertificateRequest",
      "IssueWorkloadCertificateResponse",
      "IssueLeafInput",
      buildKcIssueLeafInputFields(),
      "IssueLeafOutput",
      buildKcIssueLeafOutputFields(),
    );

    return mapper.content;
  }

  it("re-emitted real KC issuance transport mappers .g.cs (temporal outbound arm) is byte-identical", () => {
    const m = emitMapper();
    // Non-vacuity: the DateTimeOffset outbound conversion is present.
    expect(m).toContain('NotBefore = output.NotBefore.ToString("O"),');
    expect(m).toContain('NotAfter = output.NotAfter.ToString("O"),');
    expect(m).toBe(
      readFixture(join(GRPC_HOME, "IssueLeafTransportMappers.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "IssueLeafTransportMappers.g.cs"),
    ).replace("IssueLeafTransportMappers", "IssueLeafTransportMappersDRIFTED");
    expect(emitMapper()).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Real KeyCustodian CA-CERTIFICATE gRPC wire surface — the getCaCertificate op
// (its own service KeyCustodianCaCertificate — one gRPC service per op). Empty
// request in, root + issuing-intermediate DER out (public trust material). The
// empty request exercises the parameterless proto message + parameterless C#
// record end-to-end. The service delegates to
// IKeyCustodianApi.GetCaCertificateAsync.
// ---------------------------------------------------------------------------

function buildKcGetCaCertificateOutputFields(): readonly FieldInfo[] {
  return [
    {
      name: "rootCertificateDer",
      csName: "RootCertificateDer",
      csType: "byte[]",
      tsName: "rootCertificateDer",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 1,
    },
    {
      name: "intermediateCertificateDer",
      csName: "IntermediateCertificateDer",
      csType: "byte[]",
      tsName: "intermediateCertificateDer",
      tsType: "Uint8Array",
      protoType: "bytes",
      repeated: false,
      optional: false,
      redactReason: undefined,
      fieldNumber: 2,
    },
  ];
}

/** Real-KC CA-certificate façade delegation target (matches the committed KeyCustodianCaCertificateService.g.cs). */
const KC_CACERT_FACADE_TARGET: GrpcDelegationTarget = {
  kind: "facade",
  typeName: "IKeyCustodianApi",
  methodName: "GetCaCertificateAsync",
  targetNamespace: "D2.Edge.KeyCustodian.Clients",
};

describe("byteParity_KcCaCertificateProto_CommittedFixtureIdentical", () => {
  function emit(): string {
    return emitProto(
      "getCaCertificate",
      "KeyCustodianCaCertificate",
      "GetCaCertificate",
      "unary",
      "d2.keycustodian.v2alpha",
      KC_PROTO_CS_NS,
      KC_SOURCE,
      "GetCaCertificateRequest",
      [],
      undefined,
      "GetCaCertificateOutput",
      buildKcGetCaCertificateOutputFields(),
      undefined,
      [],
      (c, m) => {
        throw new Error(`${c}: ${m}`);
      },
    )!.content;
  }

  it("re-emitted real KC CA-certificate .proto is byte-identical to the committed fixture", () => {
    // Non-vacuity: the empty request message + the distinct per-op service.
    const p = emit();
    expect(p).toContain("message GetCaCertificateRequest {}");
    expect(p).toContain("service KeyCustodianCaCertificate {");
    expect(p).toBe(
      readFixture(
        join(
          GRPC_PROTOS,
          "key_custodian_ca_certificate_get_ca_certificate.g.proto",
        ),
      ),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(
        GRPC_PROTOS,
        "key_custodian_ca_certificate_get_ca_certificate.g.proto",
      ),
    ).replace("root_certificate_der", "root_certificate_der_drifted");
    expect(emit()).not.toBe(drifted);
  });
});

describe("byteParity_KeyCustodianCaCertificateService_FacadeDelegation_CommittedFixtureIdentical", () => {
  function emitService(): string {
    const [svc] = emitGrpcService(
      "getCaCertificate",
      "KeyCustodianCaCertificate",
      "GetCaCertificate",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "GetCaCertificateRequest",
      "GetCaCertificateResponse",
      "GetCaCertificateInput",
      [],
      "GetCaCertificateOutput",
      buildKcGetCaCertificateOutputFields(),
      KC_CACERT_FACADE_TARGET,
    );

    return svc.content;
  }

  it("re-emitted real KC CA-certificate service .g.cs (façade delegation) is byte-identical", () => {
    expect(emitService()).toBe(
      readFixture(join(GRPC_HOME, "KeyCustodianCaCertificateService.g.cs")),
    );
  });

  it("deliberate-drift detection: handler delegation does NOT match façade fixture", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "KeyCustodianCaCertificateService.g.cs"),
    ).replace("facade.GetCaCertificateAsync", "handler.HandleAsync");
    expect(emitService()).not.toBe(drifted);
  });
});

describe("byteParity_GetCaCertificateTransportMappers_CommittedFixtureIdentical", () => {
  function emitMapper(): string {
    const [, mapper] = emitGrpcService(
      "getCaCertificate",
      "KeyCustodianCaCertificate",
      "GetCaCertificate",
      KC_PROTO_CS_NS,
      KC_GRPC_NS,
      KC_DTO_NS,
      KC_SOURCE,
      "GetCaCertificateRequest",
      "GetCaCertificateResponse",
      "GetCaCertificateInput",
      [],
      "GetCaCertificateOutput",
      buildKcGetCaCertificateOutputFields(),
    );

    return mapper.content;
  }

  it("re-emitted real KC CA-certificate transport mappers .g.cs (empty-input arm) is byte-identical", () => {
    const m = emitMapper();
    // Non-vacuity: the parameterless-request inbound arm constructs the empty DTO.
    expect(m).toContain("return new GetCaCertificateInput();");
    expect(m).toBe(
      readFixture(join(GRPC_HOME, "GetCaCertificateTransportMappers.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match re-emitted output", () => {
    const drifted = readFixture(
      join(GRPC_HOME, "GetCaCertificateTransportMappers.g.cs"),
    ).replace(
      "GetCaCertificateTransportMappers",
      "GetCaCertificateTransportMappersDRIFTED",
    );
    expect(emitMapper()).not.toBe(drifted);
  });
});
