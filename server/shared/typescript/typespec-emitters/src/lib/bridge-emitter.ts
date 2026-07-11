// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Edge HTTP→gRPC bridge emitter — pure string-template emission of:
//   1. <PascalOp>BridgeRegistration.g.cs — a static C# 14 extension on
//      IEndpointRouteBuilder that maps one HTTP verb+path, attaches scope /
//      harmless enforcement via the real D2 auth mechanism, adds faithful
//      metadata markers for rate-tier / CSRF, and delegates to
//      I{Module}GrpcClient.{Op}Async → D2Result → IResult via MAP-ii.
//   2. <Module>BridgeRegistrations.g.cs — optional MapAll{Module}Bridges()
//      aggregator that invokes every per-op Map{Op}Bridge for one ServedBy.
//
// Conventions:
//   - Auto-generated banner, #nullable enable, namespace BEFORE using.
//   - C# 14 extension(IEndpointRouteBuilder endpoints) block form (NOT `this T`).
//   - Verb → MapGet / MapPost / MapPut / MapDelete / MapPatch (caller validates).
//   - Delegation is ALWAYS the typed gRPC client surface
//     I{Module}GrpcClient.{PascalOp}Async — never façade / handler, never
//     server <Op>TransportMappers. ClientMappers stay inside the gRPC-client
//     emitter artifact; the bridge only DI-resolves the client interface.
//   - DI registration name (host-owned, documented in emitted remarks):
//     AddD2{Module}GrpcClients + {Module}GrpcClientOptions.Address.
//   - D2Result → IResult via MAP-ii (status < 400 → Json; ≥400 → ToProblemDetails).
//   - No phase/step/deliverable/audit-round identifiers in emitted code or source.

import { buildBanner } from "./banner.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";
import {
  buildIdempotencyGate,
  type IdempotencyKeySource,
} from "./idempotency-gate-emitter.js";
import { toPascal } from "./name-transforms.js";
import type { HttpVerb, ScopePolicy } from "./route-policy-emitter.js";
import { verbToMapMethod } from "./route-policy-emitter.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** All inputs required to emit one Edge HTTP→gRPC bridge registration. */
export interface BridgeEmitInput {
  /** lowerCamelCase op name (e.g. "pingAudit"). */
  readonly opName: string;
  /** Validated HTTP verb. */
  readonly verb: HttpVerb;
  /** The full route path (e.g. "/internal/v1/audit/ping"). */
  readonly routePath: string;
  /**
   * @d2ServedBy module name in PascalCase (e.g. "Audit"). Drives
   * I{Module}GrpcClient type name and AddD2{Module}GrpcClients remarks.
   */
  readonly moduleName: string;
  /** C# namespace where I{Module}GrpcClient lives. */
  readonly grpcClientNamespace: string;
  /** C# input DTO type name (e.g. "PingAuditInput"). */
  readonly inputTypeName: string;
  /** C# output DTO type name (e.g. "PingAuditOutput"). */
  readonly outputTypeName: string;
  /** C# namespace where the DTO types live. */
  readonly dtoNamespace: string;
  /** Auth policy for the route. */
  readonly scopePolicy: ScopePolicy;
  /** Optional rate-limit tier string — faithful seam only. */
  readonly rateTier?: string;
  /** Optional CSRF posture string — faithful seam only. */
  readonly csrf?: string;
  /**
   * Optional `@d2Idempotent` gate config. When set, weaves the same
   * `buildIdempotencyGate` fragments as in-process Map* (header/derived key +
   * store replay). PascalCase field names for derived keySource.
   */
  readonly idempotency?: {
    readonly keySource: IdempotencyKeySource;
    readonly ttlSeconds: number;
    readonly fields: readonly string[];
  };
  /** Target C# namespace for the generated bridge file (Edge.Api.Bridges.*). */
  readonly registrationNamespace: string;
  /** Relative spec path for the banner. */
  readonly sourceSpec: string;
}

