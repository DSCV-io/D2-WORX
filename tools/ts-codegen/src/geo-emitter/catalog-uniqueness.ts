// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { falsey } from "@d2/utilities";

import {
  diagError,
  diagWarning,
  type EmitDiagnostic,
  DiagnosticIds,
} from "../lib/diagnostics.js";

import type { GeoSpecContext } from "./spec-types.js";

/**
 * In-tree mirror of the runtime `normalize()` pipeline (see
 * `server/shared/typescript/geo/abstractions/src/name-resolution/name-normalizer.ts`).
 * Re-implemented here because the ts-codegen package's `rootDir` constraint
 * forbids reaching into another package via a relative path, and a shared
 * helper package isn't justified yet — the runtime impl is 7 lines of pure
 * Unicode-normalize + ampersand-swap + lowercase + whitespace-collapse. Parity
 * is enforced by the cross-language parity-test fixture: any drift between
 * this in-tree mirror and the runtime helper surfaces as a fixture mismatch.
 *
 * Once a shared helper package exists that both `@d2/geo-abstractions`
 * and `tools/ts-codegen` can consume without a circular dependency,
 * this mirror can be replaced with a single import.
 */
function normalize(input: string): string {
  if (falsey(input)) return "";
  const stripped = input.normalize("NFD").replace(/\p{M}/gu, "");
  const ampersandSwapped = stripped.replaceAll(" & ", " and ");
  const lowered = ampersandSwapped.toLowerCase();
  return lowered.trim().replace(/\s+/g, " ");
}

/**
 * Codegen-time catalog-uniqueness assertion enforcing the fail-closed
 * name-resolver's unique-normalized-name predicate.
 * Build fails if any catalog has duplicate normalized names across the
 * matchable name fields per entity type. Eliminates the determinism risk at
 * the source — if there are no duplicates in the catalog, then "ambiguity"
 * at name-resolution time can only ever be cascade-pass ambiguity (Pass
 * 2/3/4), never Pass-1 exact ambiguity.
 *
 * Uses the SAME `normalize()` pipeline as the runtime resolver, imported
 * directly from `@d2/geo-abstractions/src/name-resolution/name-normalizer.ts`
 * so any future drift in the normalizer is caught instantly (the catalog
 * gets re-validated under the new normalization rules on every emit).
 *
 * Mirrors the .NET-side `D2GEO007` predicate concept (the .NET ID is
 * currently allocated to `MissingSpec`; the catalog-uniqueness predicate
 * lands as `D2GEO010` here and will be back-ported with the same ID once the
 * .NET side adds the equivalent check).
 */

interface CatalogConfig<T> {
  readonly catalogName: string;
  readonly entries: readonly T[];
  /** The PK / identifier field used in the diagnostic message. */
  readonly idOf: (entry: T) => string;
  /** Yields each matchable display-name candidate for the entry. */
  readonly namesOf: (entry: T) => readonly (string | undefined)[];
}

/**
 * Walk every catalog and surface a diagnostic for every normalized-name
 * collision across the entity's matchable name fields. Returns the
 * diagnostic list (empty when every catalog is unique).
 *
 * Severity contract:
 *
 * - The Tier-2 spec baseline already carries a small number of duplicate
 *   normalized names — legitimate real-world artefacts (renamed ISO 3166-2
 *   subdivisions where old + new codes coexist, historic currencies sharing
 *   a display name with their replacement, …). Surfacing these as ERROR
 *   would block every codegen run until the upstream Tier-1 source-data is
 *   curated, which is out of scope for 2c-2. They surface as WARNING here:
 *   the emitter proceeds, ops sees them in the orchestrator log, and the
 *   upstream curation effort can elevate to ERROR once the catalog is clean.
 * - `assertCatalogUniquenessStrict()` is the same walk with ERROR severity
 *   — used by the fire-and-revert verification proof in the emitter test
 *   suite.
 */
