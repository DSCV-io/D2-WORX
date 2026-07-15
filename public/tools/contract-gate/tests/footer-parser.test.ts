// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { parseBreakingFooters } from "../src/footer-parser.js";

// ---------------------------------------------------------------------------
// Helper: build a commit message with an explicit footer block.
// ---------------------------------------------------------------------------

function makeMessage(subject: string, body: string, footer: string): string {
  // Conventional Commits layout:
  //   <subject>\n\n<body>\n\n<footer>
  return `${subject}\n\n${body}\n\n${footer}`;
}

// ---------------------------------------------------------------------------
// Empty / blank input
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — empty input", () => {
  it("empty array returns not-forced with empty arrays", () => {
    const result = parseBreakingFooters([]);
    expect(result.forced).toBe(false);
    expect(result.wireBreaking).toHaveLength(0);
    expect(result.apiBreaking).toHaveLength(0);
  });

  it("array of blank strings returns not-forced", () => {
    const result = parseBreakingFooters(["", "   ", "\n\n"]);
    expect(result.forced).toBe(false);
  });

  it("single message with subject and body but NO footer returns not-forced", () => {
    const msg =
      "feat: add new endpoint\n\nThis is a body paragraph with no footer.";
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// WIRE-BREAKING: footer token
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — WIRE-BREAKING: token", () => {
  it("single WIRE-BREAKING footer sets forced and populates wireBreaking", () => {
    const msg = makeMessage(
      "feat: rename proto field",
      "Renamed field 'name' to 'display_name' in the Sign request.",
      "WIRE-BREAKING: renamed 'name' to 'display_name' in SignRequest (field 2)",
    );
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.wireBreaking).toEqual([
      "renamed 'name' to 'display_name' in SignRequest (field 2)",
    ]);
    expect(result.apiBreaking).toHaveLength(0);
  });

  it("WIRE-BREAKING footer value is trimmed of surrounding whitespace", () => {
    const msg = makeMessage(
      "feat: drop field",
      "Body text.",
      "WIRE-BREAKING:   removed reserved field 3   ",
    );
    const result = parseBreakingFooters([msg]);
    expect(result.wireBreaking).toEqual(["removed reserved field 3"]);
  });

  it("WIRE-BREAKING footer with trailing CRLF line endings is parsed correctly", () => {
    const msg =
      "feat: break wire\r\n\r\nBody.\r\n\r\nWIRE-BREAKING: dropped field 5\r\n";
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.wireBreaking).toEqual(["dropped field 5"]);
  });
});

// ---------------------------------------------------------------------------
// BREAKING CHANGE: token (api axis)
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — BREAKING CHANGE: token", () => {
  it("BREAKING CHANGE footer sets forced and populates apiBreaking", () => {
    const msg = makeMessage(
      "feat: remove error code",
      "Removed the DEPRECATED_CODE error code from the catalog.",
      "BREAKING CHANGE: removed DEPRECATED_CODE from keycustodian-error-codes catalog",
    );
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.apiBreaking).toEqual([
      "removed DEPRECATED_CODE from keycustodian-error-codes catalog",
    ]);
    expect(result.wireBreaking).toHaveLength(0);
  });

  it("BREAKING-CHANGE: (hyphen form) also populates apiBreaking", () => {
    const msg = makeMessage(
      "feat: remove i18n key",
      "Removed auth_email_changed_body from all locale files.",
      "BREAKING-CHANGE: removed auth_email_changed_body translation key",
    );
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.apiBreaking).toEqual([
      "removed auth_email_changed_body translation key",
    ]);
  });

  it("both BREAKING CHANGE and BREAKING-CHANGE forms in the same footer are each captured", () => {
    const footer = [
      "BREAKING CHANGE: removed scope 'admin:read'",
      "BREAKING-CHANGE: removed scope 'admin:write'",
    ].join("\n");
    const msg = makeMessage(
      "feat: scope cleanup",
      "Removed two scopes.",
      footer,
    );
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.apiBreaking).toEqual([
      "removed scope 'admin:read'",
      "removed scope 'admin:write'",
    ]);
  });
});

