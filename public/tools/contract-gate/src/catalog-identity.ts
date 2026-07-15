// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Catalog-identity registry for the spec/i18n arm.
//
// Each `*.spec.json` catalog is an array of objects keyed by an IDENTITY
// field whose value uniquely identifies an entry. The identity field is NOT
// the array index — reorder is not a break. This module maps a spec file
// path (matched by suffix/basename) to its identity descriptor.
//
// FAIL-LOUD on an unregistered catalog: an unknown spec file must NOT
// silently pass the gate (strict + fail-loud per the codebase convention).
//
// Geo Tier-2 `$generated` specs are explicitly EXEMPT — they are regenerable
// pipeline outputs governed by their own drift-guard (§26.5).

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Describes how to identify entries in a flat array-of-objects catalog. */
export interface FlatCatalogIdentity {
  readonly kind: "flat";
  /** The JSON property name of the top-level array, e.g. "errorCodes". */
  readonly arrayProp: string;
  /** The property on each entry whose value is the unique identifier, e.g. "code". */
  readonly idField: string;
}

/**
 * Describes a nested catalog where the identity is composite:
 * a top-level array, each entry having a nested array, etc.
 *
 * Used for the telemetry catalog (meters → instruments → tags → values).
 */
export interface NestedCatalogIdentity {
  readonly kind: "nested";
  /** The JSON property name of the top-level array, e.g. "meters". */
  readonly arrayProp: string;
  /** The property on each top-level entry that serves as its ID, e.g. "meter". */
  readonly idField: string;
  /** Nested level descriptor. */
  readonly nested: FlatCatalogIdentity & {
    /** Deeply nested level — for tags inside instruments. */
    readonly nested?: FlatCatalogIdentity & {
      /** Tag values array property (flat string array, not object array). */
      readonly valuesArrayProp?: string;
    };
  };
}

/**
 * Describes a spec document that exposes multiple independently-gated catalogs
 * as sibling arrays at the document root. Each member of `parts` is diffed
 * independently — a break in ANY part produces a finding.
 *
 * Used for specs that carry more than one wire-contract surface in the same
 * file (e.g. `field-constraints.spec.json` has `constraints[]` + `enums[]`,
 * `problem-details.spec.json` has `extensionKeys[]` + `titles[]`).
 */
export interface MultiCatalogIdentity {
  readonly kind: "multi";
  /** Each part is diffed as an independent flat or nested catalog. */
  readonly parts: ReadonlyArray<FlatCatalogIdentity | NestedCatalogIdentity>;
}

/** Marks a spec file as generated / exempt from gate enforcement. */
export interface ExemptCatalog {
  readonly kind: "exempt";
  readonly reason: string;
}

export type CatalogIdentity =
  | FlatCatalogIdentity
  | NestedCatalogIdentity
  | MultiCatalogIdentity
  | ExemptCatalog;

// ---------------------------------------------------------------------------
// Registry
// ---------------------------------------------------------------------------

/**
 * Registry mapping spec file path suffixes to their catalog identity
 * descriptors. The match is performed by the LAST path segment (basename)
 * or by a path pattern that uniquely identifies the catalog.
 *
 * Entries are matched in order; first match wins. An unregistered spec
 * file causes the gate to FAIL-LOUD (not silently pass).
 */
