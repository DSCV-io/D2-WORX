// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Pure-unit tests for the Edge HTTP→gRPC bridge emitter.

import { describe, it, expect } from "vitest";
import {
  emitBridgeRegistration,
  emitMapAllBridges,
} from "../src/lib/bridge-emitter.js";
import type { BridgeEmitInput } from "../src/lib/bridge-emitter.js";

const SOURCE = "contracts/typespec/fixtures/bridge-shaped.tsp";

function makeBridgeInput(
  overrides: Partial<BridgeEmitInput> = {},
): BridgeEmitInput {
  return {
    opName: "pingAudit",
    verb: "get",
    routePath: "/internal/v1/audit/ping",
    moduleName: "Audit",
    grpcClientNamespace: "D2.Services.Audit.Client",
    inputTypeName: "PingAuditInput",
    outputTypeName: "PingAuditOutput",
    dtoNamespace: "D2.Services.Audit.Client.Ping",
    scopePolicy: { kind: "any", scopes: ["internal.audit.ping"] },
    registrationNamespace: "D2.Edge.Api.Bridges.Audit",
    sourceSpec: SOURCE,
    ...overrides,
  };
}

describe("emitBridgeRegistration — happy path", () => {
  it("Emitter_Bridge_CompilesAndMapsToGrpcClientCall", () => {
    const file = emitBridgeRegistration(makeBridgeInput());

    expect(file.fileName).toBe("PingAuditBridgeRegistration.g.cs");
    expect(file.content).toContain("namespace D2.Edge.Api.Bridges.Audit;");
    expect(file.content).toContain("MapPingAuditBridge()");
    expect(file.content).toContain("MapGet(");
    expect(file.content).toContain('"/internal/v1/audit/ping"');
    expect(file.content).toContain("IAuditGrpcClient client");
    expect(file.content).toContain("client.PingAuditAsync(input, ct)");
    expect(file.content).toContain("AddD2AuditGrpcClients");
    expect(file.content).toContain("AuditGrpcClientOptions");
    expect(file.content).toContain("RequireAnyScope");
    expect(file.content).toContain('"internal.audit.ping"');
    // MAP-ii
    expect(file.content).toContain("var status = (int)result.StatusCode;");
    expect(file.content).toContain("if (status < 400)");
    expect(file.content).toContain("ToProblemDetails");
    // Wrong-seam negatives — never façade / TransportMappers / hardcoded URL
    expect(file.content).not.toContain("TransportMappers");
    expect(file.content).not.toContain("IKeyCustodianApi");
    expect(file.content).not.toContain("IAuditApi");
    expect(file.content).not.toContain("HandleAsync");
    expect(file.content).not.toContain("https://");
  });

  it("post verb uses body binding (no AsParameters)", () => {
    const file = emitBridgeRegistration(
      makeBridgeInput({
        verb: "post",
        opName: "createAuditEvent",
        inputTypeName: "CreateAuditEventInput",
        outputTypeName: "CreateAuditEventOutput",
      }),
    );
    expect(file.content).toContain("MapPost(");
    expect(file.content).toContain("CreateAuditEventInput input");
    expect(file.content).not.toContain("[AsParameters]");
  });

  it("harmless auth → MarkAsD2HarmlessEndpoint", () => {
    const file = emitBridgeRegistration(
      makeBridgeInput({ scopePolicy: { kind: "harmless" } }),
    );
    expect(file.content).toContain("MarkAsD2HarmlessEndpoint()");
    expect(file.content).not.toContain("RequireAnyScope");
  });

  it("requireAllScopes → RequireAllScopes", () => {
    const file = emitBridgeRegistration(
      makeBridgeInput({
        scopePolicy: { kind: "all", scopes: ["a.read", "a.write"] },
      }),
    );
    expect(file.content).toContain("RequireAllScopes");
    expect(file.content).toContain('"a.read"');
    expect(file.content).toContain('"a.write"');
  });

  it("rateTier + csrf markers present", () => {
    const file = emitBridgeRegistration(
      makeBridgeInput({ rateTier: "Standard", csrf: "exempt" }),
    );
    expect(file.content).toContain("D2GeneratedRateLimitTier");
    expect(file.content).toContain("D2GeneratedCsrfPosture");
    expect(file.content).toContain('"Standard"');
    expect(file.content).toContain('"exempt"');
  });

  // Fail-without-fix (§2.3): without emitBridgeRegistration weaving
  // buildIdempotencyGate when idempotency is present, content would lack
  // D2GeneratedIdempotencyStore / Idempotency-Key / TryGetAsync / StoreAsync.
  it("idempotency header gate weaves store + replay (parity with Map*)", () => {
    const file = emitBridgeRegistration(
      makeBridgeInput({
        verb: "post",
        opName: "createAuditEvent",
        inputTypeName: "CreateAuditEventInput",
        outputTypeName: "CreateAuditEventOutput",
        idempotency: {
          keySource: "header",
          ttlSeconds: 3600,
          fields: [],
        },
      }),
    );
    expect(file.content).toContain("D2GeneratedIdempotencyStore store");
    expect(file.content).toContain('Headers["Idempotency-Key"]');
    expect(file.content).toContain("TryGetAsync");
    expect(file.content).toContain("StoreAsync");
    expect(file.content).toContain("TimeSpan.FromSeconds(3600)");
    expect(file.content).toContain("D2.Shared.Utilities.Extensions");
    expect(file.content).toContain("client.CreateAuditEventAsync");
  });

  it("fixture-shaped emit matches C# compile/run suite seam names", () => {
    // Pins the names consumed by Edge.Tests TypeSpecBridge validation suite
    // (IBridgeFixtureGrpcClient + MapPingBridgeFixtureBridge).
    const file = emitBridgeRegistration({
      opName: "pingBridgeFixture",
      verb: "get",
      routePath: "/internal/v1/fixtures/bridge-ping",
      moduleName: "BridgeFixture",
      grpcClientNamespace:
        "D2.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Fixtures",
      inputTypeName: "BridgeFixturePingInput",
      outputTypeName: "BridgeFixturePingOutput",
      dtoNamespace: "D2.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Fixtures",
      scopePolicy: { kind: "any", scopes: ["self.read"] },
      registrationNamespace:
        "D2.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Generated",
      sourceSpec: "contracts/typespec/fixtures/bridge-shaped.tsp",
    });
    expect(file.fileName).toBe("PingBridgeFixtureBridgeRegistration.g.cs");
    expect(file.content).toContain("IBridgeFixtureGrpcClient");
    expect(file.content).toContain("MapPingBridgeFixtureBridge()");
    expect(file.content).toContain("client.PingBridgeFixtureAsync");
    expect(file.content).toContain("AddD2BridgeFixtureGrpcClients");
    expect(file.content).toContain("RequireAnyScope");
    expect(file.content).toContain("ToProblemDetails");
  });
});

