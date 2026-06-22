// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// TypeScript browser REST client emitter. Emits, per @d2ServedBy module, one
// `<module>-rest-client.g.ts` carrying:
//
//   - a `<Module>RestClient` interface listing the module's @route ops with the
//     per-call signature `(input, opts?) => Promise<D2Result<<Op>Output>>`.
//   - a `<module>RestClient` const implementing it, each method delegating to the
//     REAL browser substrate (`apiCall` for scoped ops / `apiCallAnon` for
//     harmless-only ops) from `$lib/client/rest/gateway-client.js` — the substrate
//     that owns auth (JWT, retry-once-on-401), the locale / fingerprint headers,
//     `fetch`, and ProblemDetails / envelope → `D2Result` mapping. The emitted
//     client is a thin, typed forwarder (it emits NO raw `fetch`).
//
// Verb handling:
//   - POST / PUT / PATCH → the input DTO rides as the JSON `body`.
//   - GET / DELETE       → the input fields ride as a query string appended to
//     the route path (no body); the substrate's `apiCall` takes a bare path.
//   - A streaming / unsupported verb is rejected at collection time (the emitter
//     fails loud with a D2TSP* diagnostic — handled in emitter.ts, not here).
//
// Idempotency: a `@d2Idempotent("header", …)` op threads `opts.idempotencyKey`
// into the substrate's `idempotencyKey`; a `derived` keySource is server-computed
// (no client header), so the emitted method takes no idempotency key.
//
// Why `// @ts-nocheck` + `/* eslint-disable */`: the `$lib/client/rest/gateway-client.js`
// import + the emitted DTO imports resolve only inside the SvelteKit BFF (the
// `$lib` alias is SvelteKit-only). The emitted file is plain runtime JS; the
// byte-gate pins the bytes and the behavioral test drives the emitted const
// against a faithful `apiCall` double. `**/*.g.ts` is `.prettierignore`d → the
// emitter owns the formatting.
//
// TS conventions: camelCase fns, PascalCase types, `T | undefined` (never
// `T | null`), `D2Result` semantic factories, American English. No
// phase / deliverable / audit-round identifiers in emitted code or source.

import { buildBanner } from "./banner.js";
import type { FieldInfo } from "./model-walk.js";
import type { EmittedTsFile } from "./ts-dto-emitter.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** Supported REST verbs for the emitted browser client. */
export type RestVerb = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

/** Auth posture driving `apiCall` (scoped) vs `apiCallAnon` (harmless). */
export type RestAuthIntent = "scoped" | "harmless";

/** Client-side idempotency-key source: header (caller-supplied) or derived (server, no client key). */
export type RestIdempotencyKeySource = "header" | "derived" | "none";

/**
 * One @route operation collected for a module's TS browser REST client.
 */
