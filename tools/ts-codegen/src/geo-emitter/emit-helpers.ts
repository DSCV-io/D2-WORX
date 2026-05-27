// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { StringBuilder } from "../lib/string-builder.js";

/**
 * Shared helpers used by every geo per-entity emitter. Centralised so the
 * output shape stays consistent across enum / wrapper-code / record-shape /
 * geo-catalog files.
 */

/** Escape a JSON-safe string for emission inside a TS double-quoted literal. */
export function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

/** Escape a docstring line so a literal end-of-comment sequence (an asterisk
 * followed by a slash) inside the comment won't close it. */
export function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

/** Emit a JSDoc block from a multi-line string. */
export function appendJsDoc(sb: StringBuilder, doc: string): void {
  const lines = doc.split("\n");
  sb.appendLine("/**");
  for (const l of lines) sb.appendLine(" * " + escapeJsDoc(l));
  sb.appendLine(" */");
}

/**
 * Coerce an identifier-like string into a safe TS member name. Used when an
 * ISO code (e.g. "01") cannot be a bare property name — preserves digits but
 * prefixes them with an underscore. Geo catalogs use strings that are
 * already identifier-safe in almost every case (ISO alpha codes, BCP-47
 * tags); this helper handles the few numeric-prefixed exceptions.
 */
export function safeKey(value: string): string {
  if (/^[A-Za-z_][A-Za-z0-9_]*$/.test(value)) return value;
  // BCP-47 tags / IANA identifiers / subdivision codes contain hyphens /
  // slashes / digits — wrap in quotes for the const-object key.
  return '"' + escapeStringLiteral(value) + '"';
}

/**
 * Emit the boilerplate disabling ESLint over a generated file. Mirrors the
 * pattern used by every other emitter under tools/ts-codegen/src/.
 */
export function appendEslintDisable(sb: StringBuilder): void {
  sb.appendLine("/* eslint-disable */");
}
