<!--
Copyright (c) DCSV. All rights reserved.
-->

# `contracts/geo/overlays/` — trackable manual patches

## What this is

Hand-rolled overlay files applied at Tier 2 build time on top of Tier 1 src-data. Used when the upstream sources (CLDR / IANA tzdb / datasets/\* / Wikidata SPARQL / debian iso-codes) omit, mislabel, or wrongly include something the platform needs handled differently.

Each overlay entry carries `id` + `addedAt` + `reason` (+ optional `addedBy`) so the policy decision is audit-trail visible. `pnpm geo:overlays` from `tools/geo-data-pipeline/` lists every active patch with its reason.

## When to overlay vs fix upstream vs hand-roll

| Situation                                                                                                              | Decision                                                             |
| ---------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| Upstream is wrong AND can be fixed in datasets/CLDR/etc.                                                               | Fix upstream, wait for the next refresh.                             |
| Upstream is wrong / incomplete AND fixing upstream isn't viable (political deadlock, license issue, abandoned project) | **Add an overlay**. Document the why in `reason`.                    |
| Entity has NO upstream source whatsoever (supranational groupings, internal-only classifications)                      | **Hand-roll a Tier 2 peer** (see `geopolitical-entities.spec.json`). |
| Field-level policy disagreement with upstream (e.g., disputed border, contested currency status)                       | **Add an override**. Document the disagreement in `reason`.          |
| Upstream ships an entry we don't want to surface (e.g., a withdrawn ISO code that lingers in caches)                   | **Add a removal**. Document why in `reason`.                         |

## Apply order

`pnpm tier-2:build` flow becomes:

1. Load Tier 1 src-data (upstream-faithful)
2. Load `overlays/*.overlays.spec.json` (manual patches)
3. **Apply additions** — append new entries to the per-catalog list
4. **Apply overrides** — patch named fields on existing entries
5. **Apply removals** — drop named entries from the per-catalog list
6. Compute cross-catalog M:M + denormalization (as today)
7. Write Tier 2 output

Result: overlay-injected entries flow through the same Tier 2 logic as upstream entries, INCLUDING cross-catalog inverse-nav backfill. E.g., adding Kosovo (XK) via overlay automatically gets `geopoliticalEntityShortCodes: ["EU", "BALK"]` populated because the hand-rolled GE catalog references it — no manual coordination needed.

## File-per-entity convention

One overlay file per Tier 1 catalog: `{entity}.overlays.spec.json`. Currently:

| File                              | Patches                                      | Notes                                                                  |
| --------------------------------- | -------------------------------------------- | ---------------------------------------------------------------------- |
| `countries.overlays.spec.json`    | XK (Kosovo addition) + 5 overrides           | First overlay, sets the pattern                                        |
| `subdivisions.overlays.spec.json` | IR-22 (Hormozgān override)                   | Fixes CLDR stale-label bug after 2020-11-24 ISO 3166-2:IR reassignment |
| `locales.overlays.spec.json`      | fr-TF (French Southern Territories addition) | Fills CLDR availableLocales gap (no fr-TF entry shipped upstream)      |

Additional per-entity overlays land here when needed (one file per Tier 1 catalog).

## Entry shape

Three operation types, all sharing `id` / `addedAt` / `reason` (+ optional `addedBy`):

```json
{
  "additions": [
    {
      "id": "XK",
      "addedAt": "2026-05-19",
      "addedBy": "manual policy review",
      "reason": "Why this entry exists. Future maintainers + auditors read this without git-archaeology.",
      "data": {
        /* full Tier 1 SrcData<Entity> shape */
      }
    }
  ],
  "overrides": [
    {
      "id": "AR",
      "addedAt": "...",
      "reason": "Argentina uses USD as de-facto secondary; CLDR doesn't flag this.",
      "fields": {
        /* partial: only the fields being patched */
      }
    }
  ],
  "removals": [
    {
      "id": "XX",
      "addedAt": "...",
      "reason": "Upstream ships withdrawn ISO code lingering in their cache; we don't want to surface it."
    }
  ]
}
```

## Schema validation

Per overlay file has a sibling `{entity}.overlays.schema.json` JSON Schema. Validate via:

```bash
pnpm dlx ajv-cli@5 validate --spec=draft2020 \
  -s contracts/geo/overlays/countries.overlays.schema.json \
  -d contracts/geo/overlays/countries.overlays.spec.json \
  --strict=false
```

## Auditing active overlays

```bash
cd tools/geo-data-pipeline
pnpm geo:overlays   # lists every overlay entry across all overlay files with reason + addedAt
```

## Design rationale: overlays vs hand-editing Tier 1 src-data

Tier 1 = upstream-faithful, never hand-edited. The next `pnpm geo:refresh` would overwrite any manual edit. The overlay layer is the persistence point — overlays survive refresh because they're applied AFTER Tier 1 ingestion.

The `addedAt` + `reason` discipline is what makes overlays TRACKABLE rather than mysterious git-archaeology fodder. A future maintainer reading the overlay can see exactly why XK is there without needing to find the right commit + read the message.
