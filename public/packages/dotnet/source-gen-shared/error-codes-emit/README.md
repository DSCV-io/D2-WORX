<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Unified error-codes generation engine

> Parent: [`source-gen-shared`](../README.md)

The single, parameterized generation engine behind every `*-error-codes` Roslyn catalog. ONE engine drives the generic cross-cutting catalog (`ErrorCodes`) and every per-domain catalog (e.g. `AuthErrorCodes` + `AuthFailures`); each consuming assembly wires a thin `[Generator]` shell that calls `ErrorCodesEngine.Run` with its own `CatalogConfig`. No per-catalog generation logic is duplicated.

## Why a shared engine + thin shells (not one merged analyzer)

The on-disk generated path is `Generated/{analyzerAssemblyName}/{generatorTypeFQN}/{sourceFileName}`. The catalogs target different consuming assemblies and keep distinct analyzer identities (assembly name + `ErrorCodesGenerator` type FQN), so each catalog's generated tree lands in its own committed path. A single merged analyzer would relocate every tree. The shape is therefore: shared **logic** here, distinct **identity** per shell — the same way `source-gen-shared/core/` is shared across every generator without merging their analyzer identities.

## Pieces

| File | Role |
| --- | --- |
| `CatalogConfig.cs` | Per-catalog config (class names, namespace, banner/summary/doc blocks, domain prefix, emit flags, per-catalog diagnostic ids). Every string that affects the emitted bytes is config-driven. |
| `ErrorCodeEntry.cs` | The superset entry record — 3 always-present fields (`code`/`httpStatus`/`doc`) + 4 nullable factory fields (`category`/`userMessageKey`/`factoryName`/`factoryShape`). |
| `ErrorCodesSpec.cs` | The parsed spec (an array of entries). |
| `ErrorCodeSpecLoader.cs` | JSON-shape loader; factory fields optional (absent on the generic constants-only catalog). |
| `ConstantsEmitter.cs` | Emits the `<Domain>ErrorCodes` constants class (consts + `AllCodes` + `GetHttpStatus` + optional `KebabCase`); runs the superset of per-catalog validations. |
| `FactoryHost.cs` | The `base` / `domain` axis — selects construct-onto-`D2Result` vs delegate-to-base emission. |
| `BaseFactoriesEmitter.cs` | Emits the generic catalog's CONSTRUCTING factories onto the `D2Result` / `D2Result<TData>` partials + the per-code booleans (`FactoryHost.Base`). |
| `FailuresEmitter.cs` | Emits a per-domain catalog's DELEGATING `<Domain>Failures` (→ `D2Result`) AND `<Domain>Failures<T>` (→ `D2Result<T>`) classes (`FactoryHost.Domain`). |
| `ErrorCodesEngine.cs` | Wires the incremental pipeline, gates on the consuming assembly, runs the loader + emitters, runs the engine diagnostics. |
| `MessageKeySet.cs` | Value-equatable en-US.json key-set boundary (so a translation-value edit does not re-run codegen). |
| `TkKeyTransform.cs` | Inverse of `KeyDecomposer` — `TK.Auth.Errors.UNAUTHORIZED` → `auth_errors_UNAUTHORIZED` — for the TK-existence cross-check. |
| `EngineDiagnostic*.cs` | The two catalog-neutral engine diagnostics (`D2ERC*`). |

## FactoryHost — two emission modes

`CatalogConfig.FactoryHost` selects HOW a catalog's failure factories are emitted (orthogonal to the per-entry `factoryShape`, which selects the call SIGNATURE):