// ---------------------------------------------------------------------------
// type!: subject shorthand (Conventional Commits breaking marker)
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — type!: subject shorthand", () => {
  it("type!: shorthand on the subject line populates apiBreaking", () => {
    const msg = "feat!: remove legacy scope\n\nBody text.";
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.apiBreaking).toEqual(["remove legacy scope"]);
    expect(result.wireBreaking).toHaveLength(0);
  });

  it("type(scope)!: shorthand with a scope token is recognized", () => {
    const msg = "refactor(auth)!: drop legacy session fields\n\nBody.";
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.apiBreaking).toContain("drop legacy session fields");
  });

  it("type without ! is NOT a breaking signal", () => {
    const msg = "feat: add optional field\n\nBody text.";
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
  });

  it("type!: with empty description after colon does not produce a blank entry", () => {
    // Malformed: "feat!:  " — trim yields empty, should not produce an entry.
    const msg = "feat!:   \n\nBody.";
    const result = parseBreakingFooters([msg]);
    // The regex requires (.+) after "!:\s*" so a whitespace-only description
    // won't match at all — forced stays false.
    expect(result.forced).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Footer-block vs body-prose distinction (adversarial)
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — footer block vs body prose", () => {
  it("WIRE-BREAKING: appearing in the body (not after a blank line) is NOT a valid footer", () => {
    // The body has "WIRE-BREAKING:" as prose but there is NO blank line after it
    // before another paragraph — it's embedded in the body, not the footer block.
    const msg = [
      "feat: some change",
      "",
      "This paragraph mentions WIRE-BREAKING: style changes in prose.",
      "It is not a footer because there is no blank line separating it.",
    ].join("\n");
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
  });

  it("WIRE-BREAKING: in an intermediate body paragraph (not after the LAST blank line) is not captured", () => {
    // Layout: subject → blank → body-with-footer-lookalike → blank → real footer (none).
    const msg = [
      "feat: some change",
      "",
      "WIRE-BREAKING: this looks like a footer but is body text",
      "",
      "Second paragraph after another blank line — this is the real footer block.",
      "Fixup: corrects something minor",
    ].join("\n");
    // The real footer block starts after the LAST blank line (before "Second paragraph").
    // "WIRE-BREAKING:" is NOT in that last block → not captured.
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
  });

  it("WIRE-BREAKING: after the LAST blank line IS captured as a footer", () => {
    const msg = [
      "feat: rename proto field",
      "",
      "Body paragraph with context.",
      "",
      "WIRE-BREAKING: dropped field 3 from SignRequest",
    ].join("\n");
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.wireBreaking).toEqual(["dropped field 3 from SignRequest"]);
  });

  it("mixed footer block with WIRE-BREAKING and a non-breaking token captures only the breaking token", () => {
    const footer = [
      "Fixes: #42",
      "WIRE-BREAKING: removed response field 'legacyToken'",
      "Reviewed-by: Alice",
    ].join("\n");
    const msg = makeMessage("fix: remove legacy token field", "Body.", footer);
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.wireBreaking).toEqual([
      "removed response field 'legacyToken'",
    ]);
    expect(result.apiBreaking).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Multiple footers in a single commit message
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — multiple footers in one message", () => {
  it("both WIRE-BREAKING and BREAKING CHANGE in the same message populate both arrays", () => {
    const footer = [
      "WIRE-BREAKING: removed proto field 4 (SignResponse.sessionToken)",
      "BREAKING CHANGE: removed session_token_renewed TK key from en-US.json",
    ].join("\n");
    const msg = makeMessage(
      "feat: remove session token surface",
      "Body.",
      footer,
    );
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(true);
    expect(result.wireBreaking).toEqual([
      "removed proto field 4 (SignResponse.sessionToken)",
    ]);
    expect(result.apiBreaking).toEqual([
      "removed session_token_renewed TK key from en-US.json",
    ]);
  });

  it("multiple WIRE-BREAKING footers in one message all collected", () => {
    const footer = [
      "WIRE-BREAKING: removed field 3 from SignRequest",
      "WIRE-BREAKING: removed field 5 from SignResponse",
    ].join("\n");
    const msg = makeMessage("feat!: major wire cleanup", "Body.", footer);
    const result = parseBreakingFooters([msg]);
    expect(result.wireBreaking).toHaveLength(2);
    expect(result.wireBreaking).toContain("removed field 3 from SignRequest");
    expect(result.wireBreaking).toContain("removed field 5 from SignResponse");
    // type!: also fires apiBreaking
    expect(result.apiBreaking).toHaveLength(1);
  });
});

