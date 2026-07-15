// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { AuthFailures } from "@dcsv-io/d2-auth-abstractions";
import { SupportedLocales, Translator } from "@dcsv-io/d2-i18n";
import * as resultFactories from "@dcsv-io/d2-result";
import type { D2Result } from "@dcsv-io/d2-result";

// ---------------------------------------------------------------------------
// Cross-runtime TK-validity RENDER test (TS half). For EVERY auth error-code
// spec entry, assert the ACTUAL wire TKMessage the emitted AuthFailures factory
// produces RENDERS to real text — not the raw key — via the @dcsv-io/d2-i18n Translator
// over contracts/messages/en-US.json. This is the guard that catches the
// symbol-vs-snake drift: a factory could reference a real TK symbol whose WIRE
// key still doesn't render. The .NET half (AuthFailuresTkValidityTests) asserts
// the same invariant on the .NET catalog; both must render the same text.
//
// Drives off the ACTUAL factory output (not a re-derived key), so the test
// guards the real wire path. Data-driven over the auth spec so a future added
// entry is automatically covered.
// ---------------------------------------------------------------------------

function findRepoRoot(startDir: string): string {
  let dir = startDir;
  for (;;) {
    try {
      readFileSync(join(dir, "pnpm-workspace.yaml"), "utf8");
      return dir;
    } catch {
      const parent = dirname(dir);
      if (parent === dir)
        throw new Error("could not locate repo root (no pnpm-workspace.yaml)");
      dir = parent;
    }
  }
}

const repoRoot = findRepoRoot(dirname(fileURLToPath(import.meta.url)));

interface AuthSpec {
  readonly errorCodes: readonly {
    readonly code: string;
    readonly factoryName: string;
    readonly userMessageKey: string;
  }[];
}

const authSpec = JSON.parse(
  readFileSync(
    join(
      repoRoot,
      "public",
      "contracts",
      "auth-error-codes",
      "auth-error-codes.spec.json",
    ),
    "utf8",
  ),
) as AuthSpec;

interface GenericSpec {
  readonly errorCodes: readonly {
    readonly code: string;
    readonly factoryName: string;
    readonly factoryShape: string;
    readonly userMessageKey: string;
  }[];
}

const genericSpec = JSON.parse(
  readFileSync(
    join(
      repoRoot,
      "public",
      "contracts",
      "error-codes",
      "error-codes.spec.json",
    ),
    "utf8",
  ),
) as GenericSpec;

const enUsRaw = JSON.parse(
  readFileSync(
    join(repoRoot, "public", "contracts", "messages", "en-US.json"),
    "utf8",
  ),
) as Record<string, string>;
const { $schema: _schema, ...enUsCatalog } = enUsRaw;

const locales = new SupportedLocales({ enabled: ["en-US"] });
const translator = new Translator(locales, { "en-US": enUsCatalog });

function camelCase(pascal: string): string {
  return pascal.length === 0
    ? pascal
    : pascal[0]!.toLowerCase() + pascal.slice(1);
}

const failures = AuthFailures as unknown as Record<
  string,
  (opts?: { traceId?: string }) => { messages: readonly { key: string }[] }
>;

describe("error-codes TK-validity (TS: every auth factory's wire message renders)", () => {
  for (const entry of authSpec.errorCodes) {
    const fnName = camelCase(entry.factoryName);

    it(`${entry.code} → ${fnName}() wire message renders to real text (not the raw key)`, () => {
      const fn = failures[fnName];
      expect(typeof fn).toBe("function");

      const msg = fn!().messages[0];
      expect(msg).toBeDefined();

      const rendered = translator.t("en-US", msg!);
      // Renders to something OTHER than the raw wire key — proves the key
      // actually resolved in the catalog (the TK constant-reference invariant:
      // every auth factory's wire message must be renderable, not a symbol path).
      expect(rendered).not.toBe(msg!.key);
      expect(rendered.length).toBeGreaterThan(0);

      // And renders to exactly the en-US source text for that key.
      const expectedText = enUsCatalog[msg!.key];
      expect(expectedText).toBeDefined();
      expect(rendered).toBe(expectedText);
    });
  }
});

// ---------------------------------------------------------------------------
// Cross-runtime TK-validity RENDER test (TS half, GENERIC catalog). For every
// generic spec entry that ships a constructing factory (factoryShape != none),
// invoke the ACTUAL generated @dcsv-io/d2-result factory and assert its wire TKMessage
// renders to real en-US text — not the raw key. The .NET half
// (ErrorCodesTkValidityTests) asserts the same invariant on the .NET catalog;
// both must render the same text. This is the guard that PROVES the render fix:
// before it, the factory rode the raw "TK.Common.Errors.*" symbol path, which
// did NOT resolve in the snake-keyed catalog.
// ---------------------------------------------------------------------------

const genericFactories = resultFactories as unknown as Record<
  string,
  (opts?: unknown) => D2Result<unknown>
>;

function snakeFromSymbolPath(symbolPath: string): string {
  // TK.Common.Errors.NOT_FOUND → common_errors_NOT_FOUND
  const segments = symbolPath.split(".");
  const domain = segments[1]![0]!.toLowerCase() + segments[1]!.slice(1);
  const category = segments[2]![0]!.toLowerCase() + segments[2]!.slice(1);
  return `${domain}_${category}_${segments[3]}`;
}

describe("error-codes TK-validity (TS: every generic factory's wire message renders)", () => {
  for (const entry of genericSpec.errorCodes) {
    // none-shape codes emit no factory — nothing to render.
    if (entry.factoryShape === "none") continue;

    const fnName = camelCase(entry.factoryName);

    it(`${entry.code} → ${fnName}() wire message renders to real text (not the raw key)`, () => {
      const fn = genericFactories[fnName];
      expect(typeof fn).toBe("function");

      const result = fn!();
      expect(result.errorCode).toBe(entry.code);

      const msg = result.messages[0];
      expect(msg).toBeDefined();

      const rendered = translator.t("en-US", msg!);
      // Renders to something OTHER than the raw wire key — proves the key
      // resolved in the catalog (the render fix: the factory references the TK
      // constant whose value is the snake key, never the raw symbol path).
      expect(rendered).not.toBe(msg!.key);
      expect(rendered.length).toBeGreaterThan(0);

      const expectedText = enUsCatalog[msg!.key];
      expect(expectedText).toBeDefined();
      expect(rendered).toBe(expectedText);

      // The wire key must be the inverse-snake of the spec's userMessageKey
      // (TK.Common.Errors.UNKNOWN → common_errors_UNKNOWN) — pins the code ↔
      // userMessageKey link for the two name-mismatch quirks
      // (UNHANDLED_EXCEPTION → UNKNOWN, RATE_LIMITED → TOO_MANY_REQUESTS).
      expect(msg!.key).toBe(snakeFromSymbolPath(entry.userMessageKey));
    });
  }
});
