// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";
import {
  aggregateAndCheck,
  discoverCatalogs,
  emitErrorCodeRegistry,
  type CatalogEntry,
} from "../src/error-codes-registry-emit.js";
import { DiagnosticIds } from "../src/lib/diagnostics.js";
import { contractsPath } from "../src/lib/paths.js";

// ---------------------------------------------------------------------------
// Shared minimal catalog fixtures — deliberately synthetic so the tests never
// depend on real spec content (no spec-edit fragility, no D2ERC002 TK
// cross-checks against en-US.json — supply no enUsKeys to skip that gate).
// ---------------------------------------------------------------------------

const genericEntry = {
  code: "NOT_FOUND",
  httpStatus: 404,
  category: "not_found" as const,
  userMessageKey: "TK.Common.Errors.NOT_FOUND",
  factoryName: "NotFound",
  factoryShape: "standard" as const,
  doc: "Not found.",
};

const authEntry = {
  code: "AUTH_BEARER_MISSING",
  httpStatus: 401,
  category: "validation_failure" as const,
  userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
  factoryName: "BearerMissing",
  factoryShape: "with_error_code" as const,
  doc: "Bearer missing.",
};

const genericCatalog: CatalogEntry = {
  specPath: "contracts/error-codes/error-codes.spec.json",
  domain: "common",
  entries: [genericEntry],
};

const authCatalog: CatalogEntry = {
  specPath: "contracts/auth-error-codes/auth-error-codes.spec.json",
  domain: "auth",
  entries: [authEntry],
};

// ---------------------------------------------------------------------------
// aggregateAndCheck — collision + namespace enforcement
// ---------------------------------------------------------------------------

describe("aggregateAndCheck — happy path", () => {
  it("accepts disjoint catalogs and produces all entries", () => {
    const { entries, diagnostics } = aggregateAndCheck([
      genericCatalog,
      authCatalog,
    ]);
    expect(diagnostics).toEqual([]);
    expect(entries).toHaveLength(2);
    expect(entries.map((e) => e.code)).toContain("NOT_FOUND");
    expect(entries.map((e) => e.code)).toContain("AUTH_BEARER_MISSING");
  });

  it("annotates each entry with the correct domain token", () => {
    const { entries } = aggregateAndCheck([genericCatalog, authCatalog]);
    const generic = entries.find((e) => e.code === "NOT_FOUND");
    const auth = entries.find((e) => e.code === "AUTH_BEARER_MISSING");
    expect(generic?.domain).toBe("common");
    expect(auth?.domain).toBe("auth");
  });

  it("accepts empty catalog list", () => {
    const { entries, diagnostics } = aggregateAndCheck([]);
    expect(diagnostics).toEqual([]);
    expect(entries).toHaveLength(0);
  });
});

