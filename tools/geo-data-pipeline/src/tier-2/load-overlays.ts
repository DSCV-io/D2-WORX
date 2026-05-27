// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFile, stat } from "node:fs/promises";
import { resolve } from "node:path";
import { REPO_ROOT_PATH } from "../util/cache.js";
import type {
  SrcDataCountry,
  SrcDataLocale,
  SrcDataSubdivision,
} from "./load-src-data.js";

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

export interface SubdivisionAdditionEntry extends TrackedEntry {
  data: SrcDataSubdivision;
}

export interface SubdivisionOverrideEntry extends TrackedEntry {
  fields: Partial<SrcDataSubdivision>;
}

export type SubdivisionRemovalEntry = TrackedEntry;

export interface SubdivisionsOverlayFile {
  $generated: false;
  $source: "manual-overlay";
  $schema: string;
  $note: string;
  catalogVersion: string;
  lastEditedAt: string;
  additions: SubdivisionAdditionEntry[];
  overrides: SubdivisionOverrideEntry[];
  removals: SubdivisionRemovalEntry[];
}

export interface LocaleAdditionEntry extends TrackedEntry {
  data: SrcDataLocale;
}

export interface LocaleOverrideEntry extends TrackedEntry {
  fields: Partial<SrcDataLocale>;
}

export type LocaleRemovalEntry = TrackedEntry;

export interface LocalesOverlayFile {
  $generated: false;
  $source: "manual-overlay";
  $schema: string;
  $note: string;
  catalogVersion: string;
  lastEditedAt: string;
  additions: LocaleAdditionEntry[];
  overrides: LocaleOverrideEntry[];
  removals: LocaleRemovalEntry[];
}

/**
 * Diagnostic record of every overlay entry the Tier 2 build applied. Surfaces via
 * the `pnpm geo:overlays` CLI so operators can audit policy decisions without
 * grepping every overlay file by hand.
 */
