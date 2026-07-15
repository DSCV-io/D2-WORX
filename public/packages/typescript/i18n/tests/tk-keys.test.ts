// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, it, expect } from "vitest";
import { TK } from "@d2/i18n-keys";

const here = dirname(fileURLToPath(import.meta.url));
const EN_US_PATH = resolve(
  here,
  "../../../../../contracts/messages/en-US.json",
);

/** Decompose a flat i18n key — mirrors the emitter's decomposeKey logic. */
function decompose(
  key: string,
): { domain: string; category: string; constant: string } | undefined {
  const segments = key.split("_").filter((s) => s.length > 0);
  if (segments.length < 3) return undefined;
  return {
    domain: segments[0]!,
    category: segments[1]!,
    constant: segments.slice(2).join("_").toUpperCase(),
  };
}

describe("TK catalog", () => {
  // -------------------------------------------------------------------------
  // Explicit spot-checks (regression pins for the canonical examples).
  // -------------------------------------------------------------------------

  it("TK.common.errors.REQUEST_FAILED resolves to the correct key string", () => {
    expect(TK.common.errors.REQUEST_FAILED.key).toBe(
      "common_errors_REQUEST_FAILED",
    );
  });

  it("TK.common.errors.CANCELED resolves to the correct key string", () => {
    expect(TK.common.errors.CANCELED.key).toBe("common_errors_CANCELED");
  });

  // long test description — cannot wrap
  it("TK.webclient.app.ACCOUNT_SESSIONS_NO_SESSION_SELECTED resolves to the correct key string", () => {
    expect(TK.webclient.app.ACCOUNT_SESSIONS_NO_SESSION_SELECTED.key).toBe(
      "webclient_app_account_sessions_no_session_selected",
    );
  });

  it("TK leaf values are TKMessage instances whose key matches the original key", () => {
    // Each leaf is a TKMessage instance (`{ key }`) — the constant IS the
    // message, droppable straight into D2Result.messages.
    expect(typeof TK.common.errors.NOT_FOUND).toBe("object");
    expect(TK.common.errors.NOT_FOUND.key).toBe("common_errors_NOT_FOUND");
    expect(TK.auth.errors.UNAUTHORIZED.key).toBe("auth_errors_UNAUTHORIZED");
    expect(TK.webclient.forms.VALIDATION_FAILED.key).toBe(
      "webclient_forms_validation_failed",
    );
  });

  // -------------------------------------------------------------------------
  // Full-catalog exhaustive check: every decomposable key in en-US.json is
  // reachable in TK at its computed path and equals the key string.
  // -------------------------------------------------------------------------

  it("every decomposable key in en-US.json is reachable in TK and equals the key string", () => {
    const catalog = JSON.parse(readFileSync(EN_US_PATH, "utf8")) as Record<
      string,
      string
    >;
    const tkAny = TK as Record<
      string,
      Record<string, Record<string, { key: string }>>
    >;

    for (const key of Object.keys(catalog)) {
      const entry = decompose(key);
      if (entry === undefined) {
        // Fewer than 3 segments — expected skip (e.g. "$schema").
        continue;
      }

      const { domain, category, constant } = entry;
      const domainObj = tkAny[domain];
      expect(domainObj, `TK.${domain} must exist (key: ${key})`).toBeDefined();

      const categoryObj = domainObj?.[category];
      expect(
        categoryObj,
        `TK.${domain}.${category} must exist (key: ${key})`,
      ).toBeDefined();

      const leaf = categoryObj?.[constant];
      expect(
        leaf,
        `TK.${domain}.${category}.${constant} must exist (key: ${key})`,
      ).toBeDefined();
      expect(
        leaf?.key,
        `TK.${domain}.${category}.${constant}.key must equal the original key string`,
      ).toBe(key);
    }
  });

  // -------------------------------------------------------------------------
  // Structural invariants.
  // -------------------------------------------------------------------------

  it("TK has at least the expected top-level domains", () => {
    const domains = Object.keys(TK).sort();
    expect(domains).toContain("auth");
    expect(domains).toContain("common");
    expect(domains).toContain("webclient");
    expect(domains).toContain("geo");
    expect(domains).toContain("files");
    expect(domains).toContain("comms");
    expect(domains).toContain("middleware");
  });

  it("TK.common has errors, time, ui, and validation categories", () => {
    const categories = Object.keys(TK.common).sort();
    expect(categories).toContain("errors");
    expect(categories).toContain("time");
    expect(categories).toContain("ui");
    expect(categories).toContain("validation");
  });

  it("every leaf value is a TKMessage with a non-empty key", () => {
    const tkAny = TK as Record<
      string,
      Record<string, Record<string, { key: string }>>
    >;
    for (const [domain, domainObj] of Object.entries(tkAny))
      for (const [category, categoryObj] of Object.entries(domainObj))
        for (const [constant, leaf] of Object.entries(categoryObj)) {
          expect(
            typeof leaf === "object" &&
              typeof leaf.key === "string" &&
              leaf.key.length > 0,
            `TK.${domain}.${category}.${constant} must be a TKMessage with a non-empty key`,
          ).toBe(true);
        }
  });
});
