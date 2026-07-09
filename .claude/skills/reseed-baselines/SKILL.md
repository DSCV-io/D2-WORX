---
name: reseed-baselines
description: Reseed consumable-package release baselines (PublicAPI, .api.md, .release-fingerprint) when a fingerprint-currency pre-commit check fails or after touching consumable sources. Keywords - baseline, fingerprint, check-baselines, PublicAPI, api-extractor, release, stale, §26.20.
allowed-tools: Bash, Read
---

<!--
Copyright (c) DCSV. All rights reserved.
-->

# reseed-baselines

Regenerate every consumable-package release baseline and prove currency is stable. Backs rules.md §26.20 (re-seed after touching any consumable source; stale baseline = FINDING-HIGH).

## When to use
- The pre-commit `check-baselines` gate rejected a commit as fingerprint-stale.
- You edited any consumable source under `server/shared/typescript/**`, `server/shared/dotnet/**` (PublicAPI-tracked), or `server/services/edge/key-custodian/client-ts`.

## Run
```
bash .claude/skills/reseed-baselines/scripts/reseed.sh
```
Exit 0 = every baseline reseeded and currency all-current across two consecutive runs. Non-zero = investigate the printed step.

## What it does (order is load-bearing)
1. `node tools/scripts/seed-publicapi-baselines.mjs` — .NET `PublicAPI.Shipped/Unshipped.txt` + `.release-fingerprint`.
2. Build the TS consumable dists (`pnpm -r --filter "./server/shared/typescript/**" --filter ".../client-ts" build`) — api-extractor reads `dist/index.d.ts`.
3. `node tools/scripts/seed-apiextractor-baselines.mjs` — TS `etc/<pkg>.api.md` + `etc/.release-fingerprint`.
4. `pnpm --filter release-runner check-baselines` TWICE.

## Known flap (why the ×2 stability check)
The post-seed `tsc -b` of a TS package can move its `.api.md`-derived fingerprint on the FIRST currency run; the SECOND run must then report all-current. If run #2 is still not all-current, the baselines are genuinely stale — re-run the script. Both runs all-current ⇒ record `Baseline currency: PASS`.

## Gotcha — csharp-ls DLL lock
`csharp-ls.exe` locks source-gen DLLs, so the `.NET` seed fails with MSB3021/3027 while it runs. The script DETECTS it and aborts with the PID + `taskkill //F //PID <pid>` instruction — it never auto-kills (killing the LSP is your call). Kill it, then re-run.

## After a green reseed
Re-stage the changed baseline files (and for `.NET`, confirm `PublicAPI.Unshipped.txt` is header-only) before committing. These files are PIPELINE OUTPUT — never hand-edit (§26.5).