// ---------------------------------------------------------------------------
// Multiple commit messages in the array
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — multiple commit messages", () => {
  it("one forced commit among non-forced commits makes the result forced", () => {
    const nonBreaking = "fix: correct off-by-one\n\nBody.";
    const breaking = makeMessage(
      "feat: remove field",
      "Body.",
      "WIRE-BREAKING: removed field 2 from PingRequest",
    );
    const result = parseBreakingFooters([nonBreaking, breaking, nonBreaking]);
    expect(result.forced).toBe(true);
    expect(result.wireBreaking).toEqual(["removed field 2 from PingRequest"]);
  });

  it("all non-forced commits return not-forced", () => {
    const msgs = [
      "feat: add optional field\n\nBody.",
      "fix: typo\n\nBody.",
      "docs: update readme\n\nBody.",
    ];
    const result = parseBreakingFooters(msgs);
    expect(result.forced).toBe(false);
  });

  it("breaking signals from multiple commits are accumulated in order", () => {
    const msgA = makeMessage(
      "feat: drop field",
      "Body.",
      "WIRE-BREAKING: dropped field 3",
    );
    const msgB = makeMessage(
      "feat: drop key",
      "Body.",
      "BREAKING CHANGE: removed auth_x TK key",
    );
    const result = parseBreakingFooters([msgA, msgB]);
    expect(result.wireBreaking).toEqual(["dropped field 3"]);
    expect(result.apiBreaking).toEqual(["removed auth_x TK key"]);
  });
});

// ---------------------------------------------------------------------------
// Case-sensitivity (adversarial)
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — case-sensitivity", () => {
  it("lowercase 'wire-breaking:' is NOT recognized (tokens are uppercase per Conventional Commits)", () => {
    const msg = makeMessage(
      "feat: drop field",
      "Body.",
      "wire-breaking: lowercase token should not fire",
    );
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
  });

  it("mixed-case 'Wire-Breaking:' is NOT recognized", () => {
    const msg = makeMessage(
      "feat: drop field",
      "Body.",
      "Wire-Breaking: mixed case should not fire",
    );
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
  });

  it("lowercase 'breaking change:' is NOT recognized", () => {
    const msg = makeMessage(
      "feat: remove key",
      "Body.",
      "breaking change: lowercase token should not fire",
    );
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Malformed / partial footers (adversarial — must not throw)
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — malformed and partial inputs", () => {
  it("footer token with no description value after the colon is not captured", () => {
    const msg = makeMessage("feat: change", "Body.", "WIRE-BREAKING:");
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
    expect(result.wireBreaking).toHaveLength(0);
  });

  it("footer token with only whitespace after the colon is not captured", () => {
    const msg = makeMessage("feat: change", "Body.", "WIRE-BREAKING:   ");
    const result = parseBreakingFooters([msg]);
    expect(result.forced).toBe(false);
  });

  it("completely malformed message (no newlines at all) does not throw", () => {
    const result = parseBreakingFooters(["thishasnonewlines"]);
    expect(result.forced).toBe(false);
  });

  it("message that is only a blank footer block does not throw", () => {
    const result = parseBreakingFooters(["subject\n\n   \n   "]);
    expect(result.forced).toBe(false);
  });

  it("null-character in message text does not throw or corrupt state", () => {
    const msg =
      "feat: odd\n\nBody with \x00 null.\n\nWIRE-BREAKING: valid after null";
    // The \x00 in the body is unusual but the parser should still find the footer.
    const result = parseBreakingFooters([msg]);
    // The last blank line is before "WIRE-BREAKING:", so it IS in the footer block.
    expect(result.forced).toBe(true);
    expect(result.wireBreaking[0]).toBe("valid after null");
  });
});

// ---------------------------------------------------------------------------
// Return-type shape invariants
// ---------------------------------------------------------------------------

describe("parseBreakingFooters — return type invariants", () => {
  it("wireBreaking and apiBreaking are always readonly arrays (never undefined)", () => {
    const result = parseBreakingFooters([]);
    expect(Array.isArray(result.wireBreaking)).toBe(true);
    expect(Array.isArray(result.apiBreaking)).toBe(true);
  });

  it("forced is always a boolean", () => {
    const result = parseBreakingFooters([]);
    expect(typeof result.forced).toBe("boolean");
  });
});
