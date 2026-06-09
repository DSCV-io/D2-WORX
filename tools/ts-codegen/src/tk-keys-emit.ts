// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  diagError,
  diagWarning,
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
import { StringBuilder } from "./lib/string-builder.js";
import { readFileSync } from "node:fs";

/** Raw en-US.json shape — flat record of key → value strings. */
type MessageCatalog = Record<string, string>;

/**
 * One decomposed TK key entry. Mirrors the .NET KeyDecomposer output
 * shape: domain (segment[0]), category (segment[1]), constant
 * (segments[2..N] joined + uppercased), original key string.
 */
interface TkKeyEntry {
  readonly domain: string;
  readonly category: string;
  readonly constant: string;
  readonly key: string;
}

/**
 * Decompose a flat i18n key into a TkKeyEntry. Returns undefined (+ emits
 * a diagnostic) for keys with fewer than 3 non-empty segments. Mirrors the
 * .NET KeyDecomposer.Decompose predicate exactly:
 *
 *   segment[0] = domain  (lowercase, as-is)
 *   segment[1] = category  (lowercase, as-is)
 *   segments[2..N].join("_").toUpperCase() = constant
 *
 * The TS side keeps domain + category in lowercase (unlike the .NET side
 * which PascalCases them for C# identifiers) because TS consumers use
 * lowercase namespace traversal: `TK.common.errors.REQUEST_FAILED`.
 */
function decomposeKey(
  key: string,
  diagnostics: EmitDiagnostic[],
): TkKeyEntry | undefined {
  const segments = key.split("_").filter((s) => s.length > 0);
  if (segments.length < 3) {
    diagnostics.push(
      diagWarning(
        DiagnosticIds.TK_INVALID_KEY,
        `TK key '${key}' has fewer than 3 non-empty segments — skipped ` +
          `(mirrors .NET KeyDecomposer behavior)`,
      ),
    );
    return undefined;
  }
  const domain = segments[0]!;
  const category = segments[1]!;
  const constant = segments.slice(2).join("_").toUpperCase();
  return { domain, category, constant, key };
}

/**
 * Build the nested TK catalog from an array of decomposed key entries.
 * Returns a sorted, stable map structure:
 *   domain → category → constant → key string
 *
 * Sort order: domain asc, then category asc, then constant asc.
 * Deterministic output = byte-stable across runs regardless of JSON key
 * order in en-US.json (mirrors the .NET emitter's sort discipline).
 */
function buildCatalog(
  entries: readonly TkKeyEntry[],
): Map<string, Map<string, Map<string, string>>> {
  const catalog = new Map<string, Map<string, Map<string, string>>>();

  for (const entry of entries) {
    if (!catalog.has(entry.domain))
      catalog.set(entry.domain, new Map<string, Map<string, string>>());
    const domainMap = catalog.get(entry.domain)!;

    if (!domainMap.has(entry.category))
      domainMap.set(entry.category, new Map<string, string>());
    const categoryMap = domainMap.get(entry.category)!;

    categoryMap.set(entry.constant, entry.key);
  }

  // Sort all levels for deterministic output.
  const sortedCatalog = new Map(
    [...catalog.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([domain, domainMap]) => [
        domain,
        new Map(
          [...domainMap.entries()]
            .sort(([a], [b]) => a.localeCompare(b))
            .map(([category, categoryMap]) => [
              category,
              new Map(
                [...categoryMap.entries()].sort(([a], [b]) =>
                  a.localeCompare(b),
                ),
              ),
            ]),
        ),
      ]),
  );

  return sortedCatalog;
}

/**
 * Emit `tk-keys.g.ts` source from the decomposed + sorted catalog.
 * Stateless and unit-testable. Returns the source string and any
 * diagnostics that fired during decomposition (warnings for skipped keys).
 */