/** One op collected for a per-module MapAll{Module}Bridges helper. */
export interface BridgeModuleOp {
  /** lowerCamelCase op name. */
  readonly opName: string;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the C# Edge HTTP→gRPC bridge registration for one standalone operation.
 *
 * Pure function — no I/O. Returns an {@link EmittedFile} whose `fileName` is
 * `<PascalOp>BridgeRegistration.g.cs`.
 *
 * Caller is responsible for validating verb + auth intent (D2TSP004/005) and
 * for ensuring the op is standalone + @route + @d2GrpcMethod.
 */
export function emitBridgeRegistration(input: BridgeEmitInput): EmittedFile {
  if (input.opName.length === 0)
    throw new Error("emitBridgeRegistration: opName must not be empty");
  if (input.routePath.length === 0)
    throw new Error("emitBridgeRegistration: routePath must not be empty");
  if (input.moduleName.length === 0)
    throw new Error("emitBridgeRegistration: moduleName must not be empty");
  if (input.grpcClientNamespace.length === 0)
    throw new Error(
      "emitBridgeRegistration: grpcClientNamespace must not be empty",
    );
  if (input.registrationNamespace.length === 0)
    throw new Error(
      "emitBridgeRegistration: registrationNamespace must not be empty",
    );

  const pascalOp = toPascal(input.opName);
  const mapMethod = verbToMapMethod(input.verb);
  const banner = buildBanner(input.sourceSpec);
  const clientType = `I${input.moduleName}GrpcClient`;
  const diExtension = `AddD2${input.moduleName}GrpcClients`;
  const optionsType = `${input.moduleName}GrpcClientOptions`;

  // Same gate weave as in-process Map* (route-policy-emitter) so public HTTP
  // on standalone bridges does not silently drop @d2Idempotent.
  const gate =
    input.idempotency !== undefined
      ? buildIdempotencyGate({
          keySource: input.idempotency.keySource,
          ttlSeconds: input.idempotency.ttlSeconds,
          fields: input.idempotency.fields,
          inputTypeName: input.inputTypeName,
          outputTypeName: input.outputTypeName,
          pascalOpName: pascalOp,
        })
      : undefined;

  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${input.registrationNamespace};`);
  lines.push("");

  const usings = buildBridgeUsings(
    input.registrationNamespace,
    input.grpcClientNamespace,
    input.dtoNamespace,
    gate?.extraUsings ?? [],
  );
  for (const u of usings) lines.push(`using ${u};`);
  lines.push("");

  if (input.rateTier !== undefined || input.csrf !== undefined) {
    lines.push(...buildMarkerRecords(input.rateTier, input.csrf));
    lines.push("");
  }

  lines.push(
    `/// <summary>Generated Edge HTTP→gRPC bridge registration for the <c>${pascalOp}</c> operation.</summary>`,
  );
  lines.push(`public static class ${pascalOp}BridgeRegistration`);
  lines.push("{");
  lines.push("    extension(IEndpointRouteBuilder endpoints)");
  lines.push("    {");
  lines.push(
    `        /// <summary>Maps <c>${input.verb.toUpperCase()} ${input.routePath}</c>, delegating to <see cref="${clientType}"/>.</summary>`,
  );
  lines.push(
    `        /// <remarks>Host registers the client via <c>${diExtension}(new ${optionsType} { Address = … })</c> — the bridge never hardcodes a channel address. Audience is enforced service-wide via <c>AuthOptions.Audience</c> — no per-route audience fluent (§9.2).</remarks>`,
  );
  lines.push(
    `        public IEndpointConventionBuilder Map${pascalOp}Bridge()`,
  );
  lines.push("        {");

  const inputParam =
    input.verb === "get" || input.verb === "delete"
      ? `[AsParameters] ${input.inputTypeName} input`
      : `${input.inputTypeName} input`;

  const storeParam = gate !== undefined ? `, ${gate.storeParam}` : "";

  lines.push(`            var builder = endpoints.${mapMethod}(`);
  lines.push(`                "${input.routePath}",`);
  lines.push(
    `                static async (${inputParam}, ${clientType} client${storeParam}, HttpContext http, CancellationToken ct) =>`,
  );
  lines.push("                {");

  if (gate !== undefined) {
    for (const l of gate.preDelegateLines) lines.push(l);
    lines.push("");
  }

  lines.push(
    `                    var result = await client.${pascalOp}Async(input, ct).ConfigureAwait(false);`,
  );

  if (gate !== undefined) {
    for (const l of gate.postDelegateLines) lines.push(l);
    lines.push("");
  }

  lines.push("                    var status = (int)result.StatusCode;");
  lines.push("                    if (status < 400)");
  lines.push(
    "                        return Results.Json(result.Data, statusCode: status);",
  );
  lines.push("                    var pd = result.ToProblemDetails(http);");
  lines.push(
    '                    return Results.Json(pd, statusCode: pd.Status ?? 500, contentType: "application/problem+json");',
  );
  lines.push("                });");
  lines.push("");

  lines.push(...buildAuthLines(input.scopePolicy));

  if (input.rateTier !== undefined)
    lines.push(
      `            builder.WithMetadata(new D2GeneratedRateLimitTier("${input.rateTier}"));`,
    );
  if (input.csrf !== undefined)
    lines.push(
      `            builder.WithMetadata(new D2GeneratedCsrfPosture("${input.csrf}"));`,
    );

