// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for the handler-interface emitter (src/lib/handler-interface-emitter.ts).
//
// Covers per the plan:
//   1. emitUsing=false → no per-file using directive for IHandler.
//   2. emitUsing=true  → the using IS present.
//   3. Namespace is verbatim; fileName = I<PascalOp>Handler.g.cs.
//   4. NO HandleAsync re-declaration (bare extends).
//   5. Banner / #nullable enable / namespace-before-using / public interface /
//      NO phase/step/audit identifiers (§5/§7).
//   6. Adversarial: empty opName / namespace / type names → Error thrown.

import { describe, it, expect } from "vitest";
import { emitHandlerInterface } from "../src/lib/handler-interface-emitter.js";

// ---------------------------------------------------------------------------
// 1. emitUsing=false — no per-file using for IHandler<,>
// ---------------------------------------------------------------------------

describe("handlerInterfaceEmitter_NoUsing_WhenEmitUsingFalse", () => {
  it("getJwks with emitUsing=false emits interface without using directive", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
    );

    expect(result.content).toContain(
      "public interface IGetJwksHandler : IHandler<GetJwksInput, GetJwksOutput>;",
    );
    expect(result.content).not.toContain(
      "using DcsvIo.D2.Handler.Abstractions;",
    );
  });
});

// ---------------------------------------------------------------------------
// 2. emitUsing=true — per-file using IS present
// ---------------------------------------------------------------------------

describe("handlerInterfaceEmitter_EmitsUsing_WhenEmitUsingTrue", () => {
  it("sign with emitUsing=true emits using DcsvIo.D2.Handler.Abstractions;", () => {
    const result = emitHandlerInterface(
      "sign",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated",
      "SignInput",
      "SignOutput",
      true,
      "public/contracts/typespec/fixtures/sign-shaped.tsp",
    );

    expect(result.content).toContain("using DcsvIo.D2.Handler.Abstractions;");
    expect(result.content).toContain(
      "public interface ISignHandler : IHandler<SignInput, SignOutput>;",
    );
  });
});

// ---------------------------------------------------------------------------
// 3. Namespace verbatim; fileName = I<PascalOp>Handler.g.cs
// ---------------------------------------------------------------------------

describe("handlerInterfaceEmitter_NamespaceAndFileName", () => {
  it("namespace is verbatim and fileName is I<PascalOp>Handler.g.cs", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
    );

    expect(result.fileName).toBe("IGetJwksHandler.g.cs");
    expect(result.content).toContain(
      "namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;",
    );
  });

  it("sign op produces ISignHandler.g.cs in the given namespace", () => {
    const result = emitHandlerInterface(
      "sign",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated",
      "SignInput",
      "SignOutput",
      true,
      "public/contracts/typespec/fixtures/sign-shaped.tsp",
    );

    expect(result.fileName).toBe("ISignHandler.g.cs");
    expect(result.content).toContain(
      "namespace DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated;",
    );
  });
});

// ---------------------------------------------------------------------------
// 4. NO HandleAsync re-declaration (bare extends)
// ---------------------------------------------------------------------------

describe("handlerInterfaceEmitter_BareExtends_NoHandleAsync", () => {
  it("emitted interface has no HandleAsync body member", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
    );

    expect(result.content).not.toContain("HandleAsync");
    // The interface declaration is a one-liner (bare extends with semicolon, no braces).
    expect(result.content).not.toContain("{");
  });
});

// ---------------------------------------------------------------------------
// 5. Banner / #nullable enable / namespace-before-using / conventions
// ---------------------------------------------------------------------------

