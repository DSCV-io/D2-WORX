// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Byte-parity gate: regenerating emitter output must produce byte-identical
// content to the committed .g.cs / .g.ts fixtures.
//
// The gate is non-vacuous per §26.5.1 + §1.20: a deliberate-drift case
// (mutate one byte of the fixture) is tested to verify the gate FAILS when
// there is actual divergence. This prevents the byte-gate becoming a tautology
// (comparing a buffer to itself, never catching real drift).

import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";
import type { Model, ModelProperty, Program, Scalar } from "@typespec/compiler";
import { D2_REDACT_KEY } from "@dcsv-io/d2-typespec-decorators";
import { walkModel } from "../src/lib/model-walk.js";
import { emitCsharpDtos } from "../src/lib/csharp-dto-emitter.js";
import { emitTsDtos } from "../src/lib/ts-dto-emitter.js";
import { emitProto } from "../src/lib/proto-emitter.js";
import { emitGrpcService } from "../src/lib/grpc-service-emitter.js";
import {
  emitGrpcClient,
  emitClientKeys,
} from "../src/lib/grpc-client-emitter.js";
import { emitHandlerInterface } from "../src/lib/handler-interface-emitter.js";
import type { FieldInfo, NestedEnum } from "../src/lib/model-walk.js";

// ---------------------------------------------------------------------------
// Committed-file path constants + readFixture helper
// (shared across GetJwks, Sign, Temporal byte-gates below AND the enum /
// predicate suites further down this file)
// ---------------------------------------------------------------------------

const REPO = findRepoRoot(import.meta.url);

/** Committed home for GetJwks DTOs + façade interface (Clients namespace). */
const CLIENTS_HOME = join(REPO, "private/services/edge/key-custodian/client");

/** Committed home for Sign + Temporal + Enum fixture DTOs (TypeSpecDto/Generated/). */
const DTO_HOME = join(
  REPO,
  "private/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated",
);

/** Committed home for enum gRPC fixtures (TypeSpecGrpcEnum/Generated/). */
const GRPC_ENUM_HOME = join(
  REPO,
  "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcEnum/Generated",
);

