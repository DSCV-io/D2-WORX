// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";

import {
  diagError,
  type EmitDiagnostic,
  type EmitResult,
  DiagnosticIds,
  formatDiagnostic,
} from "./lib/diagnostics.js";
import {
  buildHeader,
  isOutputUpToDate,
  writeGeneratedFile,
} from "./lib/file-emit.js";
import { contractsPath, tsPackagePath } from "./lib/paths.js";
import { loadSpec } from "./lib/spec-loader.js";
import { StringBuilder } from "./lib/string-builder.js";
import { parseTkKey } from "./lib/tk-key-transform.js";

// ---------------------------------------------------------------------------
// Unified spec-driven error-code emitter. ONE shared engine runs per
// `*-error-codes.spec.json`, deriving the emitted symbol names + the enforced
// domain prefix + the `factoryShape`-driven failure-factory shape from each
// catalog's CatalogConfig. Mirrors the .NET DcsvIo.D2.ErrorCodes.SourceGen
// engine SEMANTICS (httpStatus -> base factory map; factoryShape branch;
// D2ERC001 prefix + D2ERC002 TK-existence + D2ERC003 unsupported-shape
// diagnostics). The precedent is `wire-shape-emit.ts` — one shared helper,
// thin per-catalog runners. Adding a catalog = add a CatalogConfig + a thin
// runner + an orchestrator line.
// ---------------------------------------------------------------------------

const CODE_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/**
 * factoryShape values. `standard` is the one universal error-factory shape
 * (messages?, inputErrors?, errorCode?, category?, traceId? — all optional) the
 * base/constructing factory emitter AND the delegating failures emitter both
 * understand; `none` emits no factory (constant + boolean only).
 */
const SHAPE_STANDARD = "standard";
const SHAPE_NONE = "none";

/** Top-level shape of an `*-error-codes.spec.json`. */
export interface ErrorCodesSpec {
  readonly errorCodes: readonly ErrorCodeEntry[];
}

/**
 * One error-code entry parsed from the spec. Superset of the generic (3-field)
 * + auth (factory-bearing) shapes — the factory fields are optional so the
 * single entry type admits both catalogs. Per-catalog required-ness is carried
 * in validation, not in the type.
 */
export interface ErrorCodeEntry {
  readonly code: string;
  readonly httpStatus: number;
  readonly doc?: string;
  readonly category?: string;
  readonly userMessageKey?: string;
  readonly factoryName?: string;
  readonly factoryShape?: string;
  /**
   * Deprecation marker. When `true`, the emitted TS constant + factory carry a
   * `@deprecated` JSDoc tag built from `deprecatedReason` + `replacedBy`
   * (mirrors the .NET `[Obsolete]`). Additive — the entry is NEVER deleted.
   */
  readonly deprecated?: boolean;
  /**
   * Plain dev-facing English explaining why the code is deprecated; rendered
   * verbatim into the `@deprecated` JSDoc. NOT a TK key.
   */
  readonly deprecatedReason?: string;
  /** Wire code of the successor entry (e.g. `RESOURCE_NOT_FOUND`). */
  readonly replacedBy?: string;
  /**
   * ISO-8601 date for the future RFC 8594 `Sunset` response header. Declared
   * on the contract now so authors can annotate deprecated codes with a target
   * removal date without a breaking spec change later.
   *
   * This field is currently inert — its emit path is not yet implemented. It
   * will be consumed by the Edge response middleware (RFC 8594 `Sunset` header
   * injection) once that middleware is built. Until then, the field is preserved
   * in the spec schema but produces no generated output and triggers no
   * validation diagnostic. This is a known to-do, not a permanent design choice.
   */
  readonly sunset?: string;
}