describe("handlerInterfaceEmitter_ConventionsAndBanner", () => {
  it("emits the auto-generated banner", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
    );

    expect(result.content).toContain("// <auto-generated>");
    expect(result.content).toContain(
      "Generated by the @dcsv-io/d2-typespec-emitters TypeSpec emitter.",
    );
    expect(result.content).toContain(
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
    );
  });

  it("emits #nullable enable", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "Some.Namespace",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "spec.tsp",
    );
    expect(result.content).toContain("#nullable enable");
  });

  it("namespace appears before any using directive", () => {
    const result = emitHandlerInterface(
      "sign",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated",
      "SignInput",
      "SignOutput",
      true,
      "public/contracts/typespec/fixtures/sign-shaped.tsp",
    );

    const nsIdx = result.content.indexOf("namespace");
    const usingIdx = result.content.indexOf(
      "using DcsvIo.D2.Handler.Abstractions;",
    );
    expect(nsIdx).toBeGreaterThanOrEqual(0);
    expect(usingIdx).toBeGreaterThan(nsIdx);
  });

  it("interface is declared public", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "Ns",
      "In",
      "Out",
      false,
      "spec.tsp",
    );
    expect(result.content).toContain("public interface");
  });

  it("contains no phase/step/audit/round identifiers in emitted code", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
    );

    // Must not contain step/audit/round labels (the recurring miss).
    expect(result.content).not.toMatch(/Step\s*\d/i);
    expect(result.content).not.toMatch(/\bR\d+\b/);
    expect(result.content).not.toMatch(/\bF\d+\b/);
    expect(result.content).not.toMatch(/audit/i);
    expect(result.content).not.toMatch(/deliverable/i);
    expect(result.content).not.toMatch(/Phase\s*\d/i);
  });
});

// ---------------------------------------------------------------------------
// 6. Adversarial: empty arguments → Error thrown
// ---------------------------------------------------------------------------

describe("handlerInterfaceEmitter_Adversarial_EmptyArgs", () => {
  it("empty opName throws an Error", () => {
    expect(() =>
      emitHandlerInterface("", "Ns", "In", "Out", false, "spec.tsp"),
    ).toThrow();
  });

  it("empty namespace throws an Error", () => {
    expect(() =>
      emitHandlerInterface("op", "", "In", "Out", false, "spec.tsp"),
    ).toThrow();
  });

  it("empty inputTypeName throws an Error", () => {
    expect(() =>
      emitHandlerInterface("op", "Ns", "", "Out", false, "spec.tsp"),
    ).toThrow();
  });

  it("empty outputTypeName throws an Error", () => {
    expect(() =>
      emitHandlerInterface("op", "Ns", "In", "", false, "spec.tsp"),
    ).toThrow();
  });
});

// ---------------------------------------------------------------------------
// 7. dtoNamespace — emits per-file using when DTOs are in a different namespace
// ---------------------------------------------------------------------------

describe("handlerInterfaceEmitter_DtoNamespace_EmitsUsing_WhenDifferentFromHandlerNamespace", () => {
  it("dtoNamespace different from handler namespace → per-file using emitted", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client",
    );

    expect(result.content).toContain(
      "using DcsvIo.D2.Private.Edge.KeyCustodian.Client;",
    );
    // Namespace must still come before the using.
    const nsIdx = result.content.indexOf(
      "namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;",
    );
    const usingIdx = result.content.indexOf(
      "using DcsvIo.D2.Private.Edge.KeyCustodian.Client;",
    );
    expect(nsIdx).toBeGreaterThanOrEqual(0);
    expect(usingIdx).toBeGreaterThan(nsIdx);
  });

  it("dtoNamespace same as handler namespace → no extra using emitted", () => {
    const ns =
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks";
    const result = emitHandlerInterface(
      "getJwks",
      ns,
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "spec.tsp",
      ns, // same → no extra using
    );

    // The same-namespace case must not produce a redundant using.
    expect(result.content.match(/^using /m)).toBeNull();
  });

  it("dtoNamespace undefined → no extra using emitted", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "spec.tsp",
      undefined,
    );

    // No dtoNamespace → no extra using.
    expect(result.content.match(/^using /m)).toBeNull();
  });

  it("dtoNamespace + emitUsing=true both emit their respective usings", () => {
    // Fixture scenario: both IHandler using AND a DTO namespace using are needed.
    const result = emitHandlerInterface(
      "sign",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated",
      "SignInput",
      "SignOutput",
      true,
      "spec.tsp",
      "DcsvIo.D2.Private.Edge.SomeOtherModule.Clients",
    );

    expect(result.content).toContain("using DcsvIo.D2.Handler.Abstractions;");
    expect(result.content).toContain(
      "using DcsvIo.D2.Private.Edge.SomeOtherModule.Clients;",
    );
  });
});

// ---------------------------------------------------------------------------
// Byte-gate for IGetJwksHandler.g.cs (real-KC handler interface — the new
// committed fixture in the KC app CQRS namespace, emitUsing=false).
// ---------------------------------------------------------------------------