export interface TsRestClientOp {
  /** lowerCamelCase op name (e.g. "sign"). */
  readonly opName: string;
  /** Resolved route path (e.g. "/internal/v1/kc/sign"). */
  readonly routePath: string;
  /** Uppercase HTTP verb. */
  readonly verb: RestVerb;
  /** Auth posture — scoped (→ apiCall) or harmless (→ apiCallAnon). */
  readonly authIntent: RestAuthIntent;
  /** Source spec path for the banner. */
  readonly sourceSpec: string;
  /** Request DTO type name (e.g. "SignInput"). */
  readonly requestModelName: string;
  /** Fields of the request DTO (for GET/DELETE query-string binding). */
  readonly requestFields: readonly FieldInfo[];
  /** Response DTO type name (e.g. "SignOutput"). */
  readonly responseModelName: string;
  /** Idempotency-key source — "header" threads a client key; "derived"/"none" do not. */
  readonly idempotencyKeySource: RestIdempotencyKeySource;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the per-module TS browser REST client file.
 *
 * Pure function — no I/O. Returns a single-element array (the one
 * `<module>-rest-client.g.ts`), or an empty array when `ops` is empty.
 *
 * @param moduleName - The @d2ServedBy module name in PascalCase (e.g.
 *                     "KeyCustodian"). Drives the interface / const names.
 * @param ops        - All @route ops for this module, in encounter order.
 * @returns Exactly one EmittedTsFile, or an empty array when ops is empty.
 */
export function emitTsRestClient(
  moduleName: string,
  ops: readonly TsRestClientOp[],
): EmittedTsFile[] {
  if (moduleName.length === 0)
    throw new Error("emitTsRestClient: moduleName must not be empty");
  if (ops.length === 0) return [];

  const sourceSpec = ops[0]!.sourceSpec;
  const banner = buildBanner(sourceSpec);
  const interfaceName = `${moduleName}RestClient`;
  const constName = `${lowerFirst(moduleName)}RestClient`;

  const needsAuth = ops.some((op) => op.authIntent === "scoped");
  const needsAnon = ops.some((op) => op.authIntent === "harmless");

  const lines: string[] = [];

  lines.push(banner.trimEnd());
  lines.push("");
  lines.push("/* eslint-disable */");
  lines.push("// @ts-nocheck");
  lines.push(
    "// Generated browser REST client. Each method delegates to the real $lib browser substrate",
  );
  lines.push(
    "// (apiCall / apiCallAnon → fetch + auth + ProblemDetails-to-D2Result). The $lib import + the",
  );
  lines.push(
    "// DTO imports resolve only inside the SvelteKit BFF — the emitted file is plain runtime",
  );
  lines.push(
    "// JS; the byte-gate pins the bytes and the behavioral test drives it against a faithful double.",
  );
  lines.push("");

  // ---- imports — the real browser substrate ----
  const substrateImports: string[] = [];
  if (needsAuth) substrateImports.push("apiCall");
  if (needsAnon) substrateImports.push("apiCallAnon");
  lines.push(
    `import { ${substrateImports.join(", ")} } from "$lib/client/rest/gateway-client.js";`,
  );
  lines.push('import type { D2Result } from "@d2/result";');
  lines.push("");

  // Module-relative DTO imports the BFF consumer resolves. Deduped by type
  // name; the import FILE is derived from the type name (the DTO emitter names a
  // type <PascalOp>Input/Output in <kebab-op>-dto.g.ts), so a model shared across
  // ops resolves to a single import (no redeclaration).
  lines.push(
    "// Emitted DTO types — paths resolve in the SvelteKit BFF consumer.",
  );
  for (const imp of collectDtoTypeImports(ops)) lines.push(imp);
  lines.push("");

  // ---- per-call options ----
  emitCallOptions(lines, ops);

  // ---- the interface ----
  lines.push(
    `/** Generated browser REST client interface for the ${moduleName} module. */`,
  );
  lines.push(`export interface ${interfaceName} {`);
  for (const op of ops) {
    const optsType = opNeedsIdempotencyKey(op)
      ? "RestCallOptionsWithIdempotency"
      : "RestCallOptions";
    lines.push(
      `  ${op.opName}(input: ${op.requestModelName}, opts?: ${optsType}): Promise<D2Result<${op.responseModelName}>>;`,
    );
  }
  lines.push("}");
  lines.push("");

  // ---- the const impl ----
  lines.push(
    `/** Generated browser REST client for the ${moduleName} module. */`,
  );
  lines.push(`export const ${constName}: ${interfaceName} = {`);
  for (let i = 0; i < ops.length; i++)
    emitOpMethod(lines, ops[i]!, i === ops.length - 1);
  lines.push("};");
  lines.push("");

  // ---- the query-string helper (only when a GET/DELETE op exists) ----
  if (ops.some((op) => verbBindsQuery(op.verb))) emitQueryHelper(lines);

  // End on a single trailing newline (the byte-gate convention). The body always
  // ends with a trailing blank (the post-const blank or the query helper), so the
  // trim always fires; the no-trim arm is defensive (a non-empty tail never occurs).
  /* v8 ignore start — defensive: the body always leaves a trailing blank to trim (the no-trim arm is unreachable) */
  if (lines[lines.length - 1] === "") lines.pop();
  /* v8 ignore stop */
  lines.push("");

  return [
    {
      fileName: `${kebab(moduleName)}-rest-client.g.ts`,
      content: lines.join("\n"),
    },
  ];
}

// ---------------------------------------------------------------------------
// Private — predicates over the op shape
// ---------------------------------------------------------------------------

function opNeedsIdempotencyKey(op: TsRestClientOp): boolean {
  return op.idempotencyKeySource === "header";
}

/**
 * Distinct `import type { <Type>, … } from "./<file>-dto.js";` lines for the DTO
 * request/response types across all ops, grouped by the dto FILE the type lives
 * in (derived from the type name). A model shared across ops resolves to ONE
 * import — no redeclaration.
 */
function collectDtoTypeImports(ops: readonly TsRestClientOp[]): string[] {
  const byFile = new Map<string, string[]>();
  const add = (typeName: string): void => {
    const file = dtoFileForType(typeName);
    const names = byFile.get(file) ?? [];
    if (!names.includes(typeName)) names.push(typeName);
    byFile.set(file, names);
  };
  for (const op of ops) {
    add(op.requestModelName);
    add(op.responseModelName);
  }
  return [...byFile.entries()].map(
    ([file, names]) =>
      `import type { ${names.join(", ")} } from "./${file}.js";`,
  );
}

/**
 * Derive the dto file base name (`<kebab-op>-dto`) for a DTO type name. The DTO
 * emitter names a type `<PascalOp>Input` / `<PascalOp>Output` co-located in
 * `<kebab-op>-dto.g.ts`; strip the Input/Output suffix and kebab the remainder.
 */
function dtoFileForType(typeName: string): string {
  const base = typeName.endsWith("Output")
    ? typeName.slice(0, -"Output".length)
    : typeName.endsWith("Input")
      ? typeName.slice(0, -"Input".length)
      : /* v8 ignore next — defensive: a request/response DTO type always carries the Input/Output suffix */
        typeName;
  return `${kebab(lowerFirst(base))}-dto`;
}

function verbBindsQuery(verb: RestVerb): boolean {
  return verb === "GET" || verb === "DELETE";
}

// ---------------------------------------------------------------------------
// Private — fixed scaffolding (call options + query helper)
// ---------------------------------------------------------------------------

function emitCallOptions(
  lines: string[],
  ops: readonly TsRestClientOp[],
): void {
  lines.push("/** Per-call options for a generated REST client method. */");
  lines.push("export interface RestCallOptions {");
  lines.push("  /** Cooperative-cancellation signal forwarded to fetch. */");
  lines.push("  readonly signal?: AbortSignal;");
  lines.push("  /** Per-call timeout (ms) forwarded to the substrate. */");
  lines.push("  readonly timeout?: number;");
  lines.push("}");
  lines.push("");

  // Only emit the idempotency-extended options when at least one op needs it.
  if (ops.some((op) => opNeedsIdempotencyKey(op))) {
    lines.push(
      "/** Per-call options for an idempotent (header keySource) REST client method. */",
    );
    lines.push(
      "export interface RestCallOptionsWithIdempotency extends RestCallOptions {",
    );
    lines.push(
      "  /** Idempotency-Key header value (caller-supplied for the header keySource). */",
    );
    lines.push("  readonly idempotencyKey?: string;");
    lines.push("}");
    lines.push("");
  }
}

function emitQueryHelper(lines: string[]): void {
  lines.push(
    "/** Append the defined input fields as a query string to a route path (GET/DELETE binding). */",
  );
  lines.push(
    "function withQuery(path: string, input: Record<string, unknown>): string {",
  );
  lines.push("  const params = new URLSearchParams();");
  lines.push("  for (const [key, value] of Object.entries(input))");
  lines.push("    if (value !== undefined)");
  lines.push("      params.set(key, String(value));");
  lines.push("  const qs = params.toString();");
  lines.push("  return qs.length > 0 ? `${path}?${qs}` : path;");
  lines.push("}");
  lines.push("");
}

// ---------------------------------------------------------------------------
// Private — per-op method body
// ---------------------------------------------------------------------------

function emitOpMethod(
  lines: string[],
  op: TsRestClientOp,
  isLast: boolean,
): void {
  const tail = isLast ? "" : ",";
  const fn = op.authIntent === "scoped" ? "apiCall" : "apiCallAnon";
  const bindsQuery = verbBindsQuery(op.verb);
  const pathExpr = bindsQuery
    ? `withQuery("${op.routePath}", input)`
    : `"${op.routePath}"`;

  lines.push(`  ${op.opName}(input, opts) {`);
  lines.push(`    return ${fn}<${op.responseModelName}>(${pathExpr}, {`);
  lines.push(`      method: "${op.verb}",`);

  // Body rides only for body verbs; GET/DELETE bind the input into the path.
  if (!bindsQuery) lines.push("      body: input,");

  if (opNeedsIdempotencyKey(op))
    lines.push("      idempotencyKey: opts?.idempotencyKey,");

  lines.push("      signal: opts?.signal,");
  lines.push("      timeout: opts?.timeout,");
  lines.push(`    });`);
  lines.push(`  }${tail}`);
}

// ---------------------------------------------------------------------------
// Private utility
// ---------------------------------------------------------------------------

function lowerFirst(s: string): string {
  // Defensive empty-string branch — every caller passes a non-empty module name.
  /* v8 ignore start — defensive: module names are never empty */
  return s.length === 0 ? s : s[0]!.toLowerCase() + s.slice(1);
  /* v8 ignore stop */
}

/**
 * Convert a lowerCamelCase op/module name to kebab-case for the file / import
 * name. Linear with bounded input (identifier strings) — Bucket 2 per the
 * regex-redos discipline; no matchTimeout needed.
 */
function kebab(s: string): string {
  return s.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
}