describe("aggregateAndCheck — D2ERC004 cross-catalog duplicate code", () => {
  it("fires D2ERC004 when the same code appears in two catalogs", () => {
    // Plant the SAME code in both the generic and a synthetic per-domain catalog.
    // Use `AUTH_DUPE` for the per-domain catalog so the prefix check passes.
    const dupAuthCatalog: CatalogEntry = {
      specPath: "contracts/auth-error-codes/auth-error-codes.spec.json",
      domain: "auth",
      entries: [
        {
          code: "AUTH_DUPE",
          httpStatus: 401,
          category: "validation_failure" as const,
          userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
          factoryName: "Dupe",
          factoryShape: "with_error_code" as const,
          doc: "Dupe.",
        },
      ],
    };
    const dupAuthCatalog2: CatalogEntry = {
      specPath: "contracts/auth-error-codes/auth-other.spec.json",
      domain: "auth",
      entries: [
        {
          code: "AUTH_DUPE",
          httpStatus: 401,
          category: "policy_denied" as const,
          userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
          factoryName: "Dupe2",
          factoryShape: "with_error_code" as const,
          doc: "Dupe again.",
        },
      ],
    };
    const { diagnostics } = aggregateAndCheck([
      dupAuthCatalog,
      dupAuthCatalog2,
    ]);
    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0]?.id).toBe(
      DiagnosticIds.ERC_CROSS_CATALOG_DUPLICATE_CODE,
    );
    expect(diagnostics[0]?.message).toContain("AUTH_DUPE");
    expect(diagnostics[0]?.message).toContain("D2ERC004");
  });

  it("fires D2ERC004 when the same generic code appears in the generic spec twice (via two catalog entries)", () => {
    // This models someone adding a duplicate in the same domain catalog list.
    const dupGenericCatalog1: CatalogEntry = {
      specPath: "contracts/error-codes/error-codes.spec.json",
      domain: "common",
      entries: [
        {
          code: "CONFLICT",
          httpStatus: 409,
          category: "conflict" as const,
          userMessageKey: "TK.Common.Errors.CONFLICT",
          factoryName: "Conflict",
          factoryShape: "standard" as const,
          doc: "Conflict.",
        },
      ],
    };
    const dupGenericCatalog2: CatalogEntry = {
      specPath: "contracts/other-error-codes/other.spec.json",
      domain: "common",
      entries: [
        {
          code: "CONFLICT",
          httpStatus: 409,
          category: "conflict" as const,
          userMessageKey: "TK.Common.Errors.CONFLICT",
          factoryName: "Conflict2",
          factoryShape: "standard" as const,
          doc: "Conflict again.",
        },
      ],
    };
    const { diagnostics } = aggregateAndCheck([
      dupGenericCatalog1,
      dupGenericCatalog2,
    ]);
    expect(
      diagnostics.some(
        (d) => d.id === DiagnosticIds.ERC_CROSS_CATALOG_DUPLICATE_CODE,
      ),
    ).toBe(true);
  });

  it("no registry emitted when D2ERC004 fires", () => {
    const dupAuthA: CatalogEntry = {
      specPath: "contracts/auth-error-codes/a.spec.json",
      domain: "auth",
      entries: [authEntry],
    };
    const dupAuthB: CatalogEntry = {
      specPath: "contracts/auth-error-codes/b.spec.json",
      domain: "auth",
      entries: [{ ...authEntry, factoryName: "Other" }],
    };
    const result = emitErrorCodeRegistry([dupAuthA, dupAuthB]);
    expect(result.source).toBe("");
    expect(
      result.diagnostics.some(
        (d) => d.id === DiagnosticIds.ERC_CROSS_CATALOG_DUPLICATE_CODE,
      ),
    ).toBe(true);
  });
});