export interface OverlaysApplied {
  countries: {
    additions: Array<{
      id: string;
      addedAt: string;
      reason: string;
      addedBy?: string;
    }>;
    overrides: Array<{
      id: string;
      addedAt: string;
      reason: string;
      fields: string[];
      addedBy?: string;
    }>;
    removals: Array<{
      id: string;
      addedAt: string;
      reason: string;
      addedBy?: string;
    }>;
  };
  subdivisions: {
    additions: Array<{
      id: string;
      addedAt: string;
      reason: string;
      addedBy?: string;
    }>;
    overrides: Array<{
      id: string;
      addedAt: string;
      reason: string;
      fields: string[];
      addedBy?: string;
    }>;
    removals: Array<{
      id: string;
      addedAt: string;
      reason: string;
      addedBy?: string;
    }>;
  };
  locales: {
    additions: Array<{
      id: string;
      addedAt: string;
      reason: string;
      addedBy?: string;
    }>;
    overrides: Array<{
      id: string;
      addedAt: string;
      reason: string;
      fields: string[];
      addedBy?: string;
    }>;
    removals: Array<{
      id: string;
      addedAt: string;
      reason: string;
      addedBy?: string;
    }>;
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

export async function loadSubdivisionsOverlay(): Promise<SubdivisionsOverlayFile | null> {
  const path = resolve(OVERLAYS_DIR, "subdivisions.overlays.spec.json");
  const exists = await stat(path).catch(() => null);
  if (!exists?.isFile()) return null;
  const text = await readFile(path, "utf8");
  return JSON.parse(text) as SubdivisionsOverlayFile;
}

/**
 * Applies a subdivisions overlay to a Tier 1 subdivisions list. Returns the patched
 * list + a diagnostic record of every patch applied (for the geo:overlays CLI).
 *
 * Apply order per overlays/README.md: additions append → overrides patch fields →
 * removals drop entries. An override on a removed id is a no-op; an addition with
 * an id matching an existing Tier 1 entry throws (caller should use an override
 * instead).
 */
export function applySubdivisionsOverlay(
  tier1Subdivisions: SrcDataSubdivision[],
  overlay: SubdivisionsOverlayFile | null,
): {
  subdivisions: SrcDataSubdivision[];
  applied: OverlaysApplied["subdivisions"];
} {
  const applied: OverlaysApplied["subdivisions"] = {
    additions: [],
    overrides: [],
    removals: [],
  };
  if (!overlay) return { subdivisions: tier1Subdivisions, applied };

  const byCode = new Map<string, SrcDataSubdivision>();
  for (const s of tier1Subdivisions) byCode.set(s.iso31662Code, s);

  // 1. Additions — append new entries; refuse to silently shadow existing
  for (const add of overlay.additions) {
    if (byCode.has(add.id)) {
      throw new Error(
        `Overlay addition collides with Tier 1: subdivisions[${add.id}] already exists ` +
          `in Tier 1 src-data. Use an override entry instead, or remove the Tier 1 entry ` +
          `via a removal first if the upstream is wrong.`,
      );
    }
    if (add.data.iso31662Code !== add.id) {
      const dataCode = add.data.iso31662Code;
      throw new Error(
        `Overlay addition id="${add.id}" doesn't match data.iso31662Code="${dataCode}".`,
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
        `Overlay override targets subdivisions[${ov.id}] but no such entry exists ` +
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

  // Preserve stable ordering: sort by ISO 3166-2 code (matches Tier 1 sort)
  const subdivisions = [...byCode.values()].sort((a, b) =>
    a.iso31662Code.localeCompare(b.iso31662Code),
  );

  return { subdivisions, applied };
}

export async function loadLocalesOverlay(): Promise<LocalesOverlayFile | null> {
  const path = resolve(OVERLAYS_DIR, "locales.overlays.spec.json");
  const exists = await stat(path).catch(() => null);
  if (!exists?.isFile()) return null;
  const text = await readFile(path, "utf8");
  return JSON.parse(text) as LocalesOverlayFile;
}

/**
 * Applies a locales overlay to a Tier 1 locales list. Returns the patched list +
 * a diagnostic record of every patch applied (for the geo:overlays CLI).
 *
 * Apply order per overlays/README.md: additions append → overrides patch fields →
 * removals drop entries. An override on a removed id is a no-op; an addition with
 * an id matching an existing Tier 1 entry throws (caller should use an override
 * instead).
 */
export function applyLocalesOverlay(
  tier1Locales: SrcDataLocale[],
  overlay: LocalesOverlayFile | null,
): { locales: SrcDataLocale[]; applied: OverlaysApplied["locales"] } {
  const applied: OverlaysApplied["locales"] = {
    additions: [],
    overrides: [],
    removals: [],
  };
  if (!overlay) return { locales: tier1Locales, applied };

  const byTag = new Map<string, SrcDataLocale>();
  for (const l of tier1Locales) byTag.set(l.ietfBcp47Tag, l);

  // 1. Additions — append new entries; refuse to silently shadow existing
  for (const add of overlay.additions) {
    if (byTag.has(add.id)) {
      throw new Error(
        `Overlay addition collides with Tier 1: locales[${add.id}] already exists ` +
          `in Tier 1 src-data. Use an override entry instead, or remove the Tier 1 entry ` +
          `via a removal first if the upstream is wrong.`,
      );
    }
    if (add.data.ietfBcp47Tag !== add.id) {
      const dataTag = add.data.ietfBcp47Tag;
      throw new Error(
        `Overlay addition id="${add.id}" doesn't match data.ietfBcp47Tag="${dataTag}".`,
      );
    }
    byTag.set(add.id, add.data);
    applied.additions.push({
      id: add.id,
      addedAt: add.addedAt,
      reason: add.reason,
      addedBy: add.addedBy,
    });
  }

  // 2. Overrides — patch named fields on existing entries
  for (const ov of overlay.overrides) {
    const existing = byTag.get(ov.id);
    if (!existing) {
      throw new Error(
        `Overlay override targets locales[${ov.id}] but no such entry exists ` +
          `in Tier 1 (post-additions). Use an addition entry instead, or remove the override.`,
      );
    }
    const patched = { ...existing, ...ov.fields };
    byTag.set(ov.id, patched);
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
    if (byTag.delete(rm.id)) {
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

  // Preserve stable ordering: sort by IETF BCP 47 tag (matches Tier 1 sort)
  const locales = [...byTag.values()].sort((a, b) =>
    a.ietfBcp47Tag.localeCompare(b.ietfBcp47Tag),
  );

  return { locales, applied };
}
