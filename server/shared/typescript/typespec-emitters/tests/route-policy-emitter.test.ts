// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Unit tests for the REST route+policy emitter.
//
// Coverage: verb→Map* (all 5 verbs), scope policy (any/all/harmless/none),
// delegation branches (façade and handler), marker records (rate-tier/csrf),
// MAP-ii 2xx-status-mapped shape, §9.2 no per-route audience, banner/conventions,
// D2TSP004/D2TSP005 guards, adversarial inputs.

import { describe, it, expect } from "vitest";
import {
  emitRoutePolicy,
  emitRoutePolicyMarkers,
  verbToMapMethod,
} from "../src/lib/route-policy-emitter.js";
import type { RoutePolicyEmitInput, HttpVerb, ScopePolicy } from "../src/lib/route-policy-emitter.js";

// ---------------------------------------------------------------------------
// Fixture builders
// ---------------------------------------------------------------------------

function makeFacadeInput(overrides: Partial<RoutePolicyEmitInput> = {}): RoutePolicyEmitInput {
  return {
    opName: "sign",
    verb: "post",
    routePath: "/internal/v1/kc/sign",
    delegationTarget: {
      kind: "facade",
      typeName: "IKeyCustodianSignerFacade",
      methodName: "SignAsync",
    },
    delegationTargetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
    inputTypeName: "SignInput",
    outputTypeName: "SignOutput",
    dtoNamespace: "D2.Edge.Tests.TypeSpecDto.Generated",
    scopePolicy: { kind: "any", scopes: ["self.write"] },
    registrationNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated",
    sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
    ...overrides,
  };
}

function makeHandlerInput(overrides: Partial<RoutePolicyEmitInput> = {}): RoutePolicyEmitInput {
  return {
    opName: "getJwks",
    verb: "get",
    routePath: "/well-known/jwks",
    delegationTarget: {
      kind: "handler",
      typeName: "IGetJwksHandler",
      methodName: "HandleAsync",
    },
    delegationTargetNamespace: "D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks",
    inputTypeName: "GetJwksInput",
    outputTypeName: "GetJwksOutput",
    dtoNamespace: "D2.Edge.KeyCustodian.Clients",
    scopePolicy: { kind: "harmless" },
    registrationNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated",
    sourceSpec: "contracts/typespec/key-custodian/key-custodian.tsp",
    ...overrides,
  };
}

const SOURCE = "contracts/typespec/fixtures/sign-shaped.tsp";

// ---------------------------------------------------------------------------
// verbToMapMethod
// ---------------------------------------------------------------------------

describe("verbToMapMethod", () => {
  it("get → MapGet", () => expect(verbToMapMethod("get")).toBe("MapGet"));
  it("post → MapPost", () => expect(verbToMapMethod("post")).toBe("MapPost"));
  it("put → MapPut", () => expect(verbToMapMethod("put")).toBe("MapPut"));
  it("delete → MapDelete", () => expect(verbToMapMethod("delete")).toBe("MapDelete"));
  it("patch → MapPatch", () => expect(verbToMapMethod("patch")).toBe("MapPatch"));
});

// ---------------------------------------------------------------------------
// Verb → Map* in emitted content (all 5 verbs)
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — verb routing", () => {
  const verbs: Array<[HttpVerb, string]> = [
    ["get", "MapGet"],
    ["post", "MapPost"],
    ["put", "MapPut"],
    ["delete", "MapDelete"],
    ["patch", "MapPatch"],
  ];

  for (const [verb, mapMethod] of verbs) {
    it(`verb '${verb}' emits '${mapMethod}'`, () => {
      const file = emitRoutePolicy(makeFacadeInput({ verb }));
      expect(file.content).toContain(mapMethod);
    });
  }
});

// ---------------------------------------------------------------------------
// Delegation: façade branch
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — façade delegation", () => {
  it("injects the façade type, not IHandler", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("IKeyCustodianSignerFacade facade");
    expect(file.content).not.toContain("ISignHandler handler");
  });

  it("calls façade.SignAsync(input, ct)", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("facade.SignAsync(input, ct)");
  });

  it("emits a using for the façade target namespace", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;");
  });
});