export function emitTkKeys(catalog: MessageCatalog): EmitResult {
  const diagnostics: EmitDiagnostic[] = [];
  const entries: TkKeyEntry[] = [];

  for (const key of Object.keys(catalog)) {
    const entry = decomposeKey(key, diagnostics);
    if (entry !== undefined) entries.push(entry);
  }

  const nested = buildCatalog(entries);

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/messages/en-US.json"));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine('import { tk } from "@d2/i18n-abstractions";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Type-safe i18n key catalog. Mirrors the .NET `TK` class emitted by",
  );
  sb.appendLine(
    " * D2.Shared.I18n.SourceGen from the same `contracts/messages/en-US.json`",
  );
  sb.appendLine(
    " * source — single spec, two emitters, cross-language drift structurally",
  );
  sb.appendLine(" * impossible.");
  sb.appendLine(" *");
  sb.appendLine(
    " * Usage: `TK.common.errors.REQUEST_FAILED` is a `TKMessage` instance",
  );
  sb.appendLine(
    ' * (`{ key: "common_errors_REQUEST_FAILED" }`) ready to drop into',
  );
  sb.appendLine(
    " * `D2Result.messages` — no `tk()` wrapper needed at the call site.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const TK = {");
  sb.increaseIndent();

  const domainEntries = [...nested.entries()];
  for (let di = 0; di < domainEntries.length; di++) {
    const [domain, domainMap] = domainEntries[di]!;
    const isLastDomain = di === domainEntries.length - 1;
    sb.appendLine(`${domain}: {`);
    sb.increaseIndent();

    const categoryEntries = [...domainMap.entries()];
    for (let ci = 0; ci < categoryEntries.length; ci++) {
      const [category, categoryMap] = categoryEntries[ci]!;
      const isLastCategory = ci === categoryEntries.length - 1;
      sb.appendLine(`${category}: {`);
      sb.increaseIndent();

      const constantEntries = [...categoryMap.entries()];
      for (let ki = 0; ki < constantEntries.length; ki++) {
        const [constant, key] = constantEntries[ki]!;
        const isLastConstant = ki === constantEntries.length - 1;
        sb.appendLine(
          `${constant}: tk("${escapeStringLiteral(key)}")${isLastConstant ? "" : ","}`,
        );
      }

      sb.decreaseIndent();
      sb.appendLine(`}${isLastCategory ? "" : ","}`);
    }

    sb.decreaseIndent();
    sb.appendLine(`}${isLastDomain ? "" : ","}`);
  }

  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    "/** Convenience alias for a raw TK key string (e.g. `common_errors_NOT_FOUND`)." +
      " The `TK.*` constants are `TKMessage` instances; read `.key` for the raw string. */",
  );
  sb.appendLine("export type TKKey = string;");
  sb.appendLine();
  return { source: sb.toString(), diagnostics };
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

const SOURCE_PATH = contractsPath("messages", "en-US.json");
const TARGET_PATH = tsPackagePath(
  "i18n-keys",
  "src",
  "generated",
  "tk-keys.g.ts",
);

/**
 * Run the TK keys emitter. Per-source mtime check skips emit when the
 * output is newer than en-US.json; pass `force=true` to bypass.
 */
export function runTkKeysEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SOURCE_PATH])) return [];

  let raw: string;
  try {
    raw = readFileSync(SOURCE_PATH, "utf8");
  } catch (e) {
    return [
      diagError(
        DiagnosticIds.TK_MALFORMED_SOURCE,
        `failed to read en-US.json: ${(e as Error).message}`,
        SOURCE_PATH,
      ),
    ];
  }

  let catalog: MessageCatalog;
  try {
    catalog = JSON.parse(raw) as MessageCatalog;
  } catch (e) {
    return [
      diagError(
        DiagnosticIds.TK_MALFORMED_SOURCE,
        `en-US.json parse failed: ${(e as Error).message}`,
        SOURCE_PATH,
      ),
    ];
  }

  const result = emitTkKeys(catalog);
  // Warnings are non-fatal — emit proceeds even if some keys were skipped.
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("tk-keys-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runTkKeysEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
