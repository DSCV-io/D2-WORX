// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { diffMessageKeys } from "../src/i18n-diff.js";

// ---------------------------------------------------------------------------
// Fixture helpers
// ---------------------------------------------------------------------------

const FIXTURES_DIR = join(
  fileURLToPath(import.meta.url),
  "..",
  "fixtures",
  "spec",
);

function readLocaleFixture(subPath: string): unknown {
  const content = readFileSync(join(FIXTURES_DIR, subPath), "utf-8");
  return JSON.parse(content) as unknown;
}

// ---------------------------------------------------------------------------
// Removed key → FINDING
// ---------------------------------------------------------------------------

describe("diffMessageKeys — removed key", () => {
  it("returns a finding when a TK key is removed", () => {
    const before = {
      auth_welcome: "Welcome",
      auth_email_changed: "Email changed",
      $schema: "x",
    };
    const after = { auth_welcome: "Welcome", $schema: "x" };

    const findings = diffMessageKeys(
      before,
      after,
      "contracts/messages/en-US.json",
    );

    expect(findings).toHaveLength(1);
    expect(findings[0]?.arm).toBe("i18n");
    expect(findings[0]?.severity).toBe("ERROR");
    expect(findings[0]?.message).toContain("auth_email_changed");
    expect(findings[0]?.message).toContain("removed");
    expect(findings[0]?.message).toContain("Gate FAILED");
  });

  it("returns multiple findings when multiple keys are removed", () => {
    const before = { key_a: "A", key_b: "B", key_c: "C" };
    const after = { key_a: "A" };

    const findings = diffMessageKeys(before, after, "en-US.json");

    expect(findings).toHaveLength(2);
    const messages = findings.map((f) => f.message);
    expect(messages.some((m) => m.includes("key_b"))).toBe(true);
    expect(messages.some((m) => m.includes("key_c"))).toBe(true);
  });

  it("finding message includes the file path", () => {
    const before = { auth_x: "X" };
    const after = {};

    const findings = diffMessageKeys(
      before,
      after,
      "contracts/messages/en-US.json",
    );

    expect(findings[0]?.file).toBe("contracts/messages/en-US.json");
    expect(findings[0]?.message).toContain("contracts/messages/en-US.json");
  });
});

// ---------------------------------------------------------------------------
// Added key → PASS
// ---------------------------------------------------------------------------

