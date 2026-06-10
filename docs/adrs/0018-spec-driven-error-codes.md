<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0018: Spec-driven cross-service error codes + a closed `ErrorCategory` semantic set

- **Status**: Accepted
- **Date**: 2026-06-09
- **Deliverable**: 0017-error-codes

## Context

Every handler in every D²-WORX service fails in a finite, well-understood set of ways, and each failure must travel a long path: a domain factory stamps an error code, a transport carries it (gRPC envelope / HTTP ProblemDetails), and a remote consumer — possibly in the other runtime — has to make sense of it. For that path to be sound, an error code is not just a string: it is a contract that binds together an HTTP status, a semantic class, and a user-facing translation key, and that binding must agree byte-for-byte across .NET and TypeScript.

Before this deliverable the binding was fractured:

- **Codes were free text.** A handler could `Fail()` with any string. Nothing enforced that the string was declared anywhere, was unique, or was spelled the same on both runtimes.
- **The code↔status↔message links were hand-maintained and drifted.** The HTTP status and the default `TKMessage` for a code lived buried inside C# factory bodies (`D2Result.NotFound()` hard-coded both). The generic `error-codes.spec.json` carried only `code` + `httpStatus` — no `userMessageKey` at all. The one catalog that already linked a code to its translation key was `auth-error-codes.spec.json`; that pattern was a one-off, not the project convention.
- **Three disconnected catalogs.** Generic / auth / DB codes were separate stacks with near-identical but independently-maintained generators.
- **No cross-service resolution.** Given a wire code, no component could answer "what status / category / localized message does this map to?" without importing the producing service's catalog — which a central boundary (Edge / BFF) resolving *anybody's* code cannot do.
- **No semantic class on the wire.** A consumer wanting to "retry any infrastructure failure" had no way to classify a failure without a code-by-code lookup against the producer's catalog.

The forcing function was KeyCustodian (0016): its domain transitions needed to fail with custodian-specific, cross-service-resolvable codes. Building that on the fractured foundation would have cemented a half-baked precedent into the first service that consumes the error layer. The decision was made to fix the foundation first.

This decision is a concrete, large instance of the spec-driven codegen architecture (ADR-0002): error codes become one more cross-language catalog, generated rather than hand-copied. It extends the `D2Result` model (ADR-0003) by making the `(code, httpStatus, default message)` triple spec-sourced instead of factory-body-sourced, and it leans on the `TKMessage` translation-key-as-type guarantee (ADR-0004) for the `userMessageKey` link.

## Decision

Every error code in the system is declared in a `*-error-codes.spec.json` file conforming to one canonical schema, and the whole error layer — constants, factories, the cross-service registry, and the `ErrorCategory` enum — is generated from those specs.

