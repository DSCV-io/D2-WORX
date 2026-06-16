// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// REST route+policy emitter — pure string-template emission of:
//   1. <Service>RouteRegistration.g.cs — a static C# 14 extension on
//      IEndpointRouteBuilder that maps one HTTP verb+path, attaches scope /
//      harmless enforcement via the real D2 auth mechanism, adds faithful
//      metadata markers for rate-tier / CSRF, and maps D2Result → IResult
//      via the real ToProblemDetails (failure-only, success-first).
//   2. D2GeneratedRoutePolicyMarkers.g.cs — small emitter-owned marker records
//      (D2GeneratedRateLimitTier + D2GeneratedCsrfPosture) emitted once per
//      registration namespace for use by future Edge middleware.
//
// Conventions:
//   - Auto-generated banner, #nullable enable, namespace BEFORE using.
//   - C# 14 extension(IEndpointRouteBuilder endpoints) block form (NOT `this T`).
//   - Verb → MapGet / MapPost / MapPut / MapDelete / MapPatch.
//     Unsupported verbs → the caller must surface D2TSP005 before calling
//     this function; this emitter trusts verb is already validated.
//   - Scope/harmless enforcement uses the REAL D2 auth fluents:
//       RequireAnyScope / RequireAllScopes / MarkAsD2HarmlessEndpoint.
//     No per-route audience fluent (§9.2 — audience is service-level).
//   - D2Result → IResult via MAP-ii: success-first short-circuit, then
//     ToProblemDetails (failure-only extension) serialized verbatim.
//   - Rate-tier / CSRF: faithful metadata markers (no enforcement — unbuilt
//     consumer is future Edge middleware; ledgered in VALIDATION.md).
//   - No phase/step/deliverable/audit-round identifiers in emitted code or source.

import { buildBanner } from "./banner.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";
import { buildIdempotencyGate } from "./idempotency-gate-emitter.js";
import { toPascal } from "./name-transforms.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** Which HTTP verb the route uses. */
export type HttpVerb = "get" | "post" | "put" | "delete" | "patch";

/** How the route delegate delegates to the operation handler. */
export interface DelegationTarget {
  /** "facade" when the op has @d2InProcess; "handler" otherwise. */
  readonly kind: "facade" | "handler";
  /** C# interface type name (e.g. "IKeyCustodianSignerFacade" or "ISignHandler"). */
  readonly typeName: string;
  /** Method name to call on the target (e.g. "SignAsync" or "HandleAsync"). */
  readonly methodName: string;
}

/** Auth intent for the route. Exactly one of these must be set per routed op. */
export type ScopePolicy =
  | { readonly kind: "any"; readonly scopes: readonly string[] }
  | { readonly kind: "all"; readonly scopes: readonly string[] }
  | { readonly kind: "harmless" }
  | { readonly kind: "none" };