// ---------------------------------------------------------------------------
// Delegation: handler branch
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — handler delegation", () => {
  it("injects the handler type, not a façade", () => {
    const file = emitRoutePolicy(makeHandlerInput());
    expect(file.content).toContain("IGetJwksHandler handler");
    expect(file.content).not.toContain("facade");
  });

  it("calls handler.HandleAsync(input, ct)", () => {
    const file = emitRoutePolicy(makeHandlerInput());
    expect(file.content).toContain("handler.HandleAsync(input, ct)");
  });

  it("emits a using for the handler target namespace", () => {
    const file = emitRoutePolicy(makeHandlerInput());
    expect(file.content).toContain(
      "using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;",
    );
  });
});

// ---------------------------------------------------------------------------
// Scope policy: RequireAnyScope
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — RequireAnyScope", () => {
  it("single scope → RequireAnyScope(\"self.write\")", () => {
    const file = emitRoutePolicy(
      makeFacadeInput({ scopePolicy: { kind: "any", scopes: ["self.write"] } }),
    );
    expect(file.content).toContain('builder.RequireAnyScope("self.write");');
  });

  it("multi-scope → RequireAnyScope first positional + rest params", () => {
    const policy: ScopePolicy = { kind: "any", scopes: ["self.read", "self.write"] };
    const file = emitRoutePolicy(makeFacadeInput({ scopePolicy: policy }));
    expect(file.content).toContain('builder.RequireAnyScope("self.read", "self.write");');
  });

  it("does NOT emit RequireAllScopes or MarkAsD2HarmlessEndpoint", () => {
    const file = emitRoutePolicy(
      makeFacadeInput({ scopePolicy: { kind: "any", scopes: ["self.write"] } }),
    );
    expect(file.content).not.toContain("RequireAllScopes");
    expect(file.content).not.toContain("MarkAsD2HarmlessEndpoint");
  });
});

// ---------------------------------------------------------------------------
// Scope policy: RequireAllScopes
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — RequireAllScopes", () => {
  it("single scope → RequireAllScopes(\"self.read\")", () => {
    const file = emitRoutePolicy(
      makeFacadeInput({ scopePolicy: { kind: "all", scopes: ["self.read"] } }),
    );
    expect(file.content).toContain('builder.RequireAllScopes("self.read");');
  });

  it("two scopes → RequireAllScopes first positional + one rest param", () => {
    const policy: ScopePolicy = { kind: "all", scopes: ["self.read", "self.write"] };
    const file = emitRoutePolicy(makeFacadeInput({ scopePolicy: policy }));
    expect(file.content).toContain('builder.RequireAllScopes("self.read", "self.write");');
  });

  it("does NOT emit RequireAnyScope or MarkAsD2HarmlessEndpoint", () => {
    const file = emitRoutePolicy(
      makeFacadeInput({ scopePolicy: { kind: "all", scopes: ["self.read"] } }),
    );
    expect(file.content).not.toContain("RequireAnyScope");
    expect(file.content).not.toContain("MarkAsD2HarmlessEndpoint");
  });
});

// ---------------------------------------------------------------------------
// Scope policy: Harmless
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — MarkAsD2HarmlessEndpoint", () => {
  it("harmless → MarkAsD2HarmlessEndpoint()", () => {
    const file = emitRoutePolicy(
      makeFacadeInput({ scopePolicy: { kind: "harmless" } }),
    );
    expect(file.content).toContain("builder.MarkAsD2HarmlessEndpoint();");
  });

  it("harmless does NOT emit RequireAnyScope or RequireAllScopes", () => {
    const file = emitRoutePolicy(
      makeFacadeInput({ scopePolicy: { kind: "harmless" } }),
    );
    expect(file.content).not.toContain("RequireAnyScope");
    expect(file.content).not.toContain("RequireAllScopes");
  });
});