**One canonical schema, seven fields per code.** `contracts/error-codes/error-codes.canonical.schema.json` (draft-07) defines each entry as `code` (SCREAMING_SNAKE, unique) · `httpStatus` (closed enum) · `category` (closed semantic enum) · `userMessageKey` (a `TK.*` symbol-path reference) · `factoryName` (the emitted factory method) · `factoryShape` (a closed enum — `standard`, the one universal error-factory shape `messages?, inputErrors?, errorCode?, category?, traceId?` (all optional), or `none` (constant + boolean only) — that drives the generated factory's *signature* so regeneration is byte-identical) · `doc`. The generic cross-cutting catalog (`contracts/error-codes/error-codes.spec.json`) and every per-domain catalog (`contracts/<domain>-error-codes/<domain>-error-codes.spec.json`) conform to this shape; per-domain `schema.json` files copy it verbatim (narrowed) rather than `$ref` it, because a `$ref`+`allOf` overlay collides with the canonical's `additionalProperties: false`.

**Codegen emits both runtimes from the same spec.** Per catalog the generator (one generalized .NET Roslyn engine in `source-gen-shared/error-codes-emit/` shared by per-catalog generator shells; one generalized `tools/ts-codegen` emitter) emits `<Domain>ErrorCodes` constants, typed `<Domain>Failures` / `<Domain>Failures<T>` `D2Result` factories (delegating to the `httpStatus`-selected base factory and stamping the domain `code` + `userMessageKey`), and per-code booleans. Framework-universal codes stay on `D2Result` / `D2Result<T>` directly; domain codes live on the `<Domain>Failures` classes — the call site names the scope.

**Code uniqueness is structural.** The generator derives a domain token from the catalog filename and requires every per-domain code to be `<DOMAIN>_*` (`AUTH_*`, `GEO_*`, …). The generic catalog owns the sole reserved unprefixed namespace (`NOT_FOUND`, `CONFLICT`, …). Prefix-as-namespace makes collisions structurally hard; the merged-registry build is the backstop that hard-fails on any cross-catalog collision.

**A merged cross-service registry.** A second generator aggregates *every* `*-error-codes.spec.json` into `D2.Shared.ErrorCodes.Registry` / `@d2/error-codes-registry`, emitting `code → { httpStatus, category, userMessageKey, factoryName, factoryShape, doc, domain }` plus a resolution API (`TryResolve` / `Resolve` / `All` ↔ `resolve` / `has` / `all`). Any service — typically a central Edge / BFF boundary — can resolve any other service's code to its status, category, and localized message without importing the producer's catalog. Services that handle *specific* codes import only the catalogs they branch on; generic class-based handling rides the wire `category` instead.

**`ErrorCategory` is a closed 9-value semantic set in a foundational zero-dep lib.** `category` is a closed enum — `validation_failure`, `not_found`, `conflict`, `policy_denied`, `rate_limited`, `payload_too_large`, `infrastructure_unavailable`, `internal_error`, `partial_success` — that classifies a failure for telemetry and for generic class-based handling. It is **not** the factory selector (`httpStatus` selects the base factory). The enum, its wire mapping (`ErrorCategoryWire.ToWire` / `TryFromWire`), and its `[JsonConverter]` are generated from a dedicated shared spec `contracts/error-category/error-category.spec.json` into a foundational per-runtime leaf — .NET `D2.Shared.ErrorCodes.Category` and TS `@d2/error-category` — that depends on the BCL / nothing. This relocation (out of the registry, where the enum was originally generated) lets `result-core` reference `ErrorCategory` *downward* for a typed `D2Result.Category`, mirroring the `D2.Shared.I18n.Keys` / `@d2/i18n-keys` keys-leaf precedent this deliverable also set. The registry stops emitting the enum and references the relocated one; on the wire `category` is a snake-case string serialized via the `[JsonConverter]`.

**Generator diagnostics hard-fail on malformed input.** The error-codes generator enforces the domain prefix and the `userMessageKey`-resolves-to-a-real-TK check (inverse-transforming the symbol path to its snake key and asserting membership in `contracts/messages/en-US.json`). The merged-registry generator fires build errors on cross-catalog collision (`D2ERC004`), reserved-namespace violation (`D2ERC005`), malformed spec (`D2ERC006`), and a category not in the closed set (`D2ERC007`). The category generator fires its own `D2ECAT*` family for a malformed spec, duplicate wire string, invalid wire shape, or empty doc. All are `DiagnosticSeverity.Error`.

**Byte-parity gates and cross-runtime parity tests pin everything.** Every generated `.g.cs` / `.g.ts` carries a committed byte-parity gate test that fails if the file is hand-edited or the generator drifts; cross-runtime fixture parity tests prove .NET↔TS agreement on the constants, the factories' produced `category`, and the registry's resolved metadata.

## Consequences

**Positive.**

- A wire error code is a self-describing, structurally-validated contract: declared once, unique by construction, emitted identically into both runtimes, and resolvable to status + category + localized message anywhere — including by a boundary that never imported the producing service.
- Free-text codes are gone. A code that is not in a spec cannot reach a `D2Result` factory, and a `userMessageKey` that does not resolve to a real translation key fails the build.
- The `(code, httpStatus, default message)` triple is single-sourced in the spec, not duplicated across factory bodies — the drift class that motivated the work cannot recur.
- Generic class-based handling ("retry any `infrastructure_unavailable`") works off the wire `category` without coupling the consumer to the producer's catalog.
- Adding a domain catalog is "drop a spec + register it" — no new generator, because the engine and the TS emitter are generalized and parameterized.
- The closed `ErrorCategory` set lives in a zero-dep leaf that `result-core` can reference downward, so `D2Result.Category` is typed (not stringly), and the same enum is byte-aligned across runtimes from one shared spec.

**Negative / risks.**

- The spine has the widest blast radius in the codebase: backfilling the generic factories touched every `D2Result` consumer, gated by full-solution build + test + auth byte-parity on every step.
- Bootstrap friction per catalog is real: design the spec, ensure each `userMessageKey` exists in all locales, regenerate both runtimes, write factory + parity tests. Only worth it for codes with genuine cross-service consumers.
- Two generators (error-codes + registry) plus the category generator must stay in lockstep; a category added to the closed set is a coordinated edit across the category spec, the canonical schema's `category` enum, and the locales behind any new `userMessageKey`.
- The registry is an aggregate over all specs; a malformed or colliding spec anywhere fails the registry build for everyone — intended (fail loud and early), but it makes the registry a shared chokepoint.
- The `factoryShape` enum is the one universal `standard` shape (every optional param) plus `none`; every error factory shares that signature so a new code is a spec-only edit. Introducing a genuinely new factory signature beyond `standard` would be a canonical-schema change plus a generator change, not a spec-only edit.

## Alternatives considered

**Per-service hand-written enums / constants.** The pre-deliverable baseline — three disconnected catalogs and free-text codes. Rejected: it creates as many sources of truth as there are services × runtimes, with no structural uniqueness, no cross-service resolution, and silent .NET↔TS drift. It is exactly the failure mode ADR-0002 exists to close, applied to the error layer.

**Hand-written constants paired with a hand-maintained code→message map.** Keep typing the constants but maintain a lookup table for status / message. Rejected: the table is a fourth source of truth that drifts from the constants and the factory bodies; a code rename does not fail the build; the BFF's dead `DOMAIN_ERROR_MAP` (deleted in this deliverable) was exactly this and had already gone stale.

**A runtime registry built by reflection / config at startup.** Resolve codes by scanning assemblies or loading config when the service boots. Rejected: defers every validation (uniqueness, category membership, TK existence) to runtime or to the first integration test, adds a startup dependency, and turns a malformed catalog into a runtime exception instead of a build error — not competitive with compile-time enforcement for a fail-loud framework.

**`ErrorCategory` as a hand-authored enum in `result-core`.** Avoid the relocation by just declaring the 9 values where `D2Result` needs them. Rejected: it splits the category source of truth (the spec carries each code's category string; a hand-enum carries the members) so the registry and `result-core` can drift, and it breaks the "every closed wire enum is spec-derived" convention. The zero-dep generated leaf keeps one source and stays byte-aligned across runtimes.

## References

- `contracts/error-codes/error-codes.canonical.schema.json` — the 7-field canonical schema (draft-07).
- `contracts/error-codes/error-codes.spec.json` — the generic cross-cutting catalog; `contracts/auth-error-codes/auth-error-codes.spec.json` — the per-domain precedent generalized here.
- `contracts/error-category/error-category.spec.json` — the 9 category wire strings (the shared cross-runtime source).
- `server/shared/dotnet/source-gen-shared/error-codes-emit/` — the generalized .NET emit engine (constants + factories); `tools/ts-codegen` — the TS twin.
- `server/shared/dotnet/error-codes/registry/` + `registry-source-gen/` (`RegistryDiagnosticDescriptors.cs` — `D2ERC004`–`D2ERC007`) and `server/shared/typescript/error-codes-registry/` — the merged cross-service registry, both runtimes.
- `server/shared/dotnet/error-codes/category/` + `category-source-gen/` (`DiagnosticDescriptors.cs` — `D2ECAT*`) and `server/shared/typescript/error-category/` — the foundational `ErrorCategory` leaf, both runtimes.
- `server/shared/typescript/contract-tests/` — cross-runtime parity fixtures (constants, factory-produced category, registry metadata); the byte-parity gate tests under `server/shared/dotnet/tests/Unit/SpecsConsistency/` + `tools/ts-codegen/tests/`.
- [ADR-0002](0002-spec-driven-codegen.md) — the spec-driven codegen architecture this is a concrete instance of. [ADR-0003](0003-d2result-errors-as-values.md) — `D2Result`, whose `(code, httpStatus, message)` triple becomes spec-sourced. [ADR-0004](0004-i18n-tkmessage.md) — the `TKMessage` translation-key-as-type guarantee behind `userMessageKey`.