/** All inputs required to emit one route registration. */
export interface RoutePolicyEmitInput {
  /** lowerCamelCase op name (e.g. "sign"). */
  readonly opName: string;
  /** Validated HTTP verb. */
  readonly verb: HttpVerb;
  /** The full route path (e.g. "/internal/v1/kc/sign"). */
  readonly routePath: string;
  /** Delegation target — façade or handler. */
  readonly delegationTarget: DelegationTarget;
  /** C# namespace for the delegation target (for the using directive). */
  readonly delegationTargetNamespace: string;
  /** C# input DTO type name (e.g. "SignInput"). */
  readonly inputTypeName: string;
  /** C# output DTO type name (e.g. "SignOutput"). */
  readonly outputTypeName: string;
  /** C# namespace where the DTO types live (for the using directive). */
  readonly dtoNamespace: string;
  /** Auth policy for the route. */
  readonly scopePolicy: ScopePolicy;
  /** Optional rate-limit tier string (e.g. "Standard") — faithful seam only. */
  readonly rateTier?: string | undefined;
  /** Optional CSRF posture string (e.g. "exempt") — faithful seam only. */
  readonly csrf?: string | undefined;
  /**
   * Optional idempotency gate configuration. When present, the emitter weaves
   * a dedupe gate into the route delegate: key resolution (header or derived),
   * a replay-check before the delegation call, and a store-outcome call after.
   * When absent, the emitted route is byte-identical to a non-gated route.
   *
   * `fields` must be PascalCase C# property names (camel→Pascal mapping is
   * the caller's responsibility before passing here).
   */
  readonly idempotency?: {
    readonly keySource: "header" | "derived";
    readonly ttlSeconds: number;
    readonly fields: readonly string[];
  } | undefined;
  /** Target C# namespace for the generated file. */
  readonly registrationNamespace: string;
  /** Relative spec path for the banner. */
  readonly sourceSpec: string;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the C# REST route registration for one operation.
 *
 * Pure function — no I/O. Returns an {@link EmittedFile} whose `fileName` is
 * `<PascalOp>RouteRegistration.g.cs`.
 *
 * Caller is responsible for:
 *   - validating the verb (D2TSP005 must be raised before calling here);
 *   - validating the auth intent (D2TSP004 must be raised and this function
 *     must NOT be called when scopePolicy.kind === "none").
 */
export function emitRoutePolicy(input: RoutePolicyEmitInput): EmittedFile {
  if (input.opName.length === 0) throw new Error("emitRoutePolicy: opName must not be empty");
  if (input.routePath.length === 0) throw new Error("emitRoutePolicy: routePath must not be empty");
  if (input.delegationTarget.typeName.length === 0) throw new Error("emitRoutePolicy: delegationTarget.typeName must not be empty");
  if (input.delegationTargetNamespace.length === 0) throw new Error("emitRoutePolicy: delegationTargetNamespace must not be empty");
  if (input.registrationNamespace.length === 0) throw new Error("emitRoutePolicy: registrationNamespace must not be empty");

  const pascalOp = toPascal(input.opName);
  const mapMethod = verbToMapMethod(input.verb);
  const banner = buildBanner(input.sourceSpec);

  // Build the idempotency gate weave (if configured).
  const gate = input.idempotency !== undefined
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

  // Sorted using directives (SA1210) — merge extra usings from the gate.
  const extraUsings = gate?.extraUsings ?? [];
  const usings = buildUsings(
    input.registrationNamespace,
    input.delegationTargetNamespace,
    input.dtoNamespace,
    input.delegationTarget.kind,
    extraUsings,
  );
  for (const u of usings)
    lines.push(`using ${u};`);
  lines.push("");

  // Marker records (emitted inline, before the registration class).
  if (input.rateTier !== undefined || input.csrf !== undefined) {
    lines.push(...buildMarkerRecords(input.rateTier, input.csrf));
    lines.push("");
  }

  // Registration class.
  lines.push(`/// <summary>Generated REST route registration for the <c>${pascalOp}</c> operation.</summary>`);
  lines.push(`public static class ${pascalOp}RouteRegistration`);
  lines.push("{");
  lines.push("    extension(IEndpointRouteBuilder endpoints)");
  lines.push("    {");

  const xmlDocTarget = input.delegationTarget.kind === "facade"
    ? `<see cref="${input.delegationTarget.typeName}"/>`
    : `<see cref="${input.delegationTarget.typeName}"/>`;

  lines.push(`        /// <summary>Maps <c>${input.verb.toUpperCase()} ${input.routePath}</c>, delegating to ${xmlDocTarget}.</summary>`);
  lines.push(`        /// <remarks>Audience is enforced service-wide via <c>AuthOptions.Audience</c> — no per-route audience fluent (§9.2).</remarks>`);
  lines.push(`        public IEndpointConventionBuilder Map${pascalOp}Route()`);
  lines.push("        {");

  // Route delegate.
  const targetParam = input.delegationTarget.kind === "facade"
    ? `${input.delegationTarget.typeName} facade`
    : `${input.delegationTarget.typeName} handler`;
  const callTarget = input.delegationTarget.kind === "facade" ? "facade" : "handler";

  // GET and DELETE do not carry a request body. ASP.NET Core Minimal APIs infer
  // body binding for any complex type, which fails at runtime for body-less verbs.
  // [AsParameters] tells the framework to bind the DTO from query-string / route
  // segments instead.
  const inputParam = (input.verb === "get" || input.verb === "delete")
    ? `[AsParameters] ${input.inputTypeName} input`
    : `${input.inputTypeName} input`;

  // When the gate is present, inject the store parameter into the delegate signature.
  const storeParam = gate !== undefined ? `, ${gate.storeParam}` : "";

  lines.push(`            var builder = endpoints.${mapMethod}(`);
  lines.push(`                "${input.routePath}",`);
  lines.push(`                static async (${inputParam}, ${targetParam}${storeParam}, HttpContext http, CancellationToken ct) =>`);
  lines.push("                {");

  // Pre-delegate gate lines (key resolution + replay check). Placed at the TOP
  // of the delegate body, before the façade/handler call (§9.4 — validate / check
  // before delegating; the gate is a mandatory pre-condition).
  if (gate !== undefined) {
    for (const l of gate.preDelegateLines)
      lines.push(l);
    lines.push("");
  }

  lines.push(`                    var result = await ${callTarget}.${input.delegationTarget.methodName}(input, ct).ConfigureAwait(false);`);

  // Post-delegate gate lines (store the outcome with TTL). Placed AFTER the
  // delegation call and BEFORE the MAP-ii success check so the stored result
  // mirrors the final outcome (success or failure).
  if (gate !== undefined) {
    for (const l of gate.postDelegateLines)
      lines.push(l);
    lines.push("");
  }

  lines.push("                    if (result.Success)");
  lines.push("                        return Results.Ok(result.Data);");
  lines.push("                    var pd = result.ToProblemDetails(http);");
  lines.push("                    return Results.Json(pd, statusCode: pd.Status ?? 500, contentType: \"application/problem+json\");");
  lines.push("                });");
  lines.push("");

  // Auth enforcement.
  lines.push(...buildAuthLines(input.scopePolicy));

  // Metadata markers.
  if (input.rateTier !== undefined)
    lines.push(`            builder.WithMetadata(new D2GeneratedRateLimitTier("${input.rateTier}"));`);
  if (input.csrf !== undefined)
    lines.push(`            builder.WithMetadata(new D2GeneratedCsrfPosture("${input.csrf}"));`);

  lines.push("            return builder;");
  lines.push("        }");
  lines.push("    }");
  lines.push("}");
  lines.push("");

  return {
    fileName: `${pascalOp}RouteRegistration.g.cs`,
    content: lines.join("\n"),
  };
}

/**
 * Emit the D2GeneratedRoutePolicyMarkers.g.cs file containing the small
 * marker records for rate-limit tier and CSRF posture.
 *
 * Pure function — no I/O. Returns an {@link EmittedFile}.
 *
 * These records carry no enforcement logic — they are faithful seam markers
 * the future Edge rate-limit and CSRF middleware will read from endpoint
 * metadata via GetMetadata<T>(). The unbuilt consumer is ledgered in
 * VALIDATION.md; replace-trigger is when the Edge middleware lands.
 */
export function emitRoutePolicyMarkers(
  registrationNamespace: string,
  sourceSpec: string,
): EmittedFile {
  if (registrationNamespace.length === 0)
    throw new Error("emitRoutePolicyMarkers: registrationNamespace must not be empty");

  const banner = buildBanner(sourceSpec);
  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${registrationNamespace};`);
  lines.push("");
  lines.push("/// <summary>");
  lines.push("/// Faithful seam marker for a generated route's rate-limit tier declaration.");
  lines.push("/// Future Edge rate-limit middleware reads this from endpoint metadata.");
  lines.push("/// No enforcement logic is present in this record.");
  lines.push("/// </summary>");
  lines.push("public sealed record D2GeneratedRateLimitTier(string Tier);");
  lines.push("");
  lines.push("/// <summary>");
  lines.push("/// Faithful seam marker for a generated route's CSRF posture declaration.");
  lines.push("/// Future Edge CSRF middleware reads this from endpoint metadata.");
  lines.push("/// No enforcement logic is present in this record.");
  lines.push("/// </summary>");
  lines.push("public sealed record D2GeneratedCsrfPosture(string Posture);");
  lines.push("");

  return {
    fileName: "D2GeneratedRoutePolicyMarkers.g.cs",
    content: lines.join("\n"),
  };
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

/** Map an HTTP verb string to the Minimal-API Map* method name. */
export function verbToMapMethod(verb: HttpVerb): string {
  switch (verb) {
    case "get":    return "MapGet";
    case "post":   return "MapPost";
    case "put":    return "MapPut";
    case "delete": return "MapDelete";
    case "patch":  return "MapPatch";
  }
}

/**
 * Build sorted using directives for the route registration file.
 *
 * The usings always include the D2 auth http + result namespaces and the
 * ASP.NET Core routing/http namespaces. The delegation target namespace and
 * DTO namespace are added only when they differ from the registration namespace.
 * Extra usings (e.g. from the idempotency gate) are merged and de-duplicated.
 */
function buildUsings(
  registrationNamespace: string,
  delegationTargetNamespace: string,
  dtoNamespace: string,
  delegationKind: "facade" | "handler",
  extraUsings: readonly string[] = [],
): readonly string[] {
  void delegationKind; // kind drives which namespace we add — both go in usings
  const set = new Set<string>([
    "D2.Shared.Auth.Http.Endpoints",
    "D2.Shared.Auth.Http.ProblemDetails",
    "D2.Shared.Result",
    "Microsoft.AspNetCore.Builder",
    "Microsoft.AspNetCore.Http",
    "Microsoft.AspNetCore.Routing",
    ...extraUsings,
  ]);

  if (delegationTargetNamespace !== registrationNamespace)
    set.add(delegationTargetNamespace);
  if (dtoNamespace !== registrationNamespace && dtoNamespace !== delegationTargetNamespace)
    set.add(dtoNamespace);

  return [...set].sort();
}

/** Build the auth-enforcement lines for the route builder. */
function buildAuthLines(policy: ScopePolicy): string[] {
  if (policy.kind === "any") {
    const first = policy.scopes[0]!;
    const rest = policy.scopes.slice(1);
    const restArgs = rest.length > 0 ? `, ${rest.map((s) => `"${s}"`).join(", ")}` : "";
    return [`            builder.RequireAnyScope("${first}"${restArgs});`];
  }
  if (policy.kind === "all") {
    const first = policy.scopes[0]!;
    const rest = policy.scopes.slice(1);
    const restArgs = rest.length > 0 ? `, ${rest.map((s) => `"${s}"`).join(", ")}` : "";
    return [`            builder.RequireAllScopes("${first}"${restArgs});`];
  }
  if (policy.kind === "harmless")
    return ["            builder.MarkAsD2HarmlessEndpoint();"];
  // "none" — caller must have raised D2TSP004 and not reached here.
  return [];
}

/** Build inline marker record declarations for rate-tier and/or CSRF. */
function buildMarkerRecords(
  rateTier: string | undefined,
  csrf: string | undefined,
): string[] {
  const lines: string[] = [];
  if (rateTier !== undefined) {
    lines.push("/// <summary>Faithful seam marker: rate-limit tier declaration for this route.</summary>");
    lines.push("public sealed record D2GeneratedRateLimitTier(string Tier);");
  }
  if (csrf !== undefined) {
    if (rateTier !== undefined) lines.push("");
    lines.push("/// <summary>Faithful seam marker: CSRF posture declaration for this route.</summary>");
    lines.push("public sealed record D2GeneratedCsrfPosture(string Posture);");
  }
  return lines;
}