  lines.push("            return builder;");
  lines.push("        }");
  lines.push("    }");
  lines.push("}");
  lines.push("");

  return {
    fileName: `${pascalOp}BridgeRegistration.g.cs`,
    content: lines.join("\n"),
  };
}

/**
 * Emit the per-module MapAll{Module}Bridges aggregator.
 *
 * Pure function — no I/O. Returns undefined when `ops` is empty (no empty trap).
 * File name: `<Module>BridgeRegistrations.g.cs`.
 */
export function emitMapAllBridges(
  moduleName: string,
  ops: readonly BridgeModuleOp[],
  registrationNamespace: string,
  sourceSpec: string,
): EmittedFile | undefined {
  if (moduleName.length === 0)
    throw new Error("emitMapAllBridges: moduleName must not be empty");
  if (registrationNamespace.length === 0)
    throw new Error(
      "emitMapAllBridges: registrationNamespace must not be empty",
    );
  if (ops.length === 0) return undefined;

  const banner = buildBanner(sourceSpec);
  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${registrationNamespace};`);
  lines.push("");
  lines.push("using Microsoft.AspNetCore.Builder;");
  lines.push("using Microsoft.AspNetCore.Routing;");
  lines.push("");
  lines.push(
    `/// <summary>Generated aggregator that maps every Edge HTTP→gRPC bridge for the <c>${moduleName}</c> module.</summary>`,
  );
  lines.push(`public static class ${moduleName}BridgeRegistrations`);
  lines.push("{");
  lines.push("    extension(IEndpointRouteBuilder endpoints)");
  lines.push("    {");
  lines.push(
    `        /// <summary>Maps all generated <c>${moduleName}</c> Edge HTTP→gRPC bridges.</summary>`,
  );
  lines.push(`        public void MapAll${moduleName}Bridges()`);
  lines.push("        {");
  for (const op of ops) {
    const pascalOp = toPascal(op.opName);
    lines.push(`            endpoints.Map${pascalOp}Bridge();`);
  }
  lines.push("        }");
  lines.push("    }");
  lines.push("}");
  lines.push("");

  return {
    fileName: `${moduleName}BridgeRegistrations.g.cs`,
    content: lines.join("\n"),
  };
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function buildBridgeUsings(
  registrationNamespace: string,
  grpcClientNamespace: string,
  dtoNamespace: string,
  extraUsings: readonly string[] = [],
): readonly string[] {
  const set = new Set<string>([
    "D2.Shared.Auth.Http.Endpoints",
    "D2.Shared.Auth.Http.ProblemDetails",
    "D2.Shared.Result",
    "Microsoft.AspNetCore.Builder",
    "Microsoft.AspNetCore.Http",
    "Microsoft.AspNetCore.Routing",
  ]);

  if (grpcClientNamespace !== registrationNamespace)
    set.add(grpcClientNamespace);
  if (
    dtoNamespace !== registrationNamespace &&
    dtoNamespace !== grpcClientNamespace
  )
    set.add(dtoNamespace);

  for (const u of extraUsings) set.add(u);

  return [...set].sort();
}

function buildAuthLines(policy: ScopePolicy): string[] {
  if (policy.kind === "any") {
    const first = policy.scopes[0]!;
    const rest = policy.scopes.slice(1);
    const restArgs =
      rest.length > 0 ? `, ${rest.map((s) => `"${s}"`).join(", ")}` : "";
    return [`            builder.RequireAnyScope("${first}"${restArgs});`];
  }
  if (policy.kind === "all") {
    const first = policy.scopes[0]!;
    const rest = policy.scopes.slice(1);
    const restArgs =
      rest.length > 0 ? `, ${rest.map((s) => `"${s}"`).join(", ")}` : "";
    return [`            builder.RequireAllScopes("${first}"${restArgs});`];
  }
  if (policy.kind === "harmless")
    return ["            builder.MarkAsD2HarmlessEndpoint();"];
  return [];
}

function buildMarkerRecords(
  rateTier: string | undefined,
  csrf: string | undefined,
): string[] {
  const lines: string[] = [];
  if (rateTier !== undefined) {
    lines.push(
      "/// <summary>Faithful seam marker: rate-limit tier declaration for this route.</summary>",
    );
    lines.push("public sealed record D2GeneratedRateLimitTier(string Tier);");
  }
  if (csrf !== undefined) {
    if (rateTier !== undefined) lines.push("");
    lines.push(
      "/// <summary>Faithful seam marker: CSRF posture declaration for this route.</summary>",
    );
    lines.push("public sealed record D2GeneratedCsrfPosture(string Posture);");
  }
  return lines;
}