/** Result of validating the spec. */
export interface ValidatedErrorCodes {
  readonly entries: readonly ErrorCodeEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/** Per-catalog validation-diagnostic id family (preserves D2EC* / D2AEC*). */
export interface ValidationDiagnosticIds {
  readonly duplicateCode: string;
  readonly invalidHttpStatus: string;
  readonly invalidCode?: string;
  readonly missingDoc?: string;
  readonly duplicateFactory?: string;
  readonly unknownCategory?: string;
}

/**
 * Every byte-affecting per-catalog divergence. The shared engine stays
 * identical; only this config differs between the generic + auth catalogs (the
 * same reasoning as the .NET CatalogConfig — byte-parity needs the EXACT
 * object names / ordering / per-code JSDoc / class-doc strings).
 */
export interface CatalogConfig {
  /** Relative spec path used in the auto-generated header (the ONLY banner variation). */
  readonly specRelativePath: string;
  /** Exported `as const` constants-object identifier (`ErrorCodes` / `AuthErrorCodes`). */
  readonly constObjectName: string;
  /** Element-type alias name (`ErrorCode` / `AuthErrorCode`). */
  readonly elementTypeName: string;
  /** `ALL_*` array identifier (`ALL_ERROR_CODES` / `ALL_AUTH_ERROR_CODES`). */
  readonly allCodesArrayName: string;
  /** httpStatus lookup-function identifier (`getErrorHttpStatus` / `getAuthErrorHttpStatus`). */
  readonly httpStatusFnName: string;
  /** Verbatim class-doc summary lines (the inside of the opening JSDoc block). */
  readonly classDocLines: readonly string[];
  /** Verbatim httpStatus-fn doc summary lines. */
  readonly httpStatusFnDocLines: readonly string[];
  /** Verbatim `ALL_*` array doc comment, or `undefined` to omit it. */
  readonly allCodesArrayDoc?: string;
  /** `true` => emit a per-code JSDoc on each constant (generic); `false` => none (auth). */
  readonly emitPerCodeJsDoc: boolean;
  /** `true` => sort entries by code (auth); `false` => preserve spec order (generic). */
  readonly sortByCode: boolean;
  /** Supported httpStatus set for the validation surface. */
  readonly supportedHttpStatuses: ReadonlySet<number>;
  /** Per-catalog validation diagnostic ids (preserves D2EC* / D2AEC*). */
  readonly diagnosticIds: ValidationDiagnosticIds;
  /** `true` => validate the SCREAMING_SNAKE code regex (generic); auth relies on its schema. */
  readonly validateCodeRegex: boolean;
  /** `true` => require a non-empty `doc` (generic); auth `doc` is optional on the constant. */
  readonly requireDoc: boolean;
  /** Closed `category` enum, or `undefined` if the catalog has no category field. */
  readonly validCategories?: ReadonlySet<string>;
  /** `true` => validate duplicate `factoryName` (factory-bearing catalogs). */
  readonly validateDuplicateFactory: boolean;
  /** Enforced domain prefix (`AUTH_`), or `undefined` for the unprefixed generic catalog. */
  readonly domainPrefix?: string;
}

/** Config for emitting the `<Domain>Failures` companion file. */
export interface FailuresConfig {
  readonly specRelativePath: string;
  /** Exported failures `as const` object identifier (`AuthFailures`). */
  readonly failuresObjectName: string;
  /** Verbatim class-doc summary lines for the failures object. */
  readonly classDocLines: readonly string[];
  /**
   * The constants-file basename imported for the `errorCode:` references
   * (e.g. `auth-error-codes.g.js`).
   */
  readonly constantsImportPath: string;
  /** The constants-object name imported from {@link constantsImportPath}. */
  readonly constObjectName: string;
}

/**
 * Config for emitting the base/constructing factories file (the generic
 * `D2Result` semantic factories — `notFound` / `unauthorized` / ...). Unlike
 * the delegating {@link FailuresConfig}, these factories ARE the base: they
 * construct a `D2Result` directly with the spec-declared status code, error
 * code, and default `userMessageKey`, and live as standalone module functions
 * (the TS twin of the .NET base factories on the `D2Result` partial class).
 */
export interface BaseFactoriesConfig {
  readonly specRelativePath: string;
}

/**
 * Validate the spec — config-driven so the generic + auth predicate surfaces
 * (which differ) both run through one walk. Adds the catalog-neutral
 * D2ERC001 domain-prefix + D2ERC002 TK-existence checks when the catalog
 * enables them. Returns the valid subset for downstream emit. Mirrors the
 * .NET emitter's predicate set so cross-language drift between the two
 * validation surfaces is structurally impossible.
 */
export function validateErrorCodesSpec(
  spec: ErrorCodesSpec,
  config: CatalogConfig,
  enUsKeys?: ReadonlySet<string>,
): ValidatedErrorCodes {
  const diagnostics: EmitDiagnostic[] = [];
  const validEntries: ErrorCodeEntry[] = [];
  const seenCodes = new Set<string>();
  const seenFactories = new Set<string>();
  const ids = config.diagnosticIds;

  for (const entry of spec.errorCodes) {
    if (config.validateCodeRegex && !CODE_NAME_RE.test(entry.code)) {
      diagnostics.push(
        diagError(
          ids.invalidCode ?? DiagnosticIds.EC_INVALID_CODE,
          `invalid error code '${entry.code}' — must match ${CODE_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (seenCodes.has(entry.code)) {
      diagnostics.push(
        diagError(ids.duplicateCode, `duplicate error code '${entry.code}'`),
      );
      continue;
    }
    seenCodes.add(entry.code);
    if (
      config.domainPrefix !== undefined &&
      !entry.code.startsWith(config.domainPrefix)
    ) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ERC_DOMAIN_PREFIX_VIOLATION,
          `error code '${entry.code}' must start with the enforced` +
            ` domain prefix '${config.domainPrefix}'`,
        ),
      );
      continue;
    }
    if (
      config.validateDuplicateFactory &&
      entry.factoryName !== undefined &&
      ids.duplicateFactory !== undefined
    ) {
      if (seenFactories.has(entry.factoryName)) {
        diagnostics.push(
          diagError(
            ids.duplicateFactory,
            `duplicate factory name '${entry.factoryName}'`,
          ),
        );
        continue;
      }
      seenFactories.add(entry.factoryName);
    }
    if (
      config.validCategories !== undefined &&
      ids.unknownCategory !== undefined &&
      (entry.category === undefined ||
        !config.validCategories.has(entry.category))
    ) {
      diagnostics.push(
        diagError(
          ids.unknownCategory,
          `unknown category '${entry.category}' on '${entry.code}' (valid: ${[
            ...config.validCategories,
          ]
            .sort()
            .join(", ")})`,
        ),
      );
      continue;
    }
    if (!config.supportedHttpStatuses.has(entry.httpStatus)) {
      diagnostics.push(
        diagError(
          ids.invalidHttpStatus,
          `unsupported httpStatus ${entry.httpStatus} on '${entry.code}' (supported: ${[
            ...config.supportedHttpStatuses,
          ]
            .sort((a, b) => a - b)
            .join(", ")})`,
        ),
      );
      continue;
    }
    if (
      config.requireDoc &&
      (entry.doc === undefined || entry.doc.trim().length === 0)
    ) {
      diagnostics.push(
        diagError(
          ids.missingDoc ?? DiagnosticIds.EC_MISSING_DOC,
          `error code '${entry.code}' is missing the required 'doc' summary text`,
        ),
      );
      continue;
    }
    // Catalog-neutral TK-existence check: when a userMessageKey is declared
    // (factory-bearing catalogs) cross-check it against the en-US.json key set,
    // NOT the generated TK catalog (which would be circular).
    if (entry.userMessageKey !== undefined && enUsKeys !== undefined) {
      const parts = parseTkKey(entry.userMessageKey);
      if (parts === undefined || !enUsKeys.has(parts.snakeKey)) {
        diagnostics.push(
          diagError(
            DiagnosticIds.ERC_TK_KEY_NOT_FOUND,
            `error code '${entry.code}' references userMessageKey '${entry.userMessageKey}' ` +
              `which does not resolve to a key in en-US.json` +
              (parts !== undefined
                ? ` (expected snake_case key '${parts.snakeKey}')`
                : ""),
          ),
        );
        continue;
      }
    }
    validEntries.push(entry);
  }
  return { entries: validEntries, diagnostics };
}

/**
 * Emit the constants `.g.ts` source (`<Const>` object + element type +
 * `ALL_*` array + httpStatus lookup). Stateless and unit-testable. Ordering +
 * per-code JSDoc + identifiers are all config-driven so one helper reproduces
 * both the generic + auth constants files byte-for-byte.
 */
export function emitErrorCodesCatalog(
  spec: ErrorCodesSpec,
  config: CatalogConfig,
  enUsKeys?: ReadonlySet<string>,
): EmitResult {
  const v = validateErrorCodesSpec(spec, config, enUsKeys);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const entries = orderEntries(v.entries, config);

  const sb = new StringBuilder();
  sb.appendLine(buildHeader(config.specRelativePath));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  for (const line of config.classDocLines) sb.appendLine(line);
  sb.appendLine(" */");
  sb.appendLine(`export const ${config.constObjectName} = {`);
  sb.increaseIndent();
  for (const e of entries) {
    const deprecatedTag = deprecatedJsDocText(e);
    // Emit the per-code JSDoc when the catalog opts into it (generic) OR the
    // entry is deprecated — a deprecated `as const` member needs the
    // `@deprecated` tag so TS / `@typescript-eslint/no-deprecated` flag every
    // use site (the TS twin of CS0618), even on auth (no per-code doc otherwise).
    if (config.emitPerCodeJsDoc) {
      sb.appendLine("/**");
      for (const rawLine of (e.doc ?? "").split("\n"))
        sb.appendLine(` * ${escapeJsDoc(rawLine)}`);
      if (deprecatedTag !== undefined) sb.appendLine(` * ${deprecatedTag}`);
      sb.appendLine(" */");
    } else if (deprecatedTag !== undefined) {
      sb.appendLine(`/** ${deprecatedTag} */`);
    }
    sb.appendLine(`${e.code}: "${escapeStringLiteral(e.code)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(`export type ${config.elementTypeName} =`);
  sb.increaseIndent();
  sb.appendLine(
    `(typeof ${config.constObjectName})[keyof typeof ${config.constObjectName}];`,
  );
  sb.decreaseIndent();
  sb.appendLine();
  if (config.allCodesArrayDoc !== undefined)
    sb.appendLine(config.allCodesArrayDoc);
  sb.appendLine(
    `export const ${config.allCodesArrayName}: readonly string[] = [`,
  );
  sb.increaseIndent();
  for (const e of entries) sb.appendLine(`"${escapeStringLiteral(e.code)}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();
  sb.appendLine("/**");
  for (const line of config.httpStatusFnDocLines) sb.appendLine(line);
  sb.appendLine(" */");
  sb.appendLine(
    `export function ${config.httpStatusFnName}(code: string): number {`,
  );
  sb.increaseIndent();
  sb.appendLine("switch (code) {");
  sb.increaseIndent();
  for (const e of entries)
    sb.appendLine(
      `case "${escapeStringLiteral(e.code)}": return ${e.httpStatus};`,
    );
  sb.appendLine("default: return 500;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
  return { source: sb.toString(), diagnostics: v.diagnostics };
}

/**
 * Emit the `<Domain>Failures` companion `.g.ts` — one static factory per
 * factory-bearing entry. Mirrors the .NET FailuresEmitter: the base D2Result
 * factory is selected by `httpStatus` (404 -> `notFound`, 401 ->
 * `unauthorized`, 503 -> `serviceUnavailable`, …); `factoryShape` drives the
 * branch (the universal `standard` shape -> emit, `none` -> skip, any other
 * value -> fail loud via D2ERC003).
 *
 * Each emitted factory takes an opts object `{ messages?, traceId? }` (the TS
 * twin of the .NET delegating factory's `IReadOnlyList<TKMessage>? messages`
 * override): when `messages` is omitted it defaults to the spec's TK constant;
 * when supplied it replaces the default, so a caller can bind the offending
 * argument via `TKMessage.With(...)`.
 *
 * The `userMessageKey` is emitted as a REFERENCE to the generated TS TK
 * constant (`TK.auth.errors.UNAUTHORIZED`) — itself a `TKMessage` instance,
 * never a raw key/path string literal. A literal silently bypasses the TK
 * catalog and rides the wire un-renderable. The transformed TK access path is
 * the inverse of the `KeyDecomposer` mapping.
 */
export function emitFailuresCatalog(
  spec: ErrorCodesSpec,
  config: CatalogConfig,
  failures: FailuresConfig,
  enUsKeys?: ReadonlySet<string>,
): EmitResult {
  const v = validateErrorCodesSpec(spec, config, enUsKeys);
  // The constants emitter surfaces the validation diagnostics; this emitter
  // operates on the valid subset only. Fail-loud factoryShape diagnostics are
  // collected below so an unsupported shape breaks the codegen run.
  const diagnostics: EmitDiagnostic[] = [];
  const factoryEntries: { entry: ErrorCodeEntry; tkConstantPath: string }[] =
    [];

  for (const e of orderEntries(v.entries, config)) {
    const shape = e.factoryShape;
    if (
      shape !== undefined &&
      shape !== SHAPE_STANDARD &&
      shape !== SHAPE_NONE
    ) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ERC_UNSUPPORTED_FACTORY_SHAPE,
          `error code '${e.code}' declares unsupported factoryShape '${shape}' ` +
            `for failure-factory emission (supported: ${SHAPE_STANDARD}, ${SHAPE_NONE})`,
        ),
      );
      continue;
    }
    if (shape === SHAPE_NONE) continue;
    if (e.userMessageKey === undefined || e.factoryName === undefined) continue;
    const parts = parseTkKey(e.userMessageKey);
    if (parts === undefined) continue;
    factoryEntries.push({ entry: e, tkConstantPath: parts.tkConstantPath });
  }

  if (diagnostics.some((d) => d.severity === "error"))
    return { source: "", diagnostics };

  // The canonical httpStatus → base D2Result factory delegation map (mirrors
  // the .NET FailuresEmitter.BaseFactory). Covers the full per-domain set so a
  // 404 / 409 / 413 domain code delegates to the right base factory; every base
  // factory is the universal `standard` shape, so the delegating body always
  // safely passes errorCode + category.
  const factoryFor = (status: number): string => {
    switch (status) {
      case 400:
        return "validationFailed";
      case 401:
        return "unauthorized";
      case 403:
        return "forbidden";
      case 404:
        return "notFound";
      case 409:
        return "conflict";
      case 413:
        return "payloadTooLarge";
      case 429:
        return "tooManyRequests";
      case 500:
        return "unhandledException";
      case 503:
        return "serviceUnavailable";
      default:
        return "unhandledException";
    }
  };

  const usedFactories = [
    ...new Set(factoryEntries.map((fe) => factoryFor(fe.entry.httpStatus))),
  ].sort();

  const sb = new StringBuilder();
  sb.appendLine(buildHeader(failures.specRelativePath));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine(
    `import { D2Result, ${usedFactories.join(", ")} } from "@dcsv-io/d2-result";`,
  );
  sb.appendLine(
    'import { ErrorCategoryWire } from "@dcsv-io/d2-error-category";',
  );
  sb.appendLine(
    'import { type TKMessage } from "@dcsv-io/d2-i18n-abstractions";',
  );
  sb.appendLine('import { TK } from "@dcsv-io/d2-i18n-keys";');
  sb.appendLine(
    `import { ${failures.constObjectName} } from "${failures.constantsImportPath}";`,
  );
  sb.appendLine();
  sb.appendLine("/**");
  for (const line of failures.classDocLines) sb.appendLine(line);
  sb.appendLine(" */");
  sb.appendLine(`export const ${failures.failuresObjectName} = {`);
  sb.increaseIndent();
  for (const { entry, tkConstantPath } of factoryEntries) {
    const factory = factoryFor(entry.httpStatus);
    const fnName = camelCase(entry.factoryName!);
    const categoryMember = categoryMemberName(entry.category!);
    const deprecatedTag = deprecatedJsDocText(entry);
    if (deprecatedTag === undefined) {
      sb.appendLine(`/** ${escapeJsDoc(entry.doc ?? "")} */`);
    } else {
      sb.appendLine("/**");
      sb.appendLine(` * ${escapeJsDoc(entry.doc ?? "")}`);
      sb.appendLine(` * ${deprecatedTag}`);
      sb.appendLine(" */");
    }
    // Generic with a `void` default: the untyped call (`AuthFailures.x()` ->
    // `D2Result<void>`) and the typed call (`AuthFailures.x<User>()` ->
    // `D2Result<User>`) share one method, mirroring the framework base
    // factories. This is the TS equivalent of the .NET `<Domain>Failures` +
    // `<Domain>Failures<T>` two-class split. The category is the auth code's
    // OWN category, overriding the base factory's default (mirrors .NET).
    //
    // The opts object carries an optional `messages` override (mirroring the
    // .NET delegating factory's `IReadOnlyList<TKMessage>? messages = null`):
    // when omitted it defaults to the spec's TK constant; when supplied it
    // replaces it, so a caller can bind the offending argument via
    // `TKMessage.With(...)`.
    // long emitter-output string literal — cannot wrap (byte-identity)
    sb.appendLine(
      `${fnName}<T = void>(opts: { messages?: readonly TKMessage[]; traceId?: string } = {}): D2Result<T> {`,
    );
    sb.increaseIndent();
    sb.appendLine(`return ${factory}<T>({`);
    sb.increaseIndent();
    sb.appendLine(`messages: opts.messages ?? [${tkConstantPath}],`);
    sb.appendLine(`errorCode: ${failures.constObjectName}.${entry.code},`);
    sb.appendLine(`category: ErrorCategoryWire.${categoryMember},`);
    sb.appendLine(`traceId: opts.traceId,`);
    sb.decreaseIndent();
    sb.appendLine(`});`);
    sb.decreaseIndent();
    sb.appendLine(`},`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  return { source: sb.toString(), diagnostics };
}

/**
 * Emit the base/constructing factories `.g.ts` — one standalone semantic
 * factory FUNCTION per factory-bearing entry, constructing a `D2Result<T>`
 * directly (the generic base factories). The TS twin of the .NET
 * `BaseFactoriesEmitter`: every error factory is the one universal `standard`
 * shape — a single `ErrorOpts` (messages?, inputErrors?, errorCode?, category?,
 * traceId? — all optional) so any factory can stamp a domain code + category
 * and optionally carry inputErrors; `none` emits nothing. Each function is
 * generic with a `void` default (`notFound<T = void>(opts?): D2Result<T>`), so
 * one function spans the untyped (`D2Result<void>`) AND typed (`D2Result<User>`)
 * cases — the same collapse the .NET two-overload set delivers.
 *
 * The `userMessageKey` is emitted as a REFERENCE to the generated TS TK
 * constant (`TK.common.errors.NOT_FOUND` from `@dcsv-io/d2-i18n-keys`) — itself a
 * `TKMessage` instance, never a raw key/path string literal. A literal
 * silently bypasses the TK catalog and rides the wire un-renderable.
 * `@dcsv-io/d2-i18n-keys` is a zero-dependency leaf package so `@dcsv-io/d2-result` may
 * reference the constant without a dependency cycle.
 */
export function emitBaseFactoriesCatalog(
  spec: ErrorCodesSpec,
  config: CatalogConfig,
  base: BaseFactoriesConfig,
  enUsKeys?: ReadonlySet<string>,
): EmitResult {
  const v = validateErrorCodesSpec(spec, config, enUsKeys);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const factoryEntries: { entry: ErrorCodeEntry; tkConstantPath: string }[] =
    [];
  for (const e of orderEntries(v.entries, config)) {
    if (!emitsBaseFactory(e.factoryShape)) continue;
    if (e.userMessageKey === undefined || e.factoryName === undefined) continue;
    const parts = parseTkKey(e.userMessageKey);
    if (parts === undefined) continue;
    factoryEntries.push({ entry: e, tkConstantPath: parts.tkConstantPath });
  }

  const sb = new StringBuilder();
  sb.appendLine(buildHeader(base.specRelativePath));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine('import { D2Result } from "./d2-result.js";');
  sb.appendLine('import { ErrorCodes } from "./error-codes.g.js";');
  sb.appendLine('import { HttpStatusCode } from "./http-status-codes.js";');
  sb.appendLine('import type { InputError } from "./input-error.js";');
  sb.appendLine(
    'import { type ErrorCategory, ErrorCategoryWire } from "@dcsv-io/d2-error-category";',
  );
  sb.appendLine(
    'import { type TKMessage } from "@dcsv-io/d2-i18n-abstractions";',
  );
  sb.appendLine('import { TK } from "@dcsv-io/d2-i18n-keys";');
  sb.appendLine();

  // The one universal error-factory opts interface (the `standard` shape).
  // Every field is optional so any factory can stamp a domain code + category
  // and optionally carry inputErrors. The PRESERVE `created` / `someFound` /
  // `fail` factories in factories.ts import it.
  sb.appendLine("export interface ErrorOpts {");
  sb.increaseIndent();
  sb.appendLine("messages?: readonly TKMessage[];");
  sb.appendLine("inputErrors?: readonly InputError[];");
  sb.appendLine("errorCode?: string;");
  sb.appendLine("category?: ErrorCategory;");
  sb.appendLine("traceId?: string;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  for (const { entry, tkConstantPath } of factoryEntries) {
    emitBaseFactory(sb, entry, tkConstantPath);
    sb.appendLine();
  }

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

/**
 * Whether an entry's `factoryShape` emits a constructing factory in base mode.
 * The universal `standard` shape emits a factory; `none` emits no factory.
 */
function emitsBaseFactory(shape: string | undefined): boolean {
  return shape === SHAPE_STANDARD;
}

/** Emit one base/constructing factory function (the universal `standard` shape). */
function emitBaseFactory(
  sb: StringBuilder,
  entry: ErrorCodeEntry,
  tkConstantPath: string,
): void {
  const fnName = camelCase(entry.factoryName!);
  const status = statusName(entry.httpStatus);
  const categoryMember = categoryMemberName(entry.category!);
  // The one universal error-factory shape: the category is baked from the
  // entry's declared category but the errorCode / category overrides let a
  // delegating domain factory stamp its own code + category on this base
  // status, and every factory can carry inputErrors.
  const errorCodeExpr = `opts.errorCode ?? ErrorCodes.${entry.code}`;
  const categoryExpr = `opts.category ?? ErrorCategoryWire.${categoryMember}`;

  const deprecatedTag = deprecatedJsDocText(entry);
  if (deprecatedTag === undefined) {
    sb.appendLine(`/** ${escapeJsDoc(entry.doc ?? "")} */`);
  } else {
    sb.appendLine("/**");
    sb.appendLine(` * ${escapeJsDoc(entry.doc ?? "")}`);
    sb.appendLine(` * ${deprecatedTag}`);
    sb.appendLine(" */");
  }

  sb.appendLine(
    `export function ${fnName}<T = void>(opts: ErrorOpts = {}): D2Result<T> {`,
  );
  sb.increaseIndent();
  sb.appendLine("return new D2Result<T>({");
  sb.increaseIndent();
  sb.appendLine("success: false,");
  sb.appendLine(`messages: opts.messages ?? [${tkConstantPath}],`);
  sb.appendLine("inputErrors: opts.inputErrors,");
  sb.appendLine(`statusCode: HttpStatusCode.${status},`);
  sb.appendLine(`errorCode: ${errorCodeExpr},`);
  sb.appendLine("traceId: opts.traceId,");
  sb.appendLine(`category: ${categoryExpr},`);
  sb.decreaseIndent();
  sb.appendLine("});");
  sb.decreaseIndent();
  sb.appendLine("}");
}

/**
 * Maps an HTTP status int to the `HttpStatusCode` member name used in the
 * constructing factory body. Mirrors the .NET `BaseFactoriesEmitter.StatusName`.
 */
function statusName(httpStatus: number): string {
  switch (httpStatus) {
    case 200:
      return "OK";
    case 206:
      return "PartialContent";
    case 207:
      return "MultiStatus";
    case 400:
      return "BadRequest";
    case 401:
      return "Unauthorized";
    case 403:
      return "Forbidden";
    case 404:
      return "NotFound";
    case 409:
      return "Conflict";
    case 413:
      return "RequestEntityTooLarge";
    case 429:
      return "TooManyRequests";
    case 500:
      return "InternalServerError";
    case 503:
      return "ServiceUnavailable";
    default:
      return "InternalServerError";
  }
}

function orderEntries(
  entries: readonly ErrorCodeEntry[],
  config: CatalogConfig,
): readonly ErrorCodeEntry[] {
  if (!config.sortByCode) return entries;
  return [...entries].sort((a, b) => a.code.localeCompare(b.code));
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

/**
 * Composes the `@deprecated` JSDoc text for a deprecated entry: the entry's
 * `deprecatedReason`, with ` Use {replacedBy} instead.` appended when a
 * successor code is declared. Mirrors the .NET `ObsoleteMessageLiteral`
 * composition so the C# `[Obsolete]` message and the TS `@deprecated` text are
 * identical. Returns `undefined` for a non-deprecated entry (no tag emitted).
 */
function deprecatedJsDocText(entry: ErrorCodeEntry): string | undefined {
  if (entry.deprecated !== true) return undefined;
  const reason = entry.deprecatedReason ?? "";
  const replacedBy = entry.replacedBy;
  const message =
    replacedBy !== undefined && replacedBy.trim().length > 0
      ? `${reason} Use ${replacedBy} instead.`
      : reason;
  return `@deprecated ${escapeJsDoc(message)}`;
}

function camelCase(pascal: string): string {
  return pascal.length === 0
    ? pascal
    : pascal[0]!.toLowerCase() + pascal.slice(1);
}

/**
 * Maps a spec category wire string (snake_case) to its `ErrorCategoryWire`
 * PascalCase member name (`validation_failure` -> `ValidationFailure`). The
 * emitted factory references `ErrorCategoryWire.<member>` (the typed
 * `ErrorCategory` union value), so the per-code category baked into the factory
 * matches the .NET `ErrorCategory.<member>` and the registry. Mirrors the .NET
 * `BaseFactoriesEmitter.CategoryMemberName`.
 */
function categoryMemberName(category: string): string {
  return category
    .split("_")
    .filter((s) => s.length > 0)
    .map((s) => s[0]!.toUpperCase() + s.slice(1).toLowerCase())
    .join("");
}

/** Parse en-US.json into its top-level key set (for the D2ERC002 cross-check). */
function loadEnUsKeys(): ReadonlySet<string> {
  const path = contractsPath("messages", "en-US.json");
  const parsed = JSON.parse(readFileSync(path, "utf8")) as Record<
    string,
    unknown
  >;
  const keys = new Set<string>();
  for (const key of Object.keys(parsed)) if (key !== "$schema") keys.add(key);
  return keys;
}

/**
 * Parse `contracts/error-category/error-category.spec.json` into its closed set
 * of category `wire` strings — the spec-derived authority for the
 * category-membership validation (mirrors the .NET `CategoryWireSetLoader`).
 * Replaces the previously hard-coded subset so the accepted categories are
 * exactly the ones declared in the category spec. A malformed / shapeless spec
 * yields an empty set, which the validation treats as "no closed set" and skips
 * — the same degradation the .NET side uses.
 */
export function loadCategoryWireSet(): ReadonlySet<string> {
  const path = contractsPath("error-category", "error-category.spec.json");
  const wires = new Set<string>();
  let parsed: unknown;
  try {
    parsed = JSON.parse(readFileSync(path, "utf8"));
  } catch {
    return wires;
  }
  if (typeof parsed !== "object" || parsed === null) return wires;
  const categories = (parsed as Record<string, unknown>)["categories"];
  if (!Array.isArray(categories)) return wires;
  for (const entry of categories) {
    if (typeof entry !== "object" || entry === null) continue;
    const wire = (entry as Record<string, unknown>)["wire"];
    if (typeof wire === "string") wires.add(wire);
  }
  return wires;
}

// ---------------------------------------------------------------------------
// Per-catalog configs.
// ---------------------------------------------------------------------------

export const GENERIC_CONFIG: CatalogConfig = {
  specRelativePath: "contracts/error-codes/error-codes.spec.json",
  constObjectName: "ErrorCodes",
  elementTypeName: "ErrorCode",
  allCodesArrayName: "ALL_ERROR_CODES",
  httpStatusFnName: "getErrorHttpStatus",
  classDocLines: [
    " * Standardized error codes surfaced as `D2Result.errorCode`. Mirrors",
    " * .NET `DcsvIo.D2.Result.ErrorCodes` byte-for-byte (single spec source",
    " * emits both sides; cross-language drift is structurally impossible).",
  ],
  httpStatusFnDocLines: [
    " * HTTP status declared in the spec for a given error code.",
    " * Returns 500 for unknown codes (defensive default).",
  ],
  allCodesArrayDoc: "/** All declared error codes in spec order. */",
  emitPerCodeJsDoc: true,
  sortByCode: false,
  supportedHttpStatuses: new Set([
    200, 206, 207, 400, 401, 403, 404, 409, 413, 429, 500, 503,
  ]),
  diagnosticIds: {
    duplicateCode: DiagnosticIds.EC_DUPLICATE_CODE,
    invalidHttpStatus: DiagnosticIds.EC_INVALID_HTTP_STATUS,
    invalidCode: DiagnosticIds.EC_INVALID_CODE,
    missingDoc: DiagnosticIds.EC_MISSING_DOC,
  },
  validateCodeRegex: true,
  requireDoc: true,
  validateDuplicateFactory: false,
};

export const AUTH_CONFIG: CatalogConfig = {
  specRelativePath: "contracts/auth-error-codes/auth-error-codes.spec.json",
  constObjectName: "AuthErrorCodes",
  elementTypeName: "AuthErrorCode",
  allCodesArrayName: "ALL_AUTH_ERROR_CODES",
  httpStatusFnName: "getAuthErrorHttpStatus",
  classDocLines: [
    " * Machine-readable error codes for auth runtime failures. Mirrors .NET",
    " * DcsvIo.D2.Auth.Errors.AuthErrorCodes (same string values).",
  ],
  httpStatusFnDocLines: [
    " * HTTP status declared in the spec for an AUTH_* code.",
    " * Returns 500 for unknown codes (defensive default).",
  ],
  emitPerCodeJsDoc: false,
  sortByCode: true,
  supportedHttpStatuses: new Set([401, 503]),
  diagnosticIds: {
    duplicateCode: DiagnosticIds.AEC_DUPLICATE_CODE,
    invalidHttpStatus: DiagnosticIds.AEC_INVALID_HTTP_STATUS,
    duplicateFactory: DiagnosticIds.AEC_DUPLICATE_FACTORY,
    unknownCategory: DiagnosticIds.AEC_UNKNOWN_CATEGORY,
  },
  validateCodeRegex: false,
  requireDoc: false,
  // Spec-derived from contracts/error-category/error-category.spec.json (the
  // closed ErrorCategory set) rather than a hand-maintained subset — mirrors
  // the .NET engine threading the category wire set into ConstantsEmitter.
  validCategories: loadCategoryWireSet(),
  validateDuplicateFactory: true,
  domainPrefix: "AUTH_",
};

export const GENERIC_FACTORIES_CONFIG: BaseFactoriesConfig = {
  specRelativePath: "contracts/error-codes/error-codes.spec.json",
};

export const AUTH_FAILURES_CONFIG: FailuresConfig = {
  specRelativePath: "contracts/auth-error-codes/auth-error-codes.spec.json",
  failuresObjectName: "AuthFailures",
  classDocLines: [
    " * Pre-built D2Result failures for inbound auth runtime — JWT validation",
    " * rejections, session liveness outages, JWKS upstream failures.",
    " * Mirrors .NET DcsvIo.D2.Auth.Errors.AuthFailures factory shape.",
  ],
  constantsImportPath: "./auth-error-codes.g.js",
  constObjectName: "AuthErrorCodes",
};

// ---------------------------------------------------------------------------
// Per-catalog runners — each keeps its EXACT exported name (the orchestrator +
// package.json codegen scripts + auth-abstractions prebuild reference them).
// ---------------------------------------------------------------------------

const GENERIC_SPEC_PATH = contractsPath("error-codes", "error-codes.spec.json");
const GENERIC_TARGET_PATH = tsPackagePath("result", "src", "error-codes.g.ts");
const GENERIC_FACTORIES_TARGET = tsPackagePath(
  "result",
  "src",
  "factories.g.ts",
);

const AUTH_SPEC_PATH = contractsPath(
  "auth-error-codes",
  "auth-error-codes.spec.json",
);
const AUTH_CONSTANTS_TARGET = tsPackagePath(
  "auth",
  "abstractions",
  "src",
  "auth-error-codes.g.ts",
);
const AUTH_FAILURES_TARGET = tsPackagePath(
  "auth",
  "abstractions",
  "src",
  "auth-failures.g.ts",
);
const EN_US_PATH = contractsPath("messages", "en-US.json");
const CATEGORY_SPEC_PATH = contractsPath(
  "error-category",
  "error-category.spec.json",
);

/**
 * Run the generic error-codes emitter (constants only). Per-spec mtime check
 * skips emit when the output is newer than the spec; pass `force=true` to
 * bypass.
 */
export function runErrorCodesEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(GENERIC_TARGET_PATH, [GENERIC_SPEC_PATH]))
    return [];
  const loadResult = loadSpec<ErrorCodesSpec>(
    GENERIC_SPEC_PATH,
    DiagnosticIds.EC_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitErrorCodesCatalog(loadResult.spec, GENERIC_CONFIG);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(GENERIC_TARGET_PATH, result.source);
  return result.diagnostics;
}

/**
 * Run the generic base/constructing factories emitter
 * (`factories.g.ts` — the `D2Result` semantic factories). Depends on the
 * generic spec + en-US.json (for the D2ERC002 userMessageKey cross-check).
 * Per-spec mtime check skips emit when the output is newer; pass `force=true`
 * to bypass.
 */
export function runErrorCodesFactoriesEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (
    !force &&
    isOutputUpToDate(GENERIC_FACTORIES_TARGET, [GENERIC_SPEC_PATH, EN_US_PATH])
  )
    return [];
  const loadResult = loadSpec<ErrorCodesSpec>(
    GENERIC_SPEC_PATH,
    DiagnosticIds.EC_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitBaseFactoriesCatalog(
    loadResult.spec,
    GENERIC_CONFIG,
    GENERIC_FACTORIES_CONFIG,
    loadEnUsKeys(),
  );
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(GENERIC_FACTORIES_TARGET, result.source);
  return result.diagnostics;
}

/**
 * Run the auth error-codes constants emitter. Per-spec mtime check skips emit
 * when the output is newer than the spec; pass `force=true` to bypass.
 */
export function runAuthErrorCodesEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (
    !force &&
    isOutputUpToDate(AUTH_CONSTANTS_TARGET, [
      AUTH_SPEC_PATH,
      EN_US_PATH,
      CATEGORY_SPEC_PATH,
    ])
  )
    return [];
  const loadResult = loadSpec<ErrorCodesSpec>(
    AUTH_SPEC_PATH,
    DiagnosticIds.AEC_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitErrorCodesCatalog(
    loadResult.spec,
    AUTH_CONFIG,
    loadEnUsKeys(),
  );
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(AUTH_CONSTANTS_TARGET, result.source);
  return result.diagnostics;
}

/**
 * Run the auth failures emitter (`AuthFailures` factories). Depends on the
 * auth constants file (`auth-error-codes.g.ts`) — emit after
 * {@link runAuthErrorCodesEmit}.
 */
export function runAuthFailuresEmit(force = false): readonly EmitDiagnostic[] {
  if (
    !force &&
    isOutputUpToDate(AUTH_FAILURES_TARGET, [
      AUTH_SPEC_PATH,
      EN_US_PATH,
      CATEGORY_SPEC_PATH,
    ])
  )
    return [];
  const loadResult = loadSpec<ErrorCodesSpec>(
    AUTH_SPEC_PATH,
    DiagnosticIds.AEC_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitFailuresCatalog(
    loadResult.spec,
    AUTH_CONFIG,
    AUTH_FAILURES_CONFIG,
    loadEnUsKeys(),
  );
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(AUTH_FAILURES_TARGET, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("error-codes-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = [
    ...runErrorCodesEmit(force),
    // The generic base factories depend on the generic constants (ErrorCodes)
    // — emit after the constants.
    ...runErrorCodesFactoriesEmit(force),
    ...runAuthErrorCodesEmit(force),
    ...runAuthFailuresEmit(force),
  ];
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
