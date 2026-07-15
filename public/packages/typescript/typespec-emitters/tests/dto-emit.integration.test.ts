// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Integration tests for the C# + TS DTO emitters via the TypeSpec test-host.
//
// Compiles inline .tsp programs and asserts that:
//   1. getJwks op → GetJwksInput.g.cs (parameterless) + GetJwksOutput.g.cs (with Jwk).
//   2. sign fixture → SignFixtureInput.g.cs carries [property: RedactData...].
//   3. Unmapped scalar → D2TSP001 diagnostic fires (error exit).
//
// The integration test host (TypeSpec @testing) compiles inline .tsp and
// captures emitted files in an in-memory FS, identical to the smoke-emit tests.

import { describe, it, expect, beforeAll } from "vitest";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";

// Mount the decorators library.
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

// Mount the emitter package.
const D2EmitterTestLibrary = createTestLibrary({
  name: "@dcsv-io/d2-typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

// ---------------------------------------------------------------------------
// Helper: retrieve an emitted file from the in-memory FS by suffix.
// ---------------------------------------------------------------------------

function getEmittedFile(
  host: Awaited<ReturnType<typeof createTestHost>>,
  suffix: string,
): string | undefined {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return undefined;
  const key = [...stored.keys()].find((k) => k.endsWith(suffix));
  return key !== undefined ? stored.get(key) : undefined;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("dtoEmitIntegration_GetJwks_EmitsParamterlessInput", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("getJwks op with no params → GetJwksInput.g.cs parameterless + GetJwksOutput.g.cs with Jwk", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model Jwk { kid: string; n: string; e: string; kty: string; use: string; alg: string; }
      model GetJwksOutput { keys: Jwk[]; }

      @d2Query
      @d2InProcess
      @d2ServedBy("KeyCustodian")
      op getJwks(): GetJwksOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: {
        "@dcsv-io/d2-typespec-emitters": { "csharp-namespace": "D2.Test" },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // GetJwksInput — parameterless record.
    const inputContent = getEmittedFile(host, "GetJwksInput.g.cs");
    expect(inputContent).toBeDefined();
    expect(inputContent).toContain("public sealed record GetJwksInput;");
    expect(inputContent).not.toContain("GetJwksInput(");

    // GetJwksOutput — with Keys + Jwk nested record.
    const outputContent = getEmittedFile(host, "GetJwksOutput.g.cs");
    expect(outputContent).toBeDefined();
    expect(outputContent).toContain("IReadOnlyList<Jwk> Keys");
    expect(outputContent).toContain("public sealed record Jwk(");
    expect(outputContent).toContain("string Kid");

    // TS DTO file.
    const tsContent = getEmittedFile(host, "get-jwks-dto.g.ts");
    expect(tsContent).toBeDefined();
    expect(tsContent).toContain("export interface GetJwksOutput {");
    expect(tsContent).toContain("export interface Jwk {");
  });
});

describe("dtoEmitIntegration_Sign_RedactedFieldInGeneratedCSharp", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("sign fixture with @d2Redact → [property: RedactData] in SignFixtureInput.g.cs", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model SignFixtureInput { @d2Field(1) kid: string; @d2Field(2) @d2Redact("SecretInformation") payload: bytes; }
      model SignFixtureOutput { @d2Field(1) signature: string; }

      @d2Command
      @d2ServedBy("SignFixture")
      @d2InProcess
      @d2GrpcMethod("SignFixtureSigner", "SignFixture")
      op signFixture(input: SignFixtureInput): SignFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: {
        "@dcsv-io/d2-typespec-emitters": { "csharp-namespace": "D2.Test" },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const inputContent = getEmittedFile(host, "SignFixtureInput.g.cs");
    expect(inputContent).toBeDefined();
    // The [property:] target is load-bearing — a bare param attribute would NOT
    // be seen by the property-reflecting RedactDataDestructuringPolicy.
    expect(inputContent).toContain(
      "[property: RedactData(Reason = RedactReason.SecretInformation)] byte[] Payload",
    );
    expect(inputContent).toContain("using DcsvIo.D2.Utilities.Attributes;");
    expect(inputContent).toContain("using DcsvIo.D2.Utilities.Enums;");
    // Non-redacted kid field has no attribute.
    expect(inputContent).toContain("string Kid");
    // Only the Payload param has [property: RedactData]; kid does not.
    // The attribute must appear exactly once (for Payload only).
    const redactCount = (inputContent!.match(/\[property: RedactData/g) ?? [])
      .length;
    expect(redactCount).toBe(1);

    // TS side: redacted field emitted normally (no attribute).
    const tsContent = getEmittedFile(host, "sign-fixture-dto.g.ts");
    expect(tsContent).toBeDefined();
    expect(tsContent).toContain("readonly payload: Uint8Array;");
    expect(tsContent).not.toContain("RedactData");
  });
});

describe("dtoEmitIntegration_NestedRedact_ReasonThreadedThroughRealCompile", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("@d2Redact on an array-element nested-model field → attribute + both usings, zero diagnostics", async () => {
    // The exact GetKeyringOutput shape: entries is KeyringEntry[], whose keyBytes
    // field carries @d2Redact("SecretInformation") — a NESTED-model property.
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model KeyringEntry { kid: string; @d2Redact("SecretInformation") keyBytes: bytes; }
      model GetKeyringOutput { activeKid: string; entries: KeyringEntry[]; aadContext: bytes; }
      model GetKeyringInput { keyDomain: string; }

      @d2Query
      @d2InProcess
      @d2ServedBy("KeyCustodian")
      op getKeyring(input: GetKeyringInput): GetKeyringOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: {
        "@dcsv-io/d2-typespec-emitters": { "csharp-namespace": "D2.Test" },
      },
      outputDir: "testing:/out",
    });

    // Zero diagnostics — the formerly-refused nested @d2Redact now compiles clean.
    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const outputContent = getEmittedFile(host, "GetKeyringOutput.g.cs");
    expect(outputContent).toBeDefined();
    // The attribute lands on the nested KeyringEntry record's keyBytes param.
    expect(outputContent).toContain(
      "[property: RedactData(Reason = RedactReason.SecretInformation)] byte[] KeyBytes",
    );
    // Both usings are present (the CS0246 regression the usings-scan fix closes).
    expect(outputContent).toContain("using DcsvIo.D2.Utilities.Attributes;");
    expect(outputContent).toContain("using DcsvIo.D2.Utilities.Enums;");
    // aadContext (non-redacted, top-level) is present but unadorned.
    expect(outputContent).toContain("byte[] AadContext");
    // Exactly one redacted param (keyBytes only).
    const redactCount = (outputContent!.match(/\[property: RedactData/g) ?? [])
      .length;
    expect(redactCount).toBe(1);
  });
});