const REGISTRY: ReadonlyArray<{
  readonly match: (filePath: string) => boolean;
  readonly identity: CatalogIdentity;
}> = [
  // ── Geo Tier-2 generated specs ──────────────────────────────────────────
  // These carry `"$generated": true` — regenerable pipeline outputs, exempt.
  {
    match: (p) =>
      p.includes("/geo/") &&
      p.endsWith(".spec.json") &&
      !p.includes("/src-data/") &&
      !p.includes("/overlays/"),
    identity: {
      kind: "exempt",
      reason:
        "Geo Tier-2 $generated spec — regenerable pipeline output governed by its own drift-guard; not a hand-authored wire contract",
    },
  },
  // Geo src-data and overlays are also pipeline artifacts
  {
    match: (p) => p.includes("/geo/src-data/") || p.includes("/geo/overlays/"),
    identity: {
      kind: "exempt",
      reason:
        "Geo pipeline source or overlay data — not a published wire contract",
    },
  },

  // ── Error codes catalogs ─────────────────────────────────────────────────
  {
    match: (p) => p.endsWith("error-codes.spec.json"),
    identity: { kind: "flat", arrayProp: "errorCodes", idField: "code" },
  },

  // ── Auth scopes ──────────────────────────────────────────────────────────
  {
    match: (p) => p.endsWith("scopes.spec.json"),
    identity: { kind: "flat", arrayProp: "scopes", idField: "name" },
  },

  // ── Headers ──────────────────────────────────────────────────────────────
  // Headers carry both `name` (the HTTP header name, e.g. "Authorization") and
  // `constName` (e.g. "AUTHORIZATION"). `name` is the stable wire-visible identity.
  {
    match: (p) => p.endsWith("headers.spec.json"),
    identity: { kind: "flat", arrayProp: "headers", idField: "name" },
  },

  // ── Telemetry (nested: meters → instruments → tags → values) ────────────
  {
    match: (p) => p.endsWith("telemetry.spec.json"),
    identity: {
      kind: "nested",
      arrayProp: "meters",
      idField: "meter",
      nested: {
        kind: "flat",
        arrayProp: "instruments",
        idField: "name",
        nested: {
          kind: "flat",
          arrayProp: "tags",
          idField: "name",
          valuesArrayProp: "values",
        },
      },
    },
  },

  // ── Auth audiences ───────────────────────────────────────────────────────
  {
    match: (p) => p.endsWith("audiences.spec.json"),
    identity: { kind: "flat", arrayProp: "audiences", idField: "name" },
  },

  // ── JWT claims ───────────────────────────────────────────────────────────
  // Identity field is `constName` (e.g. "SUB") — the stable generated constant.
  // `value` is the JWT wire claim name (e.g. "sub"), which is also stable but
  // constName is the primary index used by consumers and generated code.
  {
    match: (p) => p.endsWith("jwt-claims.spec.json"),
    identity: { kind: "flat", arrayProp: "claims", idField: "constName" },
  },

  // ── gRPC trailers ────────────────────────────────────────────────────────
  // Identity field is `constName` (e.g. "ERROR_CODE") — entries have no `name`.
  {
    match: (p) => p.endsWith("grpc-trailers.spec.json"),
    identity: { kind: "flat", arrayProp: "trailers", idField: "constName" },
  },

  // ── OTEL messaging tags ──────────────────────────────────────────────────
  // Identity field is `constName` (e.g. "MESSAGING_SYSTEM") — entries have no `name`.
  {
    match: (p) => p.endsWith("otel-messaging-tags.spec.json"),
    identity: { kind: "flat", arrayProp: "tags", idField: "constName" },
  },

  // ── Encryption domains ───────────────────────────────────────────────────
  // Identity field is `constName` (e.g. "AUDIT") — entries have no `name`.
  {
    match: (p) => p.endsWith("encryption-domains.spec.json"),
    identity: { kind: "flat", arrayProp: "domains", idField: "constName" },
  },

  // ── In-process keys ──────────────────────────────────────────────────────
  // Identity field is `constName` (e.g. "REQUEST_CONTEXT") — entries have no `name`.
  {
    match: (p) => p.endsWith("keys.spec.json"),
    identity: { kind: "flat", arrayProp: "keys", idField: "constName" },
  },

  // ── MQ messages ──────────────────────────────────────────────────────────
  // Identity field is `constant` (e.g. "AuthKeyRotated") — entries have no `name`.
  {
    match: (p) => p.endsWith("mq-messages.spec.json"),
    identity: { kind: "flat", arrayProp: "messages", idField: "constant" },
  },

  // ── MQ subscriptions ─────────────────────────────────────────────────────
  // Identity field is `constant` (e.g. "KeyringRefresh") — entries have no `name`.
  {
    match: (p) => p.endsWith("mq-subscriptions.spec.json"),
    identity: { kind: "flat", arrayProp: "subscriptions", idField: "constant" },
  },

  // ── Advisory locks ───────────────────────────────────────────────────────
  // Identity field is `constName` (e.g. "MIGRATOR") — NOT `name`. The lock
  // entries carry a human-readable key (`constName`) + a numeric PG advisory-lock
  // key (`key`). The constName is the stable identity for diff purposes.
  {
    match: (p) => p.endsWith("advisory-locks.spec.json"),
    identity: { kind: "flat", arrayProp: "locks", idField: "constName" },
  },

  // ── DLQ failure metadata ─────────────────────────────────────────────────
  // Two independently-gated surfaces in the same document:
  //   • fields[]  — the JSON property names written into the DLQ failure
  //                 metadata object (identity: constName).
  //   • causes[]  — the closed enum of failure categories that consumers
  //                 branch on (e.g. HANDLER_RESULT_FAILURE, DECRYPT_FAILURE).
  //                 Removing a cause value is a consumer-visible wire break;
  //                 consumers that match on the cause string will silently
  //                 fall through to their default path on a deleted value.
  //                 Identity: constName (e.g. "HANDLER_EXCEPTION").
  {
    match: (p) => p.endsWith("dlq-failure-metadata.spec.json"),
    identity: {
      kind: "multi",
      parts: [
        { kind: "flat", arrayProp: "fields", idField: "constName" },
        { kind: "flat", arrayProp: "causes", idField: "constName" },
      ],
    },
  },

  // ── Encryption frame (symmetric v1 + sealed v2) ──────────────────────────
  // Identity field is `constName` (e.g. "VERSION") — entries have no `name`.
  {
    match: (p) => p.endsWith("encryption-frame.spec.json"),
    identity: { kind: "flat", arrayProp: "fields", idField: "constName" },
  },
  // Sealed frame is a sibling catalog (version 2 ECDH-ES fields). Same shape.
  {
    match: (p) => p.endsWith("encryption-frame-sealed.spec.json"),
    identity: { kind: "flat", arrayProp: "fields", idField: "constName" },
  },

  // ── Input error ──────────────────────────────────────────────────────────
  // Array prop is `properties`, identity field is `constName` (e.g. "FIELD").
  // Entries have no `name` and are not under a `fields` array.
  {
    match: (p) => p.endsWith("input-error.spec.json"),
    identity: { kind: "flat", arrayProp: "properties", idField: "constName" },
  },

  // ── TK message ───────────────────────────────────────────────────────────
  // Array prop is `properties`, identity field is `constName` (e.g. "KEY").
  // Entries have no `name` and are not under a `fields` array.
  {
    match: (p) => p.endsWith("tk-message.spec.json"),
    identity: { kind: "flat", arrayProp: "properties", idField: "constName" },
  },

  // ── D2Result envelope ────────────────────────────────────────────────────
  // Identity field is `constName` (e.g. "SUCCESS") — entries have no `name`.
  {
    match: (p) => p.endsWith("d2result-envelope.spec.json"),
    identity: { kind: "flat", arrayProp: "fields", idField: "constName" },
  },

  // ── Problem details ──────────────────────────────────────────────────────
  // Two independently-gated surfaces in the same document:
  //   • extensionKeys[] — wire-facing JSON property names added to RFC 9457
  //                       ProblemDetails objects (identity: constName).
  //   • titles[]        — closed HTTP-status → RFC 9457 title map shipped to
  //                       clients; removing a title or renaming its value breaks
  //                       consumers that branch on the title string (identity: constName).
  // Both arrays use `constName` as the entry identity.
  {
    match: (p) => p.endsWith("problem-details.spec.json"),
    identity: {
      kind: "multi",
      parts: [
        { kind: "flat", arrayProp: "extensionKeys", idField: "constName" },
        { kind: "flat", arrayProp: "titles", idField: "constName" },
      ],
    },
  },

  // ── Error category ───────────────────────────────────────────────────────
  // Identity field is `wire` (e.g. "validation_failure") — entries have no `name`.
  {
    match: (p) => p.endsWith("error-category.spec.json"),
    identity: { kind: "flat", arrayProp: "categories", idField: "wire" },
  },

  // ── Field constraints ─────────────────────────────────────────────────────
  // Two independently-gated surfaces in the same document:
  //   • constraints[] — flat catalog of numeric/string bounds (identity: name).
  //   • enums[]       — closed-enum taxonomies (identity: name), each carrying a
  //                     nested members[] array (identity: name). Removing a member
  //                     is a wire break — generated serializers and consumers bind
  //                     to the closed list. Both surfaces are gated independently.
  {
    match: (p) => p.endsWith("field-constraints.spec.json"),
    identity: {
      kind: "multi",
      parts: [
        { kind: "flat", arrayProp: "constraints", idField: "name" },
        {
          kind: "nested",
          arrayProp: "enums",
          idField: "name",
          nested: { kind: "flat", arrayProp: "members", idField: "name" },
        },
      ],
    },
  },

  // ── Auth context / request context (interface specs) ─────────────────────
  // These specs use a `sections[]` top-level array (each section has a `name`
  // and a nested `properties[]` array). The gate enforces the section names and
  // the nested property names via the nested catalog identity descriptor.
  {
    match: (p) =>
      p.endsWith("IAuthContext.spec.json") ||
      p.endsWith("IRequestContext.spec.json"),
    identity: {
      kind: "nested",
      arrayProp: "sections",
      idField: "name",
      nested: {
        kind: "flat",
        arrayProp: "properties",
        idField: "name",
      },
    },
  },
];

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Look up the catalog identity descriptor for a spec file.
 *
 * @param filePath - Path to the spec file (forward or back slashes; relative
 *   or absolute). The registry matches on the normalized forward-slash form.
 * @returns The catalog identity descriptor.
 * @throws {Error} When no registry entry matches the file — fail-loud so an
 *   unregistered catalog cannot silently pass the gate.
 */
export function getCatalogIdentity(filePath: string): CatalogIdentity {
  // Normalize backslashes to forward slashes for cross-platform matching.
  const normalized = filePath.replace(/\\/g, "/");

  for (const entry of REGISTRY) {
    if (entry.match(normalized)) return entry.identity;
  }

  throw new Error(
    `[contract-gate] unregistered spec catalog: '${filePath}'\n` +
      `Every *.spec.json file must be registered in tools/contract-gate/src/catalog-identity.ts\n` +
      `with its array property name and identity field so the breaking-change gate can diff it.\n` +
      `Add an entry to REGISTRY or mark the catalog as exempt if it is a regenerable output.`,
  );
}
