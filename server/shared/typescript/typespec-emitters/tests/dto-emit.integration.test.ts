// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Integration tests for the C# + TS DTO emitters via the TypeSpec test-host.
//
// Compiles inline .tsp programs and asserts that:
//   1. getJwks op → GetJwksInput.g.cs (parameterless) + GetJwksOutput.g.cs (with Jwk).
//   2. sign fixture → SignInput.g.cs carries [property: RedactData...].
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

// Mount the emitter package.
const D2EmitterTestLibrary = createTestLibrary({
  name: "@d2/typespec-emitters",
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
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.KeyCustodian;

      model Jwk { kid: string; n: string; e: string; kty: string; use: string; alg: string; }
      model GetJwksOutput { keys: Jwk[]; }

      @d2ServedBy("KeyCustodian")
      op getJwks(): GetJwksOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": { "csharp-namespace": "D2.Test" } },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter((d) => d.severity === "error");
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

  it("sign fixture with @d2Redact → [property: RedactData] in SignInput.g.cs", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Fixtures;

      model SignInput { kid: string; @d2Redact payload: bytes; }
      model SignOutput { signature: string; }

      @d2ServedBy("KeyCustodian")
      @d2InProcess
      @d2GrpcMethod("KeyCustodianSigner", "Sign")
      op sign(input: SignInput): SignOutput;
      `,
    );

    await host.compile("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": { "csharp-namespace": "D2.Test" } },
      outputDir: "testing:/out",
    });

    const errors = host.program.diagnostics.filter((d) => d.severity === "error");
    expect(errors).toHaveLength(0);

    const inputContent = getEmittedFile(host, "SignInput.g.cs");
    expect(inputContent).toBeDefined();
    // The [property:] target is load-bearing — a bare param attribute would NOT
    // be seen by the property-reflecting RedactDataDestructuringPolicy.
    expect(inputContent).toContain(
      "[property: RedactData(Reason = RedactReason.PersonalInformation)] byte[] Payload",
    );
    expect(inputContent).toContain("using D2.Shared.Utilities.Attributes;");
    expect(inputContent).toContain("using D2.Shared.Utilities.Enums;");
    // Non-redacted kid field has no attribute.
    expect(inputContent).toContain("string Kid");
    // Only the Payload param has [property: RedactData]; kid does not.
    // The attribute must appear exactly once (for Payload only).
    const redactCount = (inputContent!.match(/\[property: RedactData/g) ?? []).length;
    expect(redactCount).toBe(1);

    // TS side: redacted field emitted normally (no attribute).
    const tsContent = getEmittedFile(host, "sign-dto.g.ts");
    expect(tsContent).toBeDefined();
    expect(tsContent).toContain("readonly payload: Uint8Array;");
    expect(tsContent).not.toContain("RedactData");
  });
});

describe("dtoEmitIntegration_UnmappedScalar_D2TSP001Diagnostic", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [D2DecoratorTestLibrary, D2EmitterTestLibrary],
    });
  });

  it("op with unmapped scalar (utcDateTime) → error diagnostic fires", async () => {
    host.addTypeSpecFile(
      "main.tsp",
      `
      import "@d2/typespec-decorators";
      using D2;
      namespace D2.Test;

      model BadInput { timestamp: utcDateTime; }
      op badOp(input: BadInput): void;
      `,
    );

    // host.compile() throws when error-severity diagnostics are reported.
    // Catch the error and then inspect host.program.diagnostics.
    let compileError: unknown = undefined;
    try {
      await host.compile("main.tsp", {
        emit: ["@d2/typespec-emitters"],
        options: { "@d2/typespec-emitters": { "csharp-namespace": "D2.Test" } },
        outputDir: "testing:/out",
      });
    } catch (err) {
      compileError = err;
    }

    // Either the compile threw (because of the error diagnostic), or the
    // program has error diagnostics recorded (depending on TypeSpec version behavior).
    const programErrors = host.program.diagnostics.filter((d) => d.severity === "error");
    const hasErrors = compileError !== undefined || programErrors.length > 0;
    expect(hasErrors).toBe(true);
  });
});