// The committed IGetJwksHandler.g.cs lives in the KC app CQRS namespace and
// references Clients DTOs — so the emitter receives dtoNamespace=Clients, which
// triggers a per-file `using DcsvIo.D2.Private.Edge.KeyCustodian.Client;`.
const GET_JWKS_HANDLER_INTERFACE_FIXTURE = [
  "// -----------------------------------------------------------------------",
  "// <auto-generated>",
  "//   Generated by the @dcsv-io/d2-typespec-emitters TypeSpec emitter.",
  "//   Source spec: private/contracts/typespec/key-custodian/key-custodian.tsp",
  "//   Manual edits will be lost on rebuild.",
  "// </auto-generated>",
  "// -----------------------------------------------------------------------",
  "",
  "#nullable enable",
  "",
  "namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;",
  "",
  "using DcsvIo.D2.Private.Edge.KeyCustodian.Client;",
  "",
  "/// <summary>Generated handler interface for the <c>GetJwks</c> operation.</summary>",
  "public interface IGetJwksHandler : IHandler<GetJwksInput, GetJwksOutput>;",
  "",
].join("\n");

describe("handlerInterfaceEmitter_ByteGate_IGetJwksHandler", () => {
  it("regenerated IGetJwksHandler.g.cs is byte-identical to the committed fixture", () => {
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client",
    );
    expect(result.content).toBe(GET_JWKS_HANDLER_INTERFACE_FIXTURE);
  });

  it("deliberate-drift detection: mutated fixture does NOT match regenerated output", () => {
    const drifted = GET_JWKS_HANDLER_INTERFACE_FIXTURE.replace(
      "IGetJwksHandler",
      "IGetJwksHandlerDRIFTED",
    );
    const result = emitHandlerInterface(
      "getJwks",
      "DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
      "GetJwksInput",
      "GetJwksOutput",
      false,
      "private/contracts/typespec/key-custodian/key-custodian.tsp",
      "DcsvIo.D2.Private.Edge.KeyCustodian.Client",
    );
    expect(result.content).not.toBe(drifted);
  });
});

// ---------------------------------------------------------------------------
// Byte-gate for ISignHandler.g.cs (fixture — emitUsing=true, fixture ns).
// ---------------------------------------------------------------------------

// The sign fixture ISignHandler.g.cs lives in the gRPC fixture namespace but
// references SignInput/SignOutput from the DTO fixture namespace — so the emitter
// receives dtoNamespace="DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated" (different from
// "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated"), which triggers a per-file DTO using.
const SIGN_HANDLER_INTERFACE_FIXTURE = [
  "// -----------------------------------------------------------------------",
  "// <auto-generated>",
  "//   Generated by the @dcsv-io/d2-typespec-emitters TypeSpec emitter.",
  "//   Source spec: public/contracts/typespec/fixtures/sign-shaped.tsp",
  "//   Manual edits will be lost on rebuild.",
  "// </auto-generated>",
  "// -----------------------------------------------------------------------",
  "",
  "#nullable enable",
  "",
  "namespace DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated;",
  "",
  "using DcsvIo.D2.Handler.Abstractions;",
  "using DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated;",
  "",
  "/// <summary>Generated handler interface for the <c>Sign</c> operation.</summary>",
  "public interface ISignHandler : IHandler<SignInput, SignOutput>;",
  "",
].join("\n");

describe("handlerInterfaceEmitter_ByteGate_ISignHandler", () => {
  it("regenerated ISignHandler.g.cs is byte-identical to the committed fixture", () => {
    const result = emitHandlerInterface(
      "sign",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated",
      "SignInput",
      "SignOutput",
      true,
      "public/contracts/typespec/fixtures/sign-shaped.tsp",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
    );
    expect(result.content).toBe(SIGN_HANDLER_INTERFACE_FIXTURE);
  });

  it("deliberate-drift detection: mutated fixture does NOT match regenerated output", () => {
    const drifted = SIGN_HANDLER_INTERFACE_FIXTURE.replace(
      "ISignHandler",
      "ISignHandlerDRIFTED",
    );
    const result = emitHandlerInterface(
      "sign",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpc.Generated",
      "SignInput",
      "SignOutput",
      true,
      "public/contracts/typespec/fixtures/sign-shaped.tsp",
      "DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated",
    );
    expect(result.content).not.toBe(drifted);
  });
});