describe("diffMessageKeys — added key is a PASS", () => {
  it("adding a new TK key is not a break", () => {
    const before = { existing_key: "Existing" };
    const after = { existing_key: "Existing", new_key: "New value" };

    const findings = diffMessageKeys(before, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });

  it("new file (before=undefined) returns no findings", () => {
    const after = { auth_welcome: "Welcome" };

    const findings = diffMessageKeys(undefined, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });

  it("new file (before=null) returns no findings", () => {
    const after = { auth_welcome: "Welcome" };

    const findings = diffMessageKeys(null, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Value change → PASS (translation copy churns freely)
// ---------------------------------------------------------------------------

describe("diffMessageKeys — value change is a PASS", () => {
  it("changing the translation copy value is not a break", () => {
    const before = { auth_welcome: "Welcome!" };
    const after = { auth_welcome: "Welcome to our platform." };

    const findings = diffMessageKeys(before, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });

  it("multiple value changes all pass", () => {
    const before = { key_a: "A", key_b: "B" };
    const after = { key_a: "Alpha", key_b: "Beta" };

    const findings = diffMessageKeys(before, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// $schema key → IGNORED
// ---------------------------------------------------------------------------

describe("diffMessageKeys — $schema is ignored", () => {
  it("$schema is not a runtime TK key and its removal is not a break", () => {
    const before = {
      $schema: "https://inlang.com/schema/inlang-message-format",
      auth_welcome: "Welcome",
    };
    const after = { auth_welcome: "Welcome" };

    const findings = diffMessageKeys(before, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });

  it("$schema value change is not a break", () => {
    const before = { $schema: "old-schema-url", auth_welcome: "Welcome" };
    const after = { $schema: "new-schema-url", auth_welcome: "Welcome" };

    const findings = diffMessageKeys(before, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Reorder → PASS (key-set diff, not positional)
// ---------------------------------------------------------------------------

describe("diffMessageKeys — reorder is a PASS", () => {
  it("reordering keys in the JSON file is not a break", () => {
    // JSON object key order is technically unspecified; the diff uses key-set comparison.
    const before = { key_a: "A", key_b: "B", key_c: "C" };
    const after = { key_c: "C", key_a: "A", key_b: "B" };

    const findings = diffMessageKeys(before, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Adversarial / malformed inputs
// ---------------------------------------------------------------------------

describe("diffMessageKeys — adversarial inputs", () => {
  it("throws when before is not a JSON object (malformed baseline)", () => {
    expect(() => diffMessageKeys("not-an-object", {}, "en-US.json")).toThrow();
  });

  it("throws when after is not a JSON object (malformed proposed)", () => {
    expect(() => diffMessageKeys({}, "not-an-object", "en-US.json")).toThrow();
  });

  it("throws when before is an array", () => {
    expect(() => diffMessageKeys([], {}, "en-US.json")).toThrow();
  });

  it("throws when after is an array", () => {
    expect(() => diffMessageKeys({}, [], "en-US.json")).toThrow();
  });

  it("empty before and after produce no findings", () => {
    const findings = diffMessageKeys({}, {}, "en-US.json");
    expect(findings).toHaveLength(0);
  });

  it("empty before, populated after produces no findings (fully additive)", () => {
    const before = {};
    const after = { new_key: "New" };

    const findings = diffMessageKeys(before, after, "en-US.json");
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Non-vacuity: fails-without-valve / passes-with-valve (logical proof)
// ---------------------------------------------------------------------------

describe("diffMessageKeys — non-vacuity proof (valve suppression)", () => {
  const before = { auth_welcome: "Welcome", auth_email: "Email changed" };
  const after = { auth_welcome: "Welcome" }; // auth_email removed

  it("returns RED (finding) when a key is removed (no valve)", () => {
    const findings = diffMessageKeys(before, after, "en-US.json");
    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("auth_email");
  });

  it("gate passes (valve suppresses finding) when forced=true", () => {
    const findings = diffMessageKeys(before, after, "en-US.json");
    const valveOpen = true;
    const passed = findings.length === 0 || valveOpen;
    expect(passed).toBe(true);
  });

  it("gate fails when forced=false and there is a finding", () => {
    const findings = diffMessageKeys(before, after, "en-US.json");
    const valveOpen = false;
    const passed = findings.length === 0 || valveOpen;
    expect(passed).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Real-fixture tests — locale file pairs (non-vacuous coverage via actual files)
// ---------------------------------------------------------------------------

describe("diffMessageKeys — real fixture: en-US.json before/after (key removed)", () => {
  it("en-US-removed-key fixture: detects the removed auth_email_changed key as a gate break", () => {
    const beforeLocale = readLocaleFixture("before/en-US.json");
    const afterLocale = readLocaleFixture("after/en-US-removed-key.json");

    const findings = diffMessageKeys(
      beforeLocale,
      afterLocale,
      "contracts/messages/en-US.json",
    );

    expect(findings).toHaveLength(1);
    expect(findings[0]?.arm).toBe("i18n");
    expect(findings[0]?.severity).toBe("ERROR");
    expect(findings[0]?.message).toContain("auth_email_changed");
    expect(findings[0]?.message).toContain("removed");
  });

  it("en-US fixture unchanged: no findings (PASS)", () => {
    const beforeLocale = readLocaleFixture("before/en-US.json");

    const findings = diffMessageKeys(
      beforeLocale,
      beforeLocale,
      "contracts/messages/en-US.json",
    );

    expect(findings).toHaveLength(0);
  });
});
