// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * `pnpm geo:overlays` — enumerates every overlay entry across all overlay files at
 * `contracts/geo/overlays/*.overlays.spec.json` so operators can audit active policy
 * decisions without grepping the files by hand.
 *
 * Output: per-catalog grouped listing with id + addedAt + reason + addedBy (when set)
 * for each addition / override / removal entry. Exits 0 always — informational only,
 * not a gate.
 *
 * Usage:
 *   pnpm geo:overlays                     human-readable summary to stdout
 *   pnpm geo:overlays --json              structured JSON to stdout (for tooling)
 */

import {
  loadCountriesOverlay,
  loadLocalesOverlay,
  loadSubdivisionsOverlay,
} from "../tier-2/load-overlays.js";

const wantsJson = process.argv.includes("--json");

const [countries, subdivisions, locales] = await Promise.all([
  loadCountriesOverlay(),
  loadSubdivisionsOverlay(),
  loadLocalesOverlay(),
]);

interface Entry {
  catalog: string;
  operation: "addition" | "override" | "removal";
  id: string;
  addedAt: string;
  reason: string;
  addedBy?: string;
  fieldsTouched?: string[];
}

const entries: Entry[] = [];

if (countries) {
  for (const a of countries.additions) {
    entries.push({
      catalog: "countries",
      operation: "addition",
      id: a.id,
      addedAt: a.addedAt,
      reason: a.reason,
      addedBy: a.addedBy,
    });
  }
  for (const o of countries.overrides) {
    entries.push({
      catalog: "countries",
      operation: "override",
      id: o.id,
      addedAt: o.addedAt,
      reason: o.reason,
      addedBy: o.addedBy,
      fieldsTouched: Object.keys(o.fields),
    });
  }
  for (const r of countries.removals) {
    entries.push({
      catalog: "countries",
      operation: "removal",
      id: r.id,
      addedAt: r.addedAt,
      reason: r.reason,
      addedBy: r.addedBy,
    });
  }
}

if (subdivisions) {
  for (const a of subdivisions.additions) {
    entries.push({
      catalog: "subdivisions",
      operation: "addition",
      id: a.id,
      addedAt: a.addedAt,
      reason: a.reason,
      addedBy: a.addedBy,
    });
  }
  for (const o of subdivisions.overrides) {
    entries.push({
      catalog: "subdivisions",
      operation: "override",
      id: o.id,
      addedAt: o.addedAt,
      reason: o.reason,
      addedBy: o.addedBy,
      fieldsTouched: Object.keys(o.fields),
    });
  }
  for (const r of subdivisions.removals) {
    entries.push({
      catalog: "subdivisions",
      operation: "removal",
      id: r.id,
      addedAt: r.addedAt,
      reason: r.reason,
      addedBy: r.addedBy,
    });
  }
}

if (locales) {
  for (const a of locales.additions) {
    entries.push({
      catalog: "locales",
      operation: "addition",
      id: a.id,
      addedAt: a.addedAt,
      reason: a.reason,
      addedBy: a.addedBy,
    });
  }
  for (const o of locales.overrides) {
    entries.push({
      catalog: "locales",
      operation: "override",
      id: o.id,
      addedAt: o.addedAt,
      reason: o.reason,
      addedBy: o.addedBy,
      fieldsTouched: Object.keys(o.fields),
    });
  }
  for (const r of locales.removals) {
    entries.push({
      catalog: "locales",
      operation: "removal",
      id: r.id,
      addedAt: r.addedAt,
      reason: r.reason,
      addedBy: r.addedBy,
    });
  }
}

if (wantsJson) {
  console.log(
    JSON.stringify({ totalEntries: entries.length, entries }, null, 2),
  );
  process.exit(0);
}

if (entries.length === 0) {
  console.log(
    "No active overlays. (Looked at: contracts/geo/overlays/countries.overlays.spec.json, contracts/geo/overlays/subdivisions.overlays.spec.json, contracts/geo/overlays/locales.overlays.spec.json)",
  );
  console.log(
    "See contracts/geo/overlays/README.md for when to add an overlay vs fix upstream.",
  );
  process.exit(0);
}

console.log(
  `Active overlays — ${entries.length} total across all overlay files.\n`,
);

const byCatalog = new Map<string, Entry[]>();
for (const e of entries) {
  const list = byCatalog.get(e.catalog) ?? [];
  list.push(e);
  byCatalog.set(e.catalog, list);
}

for (const [catalog, list] of byCatalog.entries()) {
  console.log(`━━━ ${catalog.toUpperCase()} ━━━`);
  list.sort(
    (a, b) => a.addedAt.localeCompare(b.addedAt) || a.id.localeCompare(b.id),
  );
  for (const e of list) {
    const op = e.operation.padEnd(8);
    const id = e.id.padEnd(6);
    const fields = e.fieldsTouched
      ? ` fields=[${e.fieldsTouched.join(", ")}]`
      : "";
    const by = e.addedBy ? ` (added by ${e.addedBy})` : "";
    console.log(`  ${op}  ${id}  ${e.addedAt}${fields}${by}`);
    console.log(`            reason: ${e.reason}`);
  }
  console.log();
}

console.log(
  `Edit overlay files at contracts/geo/overlays/. Then run \`pnpm geo:refresh\` to apply.`,
);