// ---------------------------------------------------------------------------
// Rate-tier and CSRF markers
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — rate-tier marker", () => {
  it("rateTier present → WithMetadata(new D2GeneratedRateLimitTier(...))", () => {
    const file = emitRoutePolicy(makeFacadeInput({ rateTier: "Standard" }));
    expect(file.content).toContain('builder.WithMetadata(new D2GeneratedRateLimitTier("Standard"));');
  });

  it("rateTier absent → no D2GeneratedRateLimitTier in emitted content", () => {
    const file = emitRoutePolicy(makeFacadeInput({ rateTier: undefined }));
    expect(file.content).not.toContain("D2GeneratedRateLimitTier");
  });
});

describe("emitRoutePolicy — CSRF marker", () => {
  it("csrf present → WithMetadata(new D2GeneratedCsrfPosture(...))", () => {
    const file = emitRoutePolicy(makeFacadeInput({ csrf: "exempt" }));
    expect(file.content).toContain('builder.WithMetadata(new D2GeneratedCsrfPosture("exempt"));');
  });

  it("csrf absent → no D2GeneratedCsrfPosture in emitted content", () => {
    const file = emitRoutePolicy(makeFacadeInput({ csrf: undefined }));
    expect(file.content).not.toContain("D2GeneratedCsrfPosture");
  });
});

// ---------------------------------------------------------------------------
// §9.2 — NO per-route audience fluent
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — §9.2 no per-route audience", () => {
  it("does not emit RequireAudience or any per-route audience fluent", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).not.toContain("RequireAudience");
    expect(file.content).not.toContain("WithAudience");
    expect(file.content).not.toContain("ValidateAudience");
  });

  it("emits a doc-note that audience is enforced service-wide", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("§9.2");
  });
});

// ---------------------------------------------------------------------------
// MAP-ii — 2xx-status-mapped success branch, then ToProblemDetails for ≥400
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — MAP-ii result mapping", () => {
  it("success branch: Results.Json(result.Data, statusCode: status) comes BEFORE ToProblemDetails call", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    const successIdx = file.content.indexOf("Results.Json(result.Data, statusCode: status)");
    const failureIdx = file.content.indexOf("ToProblemDetails");
    expect(successIdx).toBeGreaterThan(-1);
    expect(failureIdx).toBeGreaterThan(-1);
    // Success branch must appear before ToProblemDetails (the failure-only guard).
    expect(successIdx).toBeLessThan(failureIdx);
  });

  it("success branch keys on status < 400, not result.Success", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("var status = (int)result.StatusCode;");
    expect(file.content).toContain("if (status < 400)");
    expect(file.content).not.toContain("if (result.Success)");
  });

  it("failure branch calls result.ToProblemDetails(http)", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("result.ToProblemDetails(http)");
  });

  it("failure branch serializes via Results.Json with application/problem+json content type", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("application/problem+json");
    expect(file.content).toContain("Results.Json(pd");
  });
});

// ---------------------------------------------------------------------------
// Delegate signature shape
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — delegate signature", () => {
  it("façade: signature includes (SignInput input, IKeyCustodianSignerFacade facade, HttpContext http, CancellationToken ct)", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("SignInput input");
    expect(file.content).toContain("IKeyCustodianSignerFacade facade");
    expect(file.content).toContain("HttpContext http");
    expect(file.content).toContain("CancellationToken ct");
  });

  it("handler: signature includes (GetJwksInput input, IGetJwksHandler handler, HttpContext http, CancellationToken ct)", () => {
    const file = emitRoutePolicy(makeHandlerInput());
    expect(file.content).toContain("GetJwksInput input");
    expect(file.content).toContain("IGetJwksHandler handler");
    expect(file.content).toContain("HttpContext http");
    expect(file.content).toContain("CancellationToken ct");
  });
});