/** Committed home for enum gRPC .proto fixtures (TypeSpecGrpcEnum/Protos/). */
const GRPC_ENUM_PROTOS = join(
  REPO,
  "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcEnum/Protos",
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
// Helpers — build TypeSpec stubs matching the committed shapes
// ---------------------------------------------------------------------------

function makeScalar(name: string): Scalar {
  return { kind: "Scalar", name } as unknown as Scalar;
}

function makeProp(type: Scalar, optional = false): ModelProperty {
  return { type, optional } as unknown as ModelProperty;
}

function makeRedactProp(type: Scalar): {
  prop: ModelProperty;
  redactMap: Map<object, unknown>;
} {
  const prop = { type, optional: false } as unknown as ModelProperty;
  // @d2Redact stores the RedactReason member-name string; the sign fixture's
  // payload is secret-adjacent material (SecretInformation), matching the
  // committed SignFixtureInput.g.cs fixture.
  return { prop, redactMap: new Map([[prop, "SecretInformation"]]) };
}

function makeProgram(redactMap: Map<object, unknown> = new Map()): Program {
  return {
    stateMap(key: symbol): Map<object, unknown> {
      if (key === D2_REDACT_KEY) return redactMap;
      return new Map();
    },
  } as unknown as Program;
}

// ---------------------------------------------------------------------------
// GetJwksInput fixture — empty model
// ---------------------------------------------------------------------------

function buildGetJwksInputWalk() {
  const model: Model = {
    kind: "Model",
    name: "GetJwksInput",
    properties: new Map(),
  } as unknown as Model;
  return walkModel(makeProgram(), model, () => {});
}

// ---------------------------------------------------------------------------
// GetJwksOutput fixture — Jwk[] collection
// ---------------------------------------------------------------------------

function buildGetJwksOutputWalk() {
  const jwkModel: Model = {
    kind: "Model",
    name: "Jwk",
    properties: new Map<string, ModelProperty>([
      ["kid", makeProp(makeScalar("string"))],
      ["n", makeProp(makeScalar("string"))],
      ["e", makeProp(makeScalar("string"))],
      ["kty", makeProp(makeScalar("string"))],
      ["use", makeProp(makeScalar("string"))],
      ["alg", makeProp(makeScalar("string"))],
    ]),
  } as unknown as Model;

  const arrayModel: Model = {
    kind: "Model",
    name: "Array",
    indexer: { value: jwkModel },
    properties: new Map(),
  } as unknown as Model;

  const model: Model = {
    kind: "Model",
    name: "GetJwksOutput",
    properties: new Map<string, ModelProperty>([
      [
        "keys",
        { type: arrayModel, optional: false } as unknown as ModelProperty,
      ],
    ]),
  } as unknown as Model;

  return walkModel(makeProgram(), model, () => {});
}

// ---------------------------------------------------------------------------
// SignFixtureInput fixture — redacted bytes field
// ---------------------------------------------------------------------------

function buildSignFixtureInputWalk() {
  const { prop: payloadProp, redactMap } = makeRedactProp(makeScalar("bytes"));
  const kidProp = makeProp(makeScalar("string"));
  const model: Model = {
    kind: "Model",
    name: "SignFixtureInput",
    properties: new Map<string, ModelProperty>([
      ["kid", kidProp],
      ["payload", payloadProp],
    ]),
  } as unknown as Model;

  return {
    walk: walkModel(makeProgram(redactMap), model, () => {}),
    redactMap,
  };
}

// ---------------------------------------------------------------------------
// Temporal fixture — every temporal scalar + 2 composite nested models
// ---------------------------------------------------------------------------

function buildTemporalWalks() {
  const zonedInstantWire: Model = {
    kind: "Model",
    name: "ZonedInstantWire",
    properties: new Map<string, ModelProperty>([
      ["instant", makeProp(makeScalar("utcDateTime"))],
      ["zoneId", makeProp(makeScalar("string"))],
    ]),
  } as unknown as Model;

  const localAnchoredEventWire: Model = {
    kind: "Model",
    name: "LocalAnchoredEventWire",
    properties: new Map<string, ModelProperty>([
      ["scheduledLocal", makeProp(makeScalar("plainDateTime"))],
      ["ianaZone", makeProp(makeScalar("string"))],
      ["nextFireUtc", makeProp(makeScalar("utcDateTime"), true)],
    ]),
  } as unknown as Model;

  const fieldEntries: Array<[string, ModelProperty]> = [
    ["pastInstant", makeProp(makeScalar("utcDateTime"))],
    ["deadline", makeProp(makeScalar("utcDateTime"))],
    ["withOffset", makeProp(makeScalar("offsetDateTime"))],
    ["birthday", makeProp(makeScalar("plainDate"))],
    ["alarmTime", makeProp(makeScalar("plainTime"))],
    ["wallClock", makeProp(makeScalar("plainDateTime"))],
    ["elapsed", makeProp(makeScalar("duration"))],
    ["optionalInstant", makeProp(makeScalar("utcDateTime"), true)],
    [
      "zoned",
      { type: zonedInstantWire, optional: false } as unknown as ModelProperty,
    ],
    [
      "schedule",
      {
        type: localAnchoredEventWire,
        optional: false,
      } as unknown as ModelProperty,
    ],
  ];

  const inputModel: Model = {
    kind: "Model",
    name: "TemporalFixtureInput",
    properties: new Map(fieldEntries),
  } as unknown as Model;

  const outputModel: Model = {
    kind: "Model",
    name: "TemporalFixtureOutput",
    properties: new Map(fieldEntries),
  } as unknown as Model;

  return {
    input: walkModel(makeProgram(), inputModel, () => {}),
    output: walkModel(makeProgram(), outputModel, () => {}),
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("byteParity_GetJwksInput_CommittedFixtureIdentical", () => {
  it("regenerated GetJwksInput.g.cs is byte-identical to the committed fixture", () => {
    const { fields, nestedModels } = buildGetJwksInputWalk();
    const [inputFile] = emitCsharpDtos(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client.Jwks",
      "contracts/typespec/key-custodian/key-custodian.tsp",
      fields,
      [],
      nestedModels,
    );

    expect(inputFile!.content).toBe(
      readFixture(join(CLIENTS_HOME, "Jwks", "GetJwksInput.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match regenerated output", () => {
    // Non-vacuous guard: deliberately corrupt the fixture by one byte.
    const driftedFixture = readFixture(
      join(CLIENTS_HOME, "Jwks", "GetJwksInput.g.cs"),
    ).replace("GetJwksInput", "GetJwksInputDRIFTED");

    const { fields, nestedModels } = buildGetJwksInputWalk();
    const [inputFile] = emitCsharpDtos(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client.Jwks",
      "contracts/typespec/key-custodian/key-custodian.tsp",
      fields,
      [],
      nestedModels,
    );

    // The drifted fixture must NOT match — proving the gate would catch real drift.
    expect(inputFile!.content).not.toBe(driftedFixture);
  });
});

describe("byteParity_GetJwksOutput_CommittedFixtureIdentical", () => {
  it("regenerated GetJwksOutput.g.cs is byte-identical to the committed fixture", () => {
    const { fields, nestedModels } = buildGetJwksOutputWalk();
    const [, outputFile] = emitCsharpDtos(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client.Jwks",
      "contracts/typespec/key-custodian/key-custodian.tsp",
      [],
      fields,
      nestedModels,
    );

    expect(outputFile!.content).toBe(
      readFixture(join(CLIENTS_HOME, "Jwks", "GetJwksOutput.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match regenerated output", () => {
    // Non-vacuous guard: deliberately corrupt the fixture by one byte.
    const driftedFixture = readFixture(
      join(CLIENTS_HOME, "Jwks", "GetJwksOutput.g.cs"),
    ).replace("GetJwksOutput", "GetJwksOutputDRIFTED");

    const { fields, nestedModels } = buildGetJwksOutputWalk();
    const [, outputFile] = emitCsharpDtos(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client.Jwks",
      "contracts/typespec/key-custodian/key-custodian.tsp",
      [],
      fields,
      nestedModels,
    );

    // The drifted fixture must NOT match — proving the gate would catch real drift.
    expect(outputFile!.content).not.toBe(driftedFixture);
  });
});

describe("byteParity_SignFixtureInput_CommittedFixtureIdentical", () => {
  it("regenerated SignFixtureInput.g.cs is byte-identical to the committed fixture", () => {
    const { walk } = buildSignFixtureInputWalk();
    const [inputFile] = emitCsharpDtos(
      "signFixture",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
      "contracts/typespec/fixtures/sign-shaped.tsp",
      walk.fields,
      [],
      [],
    );

    expect(inputFile!.content).toBe(
      readFixture(join(DTO_HOME, "SignFixtureInput.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated fixture does NOT match regenerated output", () => {
    // §1.20 non-vacuous guard: deliberately corrupt the fixture by one byte.
    const driftedFixture = readFixture(
      join(DTO_HOME, "SignFixtureInput.g.cs"),
    ).replace("SignFixtureInput", "SignFixtureInputDRIFTED");

    const { walk } = buildSignFixtureInputWalk();
    const [inputFile] = emitCsharpDtos(
      "signFixture",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
      "contracts/typespec/fixtures/sign-shaped.tsp",
      walk.fields,
      [],
      [],
    );

    // The drifted fixture must NOT match — proving the gate would catch real drift.
    expect(inputFile!.content).not.toBe(driftedFixture);
  });
});

// The sign OUTPUT DTO (`SignFixtureOutput(string Signature)`) — the input side is gated
// above; this pins the committed output `.g.cs` byte-identical too.
function buildSignFixtureOutputWalk() {
  const model: Model = {
    kind: "Model",
    name: "SignFixtureOutput",
    properties: new Map<string, ModelProperty>([
      ["signature", makeProp(makeScalar("string"))],
    ]),
  } as unknown as Model;

  return walkModel(makeProgram(), model, () => {});
}

describe("byteParity_SignFixtureOutput_CommittedFixtureIdentical", () => {
  it("regenerated SignFixtureOutput.g.cs is byte-identical to the committed fixture", () => {
    const outputWalk = buildSignFixtureOutputWalk();
    const [, outputFile] = emitCsharpDtos(
      "signFixture",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
      "contracts/typespec/fixtures/sign-shaped.tsp",
      [],
      outputWalk.fields,
      outputWalk.nestedModels,
    );

    expect(outputFile!.content).toBe(
      readFixture(join(DTO_HOME, "SignFixtureOutput.g.cs")),
    );
  });

  it("deliberate-drift detection: a mutated SignFixtureOutput fixture does NOT match", () => {
    const drifted = readFixture(
      join(DTO_HOME, "SignFixtureOutput.g.cs"),
    ).replace("string Signature", "string SignatureDRIFTED");
    const outputWalk = buildSignFixtureOutputWalk();
    const [, outputFile] = emitCsharpDtos(
      "signFixture",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
      "contracts/typespec/fixtures/sign-shaped.tsp",
      [],
      outputWalk.fields,
      outputWalk.nestedModels,
    );

    expect(outputFile!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// GetKeyring DTOs — the REAL KC keyring op (Clients namespace). The output carries
// a nested KeyringEntry record whose keyBytes field is @d2Redact("SecretInformation")
// — the nested-model redaction path this deliverable's emitter change enables.
// ---------------------------------------------------------------------------

const KC_SPEC = "contracts/typespec/key-custodian/key-custodian.tsp";

function buildGetKeyringInputWalk() {
  const model: Model = {
    kind: "Model",
    name: "GetKeyringInput",
    properties: new Map<string, ModelProperty>([
      ["keyDomain", makeProp(makeScalar("string"))],
    ]),
  } as unknown as Model;

  return walkModel(makeProgram(), model, () => {});
}

function buildGetKeyringOutputWalk() {
  // KeyringEntry.keyBytes carries @d2Redact("SecretInformation") — a NESTED-model
  // property; the redact map keys on that property object.
  const { prop: keyBytesProp, redactMap } = makeRedactProp(makeScalar("bytes"));
  const keyringEntry: Model = {
    kind: "Model",
    name: "KeyringEntry",
    properties: new Map<string, ModelProperty>([
      ["kid", makeProp(makeScalar("string"))],
      ["keyBytes", keyBytesProp],
    ]),
  } as unknown as Model;

  const entriesArray: Model = {
    kind: "Model",
    name: "Array",
    indexer: { value: keyringEntry },
    properties: new Map(),
  } as unknown as Model;

  const model: Model = {
    kind: "Model",
    name: "GetKeyringOutput",
    properties: new Map<string, ModelProperty>([
      ["activeKid", makeProp(makeScalar("string"))],
      [
        "entries",
        { type: entriesArray, optional: false } as unknown as ModelProperty,
      ],
      ["aadContext", makeProp(makeScalar("bytes"))],
    ]),
  } as unknown as Model;

  return walkModel(makeProgram(redactMap), model, () => {});
}

describe("byteParity_GetKeyringInput_CommittedFixtureIdentical", () => {
  it("regenerated GetKeyringInput.g.cs is byte-identical to the committed fixture", () => {
    const { fields, nestedModels } = buildGetKeyringInputWalk();
    const [inputFile] = emitCsharpDtos(
      "getKeyring",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring",
      KC_SPEC,
      fields,
      [],
      nestedModels,
    );

    expect(inputFile!.content).toBe(
      readFixture(join(CLIENTS_HOME, "Keyring", "GetKeyringInput.g.cs")),
    );
  });
});

describe("byteParity_GetKeyringOutput_CommittedFixtureIdentical", () => {
  it("regenerated GetKeyringOutput.g.cs (nested KeyringEntry, keyBytes redacted) is byte-identical", () => {
    const { fields, nestedModels } = buildGetKeyringOutputWalk();
    const [, outputFile] = emitCsharpDtos(
      "getKeyring",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring",
      KC_SPEC,
      [],
      fields,
      nestedModels,
    );

    // Sanity: the nested redaction actually landed (guards against a vacuous compare).
    expect(outputFile!.content).toContain(
      "[property: RedactData(Reason = RedactReason.SecretInformation)] byte[] KeyBytes",
    );
    expect(outputFile!.content).toBe(
      readFixture(join(CLIENTS_HOME, "Keyring", "GetKeyringOutput.g.cs")),
    );
  });

  it("deliberate-drift detection: a mutated GetKeyringOutput fixture does NOT match", () => {
    const drifted = readFixture(
      join(CLIENTS_HOME, "Keyring", "GetKeyringOutput.g.cs"),
    ).replace(
      "public sealed record KeyringEntry(",
      "public sealed record KeyringEntryDRIFTED(",
    );
    const { fields, nestedModels } = buildGetKeyringOutputWalk();
    const [, outputFile] = emitCsharpDtos(
      "getKeyring",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring",
      KC_SPEC,
      [],
      fields,
      nestedModels,
    );

    expect(outputFile!.content).not.toBe(drifted);
  });
});

describe("byteParity_TemporalFixtureInput_CommittedFixtureIdentical", () => {
  it("regenerated TemporalFixtureInput.g.cs is byte-identical to the committed fixture", () => {
    const { input, output } = buildTemporalWalks();
    const [inputFile] = emitCsharpDtos(
      "temporalFixture",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
      "contracts/typespec/fixtures/temporal-shaped.tsp",
      input.fields,
      output.fields,
      output.nestedModels,
    );

    expect(inputFile!.content).toBe(
      readFixture(join(DTO_HOME, "TemporalFixtureInput.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated TemporalFixtureInput fixture does NOT match regenerated output", () => {
    // §1.20 / NV-1 non-vacuous guard: corrupt the fixture by one byte.
    const driftedFixture = readFixture(
      join(DTO_HOME, "TemporalFixtureInput.g.cs"),
    ).replace(
      "DateTimeOffset PastInstant",
      "DateTimeOffset PastInstantDRIFTED",
    );

    const { input, output } = buildTemporalWalks();
    const [inputFile] = emitCsharpDtos(
      "temporalFixture",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
      "contracts/typespec/fixtures/temporal-shaped.tsp",
      input.fields,
      output.fields,
      output.nestedModels,
    );

    expect(inputFile!.content).not.toBe(driftedFixture);
  });
});

describe("byteParity_TemporalFixtureOutput_CommittedFixtureIdentical", () => {
  it("regenerated TemporalFixtureOutput.g.cs (with the 2 nested composite records) is byte-identical", () => {
    const { input, output } = buildTemporalWalks();
    const [, outputFile] = emitCsharpDtos(
      "temporalFixture",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
      "contracts/typespec/fixtures/temporal-shaped.tsp",
      input.fields,
      output.fields,
      output.nestedModels,
    );

    expect(outputFile!.content).toBe(
      readFixture(join(DTO_HOME, "TemporalFixtureOutput.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated TemporalFixtureOutput fixture does NOT match regenerated output", () => {
    // §1.20 / NV-1 non-vacuous guard: corrupt a composite record by one byte.
    const driftedFixture = readFixture(
      join(DTO_HOME, "TemporalFixtureOutput.g.cs"),
    ).replace(
      "public sealed record ZonedInstantWire(",
      "public sealed record ZonedInstantWireDRIFTED(",
    );

    const { input, output } = buildTemporalWalks();
    const [, outputFile] = emitCsharpDtos(
      "temporalFixture",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
      "contracts/typespec/fixtures/temporal-shaped.tsp",
      input.fields,
      output.fields,
      output.nestedModels,
    );

    expect(outputFile!.content).not.toBe(driftedFixture);
  });
});

describe("byteParity_TemporalDto_TsFile", () => {
  it("regenerated temporal-fixture-dto.g.ts carries every temporal field + both composite interfaces", () => {
    const { input, output } = buildTemporalWalks();
    const tsFile = emitTsDtos(
      "temporalFixture",
      "contracts/typespec/fixtures/temporal-shaped.tsp",
      input.fields,
      output.fields,
      output.nestedModels,
    );

    expect(tsFile.fileName).toBe("temporal-fixture-dto.g.ts");
    expect(tsFile.content).toContain("export interface ZonedInstantWire {");
    expect(tsFile.content).toContain("readonly instant: string;");
    expect(tsFile.content).toContain(
      "export interface LocalAnchoredEventWire {",
    );
    expect(tsFile.content).toContain("readonly nextFireUtc?: string;");
    expect(tsFile.content).toContain("export interface TemporalFixtureInput {");
    expect(tsFile.content).toContain("readonly optionalInstant?: string;");
    expect(tsFile.content).toContain("readonly zoned: ZonedInstantWire;");
    expect(tsFile.content).toContain(
      "readonly schedule: LocalAnchoredEventWire;",
    );
    // No null union — prefer-undefined.
    expect(tsFile.content).not.toContain("| null");
  });

  it("regenerated temporal-fixture-dto.g.ts is byte-identical to the committed fixture", () => {
    const { input, output } = buildTemporalWalks();
    const tsFile = emitTsDtos(
      "temporalFixture",
      "contracts/typespec/fixtures/temporal-shaped.tsp",
      input.fields,
      output.fields,
      output.nestedModels,
    );

    expect(tsFile.content).toBe(
      readFixture(join(DTO_HOME, "temporal-fixture-dto.g.ts")),
    );
  });

  it("deliberate-drift detection: a mutated temporal-fixture-dto.g.ts fixture does NOT match", () => {
    const drifted = readFixture(
      join(DTO_HOME, "temporal-fixture-dto.g.ts"),
    ).replace(
      "export interface ZonedInstantWire {",
      "export interface ZonedInstantWireDRIFTED {",
    );
    const { input, output } = buildTemporalWalks();
    const tsFile = emitTsDtos(
      "temporalFixture",
      "contracts/typespec/fixtures/temporal-shaped.tsp",
      input.fields,
      output.fields,
      output.nestedModels,
    );

    expect(tsFile.content).not.toBe(drifted);
  });
});

describe("byteParity_GetJwksDto_TsFile", () => {
  it("regenerated get-jwks-dto.g.ts matches expected shape", () => {
    const inputWalk = buildGetJwksInputWalk();
    const outputWalk = buildGetJwksOutputWalk();
    const tsFile = emitTsDtos(
      "getJwks",
      "contracts/typespec/key-custodian/key-custodian.tsp",
      inputWalk.fields,
      outputWalk.fields,
      outputWalk.nestedModels,
    );

    expect(tsFile.fileName).toBe("get-jwks-dto.g.ts");
    // Structural checks (byte-exact would require committing the .g.ts too;
    // structural assertion pins the shape without the fragility of full-string compare).
    expect(tsFile.content).toContain("export interface Jwk {");
    expect(tsFile.content).toContain("readonly kid: string;");
    expect(tsFile.content).toContain("export interface GetJwksInput {");
    expect(tsFile.content).toContain("export interface GetJwksOutput {");
    expect(tsFile.content).toContain("readonly keys: readonly Jwk[];");
  });
});

// ---------------------------------------------------------------------------
// Enum-shaped byte-gates (the enum-shaped.tsp committed fixtures)
// ---------------------------------------------------------------------------

const ENUM_SHAPED_SRC = "contracts/typespec/fixtures/enum-shaped.tsp";
const ENUM_DTO_NS = "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated";

// The enum-shaped `enums` op model (Input mirrors Output) — synthetic stubs that
// produce the SAME walk result the real .tsp compile produces.
function makeEnumStub(
  name: string,
  members: Array<{ name: string; value?: string | number }>,
): Scalar {
  return {
    kind: "Enum",
    name,
    members: new Map(
      members.map((m) => [m.name, { name: m.name, value: m.value }]),
    ),
  } as unknown as Scalar;
}

function makeUnionStub(name: string | undefined, literals: string[]): Scalar {
  return {
    kind: "Union",
    name,
    variants: new Map(
      literals.map((lit, i) => [
        Symbol(`v${i}`),
        { type: { kind: "String", value: lit } },
      ]),
    ),
  } as unknown as Scalar;
}

function buildEnumsWalks() {
  const keyKind = makeEnumStub("FixtureKeyKind", [
    { name: "Rsa" },
    { name: "Aes" },
    { name: "Secret" },
  ]);
  const level = makeEnumStub("FixtureLevel", [
    { name: "Low", value: 0 },
    { name: "Medium", value: 5 },
    { name: "High", value: 10 },
  ]);
  const status = makeUnionStub("FixtureStatus", [
    "active",
    "inactive",
    "pending",
  ]);
  const accountKind = makeUnionStub("FixtureAccountKind", [
    "internal",
    "third-party",
  ]);
  const inline = makeUnionStub(undefined, ["draft", "published", "archived"]);
  const keyKindArray: Model = {
    kind: "Model",
    name: "Array",
    indexer: { value: keyKind },
    properties: new Map(),
  } as unknown as Model;

  const fields = (modelName: string): Model =>
    ({
      kind: "Model",
      name: modelName,
      properties: new Map<string, ModelProperty>([
        [
          "keyKind",
          { type: keyKind, optional: false } as unknown as ModelProperty,
        ],
        ["level", { type: level, optional: false } as unknown as ModelProperty],
        [
          "status",
          { type: status, optional: false } as unknown as ModelProperty,
        ],
        [
          "accountKind",
          { type: accountKind, optional: false } as unknown as ModelProperty,
        ],
        [
          "inlineState",
          { type: inline, optional: false } as unknown as ModelProperty,
        ],
        [
          "optionalKind",
          { type: keyKind, optional: true } as unknown as ModelProperty,
        ],
        [
          "kinds",
          { type: keyKindArray, optional: false } as unknown as ModelProperty,
        ],
      ]),
    }) as unknown as Model;

  return {
    input: walkModel(makeProgram(), fields("EnumFixtureWalkInput"), () => {}),
    output: walkModel(makeProgram(), fields("EnumFixtureWalkOutput"), () => {}),
  };
}

describe("byteParity_EnumsDto_CommittedFixtures", () => {
  it("regenerated EnumFixtureOutput.g.cs is byte-identical to the committed fixture", () => {
    const { input, output } = buildEnumsWalks();
    const [, outputFile] = emitCsharpDtos(
      "enumFixture",
      ENUM_DTO_NS,
      ENUM_SHAPED_SRC,
      input.fields,
      output.fields,
      output.nestedModels,
      input.nestedEnums,
      output.nestedEnums,
    );

    expect(outputFile!.content).toBe(
      readFixture(join(DTO_HOME, "EnumFixtureOutput.g.cs")),
    );
  });

  it("regenerated EnumFixtureInput.g.cs is byte-identical to the committed fixture", () => {
    const { input, output } = buildEnumsWalks();
    const [inputFile] = emitCsharpDtos(
      "enumFixture",
      ENUM_DTO_NS,
      ENUM_SHAPED_SRC,
      input.fields,
      output.fields,
      output.nestedModels,
      input.nestedEnums,
      output.nestedEnums,
    );

    expect(inputFile!.content).toBe(
      readFixture(join(DTO_HOME, "EnumFixtureInput.g.cs")),
    );
  });

  it("regenerated enum-fixture-dto.g.ts is byte-identical to the committed fixture", () => {
    const { input, output } = buildEnumsWalks();
    const tsFile = emitTsDtos(
      "enumFixture",
      ENUM_SHAPED_SRC,
      input.fields,
      output.fields,
      output.nestedModels,
      input.nestedEnums,
      output.nestedEnums,
    );

    expect(tsFile.content).toBe(
      readFixture(join(DTO_HOME, "enum-fixture-dto.g.ts")),
    );
  });

  it("deliberate-drift detection: mutated EnumFixtureOutput fixture does NOT match", () => {
    const drifted = readFixture(
      join(DTO_HOME, "EnumFixtureOutput.g.cs"),
    ).replace(
      "public enum FixtureKeyKind",
      "public enum FixtureKeyKindDRIFTED",
    );
    const { input, output } = buildEnumsWalks();
    const [, outputFile] = emitCsharpDtos(
      "enumFixture",
      ENUM_DTO_NS,
      ENUM_SHAPED_SRC,
      input.fields,
      output.fields,
      output.nestedModels,
      input.nestedEnums,
      output.nestedEnums,
    );

    expect(outputFile!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// gRPC enum byte-gates (proto string-field + transport mapper enum bridge)
// ---------------------------------------------------------------------------

const GRPC_ENUM_NS = "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcEnum.Generated";
const ENUM_PROTO_NS = "D2.Services.Protos.EnumFixtures.V1";

const SIGN_WITH_KIND_KEY_KIND: NestedEnum = {
  name: "FixtureKeyKind",
  members: [
    { csName: "Rsa", wireValue: "Rsa", needsEnumMember: false },
    { csName: "Aes", wireValue: "Aes", needsEnumMember: false },
    { csName: "Secret", wireValue: "Secret", needsEnumMember: false },
  ],
};

function signWithKindReqFields(): readonly FieldInfo[] {
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
      name: "keyKind",
      csName: "KeyKind",
      csType: "FixtureKeyKind",
      tsName: "keyKind",
      tsType: "FixtureKeyKind",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      enumRef: SIGN_WITH_KIND_KEY_KIND,
      fieldNumber: 2,
    },
  ];
}

function signWithKindRespFields(): readonly FieldInfo[] {
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
      // Response enum (S-1) — exercises the client proto-string -> DTO-enum parse.
      name: "keyKind",
      csName: "KeyKind",
      csType: "FixtureKeyKind",
      tsName: "keyKind",
      tsType: "FixtureKeyKind",
      protoType: "string",
      repeated: false,
      optional: false,
      redactReason: undefined,
      enumRef: SIGN_WITH_KIND_KEY_KIND,
      fieldNumber: 2,
    },
  ];
}

describe("byteParity_SignWithKindEnumGrpc_CommittedFixtures", () => {
  it("regenerated enum-proto carries `string key_kind` + is byte-identical", () => {
    const proto = emitProto(
      "signWithKindFixture",
      "EnumFixturesSigner",
      "SignWithKindFixture",
      "unary",
      "d2.enumfixtures.v1",
      ENUM_PROTO_NS,
      ENUM_SHAPED_SRC,
      "SignWithKindFixtureRequest",
      signWithKindReqFields(),
      undefined,
      "SignWithKindFixtureOutput",
      signWithKindRespFields(),
      undefined,
      [],
      () => {},
    );

    expect(proto!.content).toContain("string key_kind = 2;");
    expect(proto!.content).toBe(
      readFixture(
        join(
          GRPC_ENUM_PROTOS,
          "enum_fixtures_signer_sign_with_kind_fixture.g.proto",
        ),
      ),
    );
  });

  it("regenerated SignWithKindFixtureTransportMappers.g.cs (the enum-string bridge) is byte-identical", () => {
    const [, mapper] = emitGrpcService(
      "signWithKindFixture",
      "EnumFixturesSigner",
      "SignWithKindFixture",
      ENUM_PROTO_NS,
      GRPC_ENUM_NS,
      GRPC_ENUM_NS,
      ENUM_SHAPED_SRC,
      "SignWithKindFixtureRequest",
      "SignWithKindFixtureResponse",
      "SignWithKindFixtureInput",
      signWithKindReqFields(),
      "SignWithKindFixtureOutput",
      signWithKindRespFields(),
      {
        kind: "handler",
        typeName: "ISignWithKindFixtureHandler",
        methodName: "HandleAsync",
        targetNamespace: undefined,
      },
    );

    expect(mapper.content).toContain(
      "internal static D2Result<FixtureKeyKind> ParseFixtureKeyKindWire(string? value)",
    );
    expect(mapper.content).toContain("internal string ToWire()");
    expect(mapper.content).toBe(
      readFixture(
        join(GRPC_ENUM_HOME, "SignWithKindFixtureTransportMappers.g.cs"),
      ),
    );
  });

  it("deliberate-drift detection: mutated mapper fixture does NOT match", () => {
    const drifted = readFixture(
      join(GRPC_ENUM_HOME, "SignWithKindFixtureTransportMappers.g.cs"),
    ).replace("ParseFixtureKeyKindWire", "ParseFixtureKeyKindWireDRIFTED");
    const [, mapper] = emitGrpcService(
      "signWithKindFixture",
      "EnumFixturesSigner",
      "SignWithKindFixture",
      ENUM_PROTO_NS,
      GRPC_ENUM_NS,
      GRPC_ENUM_NS,
      ENUM_SHAPED_SRC,
      "SignWithKindFixtureRequest",
      "SignWithKindFixtureResponse",
      "SignWithKindFixtureInput",
      signWithKindReqFields(),
      "SignWithKindFixtureOutput",
      signWithKindRespFields(),
      {
        kind: "handler",
        typeName: "ISignWithKindFixtureHandler",
        methodName: "HandleAsync",
        targetNamespace: undefined,
      },
    );

    expect(mapper.content).not.toBe(drifted);
  });
});

// The CLIENT-side gRPC enum fixtures (mapper + impl) carry the response-enum
// parse (To<Output>() returns D2Result<<Output>> + the impl surfaces a parse
// failure via BubbleFail). Pin them byte-identical so the committed fixtures stay
// emitter-determined (never hand-edited, §26.5).
const GRPC_ENUM_CLIENTS_NS =
  "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcEnum.Clients";

function signWithKindClientOp() {
  return {
    opName: "signWithKindFixture",
    grpcService: "EnumFixturesSigner",
    grpcMethod: "SignWithKindFixture",
    protoCsharpNs: ENUM_PROTO_NS,
    dtoCsharpNs: GRPC_ENUM_NS,
    sourceSpec: ENUM_SHAPED_SRC,
    requestModelName: "SignWithKindFixtureInput",
    requestFields: signWithKindReqFields(),
    responseModelName: "SignWithKindFixtureOutput",
    responseFields: signWithKindRespFields(),
  };
}

describe("byteParity_SignWithKindEnumGrpcClient_CommittedFixtures", () => {
  it("regenerated SignWithKindFixtureClientMappers.g.cs (response-enum parse) is byte-identical", () => {
    const [, , clientMappers] = emitGrpcClient(
      "EnumFixtures",
      [signWithKindClientOp()],
      GRPC_ENUM_CLIENTS_NS,
    );

    expect(clientMappers!.content).toContain(
      "var keyKindResult = string.ParseFixtureKeyKindWire(data.KeyKind);",
    );
    expect(clientMappers!.content).toBe(
      readFixture(
        join(GRPC_ENUM_HOME, "SignWithKindFixtureClientMappers.g.cs"),
      ),
    );
  });

  it("regenerated EnumFixturesGrpcClient.g.cs (response-parse surfacing) is byte-identical", () => {
    const [, impl] = emitGrpcClient(
      "EnumFixtures",
      [signWithKindClientOp()],
      GRPC_ENUM_CLIENTS_NS,
    );

    expect(impl!.content).toContain(
      "return D2Result<SignWithKindFixtureOutput?>.BubbleFail(responseParseFailure);",
    );
    expect(impl!.content).toBe(
      readFixture(join(GRPC_ENUM_HOME, "EnumFixturesGrpcClient.g.cs")),
    );
  });

  it("deliberate-drift detection: mutated client mapper fixture does NOT match", () => {
    const drifted = readFixture(
      join(GRPC_ENUM_HOME, "SignWithKindFixtureClientMappers.g.cs"),
    ).replace("ParseFixtureKeyKindWire", "ParseFixtureKeyKindWireDRIFTED");
    const [, , clientMappers] = emitGrpcClient(
      "EnumFixtures",
      [signWithKindClientOp()],
      GRPC_ENUM_CLIENTS_NS,
    );

    expect(clientMappers!.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Enum-module completeness byte-gates — the DTO pair (+ enum), the handler
// interface, the service impl, the client interface + DI extension + keys.
// These committed `.g.*` files were produced by the same pure emitters; this
// pins each byte-identical so a hand-edit (§26.5) would be caught. The
// SignWithKind DTOs were emitted with the per-op source-spec form the one-off
// generator passed; reproduced verbatim so the banner matches.
// ---------------------------------------------------------------------------

const SIGN_WITH_KIND_DTO_SRC = "<typespec op: signWithKindFixture>";

describe("byteParity_SignWithKindEnumDtos_CommittedFixtures", () => {
  it("regenerated SignWithKindFixtureInput.g.cs is byte-identical to the committed fixture", () => {
    const [inputFile] = emitCsharpDtos(
      "signWithKindFixture",
      GRPC_ENUM_NS,
      SIGN_WITH_KIND_DTO_SRC,
      signWithKindReqFields(),
      signWithKindRespFields(),
      [],
      [SIGN_WITH_KIND_KEY_KIND],
      [SIGN_WITH_KIND_KEY_KIND],
    );

    expect(inputFile!.content).toBe(
      readFixture(join(GRPC_ENUM_HOME, "SignWithKindFixtureInput.g.cs")),
    );
  });

  it("regenerated SignWithKindFixtureOutput.g.cs (with the co-located KeyKind enum) is byte-identical", () => {
    const [, outputFile] = emitCsharpDtos(
      "signWithKindFixture",
      GRPC_ENUM_NS,
      SIGN_WITH_KIND_DTO_SRC,
      signWithKindReqFields(),
      signWithKindRespFields(),
      [],
      [SIGN_WITH_KIND_KEY_KIND],
      [SIGN_WITH_KIND_KEY_KIND],
    );

    expect(outputFile!.content).toBe(
      readFixture(join(GRPC_ENUM_HOME, "SignWithKindFixtureOutput.g.cs")),
    );
  });

  it("regenerated sign-with-kind-fixture-dto.g.ts is byte-identical to the committed fixture", () => {
    const tsFile = emitTsDtos(
      "signWithKindFixture",
      SIGN_WITH_KIND_DTO_SRC,
      signWithKindReqFields(),
      signWithKindRespFields(),
      [],
      [SIGN_WITH_KIND_KEY_KIND],
      [SIGN_WITH_KIND_KEY_KIND],
    );

    expect(tsFile.content).toBe(
      readFixture(join(GRPC_ENUM_HOME, "sign-with-kind-fixture-dto.g.ts")),
    );
  });

  it("deliberate-drift detection: a mutated SignWithKindFixtureOutput fixture does NOT match", () => {
    const drifted = readFixture(
      join(GRPC_ENUM_HOME, "SignWithKindFixtureOutput.g.cs"),
    ).replace(
      "public enum FixtureKeyKind",
      "public enum FixtureKeyKindDRIFTED",
    );
    const [, outputFile] = emitCsharpDtos(
      "signWithKindFixture",
      GRPC_ENUM_NS,
      SIGN_WITH_KIND_DTO_SRC,
      signWithKindReqFields(),
      signWithKindRespFields(),
      [],
      [SIGN_WITH_KIND_KEY_KIND],
      [SIGN_WITH_KIND_KEY_KIND],
    );

    expect(outputFile!.content).not.toBe(drifted);
  });
});

describe("byteParity_SignWithKindEnumHandlerAndService_CommittedFixtures", () => {
  it("regenerated ISignWithKindFixtureHandler.g.cs is byte-identical to the committed fixture", () => {
    const handler = emitHandlerInterface(
      "signWithKindFixture",
      GRPC_ENUM_NS,
      "SignWithKindFixtureInput",
      "SignWithKindFixtureOutput",
      true,
      ENUM_SHAPED_SRC,
    );

    expect(handler.content).toBe(
      readFixture(join(GRPC_ENUM_HOME, "ISignWithKindFixtureHandler.g.cs")),
    );
  });

  it("regenerated EnumFixturesSignerService.g.cs is byte-identical to the committed fixture", () => {
    const [service] = emitGrpcService(
      "signWithKindFixture",
      "EnumFixturesSigner",
      "SignWithKindFixture",
      ENUM_PROTO_NS,
      GRPC_ENUM_NS,
      GRPC_ENUM_NS,
      ENUM_SHAPED_SRC,
      "SignWithKindFixtureRequest",
      "SignWithKindFixtureResponse",
      "SignWithKindFixtureInput",
      signWithKindReqFields(),
      "SignWithKindFixtureOutput",
      signWithKindRespFields(),
      {
        kind: "handler",
        typeName: "ISignWithKindFixtureHandler",
        methodName: "HandleAsync",
        targetNamespace: undefined,
      },
    );

    expect(service.content).toBe(
      readFixture(join(GRPC_ENUM_HOME, "EnumFixturesSignerService.g.cs")),
    );
  });

  it("deliberate-drift detection: a mutated handler-interface fixture does NOT match", () => {
    const drifted = readFixture(
      join(GRPC_ENUM_HOME, "ISignWithKindFixtureHandler.g.cs"),
    ).replace(
      "ISignWithKindFixtureHandler",
      "ISignWithKindFixtureHandlerDRIFTED",
    );
    const handler = emitHandlerInterface(
      "signWithKindFixture",
      GRPC_ENUM_NS,
      "SignWithKindFixtureInput",
      "SignWithKindFixtureOutput",
      true,
      ENUM_SHAPED_SRC,
    );

    expect(handler.content).not.toBe(drifted);
  });
});

describe("byteParity_SignWithKindEnumClientModule_CommittedFixtures", () => {
  it("regenerated IEnumFixturesGrpcClient.g.cs (client interface) is byte-identical", () => {
    const [iface] = emitGrpcClient(
      "EnumFixtures",
      [signWithKindClientOp()],
      GRPC_ENUM_CLIENTS_NS,
    );

    expect(iface!.content).toBe(
      readFixture(join(GRPC_ENUM_HOME, "IEnumFixturesGrpcClient.g.cs")),
    );
  });

  it("regenerated EnumFixturesGrpcClientsGenerated.g.cs (DI extension) is byte-identical", () => {
    const files = emitGrpcClient(
      "EnumFixtures",
      [signWithKindClientOp()],
      GRPC_ENUM_CLIENTS_NS,
    );

    expect(files[3]!.content).toBe(
      readFixture(
        join(GRPC_ENUM_HOME, "EnumFixturesGrpcClientsGenerated.g.cs"),
      ),
    );
  });

  it("regenerated SignWithKindFixtureClientKeys.g.cs is byte-identical to the committed fixture", () => {
    const keys = emitClientKeys(
      "signWithKindFixture",
      GRPC_ENUM_CLIENTS_NS,
      ENUM_SHAPED_SRC,
    );

    expect(keys.content).toBe(
      readFixture(join(GRPC_ENUM_HOME, "SignWithKindFixtureClientKeys.g.cs")),
    );
  });

  it("deliberate-drift detection: a mutated client-interface fixture does NOT match", () => {
    const drifted = readFixture(
      join(GRPC_ENUM_HOME, "IEnumFixturesGrpcClient.g.cs"),
    ).replace("IEnumFixturesGrpcClient", "IEnumFixturesGrpcClientDRIFTED");
    const [iface] = emitGrpcClient(
      "EnumFixtures",
      [signWithKindClientOp()],
      GRPC_ENUM_CLIENTS_NS,
    );

    expect(iface!.content).not.toBe(drifted);
  });
});