- **`base`** (the generic cross-cutting catalog) — the factories ARE the base. They **construct** a `D2Result` / `D2Result<TData>` directly and are emitted as members ONTO the `D2Result` / `D2Result<TData>` partial classes (`D2Result.Factories.g.cs` + `D2Result.Generic.Factories.g.cs`), plus the per-code boolean discriminators (`D2Result.Booleans.g.cs`). No `<Domain>Failures` class is emitted — the host IS `D2Result`. Every error factory is the one universal `standard` shape `(messages?, inputErrors?, errorCode?, category?, traceId?)` — all optional; `none` emits only the constant + boolean. The hand-rolled `Ok` / `Created` / `Fail` / `Bubble*` / `SomeFound` / `PartialSuccess` factories + the composite/status booleans (`IsOk` / `IsCreated` / `IsPartialOrMissing` / `IsTransientRetryable`) stay on the same partials.
- **`domain`** (per-domain catalogs, e.g. auth) — the factories **delegate** to the `httpStatus`-selected base factory (`401 → Unauthorized`, `409 → Conflict`, `400 → ValidationFailed`, `500 → UnhandledException`, `503 → ServiceUnavailable`, …), stamping the domain `code` + `userMessageKey`. Emits BOTH a non-generic `<Domain>Failures` class (→ `D2Result`, in `<Domain>Failures.g.cs`) AND a generic `<Domain>Failures<T>` class (→ `D2Result<T>`, in the sibling `<Domain>Failures.Generic.g.cs`) — both carry identical method names. The two are distinct types (arity differs), exactly as `D2Result` / `D2Result<TData>` coexist. The delegating path emits the universal `standard` shape (every domain catalog's entire set) + skips `none`; the non-generic class additionally carries a legacy typed `<T>` overload on `503` entries. The call **signature** is the `standard` shape: an optional `IReadOnlyList<TKMessage>? messages = null` override that defaults to the spec's `userMessageKey` when omitted and replaces it when supplied — so a caller can bind the offending argument via `TKMessage.With(...)` (e.g. naming a null lifecycle argument). The TS twin (`error-codes-emit.ts` `emitFailuresCatalog`) mirrors this with an `{ messages?, traceId? }` opts object.

## Engine diagnostics (catalog-neutral — fire for ANY catalog)

| ID | Severity | Fires when | Exempt |
| --- | --- | --- | --- |
| `D2ERC001` | Error | A per-domain code does not start with the catalog's enforced domain prefix (e.g. a non-`AUTH_` code in the auth catalog). | The generic catalog (no domain prefix — owns the reserved unprefixed namespace). |
| `D2ERC002` | Error | A `userMessageKey` does not inverse-resolve to a key in `contracts/messages/en-US.json`. | Catalogs with no `userMessageKey` (the generic constants-only catalog). |
| `D2ERC003` | Error | A `factoryShape` value other than the universal `standard` shape or `none` appears on the DELEGATING per-domain path (a hand-malformed spec). The schema constrains `factoryShape` to `standard` / `none`. | `standard` and `none` delegating entries — a conforming spec never fires this. |

Each catalog's pre-existing per-catalog validation diagnostics (`D2EC*` generic / `D2AEC*` auth) stay in their shells and are mapped to descriptors there — only the two engine-level diagnostics are catalog-neutral.

The **merged-registry generator** (`DcsvIo.D2.ErrorCodes.Registry.SourceGen`) adds two further diagnostics that fire at the registry-aggregation layer (cross-catalog visibility):

| ID | Severity | Fires when |
| --- | --- | --- |
| `D2ERC004` | Error | Two catalogs declare the same `code` (cross-catalog collision). Fires at registry build time; no registry is emitted until the collision is resolved. |
| `D2ERC005` | Error | Reserved-namespace violation: an unprefixed code appears in a per-domain spec, or a domain-prefixed code appears in the generic spec. Re-asserts the ownership rule across the full catalog set (per-catalog `D2ERC001` guards within each shell; `D2ERC005` is the global backstop). |

## Adding a catalog

1. Drop `contracts/<domain>-error-codes/<domain>-error-codes.spec.json` + a `schema.json` (a domain-specialized copy of `contracts/error-codes/error-codes.canonical.schema.json`, narrowing the `code` prefix / `httpStatus` / `category` constraints).
2. Add an `<AdditionalFiles>` line + an analyzer `<ProjectReference>` to the consuming csproj. Factory-bearing catalogs also add `<AdditionalFiles Include="$(D2ContractsRoot)messages\en-US.json" />` for the `D2ERC002` cross-check.
3. If the catalog targets a NEW assembly: add a ~30-line `[Generator]` shell (its own analyzer csproj) supplying that catalog's `CatalogConfig`, and reference the shared engine via `<Compile Include="$(D2ErrorCodesEmitRoot)**\*.cs">`. No new emitter logic.
