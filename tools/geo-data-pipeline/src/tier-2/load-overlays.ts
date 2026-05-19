// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFile, stat } from "node:fs/promises";
import { resolve } from "node:path";
import { REPO_ROOT_PATH } from "../util/cache.js";
import type { SrcDataCountry } from "./load-src-data.js";

const OVERLAYS_DIR = resolve(REPO_ROOT_PATH, "contracts", "geo", "overlays");

/**
 * Trackable manual patches applied at Tier 2 build time on top of Tier 1 src-data.
 * See `contracts/geo/overlays/README.md` for the pattern + when to overlay vs fix
 * upstream vs hand-roll.
 *
 * Each entry MUST carry `id` + `addedAt` + `reason` so the policy decision is
 * audit-trail visible — `pnpm geo:overlays` enumerates active patches.
 */

interface TrackedEntry {
  id: string;
  addedAt: string;
  reason: string;
  addedBy?: string;
}

export interface CountryAdditionEntry extends TrackedEntry {
  data: SrcDataCountry;
}

export interface CountryOverrideEntry extends TrackedEntry {
  fields: Partial<SrcDataCountry>;
}

export type CountryRemovalEntry = TrackedEntry;

export interface CountriesOverlayFile {
  $generated: false;
  $source: "manual-overlay";
  $schema: string;
  $note: string;
  catalogVersion: string;
  lastEditedAt: string;
  additions: CountryAdditionEntry[];
  overrides: CountryOverrideEntry[];
  removals: CountryRemovalEntry[];
}

/**
 * Diagnostic record of every overlay entry the Tier 2 build applied. Surfaces via
 * the `pnpm geo:overlays` CLI so operators can audit policy decisions without
 * grepping every overlay file by hand.
 */
export interface OverlaysApplied {
  countries: {
    additions: Array<{ id: string; addedAt: string; reason: string; addedBy?: string }>;
    overrides: Array<{
      id: string;
      addedAt: string;
      reason: string;
      fields: string[];
      addedBy?: string;
    }>;
    removals: Array<{ id: string; addedAt: string; reason: string; addedBy?: string }>;
  };
}

export async function loadCountriesOverlay(): Promise<CountriesOverlayFile | null> {
  const path = resolve(OVERLAYS_DIR, "countries.overlays.spec.json");
  const exists = await stat(path).catch(() => null);
  if (!exists?.isFile()) return null;
  const text = await readFile(path, "utf8");
  return JSON.parse(text) as CountriesOverlayFile;
}

/**
 * Applies a countries overlay to a Tier 1 countries list. Returns the patched list +
 * a diagnostic record of every patch applied (for the geo:overlays CLI).
 *
 * Apply order per overlays/README.md: additions append → overrides patch fields →
 * removals drop entries. An override on a removed id is a no-op; an addition with
 * an id matching an existing Tier 1 entry throws (caller should use an override
 * instead).
 */
export function applyCountriesOverlay(
  tier1Countries: SrcDataCountry[],
  overlay: CountriesOverlayFile | null,
): { countries: SrcDataCountry[]; applied: OverlaysApplied["countries"] } {
  const applied: OverlaysApplied["countries"] = {
    additions: [],
    overrides: [],
    removals: [],
  };
  if (!overlay) return { countries: tier1Countries, applied };

  const byCode = new Map<string, SrcDataCountry>();
  for (const c of tier1Countries) byCode.set(c.iso31661Alpha2Code, c);

  // 1. Additions — append new entries; refuse to silently shadow existing
  for (const add of overlay.additions) {
    if (byCode.has(add.id)) {
      throw new Error(
        `Overlay addition collides with Tier 1: countries[${add.id}] already exists ` +
        `in Tier 1 src-data. Use an override entry instead, or remove the Tier 1 entry ` +
        `via a removal first if the upstream is wrong.`,
      );
    }
    if (add.data.iso31661Alpha2Code !== add.id) {
      const dataCode = add.data.iso31661Alpha2Code;
      throw new Error(
        `Overlay addition id="${add.id}" doesn't match data.iso31661Alpha2Code="${dataCode}".`,
      );
    }
    byCode.set(add.id, add.data);
    applied.additions.push({
      id: add.id,
      addedAt: add.addedAt,
      reason: add.reason,
      addedBy: add.addedBy,
    });
  }

  // 2. Overrides — patch named fields on existing entries
  for (const ov of overlay.overrides) {
    const existing = byCode.get(ov.id);
    if (!existing) {
      throw new Error(
        `Overlay override targets countries[${ov.id}] but no such entry exists ` +
        `in Tier 1 (post-additions). Use an addition entry instead, or remove the override.`,
      );
    }
    const patched = { ...existing, ...ov.fields };
    byCode.set(ov.id, patched);
    applied.overrides.push({
      id: ov.id,
      addedAt: ov.addedAt,
      reason: ov.reason,
      fields: Object.keys(ov.fields),
      addedBy: ov.addedBy,
    });
  }

  // 3. Removals — drop entries by id (after overrides, so override-then-remove is order-safe)
  for (const rm of overlay.removals) {
    if (byCode.delete(rm.id)) {
      applied.removals.push({
        id: rm.id,
        addedAt: rm.addedAt,
        reason: rm.reason,
        addedBy: rm.addedBy,
      });
    }
    // If the id wasn't present, silent no-op — could mean the upstream already
    // removed it, which is the goal of the overlay anyway.
  }

  // Preserve stable ordering: sort by ISO 3166-1 alpha-2 code (matches Tier 1 sort)
  const countries = [...byCode.values()].sort((a, b) =>
    a.iso31661Alpha2Code.localeCompare(b.iso31661Alpha2Code),
  );

  return { countries, applied };
}
