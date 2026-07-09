---
name: regen
description: Codegen regeneration matrix - which pipeline owns which generated output and the exact regen command, plus the byte-stability check. Use before/after editing any spec or generator. Keywords - codegen, regenerate, generated, .g.cs, .g.ts, source-gen, proto, typespec, emitter, §26.5.
---

<!--
Copyright (c) DCSV. All rights reserved.
-->

# regen — codegen regeneration matrix

Distilled from `docs/SRC_GEN.md` + `docs/COMMANDS.md` (both canonical — if this disagrees, they win; update in lockstep §11.32). **NEVER hand-edit generated output (§26.5)** — fix the GENERATOR / the INPUT / the pipeline and REGENERATE. "Generated" = `*.g.*`, anything under `Generated/`, any documented-pipeline output, anything banner-marked.

## Which pipeline owns which output → regen command
| Output family | Owner pipeline | Regen command |
| --- | --- | --- |
| .NET `*.g.cs` (Roslyn `IIncrementalGenerator`: error-codes, auth-context, geo, field-constraints, encryption-frame, …) | source-gen in the owning csproj | `dotnet build server/D2.slnx` (or the single owning `*.csproj`) |
| TS `*.g.ts` constant catalogs (auth-context, request-context, error-codes[-registry], error-category, problem-details, grpc-trailers, otel-messaging-tags, encryption-domains/-frame[-sealed], dlq-failure-metadata, geo-abstractions, field-constraints) | `tools/ts-codegen` | `pnpm --filter ts-codegen codegen` (one target: `pnpm --filter ts-codegen codegen:<target>`; force: `… codegen --force`) |
| TypeSpec operation-contract artifact fleet (client DTOs, façade, gRPC, DI) | `tools/scripts/regen-typespec-emitters.mjs` (drives `tsp compile` + `COPY_MANIFEST`) | `node tools/scripts/regen-typespec-emitters.mjs` |
| Proto-derived TS (`@d2/protos`) | `buf` + `ts-proto` | `pnpm --filter @d2/protos generate` |
| ContractFixtures golden files (`Integration/ContractFixtures/*FixtureEmitter`, incl. `MqGoldenMessageFixtureEmitter`) | emitter tests in `D2.Shared.Tests` | `dotnet test server/shared/dotnet/tests/D2.Shared.Tests.csproj` (emitters WriteAllText the goldens; MTP ignores `--filter`, so the whole project run regenerates them) |
| Release baselines (`PublicAPI.*.txt`, `.api.md`, `.release-fingerprint`) | seed scripts | use the `reseed-baselines` skill |

## Byte-stability check convention
After regenerating, run the SAME regen command a SECOND time. The `git status --porcelain` delta must NOT grow between run #1 and run #2 — a stable generator is idempotent (emitted files are LF-normalized + byte-equal on no-op). A growing delta on the second run signals a non-deterministic emitter (ordering, timestamps) — fix the generator, not the output.

## Reminders
- Hand-authored files MUST NOT carry a generated banner or `.g.*` extension (§26.18) — normal header + plain extension + a ledger note.
- Proto is the wire source of truth; a hand-written DTO mirroring a `.proto` / `.spec.json` / `.openapi.yaml` shape in a published package is a process-integrity failure (§26.1).
- Emitters reference the TK CONSTANT, never a string-literal key/symbol-path (§26.7).