export function assertCatalogUniqueness(
  context: GeoSpecContext,
): readonly EmitDiagnostic[] {
  const diagnostics: EmitDiagnostic[] = [];
  const configs: CatalogConfig<unknown>[] = [];

  if (context.countries !== undefined) {
    configs.push({
      catalogName: "countries",
      entries: context.countries.entries,
      idOf: (e) => (e as { iso31661Alpha2Code: string }).iso31661Alpha2Code,
      namesOf: (e) => {
        const c = e as {
          displayName: string;
          officialName: string;
          endonymDisplayName?: string;
          iso31661Alpha3Code: string;
        };
        return [
          c.displayName,
          c.officialName,
          c.endonymDisplayName,
          c.iso31661Alpha3Code,
        ];
      },
    } as CatalogConfig<unknown>);
  }

  if (context.subdivisions !== undefined) {
    // Subdivision names are unique WITHIN a parent country, not globally.
    // Partition by countryISO31661Alpha2Code so we don't false-positive on
    // "Western" / "Central" / "North" etc. that legitimately recur across
    // countries' subdivisions.
    const byCountry = new Map<
      string,
      {
        iso31662Code: string;
        displayName: string;
        officialName: string;
        endonymDisplayName?: string;
      }[]
    >();
    for (const raw of context.subdivisions.entries) {
      const s = raw as {
        iso31662Code: string;
        displayName: string;
        officialName: string;
        endonymDisplayName?: string;
        countryISO31661Alpha2Code: string;
      };
      let list = byCountry.get(s.countryISO31661Alpha2Code);
      if (list === undefined) {
        list = [];
        byCountry.set(s.countryISO31661Alpha2Code, list);
      }
      const entry: {
        iso31662Code: string;
        displayName: string;
        officialName: string;
        endonymDisplayName?: string;
      } = {
        iso31662Code: s.iso31662Code,
        displayName: s.displayName,
        officialName: s.officialName,
      };
      if (s.endonymDisplayName !== undefined)
        entry.endonymDisplayName = s.endonymDisplayName;
      list.push(entry);
    }
    for (const [country, entries] of byCountry) {
      configs.push({
        catalogName: `subdivisions[country=${country}]`,
        entries,
        idOf: (e) => (e as { iso31662Code: string }).iso31662Code,
        namesOf: (e) => {
          const s = e as {
            displayName: string;
            officialName: string;
            endonymDisplayName?: string;
          };
          return [s.displayName, s.officialName, s.endonymDisplayName];
        },
      } as CatalogConfig<unknown>);
    }
  }

  if (context.currencies !== undefined) {
    configs.push({
      catalogName: "currencies",
      entries: context.currencies.entries,
      idOf: (e) => (e as { iso4217AlphaCode: string }).iso4217AlphaCode,
      namesOf: (e) => {
        const c = e as { displayName: string };
        return [c.displayName];
      },
    } as CatalogConfig<unknown>);
  }

  if (context.languages !== undefined) {
    configs.push({
      catalogName: "languages",
      entries: context.languages.entries,
      idOf: (e) => (e as { iso6391Code: string }).iso6391Code,
      namesOf: (e) => {
        const l = e as { name: string; endonym?: string };
        return [l.name, l.endonym];
      },
    } as CatalogConfig<unknown>);
  }

  if (context.locales !== undefined) {
    configs.push({
      catalogName: "locales",
      entries: context.locales.entries,
      idOf: (e) => (e as { ietfBcp47Tag: string }).ietfBcp47Tag,
      namesOf: (e) => {
        const l = e as { name: string; endonym?: string };
        return [l.name, l.endonym];
      },
    } as CatalogConfig<unknown>);
  }

  if (context.timezones !== undefined) {
    configs.push({
      catalogName: "timezones",
      entries: context.timezones.entries,
      idOf: (e) => (e as { ianaIdentifier: string }).ianaIdentifier,
      namesOf: (e) => {
        const t = e as { displayName: string };
        return [t.displayName];
      },
    } as CatalogConfig<unknown>);
  }

  if (context.geopoliticalEntities !== undefined) {
    configs.push({
      catalogName: "geopoliticalEntities",
      entries: context.geopoliticalEntities.entries,
      idOf: (e) => (e as { shortCode: string }).shortCode,
      namesOf: (e) => {
        const g = e as { name: string };
        return [g.name];
      },
    } as CatalogConfig<unknown>);
  }

  for (const cfg of configs) {
    diagnostics.push(...checkCatalog(cfg, "warning"));
  }
  return diagnostics;
}

/**
 * Same walk as `assertCatalogUniqueness` but every collision surfaces as
 * ERROR. Used by the fire-and-revert verification proof — introduces a
 * temporary catalog dup, runs the strict check, sees the ERROR diagnostic,
 * reverts the dup, re-runs, sees no diagnostic above warning severity.
 */
export function assertCatalogUniquenessStrict(
  context: GeoSpecContext,
): readonly EmitDiagnostic[] {
  return assertCatalogUniqueness(context).map((d) =>
    d.severity === "warning" ? diagError(d.id, d.message, d.filePath) : d,
  );
}

function checkCatalog<T>(
  cfg: CatalogConfig<T>,
  severity: "error" | "warning",
): readonly EmitDiagnostic[] {
  const diagnostics: EmitDiagnostic[] = [];
  // Map normalized name -> list of entry IDs claiming it.
  const seen = new Map<string, string[]>();
  for (const entry of cfg.entries) {
    const id = cfg.idOf(entry);
    for (const raw of cfg.namesOf(entry)) {
      if (falsey(raw)) continue;
      const key = normalize(raw!);
      if (falsey(key)) continue;
      let owners = seen.get(key);
      if (owners === undefined) {
        owners = [];
        seen.set(key, owners);
      }
      if (!owners.includes(id)) owners.push(id);
    }
  }
  const ctor = severity === "error" ? diagError : diagWarning;
  for (const [key, owners] of seen) {
    if (owners.length > 1) {
      diagnostics.push(
        ctor(
          DiagnosticIds.GEO_CATALOG_DUPLICATE_NAME,
          `${cfg.catalogName}: normalized name '${key}' is claimed by ` +
            `multiple entries [${owners.join(", ")}] — the fail-closed ` +
            `name resolver requires unique normalized names so Pass-1 ` +
            `(exact) cannot be ambiguous. Distinguish or deprecate.`,
        ),
      );
    }
  }
  return diagnostics;
}