// ---------------------------------------------------------------------------
// C# conventions
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — C# conventions", () => {
  it("emits auto-generated banner", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("<auto-generated>");
    expect(file.content).toContain("Manual edits will be lost on rebuild.");
  });

  it("emits #nullable enable", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("#nullable enable");
  });

  it("namespace appears before using directives", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    const nsIdx = file.content.indexOf("namespace ");
    const usingIdx = file.content.indexOf("using ");
    expect(nsIdx).toBeGreaterThan(-1);
    expect(usingIdx).toBeGreaterThan(-1);
    expect(nsIdx).toBeLessThan(usingIdx);
  });

  it("emits C# 14 extension(IEndpointRouteBuilder endpoints) block form", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("extension(IEndpointRouteBuilder endpoints)");
  });

  it("emits static class declaration", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("public static class SignRouteRegistration");
  });

  it("emits IEndpointConventionBuilder return type", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain("public IEndpointConventionBuilder Map");
  });

  it("file name is <PascalOp>RouteRegistration.g.cs", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.fileName).toBe("SignRouteRegistration.g.cs");
  });

  it("emits the correct route path in MapPost call", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).toContain('"/internal/v1/kc/sign"');
  });

  it("using directives are sorted alphabetically (SA1210)", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    const lines = file.content.split("\n");
    const usingLines = lines
      .filter((l) => l.startsWith("using ") && !l.startsWith("using D2.Shared.Auth.Http.ProblemDetails"))
      .map((l) => l.replace(/^using /, "").replace(";", "").trim());
    // Verify each using is >= the previous (sorted).
    for (let i = 1; i < usingLines.length; i++) {
      expect(usingLines[i]!.localeCompare(usingLines[i - 1]!)).toBeGreaterThanOrEqual(0);
    }
  });
});

// ---------------------------------------------------------------------------
// No phase/step/audit identifiers in emitted content
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — no phase/step labels", () => {
  it("emitted content contains no 'Step N' patterns", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).not.toMatch(/Step\s+\d+/i);
  });

  it("emitted content contains no 'Phase N' patterns", () => {
    const file = emitRoutePolicy(makeFacadeInput());
    expect(file.content).not.toMatch(/Phase\s+\d+/i);
  });
});

// ---------------------------------------------------------------------------
// Scope policy: "none" — defensive fallback (should never be reached; D2TSP004
// guards against calling emitRoutePolicy with kind==="none")
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — scopePolicy none (defensive fallback)", () => {
  it("emits no auth enforcement lines when scopePolicy.kind is none", () => {
    // This state is the defended-against case: D2TSP004 should have been raised
    // before calling emitRoutePolicy. This test covers the defensive fallback path.
    const file = emitRoutePolicy(
      makeFacadeInput({ scopePolicy: { kind: "none" } }),
    );
    expect(file.content).not.toContain("RequireAnyScope");
    expect(file.content).not.toContain("RequireAllScopes");
    expect(file.content).not.toContain("MarkAsD2HarmlessEndpoint");
  });
});

// ---------------------------------------------------------------------------
// emitRoutePolicyMarkers — standalone markers file
// ---------------------------------------------------------------------------

describe("emitRoutePolicyMarkers", () => {
  it("emits D2GeneratedRateLimitTier record", () => {
    const file = emitRoutePolicyMarkers("D2.Edge.Tests.TypeSpecRoute.Generated", SOURCE);
    expect(file.content).toContain("public sealed record D2GeneratedRateLimitTier(string Tier);");
  });

  it("emits D2GeneratedCsrfPosture record", () => {
    const file = emitRoutePolicyMarkers("D2.Edge.Tests.TypeSpecRoute.Generated", SOURCE);
    expect(file.content).toContain("public sealed record D2GeneratedCsrfPosture(string Posture);");
  });

  it("file name is D2GeneratedRoutePolicyMarkers.g.cs", () => {
    const file = emitRoutePolicyMarkers("D2.Edge.Tests.TypeSpecRoute.Generated", SOURCE);
    expect(file.fileName).toBe("D2GeneratedRoutePolicyMarkers.g.cs");
  });

  it("emits correct namespace", () => {
    const file = emitRoutePolicyMarkers("D2.Edge.Tests.TypeSpecRoute.Generated", SOURCE);
    expect(file.content).toContain("namespace D2.Edge.Tests.TypeSpecRoute.Generated;");
  });

  it("emits banner", () => {
    const file = emitRoutePolicyMarkers("D2.Edge.Tests.TypeSpecRoute.Generated", SOURCE);
    expect(file.content).toContain("<auto-generated>");
  });

  it("throws on empty namespace", () => {
    expect(() => emitRoutePolicyMarkers("", SOURCE)).toThrow("registrationNamespace must not be empty");
  });
});