describe("emitBridgeRegistration — adversarial", () => {
  it("empty opName throws", () => {
    expect(() =>
      emitBridgeRegistration(makeBridgeInput({ opName: "" })),
    ).toThrow(/opName/);
  });

  it("empty routePath throws", () => {
    expect(() =>
      emitBridgeRegistration(makeBridgeInput({ routePath: "" })),
    ).toThrow(/routePath/);
  });

  it("empty moduleName throws", () => {
    expect(() =>
      emitBridgeRegistration(makeBridgeInput({ moduleName: "" })),
    ).toThrow(/moduleName/);
  });

  it("empty registrationNamespace throws", () => {
    expect(() =>
      emitBridgeRegistration(makeBridgeInput({ registrationNamespace: "" })),
    ).toThrow(/registrationNamespace/);
  });

  it("empty grpcClientNamespace throws", () => {
    expect(() =>
      emitBridgeRegistration(makeBridgeInput({ grpcClientNamespace: "" })),
    ).toThrow(/grpcClientNamespace/);
  });
});

describe("emitMapAllBridges", () => {
  it("emits aggregator when ≥1 bridge op", () => {
    const file = emitMapAllBridges(
      "Audit",
      [{ opName: "pingAudit" }, { opName: "listAuditEvents" }],
      "D2.Edge.Api.Bridges.Audit",
      SOURCE,
    );
    expect(file).toBeDefined();
    expect(file!.fileName).toBe("AuditBridgeRegistrations.g.cs");
    expect(file!.content).toContain("MapAllAuditBridges()");
    expect(file!.content).toContain("endpoints.MapPingAuditBridge();");
    expect(file!.content).toContain("endpoints.MapListAuditEventsBridge();");
  });

  it("zero ops → undefined (no empty trap)", () => {
    expect(
      emitMapAllBridges("Audit", [], "D2.Edge.Api.Bridges.Audit", SOURCE),
    ).toBeUndefined();
  });

  it("empty moduleName throws", () => {
    expect(() =>
      emitMapAllBridges(
        "",
        [{ opName: "x" }],
        "D2.Edge.Api.Bridges.Audit",
        SOURCE,
      ),
    ).toThrow(/moduleName/);
  });
});