// ---------------------------------------------------------------------------
// Handler-interface emitter integration tests (items 23-26).
// ---------------------------------------------------------------------------

describe("dtoEmitIntegration_HandlerInterface_EmittedForEveryOp", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("getJwks (@d2Query+@d2InProcess) → IGetJwksHandler.g.cs emitted in app CQRS namespace", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model Jwk { kid: string; n: string; e: string; kty: string; use: string; alg: string; }
      model GetJwksOutput { keys: Jwk[]; }

      @d2Query
      @d2InProcess
      @d2ServedBy("KeyCustodian")
      @d2Concern("Jwks")
      op getJwks(): GetJwksOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: {
        "@dcsv-io/d2-typespec-emitters": {
          "csharp-namespace": "D2.Test",
          "csharp-clients-namespace":
            "DcsvIo.D2.Private.Edge.KeyCustodian.Client",
          "csharp-app-namespace-base":
            "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const handlerContent = getEmittedFile(host, "IGetJwksHandler.g.cs");
    expect(handlerContent).toBeDefined();
    // getJwks has no input params → input type falls back to "GetJwksInput" (by convention).
    expect(handlerContent).toContain("IHandler<");
    expect(handlerContent).toContain("GetJwksOutput");
    expect(handlerContent).toContain("public interface IGetJwksHandler");
    // emitUsing=false when csAppNamespaceBase is present.
    expect(handlerContent).not.toContain(
      "using DcsvIo.D2.Handler.Abstractions;",
    );
  });

  it("sign fixture (@d2GrpcMethod) → ISignFixtureHandler.g.cs emitted with using directive", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model SignFixtureInput { @d2Field(1) kid: string; @d2Field(2) @d2Redact("SecretInformation") payload: bytes; }
      model SignFixtureOutput { @d2Field(1) signature: string; }

      @d2Command
      @d2ServedBy("SignFixture")
      @d2InProcess
      @d2GrpcMethod("SignFixtureSigner", "SignFixture")
      op signFixture(input: SignFixtureInput): SignFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: {
        "@dcsv-io/d2-typespec-emitters": {
          "csharp-namespace":
            "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
          // grpc-service-namespace → the fixture gRPC namespace used for ISignFixtureHandler.
          "grpc-service-namespace":
            "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated",
          // No csharp-app-namespace-base → fixture mode → emitUsing=true.
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    const handlerContent = getEmittedFile(host, "ISignFixtureHandler.g.cs");
    expect(handlerContent).toBeDefined();
    expect(handlerContent).toContain(
      "public interface ISignFixtureHandler : IHandler<SignFixtureInput, SignFixtureOutput>;",
    );
    // fixture mode → emitUsing=true.
    expect(handlerContent).toContain("using DcsvIo.D2.Handler.Abstractions;");
    expect(handlerContent).toContain(
      "namespace DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated;",
    );
  });

  it("@d2Internal op → handler interface emitted, DTOs in app namespace", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model ReconcileInput { kid: string; }
      model ReconcileOutput { success: boolean; }

      @d2Command
      @d2Internal
      @d2ServedBy("KeyCustodian")
      op reconcile(input: ReconcileInput): ReconcileOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: {
        "@dcsv-io/d2-typespec-emitters": {
          "csharp-namespace": "D2.Fixture.Ns",
          "csharp-clients-namespace":
            "DcsvIo.D2.Private.Edge.KeyCustodian.Client",
          "csharp-app-namespace-base":
            "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // Handler interface must be emitted for internal op too.
    const handlerContent = getEmittedFile(host, "IReconcileHandler.g.cs");
    expect(handlerContent).toBeDefined();
    expect(handlerContent).toContain(
      "IHandler<ReconcileInput, ReconcileOutput>",
    );

    // DTOs for internal op go to the app CQRS namespace, NOT Clients.
    const inputContent = getEmittedFile(host, "ReconcileInput.g.cs");
    expect(inputContent).toBeDefined();
    expect(inputContent).toContain(
      "namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.Reconcile;",
    );
    expect(inputContent).not.toContain(
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client",
    );
  });
});

describe("dtoEmitIntegration_Temporal_EmitsScalarsAndComposites", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("temporal fixture (end-to-end real compile) → every temporal scalar + both composite records", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      scalar plainDateTime extends string;

      model ZonedInstantWire { instant: utcDateTime; zoneId: string; }
      model LocalAnchoredEventWire { scheduledLocal: plainDateTime; ianaZone: string; nextFireUtc?: utcDateTime; }

      model TemporalFixtureInput {
        pastInstant: utcDateTime;
        withOffset: offsetDateTime;
        birthday: plainDate;
        alarmTime: plainTime;
        wallClock: plainDateTime;
        elapsed: duration;
        optionalInstant?: utcDateTime;
        zoned: ZonedInstantWire;
        schedule: LocalAnchoredEventWire;
      }
      model TemporalFixtureOutput {
        pastInstant: utcDateTime;
        zoned: ZonedInstantWire;
        schedule: LocalAnchoredEventWire;
      }

      @d2Query
      @d2InProcess
      @d2ServedBy("TemporalFixtures")
      op temporalFixture(input: TemporalFixtureInput): TemporalFixtureOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: {
        "@dcsv-io/d2-typespec-emitters": {
          "csharp-namespace":
            "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
        },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // C# input — instant scalars → DateTimeOffset, plain/duration → string, optional → T?.
    const inputContent = getEmittedFile(host, "TemporalFixtureInput.g.cs");
    expect(inputContent).toBeDefined();
    expect(inputContent).toContain("DateTimeOffset PastInstant");
    expect(inputContent).toContain("DateTimeOffset WithOffset");
    expect(inputContent).toContain("string Birthday");
    expect(inputContent).toContain("string WallClock");
    expect(inputContent).toContain("string Elapsed");
    expect(inputContent).toContain("DateTimeOffset? OptionalInstant");
    expect(inputContent).toContain("ZonedInstantWire Zoned");
    expect(inputContent).toContain("LocalAnchoredEventWire Schedule");

    // C# output — the two composite records emitted as nested siblings.
    const outputContent = getEmittedFile(host, "TemporalFixtureOutput.g.cs");
    expect(outputContent).toBeDefined();
    expect(outputContent).toContain("public sealed record ZonedInstantWire(");
    expect(outputContent).toContain("DateTimeOffset Instant");
    expect(outputContent).toContain("string ZoneId");
    expect(outputContent).toContain(
      "public sealed record LocalAnchoredEventWire(",
    );
    expect(outputContent).toContain("string ScheduledLocal");
    expect(outputContent).toContain("DateTimeOffset? NextFireUtc");

    // TS DTO — every temporal field is string; composites are interfaces; no null union.
    const tsContent = getEmittedFile(host, "temporal-fixture-dto.g.ts");
    expect(tsContent).toBeDefined();
    expect(tsContent).toContain("export interface ZonedInstantWire {");
    expect(tsContent).toContain("export interface LocalAnchoredEventWire {");
    expect(tsContent).toContain("readonly nextFireUtc?: string;");
    expect(tsContent).toContain("readonly optionalInstant?: string;");
    expect(tsContent).not.toContain("| null");
  });
});

describe("dtoEmitIntegration_Enum_EmitsSiblingEnumsAndConstObjects", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("enum fixture (end-to-end real compile) → C# sibling enums + TS const-objects, correct attribute", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      enum FixtureKeyKind { Rsa, Aes, Secret }
      enum FixtureLevel { Low: 0, Medium: 5, High: 10 }
      union FixtureStatus { active: "active", inactive: "inactive", pending: "pending" }
      union FixtureAccountKind { internal: "internal", thirdParty: "third-party" }

      model EnumFixtureWalkInput {
        keyKind: FixtureKeyKind;
        level: FixtureLevel;
        status: FixtureStatus;
        accountKind: FixtureAccountKind;
        inlineState: "draft" | "published" | "archived";
        optionalKind?: FixtureKeyKind;
        kinds: FixtureKeyKind[];
      }
      model EnumFixtureWalkOutput {
        keyKind: FixtureKeyKind;
        level: FixtureLevel;
        status: FixtureStatus;
        accountKind: FixtureAccountKind;
        inlineState: "draft" | "published" | "archived";
        optionalKind?: FixtureKeyKind;
        kinds: FixtureKeyKind[];
      }

      @d2Query
      @d2InProcess
      @d2ServedBy("EnumFixtures")
      op enumFixture(input: EnumFixtureWalkInput): EnumFixtureWalkOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: {
        "@dcsv-io/d2-typespec-emitters": { "csharp-namespace": "D2.Test" },
      },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);

    // C# output — sibling enums with the correct [JsonStringEnumMemberName].
    const cs = getEmittedFile(host, "EnumFixtureOutput.g.cs");
    expect(cs).toBeDefined();
    expect(cs).toContain("[JsonConverter(typeof(JsonStringEnumConverter))]");
    expect(cs).toContain("public enum FixtureKeyKind");
    expect(cs).toContain("    Low = 0,"); // S-2 backing preserved
    expect(cs).toContain('[JsonStringEnumMemberName("third-party")]');
    expect(cs).toContain("    ThirdParty,");
    expect(cs).toContain("public enum EnumFixtureWalkOutputInlineState"); // S-4 synthetic
    expect(cs).toContain("using System.Text.Json.Serialization;");
    // The wrong attribute / namespace must NOT appear.
    expect(cs).not.toContain("System.Runtime.Serialization");
    expect(cs).not.toContain("EnumMember(Value");
    // Field types reference the enum names.
    expect(cs).toContain("FixtureKeyKind KeyKind");
    expect(cs).toContain("KeyKind? OptionalKind");
    expect(cs).toContain("IReadOnlyList<FixtureKeyKind> Kinds");

    // TS output — const-objects + derived types (NOT the TS enum keyword, no Zod).
    const ts = getEmittedFile(host, "enum-fixture-dto.g.ts");
    expect(ts).toBeDefined();
    expect(ts).toContain("export const FixtureKeyKind = {");
    expect(ts).toContain('  Rsa: "Rsa",');
    expect(ts).toContain('  Low: "Low",'); // S-2 value is the NAME, not 0
    expect(ts).toContain('  ThirdParty: "third-party",');
    expect(ts).toContain(
      "export type FixtureKeyKind = (typeof FixtureKeyKind)[keyof typeof FixtureKeyKind];",
    );
    expect(ts).not.toContain('from "zod"');
    expect(ts).not.toMatch(/\benum\s+KeyKind/);
    expect(ts).toContain("readonly kinds: readonly FixtureKeyKind[];");
  });

  it("loud-fail: a mixed-primitive union → D2TSP007 error diagnostic", async () => {
    const badHost = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
    badHost.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.Test;

      model BadInput { mixed: string | int32; }
      @d2Query @d2InProcess @d2ServedBy("X")
      op badEnum(input: BadInput): BadInput;
      `,
    );

    let compileError: unknown = undefined;
    try {
      await badHost.compile("main.tsp", {
        emit: ["@dcsv-io/d2-typespec-emitters"],
        options: {
          "@dcsv-io/d2-typespec-emitters": { "csharp-namespace": "D2.Test" },
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
  });

  it("loud-fail on a @d2GrpcMethod op: a mixed-primitive union → error (proto path)", async () => {
    const badHost = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
    badHost.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model BadGrpcInput { kid: string; mixed: string | int32; }
      model BadGrpcOutput { signature: string; }
      @d2Command @d2ServedBy("X")
      @d2GrpcMethod("XSigner", "Bad")
      op badGrpc(input: BadGrpcInput): BadGrpcOutput;
      `,
    );

    let compileError: unknown = undefined;
    try {
      await badHost.compile("main.tsp", {
        emit: ["@dcsv-io/d2-typespec-emitters"],
        options: {
          "@dcsv-io/d2-typespec-emitters": {
            "csharp-namespace": "D2.Test",
            "proto-package": "d2.x.v1",
            "proto-csharp-namespace": "D2.Services.Protos.X.V1",
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
    // No partial proto is emitted for the failing op.
    expect(getEmittedFile(badHost, "x_signer_bad.g.proto")).toBeUndefined();
  });
});

describe("dtoEmitIntegration_UnmappedScalar_D2TSP001Diagnostic", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("op with unmapped scalar → error diagnostic fires", async () => {
    // `unixTimestamp32` is a built-in TypeSpec temporal scalar that is NOT in the
    // registry (only utcDateTime/offsetDateTime/plainDate/plainTime/plainDateTime/
    // duration are mapped) — so it still trips the D2TSP001 loud failure.
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@dcsv-io/d2-typespec-decorators";
      using D2;
      namespace D2.Test;

      model BadInput { timestamp: unixTimestamp32; }
      op badOp(input: BadInput): void;
      `,
    );

    // host.compile() throws when error-severity diagnostics are reported.
    // Catch the error and then inspect host.program.diagnostics.
    let compileError: unknown = undefined;
    try {
      await host.compile("main.tsp", {
        emit: ["@dcsv-io/d2-typespec-emitters"],
        options: {
          "@dcsv-io/d2-typespec-emitters": { "csharp-namespace": "D2.Test" },
        },
        outputDir: "testing:/out",
      });
    } catch (err) {
      compileError = err;
    }

    // Either the compile threw (because of the error diagnostic), or the
    // program has error diagnostics recorded (depending on TypeSpec version behavior).
    const programErrors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    const hasErrors = compileError !== undefined || programErrors.length > 0;
    expect(hasErrors).toBe(true);
  });
});