// ---------------------------------------------------------------------------
// Adversarial inputs
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — adversarial inputs", () => {
  it("throws on empty opName", () => {
    expect(() => emitRoutePolicy(makeFacadeInput({ opName: "" }))).toThrow("opName must not be empty");
  });

  it("throws on empty routePath", () => {
    expect(() => emitRoutePolicy(makeFacadeInput({ routePath: "" }))).toThrow("routePath must not be empty");
  });

  it("throws on empty delegationTarget.typeName", () => {
    expect(() =>
      emitRoutePolicy(
        makeFacadeInput({
          delegationTarget: { kind: "facade", typeName: "", methodName: "SignAsync" },
        }),
      ),
    ).toThrow("delegationTarget.typeName must not be empty");
  });

  it("throws on empty delegationTargetNamespace", () => {
    expect(() => emitRoutePolicy(makeFacadeInput({ delegationTargetNamespace: "" }))).toThrow(
      "delegationTargetNamespace must not be empty",
    );
  });

  it("throws on empty registrationNamespace", () => {
    expect(() => emitRoutePolicy(makeFacadeInput({ registrationNamespace: "" }))).toThrow(
      "registrationNamespace must not be empty",
    );
  });
});

// ---------------------------------------------------------------------------
// [AsParameters] binding — GET / DELETE vs POST / PUT / PATCH
// ---------------------------------------------------------------------------

describe("emitRoutePolicy — [AsParameters] for GET and DELETE verbs", () => {
  it("GET verb emits [AsParameters] in the delegate parameter list", () => {
    const file = emitRoutePolicy(makeFacadeInput({ verb: "get" }));
    expect(file.content).toContain("[AsParameters]");
  });

  it("DELETE verb emits [AsParameters] in the delegate parameter list", () => {
    const file = emitRoutePolicy(makeFacadeInput({ verb: "delete" }));
    expect(file.content).toContain("[AsParameters]");
  });

  it("POST verb does NOT emit [AsParameters]", () => {
    const file = emitRoutePolicy(makeFacadeInput({ verb: "post" }));
    expect(file.content).not.toContain("[AsParameters]");
  });

  it("PUT verb does NOT emit [AsParameters]", () => {
    const file = emitRoutePolicy(makeFacadeInput({ verb: "put" }));
    expect(file.content).not.toContain("[AsParameters]");
  });

  it("PATCH verb does NOT emit [AsParameters]", () => {
    const file = emitRoutePolicy(makeFacadeInput({ verb: "patch" }));
    expect(file.content).not.toContain("[AsParameters]");
  });
});

// ---------------------------------------------------------------------------
// Byte-parity fixtures for the committed .g.cs route registration
// ---------------------------------------------------------------------------