describe("aggregateAndCheck — D2ERC005 reserved-namespace violation", () => {
  it("fires D2ERC005 (b) when a per-domain-prefixed code appears in the generic spec", () => {
    // A code starting with "AUTH_" in the generic catalog is a violation.
    const badGenericCatalog: CatalogEntry = {
      specPath: "contracts/error-codes/error-codes.spec.json",
      domain: "common",
      entries: [
        {
          code: "AUTH_SNEAKY",
          httpStatus: 401,
          category: "policy_denied" as const,
          userMessageKey: "TK.Common.Errors.UNAUTHORIZED",
          factoryName: "AuthSneaky",
          factoryShape: "with_error_code" as const,
          doc: "Sneaky auth code in the generic catalog.",
        },
      ],
    };
    // authCatalog provides the AUTH_ prefix detection.
    const { diagnostics } = aggregateAndCheck([badGenericCatalog, authCatalog]);
    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0]?.id).toBe(
      DiagnosticIds.ERC_RESERVED_NAMESPACE_VIOLATION,
    );
    expect(diagnostics[0]?.message).toContain("AUTH_SNEAKY");
    expect(diagnostics[0]?.message).toContain("D2ERC005");
  });

  it("fires D2ERC005 (a) when a per-domain spec declares a code without the required domain prefix", () => {
    const badAuthCatalog: CatalogEntry = {
      specPath: "contracts/auth-error-codes/auth-error-codes.spec.json",
      domain: "auth",
      entries: [
        {
          code: "NOT_PREFIXED",
          httpStatus: 401,
          category: "policy_denied" as const,
          userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
          factoryName: "NotPrefixed",
          factoryShape: "with_error_code" as const,
          doc: "Missing prefix.",
        },
      ],
    };
    const { diagnostics } = aggregateAndCheck([genericCatalog, badAuthCatalog]);
    expect(diagnostics).toHaveLength(1);
    expect(diagnostics[0]?.id).toBe(
      DiagnosticIds.ERC_RESERVED_NAMESPACE_VIOLATION,
    );
    expect(diagnostics[0]?.message).toContain("NOT_PREFIXED");
    expect(diagnostics[0]?.message).toContain("D2ERC005");
  });

  it("no registry emitted when D2ERC005 fires", () => {
    const bad: CatalogEntry = {
      specPath: "contracts/auth-error-codes/auth-error-codes.spec.json",
      domain: "auth",
      entries: [
        {
          code: "UNPREFIXED",
          httpStatus: 401,
          category: "validation_failure" as const,
          userMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
          factoryName: "Unprefixed",
          factoryShape: "with_error_code" as const,
          doc: "No prefix.",
        },
      ],
    };
    const result = emitErrorCodeRegistry([genericCatalog, bad]);
    expect(result.source).toBe("");
    expect(
      result.diagnostics.some(
        (d) => d.id === DiagnosticIds.ERC_RESERVED_NAMESPACE_VIOLATION,
      ),
    ).toBe(true);
  });

  it("generic spec codes with underscores that are NOT domain-prefixed are allowed", () => {
    // NOT_FOUND, SERVICE_UNAVAILABLE, VALIDATION_FAILED etc. have underscores
    // but are NOT domain-prefixed (no known per-domain prefix matches).
    // Provide only the generic catalog (no per-domain catalogs → no prefixes to violate).
    const { entries, diagnostics } = aggregateAndCheck([
      {
        specPath: "contracts/error-codes/error-codes.spec.json",
        domain: "common",
        entries: [
          {
            code: "SERVICE_UNAVAILABLE",
            httpStatus: 503,
            category: "infrastructure_unavailable" as const,
            userMessageKey: "TK.Common.Errors.SERVICE_UNAVAILABLE",
            factoryName: "ServiceUnavailable",
            factoryShape: "with_error_code" as const,
            doc: "Service unavailable.",
          },
          {
            code: "VALIDATION_FAILED",
            httpStatus: 400,
            category: "validation_failure" as const,
            userMessageKey: "TK.Common.Errors.VALIDATION_FAILED",
            factoryName: "ValidationFailed",
            factoryShape: "validation" as const,
            doc: "Validation failed.",
          },
        ],
      },
    ]);
    expect(diagnostics).toEqual([]);
    expect(entries).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// emitErrorCodeRegistry — source output
// ---------------------------------------------------------------------------

describe("emitErrorCodeRegistry — source output", () => {
  it("emits the auto-generated header banner", () => {
    const result = emitErrorCodeRegistry([genericCatalog]);
    expect(result.source).toContain("<auto-generated>");
    expect(result.source).toContain("Manual edits will be lost on rebuild.");
  });

  it("emits eslint-disable comment", () => {
    const result = emitErrorCodeRegistry([genericCatalog]);
    expect(result.source).toContain("/* eslint-disable */");
  });

  it("imports TK from @d2/i18n-keys", () => {
    const result = emitErrorCodeRegistry([genericCatalog]);
    expect(result.source).toContain('import { TK } from "@d2/i18n-keys";');
  });

  it("imports buildRegistry from the sibling source file", () => {
    const result = emitErrorCodeRegistry([genericCatalog]);
    expect(result.source).toContain(
      'import { buildRegistry, type ErrorCodeInfo } from "../error-code-registry.js";',
    );
  });

  it("emits all 8 fields for each entry", () => {
    const result = emitErrorCodeRegistry([genericCatalog, authCatalog]);
    expect(result.diagnostics).toEqual([]);
    // Generic entry
    expect(result.source).toContain('code: "NOT_FOUND"');
    expect(result.source).toContain("httpStatus: 404");
    expect(result.source).toContain('category: "not_found"');
    expect(result.source).toContain(
      "userMessageKey: TK.common.errors.NOT_FOUND",
    );
    expect(result.source).toContain('factoryName: "NotFound"');
    expect(result.source).toContain('factoryShape: "standard"');
    expect(result.source).toContain('doc: "Not found."');
    expect(result.source).toContain('domain: "common"');
    // Auth entry
    expect(result.source).toContain('code: "AUTH_BEARER_MISSING"');
    expect(result.source).toContain("httpStatus: 401");
    expect(result.source).toContain('category: "validation_failure"');
    expect(result.source).toContain(
      "userMessageKey: TK.auth.errors.UNAUTHORIZED",
    );
    expect(result.source).toContain('factoryName: "BearerMissing"');
    expect(result.source).toContain('factoryShape: "with_error_code"');
    expect(result.source).toContain('domain: "auth"');
  });

  it("emits the errorCodeRegistry export", () => {
    const result = emitErrorCodeRegistry([genericCatalog]);
    expect(result.source).toContain(
      "export const errorCodeRegistry = buildRegistry(_entries);",
    );
  });

  it("emits empty entries array for an empty catalog list", () => {
    const result = emitErrorCodeRegistry([]);
    expect(result.diagnostics).toEqual([]);
    expect(result.source).toContain(
      "const _entries: readonly ErrorCodeInfo[] = [",
    );
    expect(result.source).toContain("];");
  });

  it("returns no diagnostics on clean input", () => {
    const result = emitErrorCodeRegistry([genericCatalog, authCatalog]);
    expect(result.diagnostics).toEqual([]);
    expect(result.source).not.toBe("");
  });

  it("emits empty strings for optional fields (category, factoryName, factoryShape, doc) when undefined", () => {
    // Exercises the ?? "" fallback branches for optional ErrorCodeEntry fields.
    const catalogWithPartialEntry: CatalogEntry = {
      specPath: "contracts/error-codes/error-codes.spec.json",
      domain: "common",
      entries: [
        {
          code: "NOT_FOUND",
          httpStatus: 404,
          userMessageKey: "TK.Common.Errors.NOT_FOUND",
          // category, factoryName, factoryShape, doc are all omitted (undefined)
        } as unknown as typeof genericEntry,
      ],
    };
    const result = emitErrorCodeRegistry([catalogWithPartialEntry]);
    expect(result.diagnostics).toEqual([]);
    // Verify the ?? "" fallback rendered as empty strings in the output.
    expect(result.source).toContain('category: "",');
    expect(result.source).toContain('factoryName: "",');
    expect(result.source).toContain('factoryShape: "",');
    expect(result.source).toContain('doc: "",');
  });

  it("returns empty source on collision", () => {
    const dupA: CatalogEntry = {
      specPath: "a.spec.json",
      domain: "auth",
      entries: [authEntry],
    };
    const dupB: CatalogEntry = {
      specPath: "b.spec.json",
      domain: "auth",
      entries: [authEntry],
    };
    const result = emitErrorCodeRegistry([dupA, dupB]);
    expect(result.source).toBe("");
    expect(result.diagnostics.length).toBeGreaterThan(0);
  });

  // -------------------------------------------------------------------------
  // D2ERC007 — unknown category
  // -------------------------------------------------------------------------

  it("D2ERC007: returns empty source when an entry has a category not in validCategorySet", () => {
    // An entry whose category is misspelled / unknown fires D2ERC007 when
    // validCategorySet is provided — mirrors .NET CategorySpecLoader.Check().
    const catalogWithBadCategory: CatalogEntry = {
      specPath: "contracts/error-codes/error-codes.spec.json",
      domain: "common",
      entries: [
        {
          ...genericEntry,
          code: "NOT_FOUND",
          category: "typo_not_found" as unknown as typeof genericEntry.category,
        },
      ],
    };
    const validCategories = new Set([
      "not_found",
      "conflict",
      "validation_failure",
      "policy_denied",
      "rate_limited",
      "payload_too_large",
      "infrastructure_unavailable",
      "internal_error",
      "partial_success",
    ]);
    const result = emitErrorCodeRegistry(
      [catalogWithBadCategory],
      undefined,
      validCategories,
    );
    expect(result.source).toBe("");
    expect(result.diagnostics).toHaveLength(1);
    expect(result.diagnostics[0]?.id).toBe(DiagnosticIds.ERC_UNKNOWN_CATEGORY);
    expect(result.diagnostics[0]?.severity).toBe("error");
    expect(result.diagnostics[0]?.message).toContain("D2ERC007");
    expect(result.diagnostics[0]?.message).toContain("NOT_FOUND");
    expect(result.diagnostics[0]?.message).toContain("typo_not_found");
  });

  it("D2ERC007: no diagnostic when validCategorySet is not provided (opt-in guard)", () => {
    // When validCategorySet is omitted the category check is skipped entirely —
    // callers that don't have the category spec available can still emit.
    const catalogWithBadCategory: CatalogEntry = {
      specPath: "contracts/error-codes/error-codes.spec.json",
      domain: "common",
      entries: [
        {
          ...genericEntry,
          code: "NOT_FOUND",
          category: "completely_made_up" as unknown as typeof genericEntry.category,
        },
      ],
    };
    // No validCategorySet → no D2ERC007.
    const result = emitErrorCodeRegistry([catalogWithBadCategory]);
    expect(result.diagnostics).toEqual([]);
    expect(result.source).not.toBe("");
  });

  it("D2ERC007: no diagnostic when all categories are valid", () => {
    // Entries with correct categories pass the closed-set check cleanly.
    const validCategories = new Set([
      "not_found",
      "conflict",
      "validation_failure",
      "policy_denied",
      "rate_limited",
      "payload_too_large",
      "infrastructure_unavailable",
      "internal_error",
      "partial_success",
    ]);
    const result = emitErrorCodeRegistry(
      [genericCatalog, authCatalog],
      undefined,
      validCategories,
    );
    expect(result.diagnostics).toEqual([]);
    expect(result.source).not.toBe("");
  });

  it("D2ERC002: returns empty source when an entry has a missing (empty) userMessageKey", () => {
    // An entry whose userMessageKey is an empty string (falsy) → D2ERC002 fires
    // on the !entry.userMessageKey guard before parseTkKey is reached.
    const catalogWithMissingKey: CatalogEntry = {
      specPath: "contracts/error-codes/error-codes.spec.json",
      domain: "common",
      entries: [
        {
          ...genericEntry,
          code: "NOT_FOUND",
          userMessageKey: "",
        },
      ],
    };
    const result = emitErrorCodeRegistry([catalogWithMissingKey]);
    expect(result.source).toBe("");
    expect(result.diagnostics).toHaveLength(1);
    expect(result.diagnostics[0]?.id).toBe(DiagnosticIds.ERC_TK_KEY_NOT_FOUND);
    expect(result.diagnostics[0]?.message).toContain("missing userMessageKey");
  });

  it("D2ERC002: returns empty source when an entry has an unparseable userMessageKey", () => {
    // An entry whose userMessageKey does not match the TK.Namespace.Section.KEY
    // pattern — parseTkKey returns undefined → D2ERC002 fires.
    const badCatalog: CatalogEntry = {
      specPath: "contracts/error-codes/error-codes.spec.json",
      domain: "common",
      entries: [
        {
          ...genericEntry,
          code: "NOT_FOUND",
          userMessageKey: "NOT_A_TK_KEY",
        },
      ],
    };
    const result = emitErrorCodeRegistry([badCatalog]);
    expect(result.source).toBe("");
    expect(result.diagnostics).toHaveLength(1);
    expect(result.diagnostics[0]?.id).toBe(DiagnosticIds.ERC_TK_KEY_NOT_FOUND);
    expect(result.diagnostics[0]?.message).toContain("NOT_FOUND");
    expect(result.diagnostics[0]?.message).toContain(
      "unparseable userMessageKey",
    );
  });

  it("D2ERC002: returns empty source when enUsKeys is provided but the key is absent", () => {
    // A valid TK key structure but the resolved snake_case key is not present
    // in the provided en-US key set → D2ERC002 fires.
    const catalogWithValidTk: CatalogEntry = {
      specPath: "contracts/error-codes/error-codes.spec.json",
      domain: "common",
      entries: [genericEntry],
    };
    // Provide an empty en-US key set — no keys present.
    const emptyEnUsKeys: ReadonlySet<string> = new Set<string>();
    const result = emitErrorCodeRegistry([catalogWithValidTk], emptyEnUsKeys);
    expect(result.source).toBe("");
    expect(result.diagnostics).toHaveLength(1);
    expect(result.diagnostics[0]?.id).toBe(DiagnosticIds.ERC_TK_KEY_NOT_FOUND);
    expect(result.diagnostics[0]?.message).toContain("NOT_FOUND");
    expect(result.diagnostics[0]?.message).toContain("en-US.json");
  });
});

// ---------------------------------------------------------------------------
// discoverCatalogs — malformed JSON fires D2ERC006
// ---------------------------------------------------------------------------

describe("discoverCatalogs — malformed spec files fire D2ERC006 (not silently skipped)", () => {
  it("D2ERC006: fires an error diagnostic for a .spec.json file that contains invalid JSON", () => {
    // Create a temp directory with one malformed spec file — discoverCatalogs
    // must emit D2ERC006 (Error) and NOT silently continue. This mirrors the
    // .NET RegistryGenerator D2ERC006 build error.
    const tmpDir = mkdtempSync(resolve(tmpdir(), "d2-test-"));
    try {
      writeFileSync(
        resolve(tmpDir, "bad.spec.json"),
        "{ NOT VALID JSON }",
        "utf8",
      );
      const { catalogs, diagnostics } = discoverCatalogs(tmpDir);
      expect(catalogs).toHaveLength(0);
      expect(diagnostics).toHaveLength(1);
      expect(diagnostics[0]?.id).toBe(DiagnosticIds.ERC_MALFORMED_REGISTRY_SPEC);
      expect(diagnostics[0]?.severity).toBe("error");
      expect(diagnostics[0]?.message).toContain("D2ERC006");
      expect(diagnostics[0]?.message).toContain("bad.spec.json");
      expect(diagnostics[0]?.filePath).toContain("bad.spec.json");
    } finally {
      rmSync(tmpDir, { recursive: true, force: true });
    }
  });

  it("D2ERC006: no registry emitted when a malformed spec file is the only candidate", () => {
    // Simulates the full pipeline: discoverCatalogs returns an error diagnostic
    // (D2ERC006) → the runner checks discovery diagnostics before proceeding →
    // exits non-zero without writing. Verify that the discovery result carries the
    // blocking diagnostic AND the runner-level guard (hasError → early return)
    // would prevent emission.
    const tmpDir = mkdtempSync(resolve(tmpdir(), "d2-test-"));
    try {
      writeFileSync(
        resolve(tmpDir, "broken.spec.json"),
        "NOT JSON AT ALL",
        "utf8",
      );
      const { catalogs, diagnostics } = discoverCatalogs(tmpDir);
      // D2ERC006 fires — the discovery result must carry an error diagnostic.
      expect(diagnostics.some((d) => d.severity === "error")).toBe(true);
      expect(
        diagnostics.some((d) => d.id === DiagnosticIds.ERC_MALFORMED_REGISTRY_SPEC),
      ).toBe(true);
      // The runner guard: if any discovery diagnostic is an error, return them
      // immediately (no writeGeneratedFile called). Simulate the guard directly.
      const shouldAbort = diagnostics.some((d) => d.severity === "error");
      expect(shouldAbort).toBe(true);
      // catalogs is empty (only malformed file) — no entries were loaded.
      expect(catalogs).toHaveLength(0);
    } finally {
      rmSync(tmpDir, { recursive: true, force: true });
    }
  });

  it("skips a .spec.json file that is valid JSON but not an error-code spec (no errorCodes array)", () => {
    // Exercises the isErrorCodeSpec false branch for non-spec JSON files —
    // these are legitimately ignored (schema specs, fixture specs, etc.) and
    // do NOT fire D2ERC006 (they parse fine, just aren't error-code specs).
    const tmpDir = mkdtempSync(resolve(tmpdir(), "d2-test-"));
    try {
      writeFileSync(
        resolve(tmpDir, "schema.spec.json"),
        '{"type":"object"}',
        "utf8",
      );
      const { catalogs, diagnostics } = discoverCatalogs(tmpDir);
      expect(catalogs).toHaveLength(0);
      // No diagnostics: valid JSON that isn't an error-code spec is silently ignored.
      expect(diagnostics).toHaveLength(0);
    } finally {
      rmSync(tmpDir, { recursive: true, force: true });
    }
  });

  it("skips a .spec.json file whose JSON parses to null (isErrorCodeSpec null guard)", () => {
    // JSON.parse("null") === null — exercises the `data === null` branch in isErrorCodeSpec.
    // Parses fine → no D2ERC006; just not an error-code spec → silently ignored.
    const tmpDir = mkdtempSync(resolve(tmpdir(), "d2-test-"));
    try {
      writeFileSync(resolve(tmpDir, "null.spec.json"), "null", "utf8");
      const { catalogs, diagnostics } = discoverCatalogs(tmpDir);
      expect(catalogs).toHaveLength(0);
      expect(diagnostics).toHaveLength(0);
    } finally {
      rmSync(tmpDir, { recursive: true, force: true });
    }
  });

  it("assigns domain 'unknown' when spec filename does not match any known pattern", () => {
    // A valid error-code spec with an unusual filename (not error-codes.spec.json
    // and not *-error-codes.spec.json) → domainFromSpecPath returns 'unknown'.
    const tmpDir = mkdtempSync(resolve(tmpdir(), "d2-test-"));
    try {
      const spec = {
        errorCodes: [
          {
            code: "SOME_CODE",
            userMessageKey: "TK.some.section.SOME_CODE",
            httpStatus: 400,
            category: "validation_failure",
            factoryName: "Some",
            factoryShape: "standard",
            doc: "Some code.",
          },
        ],
      };
      writeFileSync(
        resolve(tmpDir, "something-else.spec.json"),
        JSON.stringify(spec),
        "utf8",
      );
      const { catalogs, diagnostics } = discoverCatalogs(tmpDir);
      expect(diagnostics).toHaveLength(0);
      expect(catalogs).toHaveLength(1);
      expect(catalogs[0]?.domain).toBe("unknown");
    } finally {
      rmSync(tmpDir, { recursive: true, force: true });
    }
  });
});

// ---------------------------------------------------------------------------
// discoverCatalogs — real contracts directory discovery
// ---------------------------------------------------------------------------

describe("discoverCatalogs — real contracts directory", () => {
  it("discovers exactly 2 error-code specs (generic + auth)", () => {
    const { catalogs } = discoverCatalogs(contractsPath());
    expect(catalogs).toHaveLength(2);
  });

  it("includes the generic catalog with domain 'common'", () => {
    const { catalogs } = discoverCatalogs(contractsPath());
    const generic = catalogs.find((c) => c.domain === "common");
    expect(generic).toBeDefined();
    expect(generic?.entries.length).toBeGreaterThan(0);
  });

  it("includes the auth catalog with domain 'auth'", () => {
    const { catalogs } = discoverCatalogs(contractsPath());
    const auth = catalogs.find((c) => c.domain === "auth");
    expect(auth).toBeDefined();
    expect(auth?.entries.length).toBeGreaterThan(0);
  });

  it("returns catalogs sorted by spec path for deterministic ordering", () => {
    const { catalogs } = discoverCatalogs(contractsPath());
    const paths = catalogs.map((c) => c.specPath);
    expect(paths).toEqual([...paths].sort());
  });

  it("real catalogs pass the aggregation check with no diagnostics", () => {
    const { catalogs, diagnostics: discoverDiags } =
      discoverCatalogs(contractsPath());
    expect(discoverDiags).toEqual([]);
    const { diagnostics } = aggregateAndCheck(catalogs);
    expect(diagnostics).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// emitErrorCodeRegistry — real spec byte-parity (idempotency)
// ---------------------------------------------------------------------------

describe("emitErrorCodeRegistry — byte-parity with committed generated file", () => {
  it("real-spec regeneration equals committed error-code-registry.g.ts (idempotent)", () => {
    const here = dirname(fileURLToPath(import.meta.url));
    const repoRoot = resolve(here, "..", "..", "..");

    function readGenerated(...parts: string[]): string {
      return readFileSync(resolve(repoRoot, ...parts), "utf8").replace(
        /\r\n/g,
        "\n",
      );
    }

    function loadEnUsKeys(): ReadonlySet<string> {
      const raw = readFileSync(
        resolve(repoRoot, "contracts", "messages", "en-US.json"),
        "utf8",
      );
      const parsed = JSON.parse(raw) as Record<string, unknown>;
      const keys = new Set<string>();
      for (const key of Object.keys(parsed))
        if (key !== "$schema") keys.add(key);
      return keys;
    }

    const { catalogs, diagnostics: discoverDiags } =
      discoverCatalogs(contractsPath());
    expect(discoverDiags).toEqual([]);
    const result = emitErrorCodeRegistry(catalogs, loadEnUsKeys());
    expect(result.diagnostics).toEqual([]);

    const committed = readGenerated(
      "server",
      "shared",
      "typescript",
      "error-codes-registry",
      "src",
      "generated",
      "error-code-registry.g.ts",
    );
    expect(result.source).toBe(committed);
  });
});
