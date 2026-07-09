// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Severity level for an emitter diagnostic. `error` blocks generation;
 * `warning` allows generation to proceed but should surface in the
 * orchestrator's exit status.
 */
export type EmitDiagnosticSeverity = "error" | "warning";

/**
 * One diagnostic surfaced during emit. Mirrors the .NET
 * `EmitDiagnostic` record — same `id` / `severity` / `message`
 * (+ optional `filePath`) shape so cross-language tooling reads
 * identical fields.
 */
export interface EmitDiagnostic {
  readonly id: string;
  readonly severity: EmitDiagnosticSeverity;
  readonly message: string;
  readonly filePath?: string;
}

/**
 * Result of an emit pass — generated source plus the diagnostics that
 * fired. Mirrors .NET `EmitResult`.
 */
export interface EmitResult {
  readonly source: string;
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Diagnostic ID prefixes — REUSED from the .NET SourceGen catalogs since
 * the diagnostics describe spec-level violations (one spec → two emitters
 * with identical interpretation = identical diagnostic semantics).
 *
 * Catalog reference (kept here so consumers can `import` for assertions):
 *
 * - `D2CTX001-006`: context spec (auth-context / request-context).
 * - `D2SCP001-009`: auth-scopes spec.
 * - `D2AEC001-005`: auth-error-codes spec.
 * - `D2EC001-005`: error-codes spec (generic D2Result error-code catalog).
 * - `D2ERC001-007`: shared error-codes engine (catalog-neutral): domain-prefix
 *   enforcement (D2ERC001), TK-key existence (D2ERC002), factoryShape support
 *   (D2ERC003), cross-catalog duplicate-code (D2ERC004), reserved-namespace
 *   violation (D2ERC005), malformed registry spec (D2ERC006), unknown category
 *   (D2ERC007). Mirrors the .NET shared `D2ERC*` family from
 *   D2.Shared.ErrorCodes.SourceGen.
 * - `D2HDR001-007`: headers spec.
 * - `D2JWT001-006`: jwt-claims spec.
 * - `D2PRB001-006`: problem-details spec.
 * - `D2GT001-005`: grpc-trailers spec.
 * - `D2OMT001-005`: otel-messaging-tags spec.
 * - `D2ED001-009`: encryption-domains spec (D2ED006-009 = mode /
 *   consumerService validation).
 * - `D2DLQ001-006`: dlq-failure-metadata spec.
 * - `D2EF001-005`: encryption-frame spec (symmetric, version 1).
 * - `D2EF006-012`: encryption-frame-sealed spec (sealed, version 2).
 * - `D2DRE001-005`: d2result-envelope spec.
 * - `D2FC001-009`: field-constraints spec (field-length bounds + taxonomy enums).
 */
export const DiagnosticIds = {
  // Context (auth-context + request-context).
  CTX_DUPLICATE_PROPERTY: "D2CTX001",
  CTX_INVALID_TYPE: "D2CTX002",
  CTX_INVALID_NAMESPACE: "D2CTX003",
  CTX_INVALID_NAME: "D2CTX004",
  CTX_EXTENDS_UNRESOLVED: "D2CTX005",
  CTX_MALFORMED_SPEC: "D2CTX006",

  // Auth scopes.
  SCP_DUPLICATE: "D2SCP001",
  SCP_INVALID_NAME: "D2SCP002",
  SCP_INVALID_SENSITIVITY: "D2SCP003",
  SCP_MALFORMED_SPEC: "D2SCP009",

  // Auth protocol audiences. Mirror the .NET
  // D2.Shared.Auth.ProtocolAudiences.SourceGen DiagnosticIds values byte-for-byte
  // — same spec source on both sides means the same predicate-violation surface,
  // so identical IDs are correct.
  PAUD_DUPLICATE_NAME: "D2PAUD001",
  PAUD_INVALID_NAME: "D2PAUD002",
  PAUD_DUPLICATE_VALUE: "D2PAUD003",
  PAUD_EMPTY_VALUE: "D2PAUD004",
  PAUD_MALFORMED_SPEC: "D2PAUD009",

  // Auth error codes.
  AEC_DUPLICATE_CODE: "D2AEC001",
  AEC_DUPLICATE_FACTORY: "D2AEC002",
  AEC_UNKNOWN_CATEGORY: "D2AEC003",
  AEC_INVALID_HTTP_STATUS: "D2AEC004",
  AEC_MALFORMED_SPEC: "D2AEC005",

  // Generic D2Result error codes. Mirror the .NET
  // D2.Shared.ResultErrorCodes.SourceGen DiagnosticIds values byte-for-byte
  // — same spec source on both sides means same predicate violation surface,
  // so identical IDs are correct.
  EC_MALFORMED_SPEC: "D2EC001",
  EC_DUPLICATE_CODE: "D2EC002",
  EC_INVALID_HTTP_STATUS: "D2EC003",
  EC_INVALID_CODE: "D2EC004",
  EC_MISSING_DOC: "D2EC005",

  // Shared error-codes engine (catalog-neutral). These fire for ANY
  // *-error-codes catalog the unified emitter processes, so they use a
  // catalog-neutral family rather than D2EC*/D2AEC*. Mirror the .NET shared
  // D2.Shared.ErrorCodes.SourceGen D2ERC* ids byte-for-byte — same spec source
  // on both sides means same predicate violation surface so identical IDs are
  // correct. A CI grep for `D2ERC` catches both runtimes.
  ERC_DOMAIN_PREFIX_VIOLATION: "D2ERC001",
  ERC_TK_KEY_NOT_FOUND: "D2ERC002",
  ERC_UNSUPPORTED_FACTORY_SHAPE: "D2ERC003",
  // Registry-level cross-catalog collision checks.
  // D2ERC004 — same `code` declared in two or more catalogs (cross-catalog).
  // D2ERC005 — reserved-namespace violation: unprefixed code in a per-domain
  //             spec, or a domain-prefixed code in the generic spec.
  // D2ERC006 — malformed registry spec: a catalog spec file could not be
  //             parsed as JSON (mirrors the .NET RegistryGenerator D2ERC006).
  // D2ERC007 — unknown category: an entry's category is not in the closed set
  //             from error-category.spec.json (mirrors the .NET D2ERC007).
  ERC_CROSS_CATALOG_DUPLICATE_CODE: "D2ERC004",
  ERC_RESERVED_NAMESPACE_VIOLATION: "D2ERC005",
  ERC_MALFORMED_REGISTRY_SPEC: "D2ERC006",
  ERC_UNKNOWN_CATEGORY: "D2ERC007",

  // Headers.
  HDR_MALFORMED_SPEC: "D2HDR001",
  HDR_UNKNOWN_TRANSPORT: "D2HDR002",
  HDR_INVALID_CONST_NAME: "D2HDR003",
  HDR_DUPLICATE: "D2HDR004",
  HDR_EMPTY_APPLICABILITY: "D2HDR005",
  HDR_UNKNOWN_CONVENTION: "D2HDR006",
  HDR_MISSING_SPEC: "D2HDR007",

  // Jwt claims.
  JWT_MALFORMED_SPEC: "D2JWT001",
  JWT_UNKNOWN_KIND: "D2JWT002",
  JWT_INVALID_CONST_NAME: "D2JWT003",
  JWT_DUPLICATE_CONST_NAME: "D2JWT004",
  JWT_MISSING_SPEC: "D2JWT005",
  JWT_EMPTY_VALUE: "D2JWT006",

  // Problem details. Mirror the .NET D2.Shared.ProblemDetails.SourceGen
  // DiagnosticIds values byte-for-byte — same spec source on both sides
  // means same predicate violation surface so identical IDs are correct.
  PRB_MALFORMED_SPEC: "D2PRB001",
  PRB_DUPLICATE_EXTENSION_KEY_CONST_NAME: "D2PRB002",
  PRB_DUPLICATE_EXTENSION_KEY_VALUE: "D2PRB003",
  PRB_DUPLICATE_TITLE_CONST_NAME: "D2PRB004",
  PRB_DUPLICATE_TITLE_HTTP_STATUS: "D2PRB005",
  PRB_TYPE_URI_PREFIX_MISSING_TRAILING_SLASH: "D2PRB006",

  // Wire shapes (TKMessage + InputError). Mirror the .NET
  // D2.Shared.WireShapes.SourceGen DiagnosticIds values byte-for-byte.
  WS_MALFORMED_SPEC: "D2WS001",
  WS_DUPLICATE_PROPERTY_CONST_NAME: "D2WS002",
  WS_DUPLICATE_PROPERTY_VALUE: "D2WS003",
  WS_INVALID_CONST_NAME: "D2WS004",
  WS_MISSING_SPEC: "D2WS005",

  // gRPC trailers. Mirror the .NET D2.Shared.Grpc.Trailers.SourceGen
  // DiagnosticIds values byte-for-byte — same spec source on both sides
  // means same predicate violation surface so identical IDs are correct.
  GT_MALFORMED_SPEC: "D2GT001",
  GT_DUPLICATE_CONST_NAME: "D2GT002",
  GT_DUPLICATE_VALUE: "D2GT003",
  GT_INVALID_CONST_NAME: "D2GT004",
  GT_EMPTY_VALUE: "D2GT005",

  // OTel messaging activity tags. Mirror the .NET
  // D2.Shared.OtelMessagingTags.SourceGen DiagnosticIds values
  // byte-for-byte — same spec source on both sides.
  OMT_MALFORMED_SPEC: "D2OMT001",
  OMT_DUPLICATE_CONST_NAME: "D2OMT002",
  OMT_DUPLICATE_VALUE: "D2OMT003",
  OMT_INVALID_CONST_NAME: "D2OMT004",
  OMT_EMPTY_VALUE: "D2OMT005",

  // MQ messages descriptor catalog (contracts/mq-messages/). Mirror the .NET
  // D2.Shared.Messaging.SourceGen.MqGenerator DiagnosticIds values — same spec
  // source on both sides means the same predicate-violation surface, so
  // identical IDs are correct. D2MQ003 (duplicate constant) + D2MQ004
  // (missing/unknown encryption default-deny) are named in the spec schema;
  // the remainder allocate consistently in the D2MQ family.
  MQ_MALFORMED_SPEC: "D2MQ001",
  MQ_INVALID_CONST_NAME: "D2MQ002",
  MQ_DUPLICATE_CONSTANT: "D2MQ003",
  MQ_MISSING_ENCRYPTION: "D2MQ004",
  MQ_DUPLICATE_MESSAGE_TYPE: "D2MQ005",
  MQ_EMPTY_VALUE: "D2MQ006",

  // Encryption domains. Mirror the .NET
  // D2.Shared.EncryptionDomains.SourceGen DiagnosticIds values
  // byte-for-byte — same spec source on both sides.
  ED_MALFORMED_SPEC: "D2ED001",
  ED_DUPLICATE_CONST_NAME: "D2ED002",
  ED_DUPLICATE_VALUE: "D2ED003",
  ED_INVALID_CONST_NAME: "D2ED004",
  ED_EMPTY_VALUE: "D2ED005",
  ED_INVALID_MODE: "D2ED006",
  ED_MISSING_CONSUMER_SERVICE: "D2ED007",
  ED_UNEXPECTED_CONSUMER_SERVICE: "D2ED008",
  ED_INVALID_CONSUMER_SERVICE: "D2ED009",

  // DLQ failure metadata (fields + causes sub-catalogs). Mirror the .NET
  // D2.Shared.Messaging.DlqMetadata.SourceGen DiagnosticIds values
  // byte-for-byte — same spec source on both sides.
  DLQ_MALFORMED_SPEC: "D2DLQ001",
  DLQ_DUPLICATE_FIELD_CONST_NAME: "D2DLQ002",
  DLQ_DUPLICATE_FIELD_VALUE: "D2DLQ003",
  DLQ_DUPLICATE_CAUSE: "D2DLQ004",
  DLQ_INVALID_CONST_NAME: "D2DLQ005",
  DLQ_EMPTY_VALUE: "D2DLQ006",

  // Encryption frame binary layout. Mirror the .NET
  // D2.Shared.EncryptionFrame.SourceGen DiagnosticIds values
  // byte-for-byte — same spec source on both sides.
  EF_MALFORMED_SPEC: "D2EF001",
  EF_DUPLICATE_FIELD_NAME: "D2EF002",
  EF_OVERLAPPING_FIELDS: "D2EF003",
  EF_INVALID_LENGTH: "D2EF004",
  EF_INVALID_VERSION: "D2EF005",

  // SEALED encryption frame binary layout (the version-2 sibling spec,
  // contracts/encryption-frame-sealed/). Mirror the .NET
  // D2.Shared.EncryptionFrame.SourceGen sealed-arm DiagnosticIds values
  // byte-for-byte — same spec source on both sides. D2EF010 rejects a
  // sealed spec version < 2 (version 1 is the symmetric frame's
  // discriminator); D2EF011 rejects a field kind outside the closed set the
  // sealed codec reads; D2EF012 enforces the variable_binary_u16be
  // structural rule (the field must sit immediately behind a byte_fixed
  // length prefix of the declared width).
  EFS_MALFORMED_SPEC: "D2EF006",
  EFS_DUPLICATE_FIELD_NAME: "D2EF007",
  EFS_OVERLAPPING_FIELDS: "D2EF008",
  EFS_INVALID_LENGTH: "D2EF009",
  EFS_INVALID_VERSION: "D2EF010",
  EFS_UNKNOWN_FIELD_KIND: "D2EF011",
  EFS_BINARY_LENGTH_PREFIX_MISSING: "D2EF012",

  // D2Result envelope (Shape B field names). Mirror the .NET
  // D2.Shared.Result.Envelope.SourceGen DiagnosticIds values
  // byte-for-byte — same spec source on both sides means same predicate
  // violation surface so identical IDs are correct.
  DRE_MALFORMED_SPEC: "D2DRE001",
  DRE_DUPLICATE_FIELD_CONST_NAME: "D2DRE002",
  DRE_DUPLICATE_FIELD_VALUE: "D2DRE003",
  DRE_INVALID_CONST_NAME: "D2DRE004",
  DRE_EMPTY_VALUE: "D2DRE005",

  // Geo catalogs. Mirror the .NET D2.Shared.Geo.SourceGen DiagnosticIds
  // values byte-for-byte — same spec source on both sides means same
  // predicate violation surface so identical IDs are correct.
  GEO_MALFORMED_SPEC: "D2GEO001",
  GEO_UNKNOWN_FK: "D2GEO002",
  GEO_FK_AMBIGUITY: "D2GEO003",
  GEO_INVALID_IDENTIFIER: "D2GEO004",
  GEO_VOCABULARY_VIOLATION: "D2GEO005",
  GEO_MISSING_CATALOG_METADATA: "D2GEO006",
  GEO_MISSING_SPEC: "D2GEO007",
  GEO_LOCALE_MESSAGE_MISMATCH: "D2GEO008",
  GEO_STRUCTURAL_PARITY_MISMATCH: "D2GEO009",

  // Catalog uniqueness — codegen-time assertion enforcing the fail-closed
  // name-resolver requirement that each catalog has unique normalized
  // names across its matchable name fields (so exact-match Pass-1 cannot
  // be ambiguous). Build fails on duplicate normalized names. Allocated
  // as D2GEO010 — the next slot after the .NET DiagnosticIds catalog
  // (which currently ends at D2GEO009); .NET will pick up the same ID
  // when it adds the equivalent uniqueness check.
  GEO_CATALOG_DUPLICATE_NAME: "D2GEO010",

  // CLDR-zombie subdivision codes — Tier 1 transformer drops codes that
  // CLDR still ships but debian/iso-codes no longer considers current ISO
  // 3166-2 codes (post-reassignment retirements that CLDR didn't catch
  // up to). Surfaces as a warning + per-code log entry so the operator
  // can confirm what was dropped on each refresh and add an overlay
  // override only when a Wikidata.en label is also wrong.
  // TS-only — the data pipeline doesn't exist on the .NET side, so the
  // .NET DiagnosticIds catalog skips D2GEO011 and resumes the shared
  // numbering at D2GEO012.
  GEO_CLDR_ZOMBIE_DROPPED: "D2GEO011",

  // Country references a locale tag absent from locales.spec.json
  // (via primaryLocaleIETFBCP47Tag or localeIETFBCP47Tags[]). Mirrors the
  // .NET-side D2GEO012 — same spec source on both sides means same
  // predicate surface. Build-time gate so the data emitters can use
  // direct indexer access (fail-loud) instead of defensive lookups that
  // mask drift between catalogs.
  GEO_MISSING_LOCALE_REFERENCE: "D2GEO012",

  // i18n TK keys catalog. Mirrors the .NET
  // D2.Shared.I18n.SourceGen.KeyDecomposer diagnostic IDs byte-for-byte —
  // same en-US.json source on both sides means same predicate violation
  // surface so identical IDs are correct.
  TK_MALFORMED_SOURCE: "D2TK001",
  TK_INVALID_KEY: "D2TK002",

  // Field-constraints catalog (field-length bounds + taxonomy enums). Mirror
  // the .NET D2.Shared.Validation.SourceGen DiagnosticIds values byte-for-byte
  // — same spec source on both sides means same predicate violation surface so
  // identical IDs are correct.
  FC_MALFORMED_SPEC: "D2FC001",
  FC_DUPLICATE_CONST_NAME: "D2FC002",
  FC_INVALID_CONST_NAME: "D2FC003",
  FC_NON_POSITIVE_VALUE: "D2FC004",
  FC_DUPLICATE_ENUM_NAME: "D2FC005",
  FC_INVALID_ENUM_NAME: "D2FC006",
  FC_EMPTY_ENUM_MEMBER_LIST: "D2FC007",
  FC_DUPLICATE_ENUM_MEMBER: "D2FC008",
  FC_INVALID_ENUM_MEMBER_NAME: "D2FC009",

  // Error category catalog (the nine closed ErrorCategory wire strings).
  // Mirror the .NET D2.Shared.ErrorCodes.Category.SourceGen DiagnosticIds
  // values byte-for-byte — same spec source on both sides.
  ECAT_MALFORMED_SPEC: "D2ECAT001",
  ECAT_DUPLICATE_WIRE: "D2ECAT002",
  ECAT_INVALID_WIRE: "D2ECAT003",
  ECAT_EMPTY_DOC: "D2ECAT004",
} as const;

/**
 * Construct an `error`-severity diagnostic.
 */
export function diagError(
  id: string,
  message: string,
  filePath?: string,
): EmitDiagnostic {
  return filePath === undefined
    ? { id, severity: "error", message }
    : { id, severity: "error", message, filePath };
}

/**
 * Construct a `warning`-severity diagnostic.
 */
export function diagWarning(
  id: string,
  message: string,
  filePath?: string,
): EmitDiagnostic {
  return filePath === undefined
    ? { id, severity: "warning", message }
    : { id, severity: "warning", message, filePath };
}

/**
 * Pretty-print one diagnostic for console output.
 */
export function formatDiagnostic(d: EmitDiagnostic): string {
  const loc = d.filePath !== undefined ? ` ${d.filePath}` : "";
  return `${d.severity.toUpperCase()} ${d.id}${loc}: ${d.message}`;
}