/** Expected committed SignRouteRegistration.g.cs content. */
const SIGN_ROUTE_REGISTRATION_FIXTURE = [
  "// -----------------------------------------------------------------------",
  "// <auto-generated>",
  "//   Generated by the @d2/typespec-emitters TypeSpec emitter.",
  "//   Source spec: contracts/typespec/fixtures/sign-shaped.tsp",
  "//   Manual edits will be lost on rebuild.",
  "// </auto-generated>",
  "// -----------------------------------------------------------------------",
  "#nullable enable",
  "",
  "namespace D2.Edge.Tests.TypeSpecRoute.Generated;",
  "",
  "using D2.Edge.Tests.TypeSpecDto.Generated;",
  "using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;",
  "using D2.Shared.Auth.Http.Endpoints;",
  "using D2.Shared.Auth.Http.ProblemDetails;",
  "using D2.Shared.Result;",
  "using Microsoft.AspNetCore.Builder;",
  "using Microsoft.AspNetCore.Http;",
  "using Microsoft.AspNetCore.Routing;",
  "",
  "/// <summary>Faithful seam marker: rate-limit tier declaration for this route.</summary>",
  "public sealed record D2GeneratedRateLimitTier(string Tier);",
  "",
  "/// <summary>Faithful seam marker: CSRF posture declaration for this route.</summary>",
  "public sealed record D2GeneratedCsrfPosture(string Posture);",
  "",
  "/// <summary>Generated REST route registration for the <c>Sign</c> operation.</summary>",
  "public static class SignRouteRegistration",
  "{",
  "    extension(IEndpointRouteBuilder endpoints)",
  "    {",
  '        /// <summary>Maps <c>POST /internal/v1/kc/sign</c>, delegating to <see cref="IKeyCustodianSignerFacade"/>.</summary>',
  "        /// <remarks>Audience is enforced service-wide via <c>AuthOptions.Audience</c> — no per-route audience fluent (§9.2).</remarks>",
  "        public IEndpointConventionBuilder MapSignRoute()",
  "        {",
  "            var builder = endpoints.MapPost(",
  '                "/internal/v1/kc/sign",',
  "                static async (SignInput input, IKeyCustodianSignerFacade facade, HttpContext http, CancellationToken ct) =>",
  "                {",
  "                    var result = await facade.SignAsync(input, ct).ConfigureAwait(false);",
  "                    var status = (int)result.StatusCode;",
  "                    if (status < 400)",
  "                        return Results.Json(result.Data, statusCode: status);",
  "                    var pd = result.ToProblemDetails(http);",
  '                    return Results.Json(pd, statusCode: pd.Status ?? 500, contentType: "application/problem+json");',
  "                });",
  "",
  '            builder.RequireAnyScope("self.write");',
  '            builder.WithMetadata(new D2GeneratedRateLimitTier("Standard"));',
  '            builder.WithMetadata(new D2GeneratedCsrfPosture("exempt"));',
  "            return builder;",
  "        }",
  "    }",
  "}",
  "",
].join("\n");

/** Expected committed AllScopesRouteRegistration.g.cs content. */
const ALL_SCOPES_ROUTE_REGISTRATION_FIXTURE = [
  "// -----------------------------------------------------------------------",
  "// <auto-generated>",
  "//   Generated by the @d2/typespec-emitters TypeSpec emitter.",
  "//   Source spec: contracts/typespec/fixtures/sign-shaped.tsp",
  "//   Manual edits will be lost on rebuild.",
  "// </auto-generated>",
  "// -----------------------------------------------------------------------",
  "#nullable enable",
  "",
  "namespace D2.Edge.Tests.TypeSpecRoute.Generated;",
  "",
  "using D2.Edge.Tests.TypeSpecDto.Generated;",
  "using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;",
  "using D2.Shared.Auth.Http.Endpoints;",
  "using D2.Shared.Auth.Http.ProblemDetails;",
  "using D2.Shared.Result;",
  "using Microsoft.AspNetCore.Builder;",
  "using Microsoft.AspNetCore.Http;",
  "using Microsoft.AspNetCore.Routing;",
  "",
  "/// <summary>Generated REST route registration for the <c>AllScopes</c> operation.</summary>",
  "public static class AllScopesRouteRegistration",
  "{",
  "    extension(IEndpointRouteBuilder endpoints)",
  "    {",
  '        /// <summary>Maps <c>GET /internal/v1/kc/all-scopes</c>, delegating to <see cref="IKeyCustodianSignerFacade"/>.</summary>',
  "        /// <remarks>Audience is enforced service-wide via <c>AuthOptions.Audience</c> — no per-route audience fluent (§9.2).</remarks>",
  "        public IEndpointConventionBuilder MapAllScopesRoute()",
  "        {",
  "            var builder = endpoints.MapGet(",
  '                "/internal/v1/kc/all-scopes",',
  "                static async ([AsParameters] SignInput input, IKeyCustodianSignerFacade facade, HttpContext http, CancellationToken ct) =>",
  "                {",
  "                    var result = await facade.AllScopesAsync(input, ct).ConfigureAwait(false);",
  "                    var status = (int)result.StatusCode;",
  "                    if (status < 400)",
  "                        return Results.Json(result.Data, statusCode: status);",
  "                    var pd = result.ToProblemDetails(http);",
  '                    return Results.Json(pd, statusCode: pd.Status ?? 500, contentType: "application/problem+json");',
  "                });",
  "",
  '            builder.RequireAllScopes("self.read", "self.write");',
  "            return builder;",
  "        }",
  "    }",
  "}",
  "",
].join("\n");

