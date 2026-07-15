// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { emitTkKeys } from "../src/tk-keys-emit.js";

const sampleCatalog: Record<string, string> = {
  common_errors_NOT_FOUND: "Not found.",
  common_errors_CONFLICT: "Conflict.",
  auth_errors_UNAUTHORIZED: "Unauthorized.",
};

describe("emitTkKeys — TKMessage-instance emission", () => {
  it('emits each constant as a tk("snake_key") TKMessage instance', () => {
    const r = emitTkKeys(sampleCatalog);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain('NOT_FOUND: tk("common_errors_NOT_FOUND")');
    expect(r.source).toContain('CONFLICT: tk("common_errors_CONFLICT")');
    expect(r.source).toContain('UNAUTHORIZED: tk("auth_errors_UNAUTHORIZED")');
  });

  it("imports tk from the zero-dep @dcsv-io/d2-i18n-abstractions leaf", () => {
    const r = emitTkKeys(sampleCatalog);
    expect(r.source).toContain(
      'import { tk } from "@dcsv-io/d2-i18n-abstractions";',
    );
    // The constants are no longer bare string literals — the wire key only
    // appears inside a tk() call, never as a standalone `: "snake_key"` leaf.
    expect(r.source).not.toContain('NOT_FOUND: "common_errors_NOT_FOUND"');
  });

  it("emits the nested domain.category.CONSTANT structure", () => {
    const r = emitTkKeys(sampleCatalog);
    expect(r.source).toContain("export const TK = {");
    expect(r.source).toContain("common: {");
    expect(r.source).toContain("errors: {");
    expect(r.source).toContain("auth: {");
    expect(r.source).toContain("export type TKKey = string;");
  });

  it("sorts domains, categories, and constants deterministically", () => {
    const r = emitTkKeys(sampleCatalog);
    // auth sorts before common at the domain level.
    expect(r.source.indexOf("auth: {")).toBeLessThan(
      r.source.indexOf("common: {"),
    );
    // CONFLICT sorts before NOT_FOUND within common.errors.
    expect(
      r.source.indexOf('CONFLICT: tk("common_errors_CONFLICT")'),
    ).toBeLessThan(
      r.source.indexOf('NOT_FOUND: tk("common_errors_NOT_FOUND")'),
    );
  });

  it("warns and skips keys with fewer than 3 segments", () => {
    const r = emitTkKeys({ too_short: "Bad.", common_errors_OK: "Ok." });
    expect(r.diagnostics.some((d) => d.id === "D2TK002")).toBe(true);
    // The valid key is still emitted; the short key is absent.
    expect(r.source).toContain('OK: tk("common_errors_OK")');
    expect(r.source).not.toContain("too_short");
  });

  it("produces identical source across two runs (idempotency)", () => {
    expect(emitTkKeys(sampleCatalog).source).toBe(
      emitTkKeys(sampleCatalog).source,
    );
  });

  it("escapes embedded quotes/backslashes in the wire key", () => {
    // Defensive: a key carrying a quote must be escaped in the emitted literal
    // so the generated source stays parseable.
    const r = emitTkKeys({ 'a_b_c"d': "x." });
    expect(r.source).toContain('tk("a_b_c\\"d")');
  });
});
