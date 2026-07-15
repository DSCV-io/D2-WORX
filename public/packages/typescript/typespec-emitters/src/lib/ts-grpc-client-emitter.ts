// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// TypeScript SSR gRPC client emitter — the TS twin of the .NET
// grpc-client-emitter.ts. Emits, per @d2ServedBy module, one
// `<module>-grpc-client.g.ts` carrying:
//
//   - a `<Module>GrpcClient` interface listing the module's @d2GrpcMethod ops
//     with the per-call signature `(input, opts?) => Promise<D2Result<<Op>Output>>`.
//   - a `create<Module>GrpcClient(stub)` factory injecting the REAL ts-proto
//     grpc-js service stub (the buf/ts-proto output — the TS twin of Grpc.Tools).
//   - per-op: a DTO→proto request mapper + a proto-data→DTO response mapper +
//     the call body delegating to the REAL `@d2/grpc-client` seam
//     (`unaryCall` / `handleGrpcCall` / `d2ResultFromProto` / `isTransientGrpcError`).
//   - for a @d2Resilience op: the predicate retry-arm (the TS analog of the .NET
//     sentinel arm) folding the emitted TS `<op>RetryWhen` / `<op>FailWhen`
//     twin into the retry decision over the EXISTING `@d2/resilience`
//     `ResilientPipeline` — NO new resilience-lib export. The module-local
//     `D2GeneratedBusinessRetrySignal` (the TS twin of the C# sentinel) carries
//     the captured business `D2Result` so the budget-exhaust restore is verbatim.
//
// Body discipline (mirrors the .NET captured-envelope rule; the seam owns all
// mapping):
//   - The business result is reconstructed with the seam's `d2ResultFromProto`
//     (the exact mapper `handleGrpcCall` delegates to) so a server business
//     failure rides the gRPC-OK envelope verbatim.
//   - A transport fault throws a grpc-js `ServiceError` out of `unaryCall`; the
//     seam's `isTransientGrpcError` classifies it for the pipeline, and the
//     seam's `handleGrpcCall` maps the TERMINAL transport fault to a
//     TK-constant-messaged D2Result (NEVER leaks `err.details` / `err.message`).
//   - A RESPONSE enum carries a fail-loud inbound parse (the TS twin of the C#
//     strict `Parse<Enum>Wire`): an unknown wire value → `validationFailed`.
//   - The retry sentinel rides the pipeline's `isTransient` arm (zero lib change),
//     exactly like the C# sentinel rides `RetryOptions.IsTransient`.
//
// Why `// @ts-nocheck` + `/* eslint-disable */` on the emitted .g.ts:
//   The client references the proto stub + proto message types + the emitted DTO
//   types + (for a predicate op) the emitted TS predicate twin — module-relative
//   imports that wire up only in a real consumer (the BFF SSR composition root).
//   The emitted file is therefore plain runtime JS (annotations erased);
//   the byte-gate pins the exact bytes and the behavioral test reconstructs the
//   factory from the emitted text, driving it against the REAL `@d2/grpc-client`
//   seam + the REAL fixture ts-proto types (buf/ts-proto output) + a fake stub.
//   `**/*.g.ts` is `.prettierignore`d → the emitter owns the formatting.
//
// TS conventions: camelCase fns, PascalCase types, `T | undefined` (never
// `T | null`), `D2Result` semantic factories, American English. No
// phase / deliverable / audit-round identifiers in emitted code or source.

import { buildBanner } from "./banner.js";
import { toKebab, toPascal } from "./name-transforms.js";
import type { FieldInfo } from "./model-walk.js";
import type { EmittedTsFile } from "./ts-dto-emitter.js";
import type { PredicateNode } from "@d2/typespec-decorators";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/**
 * One @d2GrpcMethod operation collected for a module's TS SSR gRPC client.
 * Mirrors the shape the .NET grpc-client emitter receives, narrowed to what the
 * TS client needs (the TS DTO↔proto field map is field-name-identical for
 * scalars/arrays/bytes; only response enums need an inbound parse).
 */
