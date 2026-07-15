// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { buildHeader } from "../lib/file-emit.js";
import { tsPackagePath } from "../lib/paths.js";
import { StringBuilder } from "../lib/string-builder.js";

import {
  appendEslintDisable,
  appendJsDoc,
  escapeStringLiteral,
} from "./emit-helpers.js";
import type { GeoSpecContext } from "./spec-types.js";

/**
 * Emit `geo-catalog.g.ts` — the version + timestamp constants describing the
 * catalog snapshot that the rest of the generated files were emitted from.
 * Mirrors .NET `GeoCatalog` (which uses `DateTimeOffset` for the published-at
 * field); the TS side emits an ISO-8601 string for cross-runtime parity (TS
 * lacks a `DateTimeOffset` primitive; consumers parse via
 * `Temporal.Instant.from(CATALOG_PUBLISHED_AT)` if needed).
 *
 * Drives consumer-side cache-busting (`if (cachedVersion !== CATALOG_VERSION)
 * { rebuild(); }`) plus surface-area visibility on which spec snapshot was
 * baked into the package.
 */

export function emitGeoCatalog(context: GeoSpecContext): {
  readonly path: string;
  readonly source: string;
} {
  // Source the catalog metadata from the same precedence chain the .NET
  // GeoCatalogEmitter uses (countries → subdivisions → currencies →
  // languages → locales → timezones → geopoliticalEntities) so both
  // runtimes pick the same provenance row even when an earlier catalog is
  // absent. Every pipeline-derived spec is regenerated in lockstep, so the
  // first non-null `generatedAt` is the canonical publication timestamp.
  const md =
    context.countries?.metadata ??
    context.subdivisions?.metadata ??
    context.currencies?.metadata ??
    context.languages?.metadata ??
    context.locales?.metadata ??
    context.timezones?.metadata ??
    context.geopoliticalEntities?.metadata;
  const catalogVersion = md?.catalogVersion ?? "0.0.0";
  const publishedAt =
    md?.generatedAt ?? md?.lastEditedAt ?? "1970-01-01T00:00:00Z";

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/*.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "Semantic version of the catalog snapshot baked into the generated geo",
      "types + data. Bumped when any catalog adds, removes, or modifies",
      "entries; surfaced to consumers for cache-busting + on-disk metadata.",
    ].join("\n"),
  );
  sb.appendLine(
    `export const CATALOG_VERSION = "${escapeStringLiteral(catalogVersion)}" as const;`,
  );
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "ISO-8601 timestamp recording when the underlying spec was last",
      "regenerated. TS-side equivalent of the .NET `DateTimeOffset",
      "CatalogPublishedAt` constant — wire-equivalent string format on both",
      "runtimes; Temporal-API consumers can parse via",
      "`Temporal.Instant.from(CATALOG_PUBLISHED_AT)`.",
    ].join("\n"),
  );
  sb.appendLine(
    `export const CATALOG_PUBLISHED_AT = "${escapeStringLiteral(publishedAt)}" as const;`,
  );
  sb.appendLine();
  return {
    path: tsPackagePath(
      "geo",
      "abstractions",
      "src",
      "generated",
      "geo-catalog.g.ts",
    ),
    source: sb.toString(),
  };
}
