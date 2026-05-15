// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

/**
 * Envelope shape for every committed fixture file. Metadata fields
 * (`$schema`, `$comment`, `scenario`) annotate the file; the `data`
 * payload is the only field compared by parity tests.
 */
export interface FixtureEnvelope<T> {
  readonly $schema?: string;
  readonly $comment?: string;
  readonly scenario: string;
  readonly data: T;
}

/**
 * Resolve a fixture file URL relative to the contract-tests package
 * root. The TS-side test code and the .NET emitter both reference the
 * SAME repo path; the URL form keeps the resolution working under
 * Vitest's various worker layouts.
 */
export function fixtureUrl(catalog: string, scenario: string): URL {
  return new URL(`../fixtures/${catalog}/${scenario}.json`, import.meta.url);
}

/**
 * Read and parse a fixture file. Throws if the file does not exist —
 * the failure message names the path so a missing-fixture mistake is
 * obvious.
 */
export function loadFixture<T>(
  catalog: string,
  scenario: string,
): FixtureEnvelope<T> {
  const url = fixtureUrl(catalog, scenario);
  const path = fileURLToPath(url);
  const raw = readFileSync(path, "utf8");
  return JSON.parse(raw) as FixtureEnvelope<T>;
}