export interface TsGrpcClientOp {
  /** lowerCamelCase op name (e.g. "placeOrder"). */
  readonly opName: string;
  /** gRPC service name from @d2GrpcMethod (e.g. "PredicateFixturesOrders"). */
  readonly grpcService: string;
  /** gRPC method name from @d2GrpcMethod (e.g. "PlaceOrder"). */
  readonly grpcMethod: string;
  /** Source spec path for the banner. */
  readonly sourceSpec: string;
  /** Request DTO type name (e.g. "PlaceOrderInput"). */
  readonly requestModelName: string;
  /** Fields of the request DTO (for the DTO→proto request mapper). */
  readonly requestFields: readonly FieldInfo[];
  /** Response DTO type name (e.g. "PlaceOrderOutput"). */
  readonly responseModelName: string;
  /** Fields of the response DTO (for the proto-data→DTO response mapper). */
  readonly responseFields: readonly FieldInfo[];
  /**
   * The op's `@d2Concern` segment (e.g. "Signing", "CaCertificate"), present only
   * when this client is mirrored into a concern-subfoldered consumable package (a
   * `ts-client-output-dirs` target — the module's DTOs are then written to
   * `<concern-kebab>/` and the gRPC client to `facade/`). Present ⇒ the emitted DTO
   * import specifiers are concern-relative (`../<concern-kebab>/<file>.js`, from
   * `facade/`); absent ⇒ the flat co-located form (`./<file>.js`) — the standard
   * emitter-output layout used by fixtures and non-mirrored modules.
   */
  readonly concern?: string;
  /**
   * Parsed @d2Resilience `retryWhen` predicate AST, when the op carries one.
   * Present ⇒ the client folds in the predicate retry-arm (throw the sentinel
   * when `retryWhen && !failWhen`) over a ResilientPipeline. Absent ⇒ the op
   * emits the plain forwarding body (no pipeline import, no sentinel).
   */
  readonly retryWhenAst?: PredicateNode;
  /** Parsed @d2Resilience `failWhen` predicate AST, when the op carries one. */
  readonly failWhenAst?: PredicateNode;
  /**
   * The retry budget from @d2Resilience("retry(N)") (max total attempts incl.
   * the first). Threaded as the pipeline `maxAttempts`. Defaults to 3 when a
   * predicate is present but no budget was parsed (defensive — a predicate-bearing
   * op always carries one).
   */
  readonly retryBudget?: number;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the per-module TS SSR gRPC client file.
 *
 * Pure function — no I/O. Returns a single-element array (the one
 * `<module>-grpc-client.g.ts`), or an empty array when `ops` is empty.
 *
 * @param moduleName - The @d2ServedBy module name in PascalCase (e.g.
 *                     "PredicateFixtures"). Drives the interface / factory names.
 * @param ops        - All @d2GrpcMethod ops for this module, in encounter order
 *                     (determines method order in the interface + factory).
 * @returns Exactly one EmittedTsFile, or an empty array when ops is empty.
 */
export function emitTsGrpcClient(
  moduleName: string,
  ops: readonly TsGrpcClientOp[],
): EmittedTsFile[] {
  if (moduleName.length === 0)
    throw new Error("emitTsGrpcClient: moduleName must not be empty");
  if (ops.length === 0) return [];

  const sourceSpec = ops[0]!.sourceSpec;
  const banner = buildBanner(sourceSpec);
  const interfaceName = `${moduleName}GrpcClient`;

  const anyRetryArm = ops.some((op) => opHasRetryArm(op));
  const anyResponseEnum = ops.some((op) => opHasResponseEnum(op));

  const lines: string[] = [];

  lines.push(banner.trimEnd());
  lines.push("");
  lines.push("/* eslint-disable */");
  lines.push("// @ts-nocheck");
  lines.push(
    "// Generated SSR gRPC client (the TS twin of the .NET <Module>GrpcClient). Delegates to the",
  );
  lines.push(
    "// real @d2/grpc-client seam over the real ts-proto grpc-js stub. References module-relative",
  );
  lines.push(
    "// imports (proto stub + messages + DTOs" +
      (anyRetryArm ? " + the @d2Resilience predicate twin" : "") +
      ") that wire up only in the BFF SSR",
  );
  lines.push(
    "// composition root — the emitted file is plain runtime JS; the byte-gate pins the bytes and",
  );
  lines.push(
    "// the behavioral test drives the emitted factory against the real seam + real ts-proto types.",
  );
  lines.push("");

  // ---- imports — the seam (real @d2/grpc-client, a shipped shared lib) ----
  const grpcClientImports = ["d2ResultFromProto", "handleGrpcCall"];
  if (anyRetryArm) grpcClientImports.push("isTransientGrpcError");
  grpcClientImports.push("unaryCall");
  lines.push(
    `import { ${grpcClientImports.join(", ")} } from "@d2/grpc-client";`,
  );

  if (anyResponseEnum) {
    lines.push(
      'import { type D2Result, ok, validationFailed } from "@d2/result";',
    );
  } else {
    lines.push('import type { D2Result } from "@d2/result";');
  }

  if (anyRetryArm) {
    lines.push(
      'import { ResilientPipeline, ResilientPipelineBuilder } from "@d2/resilience";',
    );
  }
  lines.push("");

  // Module-relative imports the BFF SSR consumer resolves: DTO types, the
  // ts-proto message types referenced by the mappers (none needed at type level —
  // the mappers take/return `unknown`), and (predicate ops) the result-predicate twin.
  lines.push(
    "// Emitted DTO types + (predicate ops) the result-predicate twin. Paths resolve",
  );
  lines.push("// in the BFF SSR consumer; @ts-nocheck erases them here.");
  // Dedup DTO type imports by name; the import FILE is derived from the type name
  // (the DTO emitter names a type <PascalOp>Input/Output in <kebab-op>-dto.g.ts),
  // so a model shared across ops resolves to a single import (no redeclaration).
  for (const imp of collectDtoTypeImports(ops)) lines.push(imp);
  // The retry-arm imports the predicate twin: retryWhen always (it drives the
  // sentinel), failWhen only when present (the "failWhen WINS" guard). A
  // failWhen-only op has no retry-arm → no predicate import (failWhen alone is
  // inert at the client).
  for (const op of ops) {
    if (!opHasRetryArm(op)) continue;
    const predImports = [`${op.opName}RetryWhen`];
    if (op.failWhenAst !== undefined) predImports.push(`${op.opName}FailWhen`);
    lines.push(
      `import { ${predImports.join(", ")} } from "./${toKebab(op.opName)}-resilience-predicates.js";`,
    );
  }
  lines.push("");

  // Enum const-objects referenced by the response mappers' membership parse.
  // Emitted as module-relative imports from the DTO file (deduped by name).
  const enumImports = collectResponseEnumImports(ops);
  if (enumImports.length > 0) {
    for (const imp of enumImports) lines.push(imp);
    lines.push("");
  }

  // ---- per-call options + (predicate) the sentinel + default pipelines ----
  emitCallOptions(lines, anyRetryArm);

  if (anyRetryArm) emitRetrySentinel(lines);

  for (const op of ops) if (opHasRetryArm(op)) emitDefaultPipeline(lines, op);

  // ---- the interface ----
  lines.push(
    `/** Generated SSR gRPC client interface for the ${moduleName} module. */`,
  );
  lines.push(`export interface ${interfaceName} {`);
  for (const op of ops) {
    lines.push(
      `  ${op.opName}(input: ${op.requestModelName}, opts?: GrpcCallOptions): Promise<D2Result<${op.responseModelName}>>;`,
    );
  }
  lines.push("}");
  lines.push("");

  // ---- the factory ----
  lines.push(
    `/** Build the ${moduleName} gRPC client over a ts-proto grpc-js service stub. */`,
  );
  lines.push(
    `export function create${moduleName}GrpcClient(stub: unknown): ${interfaceName} {`,
  );
  lines.push("  return {");
  for (let i = 0; i < ops.length; i++)
    emitOpMethod(lines, ops[i]!, i === ops.length - 1);
  lines.push("  };");
  lines.push("}");
  lines.push("");

  // ---- the mappers (response first, then request — discovery order) ----
  for (const op of ops) emitResponseMapper(lines, op);
  for (const op of ops) emitRequestMapper(lines, op);

  // End on a single trailing newline (the byte-gate convention). The last emitted
  // block is always a request mapper ending in a blank line, so the trim always
  // fires; the no-trim arm is defensive (a non-empty trailing line never occurs).
  /* v8 ignore start — defensive: a request mapper always leaves a trailing blank to trim (the no-trim arm is unreachable) */
  if (lines[lines.length - 1] === "") lines.pop();
  /* v8 ignore stop */
  lines.push("");

  return [
    {
      fileName: `${toKebab(moduleName)}-grpc-client.g.ts`,
      content: lines.join("\n"),
    },
  ];
}

// ---------------------------------------------------------------------------
// Private — predicates over the op shape
// ---------------------------------------------------------------------------

/**
 * True when the op folds in the business-predicate retry-arm. The arm is driven
 * SOLELY by `retryWhen` — it is the only predicate that changes the client's retry
 * decision (failWhen merely WINS when both are present). A `failWhen`-only op has
 * NO retry condition, so it carries no retry-arm (failWhen alone is inert at the
 * client — the server already produced the terminal result).
 */
function opHasRetryArm(op: TsGrpcClientOp): boolean {
  return op.retryWhenAst !== undefined;
}

function opHasResponseEnum(op: TsGrpcClientOp): boolean {
  return op.responseFields.some((f) => f.enumRef !== undefined);
}

/**
 * Distinct `import type { <Type>, … } from "<specifier>";` lines for the DTO
 * request/response types across all ops, grouped by the dto FILE the type lives
 * in. The file is derived from the type name (the DTO emitter names a type
 * <PascalOp>Input/Output in <kebab-op>-dto.g.ts → stripping the Input/Output
 * suffix recovers the owning op), so a model shared across ops (e.g. a single
 * SignInput used by two ops) resolves to ONE import — no redeclaration.
 *
 * The specifier is concern-relative (`../<concern-kebab>/<file>.js`, from the
 * `facade/`-homed client) when the op carries a concern (a mirrored consumable
 * package), else flat co-located (`./<file>.js`) for fixtures / non-mirrored
 * modules.
 */
function collectDtoTypeImports(ops: readonly TsGrpcClientOp[]): string[] {
  // file → ordered distinct type names declared in that file.
  const byFile = new Map<string, string[]>();
  // file → the concern folder its DTOs live in (undefined ⇒ flat co-located). The
  // first op that references a file fixes its concern — a DTO shared across ops of
  // one module shares one concern folder, matching the first-op-wins rule the DTO
  // mirror uses when writing the file, so folder and import always agree.
  const concernByFile = new Map<string, string | undefined>();
  const add = (typeName: string, concern: string | undefined): void => {
    const file = dtoFileForType(typeName);
    const names = byFile.get(file) ?? [];
    if (!names.includes(typeName)) names.push(typeName);
    byFile.set(file, names);
    if (!concernByFile.has(file)) concernByFile.set(file, concern);
  };
  for (const op of ops) {
    add(op.requestModelName, op.concern);
    add(op.responseModelName, op.concern);
  }
  return [...byFile.entries()].map(([file, names]) => {
    const concern = concernByFile.get(file);
    const specifier =
      concern !== undefined
        ? `../${toKebab(concern)}/${file}.js`
        : `./${file}.js`;

    return `import type { ${names.join(", ")} } from "${specifier}";`;
  });
}

/**
 * Derive the dto file base name (`<kebab-op>-dto`) for a DTO type name. The DTO
 * emitter names a type `<PascalOp>Input` / `<PascalOp>Output` co-located in
 * `<kebab-op>-dto.g.ts`; strip the Input/Output suffix and kebab the remainder.
 */
function dtoFileForType(typeName: string): string {
  /* v8 ignore start — defensive: a request/response DTO type always ends in Input or Output, so this guard never fires */
  if (!typeName.endsWith("Output") && !typeName.endsWith("Input"))
    return `${toKebab(lowerFirst(typeName))}-dto`;
  /* v8 ignore stop */
  const base = typeName.endsWith("Output")
    ? typeName.slice(0, -"Output".length)
    : typeName.slice(0, -"Input".length);
  return `${toKebab(lowerFirst(base))}-dto`;
}

/**
 * Distinct `import { <Enum> } from "<specifier>";` lines for response enums. The
 * specifier is concern-relative (`../<concern-kebab>/<op>-dto.js`) for a mirrored
 * consumable package, else flat co-located (`./<op>-dto.js`).
 */
function collectResponseEnumImports(ops: readonly TsGrpcClientOp[]): string[] {
  const seen = new Set<string>();
  const imports: string[] = [];
  for (const op of ops) {
    for (const f of op.responseFields) {
      if (f.enumRef === undefined || seen.has(f.enumRef.name)) continue;
      seen.add(f.enumRef.name);
      const dir =
        op.concern !== undefined ? `../${toKebab(op.concern)}/` : "./";
      imports.push(
        `import { ${f.enumRef.name} } from "${dir}${toKebab(op.opName)}-dto.js";`,
      );
    }
  }
  return imports;
}

// ---------------------------------------------------------------------------
// Private — fixed scaffolding (call options + sentinel + default pipelines)
// ---------------------------------------------------------------------------

function emitCallOptions(lines: string[], anyRetryArm: boolean): void {
  lines.push("/** Per-call options for a generated gRPC client method. */");
  lines.push("export interface GrpcCallOptions {");
  lines.push(
    "  /** Request deadline in milliseconds from now (gRPC CallOptions deadline). */",
  );
  lines.push("  readonly deadlineMs?: number;");
  lines.push(
    "  /** Trace identifier threaded into the reconstructed result for correlation. */",
  );
  lines.push("  readonly traceId?: string;");
  lines.push(
    "  /** Cooperative-cancellation signal threaded through the resilience pipeline. */",
  );
  lines.push("  readonly signal?: AbortSignal;");

  if (anyRetryArm) {
    lines.push(
      "  /** Resilience pipeline override; defaults to the per-op retry pipeline (transport + business-predicate retry). */",
    );
    lines.push("  readonly pipeline?: ResilientPipeline;");
  } else {
    lines.push(
      "  /** Resilience pipeline for transport-transient retry; the call runs direct when absent. */",
    );
    lines.push("  readonly pipeline?: {");
    lines.push(
      "    execute<T>(key: string, op: (signal?: AbortSignal) => Promise<T>, signal?: AbortSignal): Promise<T>;",
    );
    lines.push("  };");
  }
  lines.push("}");
  lines.push("");
}

function emitRetrySentinel(lines: string[]): void {
  lines.push("/**");
  lines.push(
    " * Generated retry sentinel for @d2Resilience retryWhen (the TS twin of the C#",
  );
  lines.push(
    " * D2GeneratedBusinessRetrySignal). The client closure throws it when a business result matches",
  );
  lines.push(
    " * retryWhen (and not failWhen); the pipeline's isTransient arm recognizes it so the EXISTING",
  );
  lines.push(
    " * ResilientPipeline retries against its budget — opting one named business condition into retry",
  );
  lines.push(
    " * with no resilience-lib change. Carries the captured business result for the budget-exhaust",
  );
  lines.push(" * restore. Never escapes the client; never logs the payload.");
  lines.push(" */");
  lines.push("class D2GeneratedBusinessRetrySignal extends Error {");
  lines.push("  constructor(readonly result: D2Result<unknown>) {");
  lines.push('    super("d2-generated-business-retry-signal");');
  lines.push('    this.name = "D2GeneratedBusinessRetrySignal";');
  lines.push("  }");
  lines.push("}");
  lines.push("");
}

/**
 * The default per-op retry pipeline — gRPC transport transients (the seam's
 * isTransientGrpcError) OR the business retry sentinel, up to the op's budget.
 */
function emitDefaultPipeline(lines: string[], op: TsGrpcClientOp): void {
  const budget = op.retryBudget ?? 3;
  lines.push(
    `// ${toPascal(op.opName)} retry pipeline — gRPC transport transients OR the business retry sentinel.`,
  );
  lines.push(
    `const ${op.opName}DefaultPipeline: ResilientPipeline = new ResilientPipelineBuilder()`,
  );
  lines.push("  .useRetries({");
  lines.push(`    maxAttempts: ${budget},`);
  lines.push(
    "    isTransient: (e) => e instanceof D2GeneratedBusinessRetrySignal || isTransientGrpcError(e),",
  );
  lines.push("  })");
  lines.push("  .build();");
  lines.push("");
}

// ---------------------------------------------------------------------------
// Private — per-op method body
//
// Two shapes:
//   - SIMPLE (no predicate, no response-enum): delegate straight to the seam's
//     handleGrpcCall; an injected opts.pipeline drives transport-transient retry.
//   - ORCHESTRATED (predicate OR response-enum): run the throwing unaryCall in a
//     closure (transport faults observable to isTransientGrpcError), reconstruct
//     via d2ResultFromProto, surface a response-enum parse failure, throw the
//     sentinel on a business-retry condition; the catch restores the captured
//     result on budget-exhaust and maps a terminal transport fault via the seam.
// ---------------------------------------------------------------------------

function emitOpMethod(
  lines: string[],
  op: TsGrpcClientOp,
  isLast: boolean,
): void {
  const tail = isLast ? "" : ",";
  if (opHasRetryArm(op) || opHasResponseEnum(op))
    emitOrchestratedMethod(lines, op, tail);
  else emitSimpleMethod(lines, op, tail);
}

function emitSimpleMethod(
  lines: string[],
  op: TsGrpcClientOp,
  tail: string,
): void {
  const lowerMethod = lowerFirst(op.grpcMethod);
  const respMapper = `to${op.responseModelName}`;
  const callExpr = `() => unaryCall(stub.${lowerMethod}.bind(stub), request, { deadlineMs: opts?.deadlineMs })`;
  const dataSel = `(r) => (r.data === undefined ? undefined : ${respMapper}(r.data))`;

  lines.push(`    async ${op.opName}(input, opts) {`);
  lines.push(`      const request = to${op.grpcMethod}Request(input);`);
  lines.push("      if (opts?.pipeline !== undefined) {");
  lines.push("        try {");
  lines.push(
    `          return await opts.pipeline.execute("${op.grpcService}/${op.grpcMethod}", async () => {`,
  );
  lines.push(
    `            const response = await unaryCall(stub.${lowerMethod}.bind(stub), request, { deadlineMs: opts?.deadlineMs });`,
  );
  lines.push(
    `            return d2ResultFromProto(response.result, response.data === undefined ? undefined : ${respMapper}(response.data)).withTraceId(opts?.traceId);`,
  );
  lines.push("          }, opts?.signal);");
  lines.push("        } catch (e) {");
  lines.push(`          return ${terminalFaultExpr()};`);
  lines.push("        }");
  lines.push("      }");
  lines.push("      return handleGrpcCall(");
  lines.push(`        ${callExpr},`);
  lines.push("        (r) => r.result,");
  lines.push(`        ${dataSel},`);
  lines.push("        opts?.traceId,");
  lines.push("      );");
  lines.push(`    }${tail}`);
}

function emitOrchestratedMethod(
  lines: string[],
  op: TsGrpcClientOp,
  tail: string,
): void {
  const lowerMethod = lowerFirst(op.grpcMethod);
  const hasRetryArm = opHasRetryArm(op);
  const hasResponseEnum = opHasResponseEnum(op);
  const respMapper = `to${op.responseModelName}`;

  lines.push(`    async ${op.opName}(input, opts) {`);
  lines.push(`      const request = to${op.grpcMethod}Request(input);`);

  // Resolve the pipeline. A predicate op has a default; a pure response-enum op
  // uses opts.pipeline (transport retry) or runs direct.
  if (hasRetryArm) {
    lines.push(
      `      const pipeline = opts?.pipeline ?? ${op.opName}DefaultPipeline;`,
    );
  } else {
    lines.push("      const pipeline = opts?.pipeline;");
  }

  // The closure: throwing call → reconstruct → (enum parse) → (predicate sentinel).
  lines.push(
    "      const run = async (): Promise<D2Result<" +
      op.responseModelName +
      ">> => {",
  );
  lines.push(
    `        const response = await unaryCall(stub.${lowerMethod}.bind(stub), request, { deadlineMs: opts?.deadlineMs });`,
  );

  if (hasResponseEnum) {
    // The response mapper returns a D2Result (ok or validationFailed on an
    // unknown enum wire value). A parse failure is the business result (the
    // server sent a value this client cannot map → client-side ValidationFailed).
    lines.push("        const dataResult = response.data === undefined");
    lines.push("          ? undefined");
    lines.push(`          : ${respMapper}(response.data);`);
    lines.push("        if (dataResult !== undefined && dataResult.failed)");
    lines.push(
      `          return dataResult.withTraceId(opts?.traceId) as D2Result<${op.responseModelName}>;`,
    );
    lines.push(
      `        const result = d2ResultFromProto(response.result, dataResult?.data).withTraceId(opts?.traceId);`,
    );
  } else {
    lines.push(
      `        const result = d2ResultFromProto(response.result, response.data === undefined ? undefined : ${respMapper}(response.data)).withTraceId(opts?.traceId);`,
    );
  }

  // The business-retry sentinel fires ONLY for a retryWhen condition (failWhen WINS
  // when both are present). A failWhen-only op carries no retry condition → no
  // sentinel throw (failWhen alone is purely terminal — nothing to retry).
  if (op.retryWhenAst !== undefined) {
    const retryGuard =
      op.failWhenAst !== undefined
        ? `${op.opName}RetryWhen(result) && !${op.opName}FailWhen(result)`
        : `${op.opName}RetryWhen(result)`;
    lines.push(`        if (${retryGuard})`);
    lines.push("          throw new D2GeneratedBusinessRetrySignal(result);");
  }

  lines.push("        return result;");
  lines.push("      };");

  // Drive the closure. With a pipeline: catch sentinel (restore) + terminal
  // transport fault (seam map). Without: catch only the terminal transport fault.
  if (hasRetryArm) {
    lines.push("      try {");
    lines.push(
      `        return await pipeline.execute("${op.grpcService}/${op.grpcMethod}", run, opts?.signal);`,
    );
    lines.push("      } catch (e) {");
    lines.push(
      "        // Budget exhausted on a business-retry condition → restore the captured result verbatim.",
    );
    lines.push("        if (e instanceof D2GeneratedBusinessRetrySignal)");
    lines.push(
      `          return e.result as D2Result<${op.responseModelName}>;`,
    );
    lines.push(
      "        // Terminal transport fault → map via the seam (never leaks err.details / err.message).",
    );
    lines.push(`        return ${terminalFaultExpr()};`);
    lines.push("      }");
  } else {
    lines.push("      try {");
    lines.push(
      `        return await (pipeline !== undefined ? pipeline.execute("${op.grpcService}/${op.grpcMethod}", run, opts?.signal) : run());`,
    );
    lines.push("      } catch (e) {");
    lines.push(
      "        // Terminal transport fault → map via the seam (never leaks err.details / err.message).",
    );
    lines.push(`        return ${terminalFaultExpr()};`);
    lines.push("      }");
  }

  lines.push(`    }${tail}`);
}

/** The terminal transport-fault mapping expression (delegates to the seam). */
function terminalFaultExpr(): string {
  // Re-run the captured transport error through the seam's handleGrpcCall so the
  // ServiceError → TK-constant D2Result mapping is the seam's, not re-implemented
  // here (never leaks err.details / err.message). The selectors are unreachable
  // (callFn rejects → the seam's catch fires first).
  return "handleGrpcCall(() => Promise.reject(e), () => undefined as never, () => undefined, opts?.traceId)";
}

// ---------------------------------------------------------------------------
// Private — mappers (DTO ↔ proto). TS field names are camelCase-identical to the
// proto field names (ts-proto camelCases proto snake_case), bytes is Uint8Array
// on both sides, and an enum is a wire string on both sides — so the outbound
// request map is a direct field copy and the inbound response map is a direct
// field copy EXCEPT a response enum needs a fail-loud membership parse.
// ---------------------------------------------------------------------------

function emitRequestMapper(lines: string[], op: TsGrpcClientOp): void {
  lines.push(
    `/** Map ${op.requestModelName} → the ${op.grpcMethod}Request proto message (field-name-identical). */`,
  );
  lines.push(
    `function to${op.grpcMethod}Request(input: ${op.requestModelName}): unknown {`,
  );
  if (op.requestFields.length === 0) {
    lines.push("  return {};");
  } else {
    lines.push("  return {");
    for (const f of op.requestFields) {
      // Field-name-identical: scalars/bytes/arrays/enums/nested models all copy
      // directly (enum DTO member value IS the wire string; bytes is Uint8Array
      // on both sides; a ts-proto message ≈ DTO interface, deep).
      lines.push(`    ${f.tsName}: input.${f.tsName},`);
    }
    lines.push("  };");
  }
  lines.push("}");
  lines.push("");
}

function emitResponseMapper(lines: string[], op: TsGrpcClientOp): void {
  const respMapper = `to${op.responseModelName}`;

  if (!opHasResponseEnum(op)) {
    lines.push(
      `/** Map the ${op.grpcMethod}Response data → ${op.responseModelName} (field-name-identical). */`,
    );
    lines.push(
      `function ${respMapper}(data: unknown): ${op.responseModelName} {`,
    );
    if (op.responseFields.length === 0) {
      lines.push(`  return {} as ${op.responseModelName};`);
    } else {
      lines.push("  return {");
      for (const f of op.responseFields)
        lines.push(`    ${f.tsName}: data.${f.tsName},`);
      lines.push(`  } as ${op.responseModelName};`);
    }
    lines.push("}");
    lines.push("");
    return;
  }

  // Response carries ≥1 enum — the inbound proto string must be a member of the
  // DTO const-object union, else fail loud (the TS twin of the C# strict
  // Parse<Enum>Wire). The mapper returns a D2Result: ok with the mapped DTO, or
  // validationFailed for an unknown wire value (no fallback). The call body
  // surfaces a parse failure as the business result.
  lines.push(
    `/** Map the ${op.grpcMethod}Response data → D2Result<${op.responseModelName}>; an unknown enum wire value fails loud. */`,
  );
  lines.push(
    `function ${respMapper}(data: unknown): D2Result<${op.responseModelName}> {`,
  );
  for (const f of op.responseFields) {
    if (f.enumRef === undefined) continue;
    lines.push(
      `  if (!Object.values(${f.enumRef.name}).includes(data.${f.tsName}))`,
    );
    lines.push(`    return validationFailed<${op.responseModelName}>();`);
  }
  lines.push("  return ok<" + op.responseModelName + ">({");
  for (const f of op.responseFields)
    lines.push(`    ${f.tsName}: data.${f.tsName},`);
  lines.push(`  } as ${op.responseModelName});`);
  lines.push("}");
  lines.push("");
}

// ---------------------------------------------------------------------------
// Private utility
// ---------------------------------------------------------------------------

function lowerFirst(s: string): string {
  // Defensive empty-string branch — every caller passes a non-empty gRPC method name.
  /* v8 ignore start — defensive: gRPC method names are never empty */
  return s.length === 0 ? s : s[0]!.toLowerCase() + s.slice(1);
  /* v8 ignore stop */
}