describe("byteParity_SignRouteRegistration_CommittedFixtureIdentical", () => {
  it("regenerated SignRouteRegistration.g.cs is byte-identical to the committed fixture", () => {
    const file = emitRoutePolicy({
      opName: "sign",
      verb: "post",
      routePath: "/internal/v1/kc/sign",
      delegationTarget: {
        kind: "facade",
        typeName: "IKeyCustodianSignerFacade",
        methodName: "SignAsync",
      },
      delegationTargetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
      inputTypeName: "SignInput",
      outputTypeName: "SignOutput",
      dtoNamespace: "D2.Edge.Tests.TypeSpecDto.Generated",
      scopePolicy: { kind: "any", scopes: ["self.write"] },
      rateTier: "Standard",
      csrf: "exempt",
      registrationNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated",
      sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
    });
    expect(file.content).toBe(SIGN_ROUTE_REGISTRATION_FIXTURE);
  });

  it("deliberate-drift detection: mutated fixture does NOT match", () => {
    const drifted = SIGN_ROUTE_REGISTRATION_FIXTURE.replace("MapPost", "MapGet");
    const file = emitRoutePolicy({
      opName: "sign",
      verb: "post",
      routePath: "/internal/v1/kc/sign",
      delegationTarget: {
        kind: "facade",
        typeName: "IKeyCustodianSignerFacade",
        methodName: "SignAsync",
      },
      delegationTargetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
      inputTypeName: "SignInput",
      outputTypeName: "SignOutput",
      dtoNamespace: "D2.Edge.Tests.TypeSpecDto.Generated",
      scopePolicy: { kind: "any", scopes: ["self.write"] },
      rateTier: "Standard",
      csrf: "exempt",
      registrationNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated",
      sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
    });
    expect(file.content).not.toBe(drifted);
  });
});

describe("byteParity_AllScopesRouteRegistration_CommittedFixtureIdentical", () => {
  it("regenerated AllScopesRouteRegistration.g.cs is byte-identical to committed fixture", () => {
    const file = emitRoutePolicy({
      opName: "allScopes",
      verb: "get",
      routePath: "/internal/v1/kc/all-scopes",
      delegationTarget: {
        kind: "facade",
        typeName: "IKeyCustodianSignerFacade",
        methodName: "AllScopesAsync",
      },
      delegationTargetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
      inputTypeName: "SignInput",
      outputTypeName: "SignOutput",
      dtoNamespace: "D2.Edge.Tests.TypeSpecDto.Generated",
      scopePolicy: { kind: "all", scopes: ["self.read", "self.write"] },
      registrationNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated",
      sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
    });
    expect(file.content).toBe(ALL_SCOPES_ROUTE_REGISTRATION_FIXTURE);
  });

  it("deliberate-drift detection: RequireAllScopes swap → not.toBe", () => {
    const drifted = ALL_SCOPES_ROUTE_REGISTRATION_FIXTURE.replace("RequireAllScopes", "RequireAnyScope");
    const file = emitRoutePolicy({
      opName: "allScopes",
      verb: "get",
      routePath: "/internal/v1/kc/all-scopes",
      delegationTarget: {
        kind: "facade",
        typeName: "IKeyCustodianSignerFacade",
        methodName: "AllScopesAsync",
      },
      delegationTargetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade",
      inputTypeName: "SignInput",
      outputTypeName: "SignOutput",
      dtoNamespace: "D2.Edge.Tests.TypeSpecDto.Generated",
      scopePolicy: { kind: "all", scopes: ["self.read", "self.write"] },
      registrationNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated",
      sourceSpec: "contracts/typespec/fixtures/sign-shaped.tsp",
    });
    expect(file.content).not.toBe(drifted);
  });
});
