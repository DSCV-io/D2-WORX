<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0002: Spec-driven codegen as the cross-language source-of-truth architecture

- **Status**: Accepted
- **Date**: 2026-05-30
- **Deliverable**: D2 shared libraries (backfilled)

## Context

D2 spans two active language runtimes (.NET and TypeScript) that share a substantial common vocabulary: error codes, JWT claim names, auth scopes, wire-format field names, header constants, binary-frame byte offsets, OTel attribute names, translation keys, geo reference catalogs, and more. Each such catalog is a cross-language contract: a string literal written in .NET code must match byte-for-byte the string read by a TypeScript client (or vice versa), because the wire is between them.

The naive resolution — write the constants by hand in each language — has a known failure mode: one runtime gets updated, the other drifts. The drift is silent at build time and surfaces either as a production incident (wrong field name deserialized as null, wrong error code matched by the wrong handler) or not at all until a cross-language integration test happens to catch it. With 20+ catalogs across two runtimes, the surface area is large enough that manual discipline is not a credible long-term guard.

Several prior patterns were in flight or considered before the current architecture settled:

- The original v1 codebase (`/old/v1/D2-WORX/`) contained hand-written constant files in both .NET and TypeScript, maintained independently.
- A proto-based approach would have covered gRPC message schemas but left non-RPC catalogs (error codes, OTel tags, translation keys, binary-frame layouts) unaddressed.
- Runtime reflection or config-driven registration was rejected because it defers validation to runtime and adds dependency on external infra at startup.

The architecture described here was implemented as part of the shared-library foundation, before any service shipped, to close this class of drift permanently. It is the single decision with the widest structural surface in the codebase — most other shared-lib decisions (the D2Result envelope of ADR-0003, the TK constants of ADR-0004) are concrete instances of it.

## Decision

Every cross-language constant catalog lives in a JSON spec file under `public/contracts/<topic>/<topic>.spec.json` (paired with a `schema.json`). Two code generators consume each spec and emit the typed artifacts for each runtime:

**On the .NET side**, a Roslyn `IIncrementalGenerator` (declared `[Generator]`, `netstandard2.0`, `IsRoslynComponent=true`) is referenced by the consuming csproj as `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`. The spec file is supplied via `<AdditionalFiles>`. The generator reads the spec at every build, parses it through a pure-logic `Loader`, passes the typed result to a pure-logic `Emitter`, and calls `context.AddSource(...)` to inject `.g.cs` source into the consuming assembly. The generator dll itself never enters any consumer's runtime closure.

**On the TypeScript side**, per-topic `tsx` scripts under `public/tools/ts-codegen/src/` read the same JSON spec files and emit `.g.ts` files into each consuming package's `src/generated/` directory. The orchestrator (`public/tools/ts-codegen/src/orchestrator.ts`) drives all emitters in dependency order via `pnpm codegen`.

**The generator anatomy** is uniform across all catalogs (verified in `D2ResultEnvelopeGenerator.cs` and `ErrorCodesGenerator.cs`): `AdditionalTextsProvider` filtered to the topic's spec file; wrapped in a value-equatable `SpecFile(Path, Content)` record for Roslyn's incremental cache; combined with `CompilationProvider`; dispatched inside `RegisterSourceOutput` by `compilation.AssemblyName`. This **single-target dispatch** pattern means one generator dll can be referenced as Analyzer by multiple consumers but emits source only into the assembly named in its `_TARGET_ASSEMBLY` constant. **Multi-target dispatch** (used by `source-gen-shared/wire-shapes-source-gen/`, `messaging/dlq-failure-metadata-source-gen/`, `encryption/in-process-keys-source-gen/`, `geo/source-gen/`, `context/source-gen/`, `headers/source-gen/`, and `telemetry/tags-source-gen/`) branches on the assembly name inside a single generator, emitting different classes into different assemblies.

**Shared scaffolding** across all .NET generators lives in `public/packages/dotnet/source-gen-shared/core/` (not a csproj — `.cs` files: `SpecFile.cs`, `LoadResult.cs`, `EmitDiagnostic.cs`, plus netstandard2.0 polyfills). Each `*-source-gen/` csproj wires them in via `<Compile Include="$(D2SourceGenSharedRoot)**\*.cs">` (the `$(D2SourceGenSharedRoot)` MSBuild property defined in `Directory.Build.props`, so the include is nesting-agnostic). This guarantees the polyfill + scaffolding cannot drift between generators.

**Diagnostic IDs** follow a `D2<TOPIC>NNN` convention (see `docs/SRC_GEN.md`). Each generator declares IDs in two files: `DiagnosticIds.cs` (pure string constants, no Roslyn dep) and `DiagnosticDescriptors.cs` (Roslyn `DiagnosticDescriptor` instances). The split lets pure-logic emitter tests assert on diagnostic IDs without spinning up a Roslyn compilation host.

**Generated output is committed to git** with `linguist-generated=true` in `.gitattributes`. PR review can inspect emitted output; IDE navigation works; CI compiles from the committed files without a pre-build regen step.

**Forward-only fixture parity tests** in `public/packages/typescript/contract-tests/tests/` (the `*.parity.test.ts` suite) are the structural drift tripwire. Each test loads a fixture produced by the .NET side's `FixtureEmitter` (serialized output from the real generator, committed under `public/contracts/<topic>/fixtures/`) and asserts per-constant name-and-value equivalence against the TypeScript catalog. A spec change that updates the .NET emitter but not the TypeScript emitter breaks the parity test; the converse likewise. The `d2result-envelope.parity.test.ts` test, for example, asserts the field-name constants in `D2ResultEnvelopeFieldNames` match byte-for-byte between the .NET `[JsonPropertyName]` attributes and the TypeScript catalog, both sourced from `public/contracts/d2result-envelope/d2result-envelope.spec.json`.

**Process discipline** is codified in `docs/dev/rules.md §26`:

- §26.1: Hand-coded spec-mirror types are forbidden in any destination assembly (a csproj/package whose types consumers can `using` or `import`). The only path to typed constants in a destination assembly is through the codegen pipeline.
- §26.2: Internal spec-mirror DTOs inside `*-source-gen/` csprojs are permitted, because `IsRoslynComponent=true` structurally prevents them from leaking to consumers. The carve-out requires both the no-leak csproj structure and parity-test coverage.
- §26.5: Hand-edits to generated files (`.g.cs`, `.g.ts`, anything under `Generated/` or `src/generated/`) are a process violation. The fix goes to the generator, the spec, or the pipeline.

## Consequences

**Positive.**

- Structural drift is impossible for any catalog covered by a spec. The literal `"AUTH_TOKEN_INVALID"` — emitted as a .NET constant and as a TypeScript constant — comes from the same `constName` field in `public/contracts/auth-error-codes/auth-error-codes.spec.json`. Neither runtime can diverge without breaking a parity test or a compile.
- Compile-time safety. Renaming a constant in the spec breaks every consumer at .NET build time (the old constant no longer exists in the emitted `.g.cs`) or at TypeScript build time. The "string literal drifted from wire value" bug class cannot be merged.
- The generator dll carries zero runtime cost: `ReferenceOutputAssembly="false"` keeps it entirely out of deployment artifacts.
- The diagnostic-ID convention (`D2<TOPIC>NNN`) surfaces malformed-spec failures from both runtimes in a greppable format.
- The incremental pipeline boundary (`SpecFile` value-equatable records) means unchanged spec files do not re-trigger emission on successive builds.
- Adding a new catalog is low marginal cost once the pattern is internalized: `docs/SRC_GEN.md` is a copy-paste checklist from any sibling; the spec design is the dominant cost.

**Negative / risks (visible in code).**

- **Codegen complexity as a build-time dependency.** Every consumer csproj carries `<ProjectReference ... OutputItemType="Analyzer">` wiring. The generator count means IDE load involves many analyzer processes. Incremental caching mitigates but does not eliminate this; first-build and clean-build times are measurably longer than a codebase with no generators.
- **The discipline burden is real.** §26.1, §26.5, and the parity-test requirement must be actively enforced at review. A contributor who hand-edits a `.g.cs` to "just fix the value" creates a silent regression the next `pnpm codegen` / `dotnet build` overwrites. The process is only as strong as the audit walks that catch it.
- **Bootstrap friction for a new catalog.** The author must design the spec + JSON schema, write the .NET Loader/Emitter/Generator, write the TypeScript emitter, wire the consuming csproj, produce and commit the generated output, write emitter tests, write parity tests, and update the registry table in `public/packages/dotnet/README.md`. This deliberate front-loading only makes sense for catalogs with genuine cross-language consumers.
- **netstandard2.0 polyfill surface.** Source-gen csprojs target `netstandard2.0` for Roslyn compatibility, so BCL helpers added after `netstandard2.0` cannot be used inside the generator; `source-gen-shared/core/` patches the most common gaps. Each new generator inherits this constraint.
- **Parity test is a forward-only fixture.** The fixture is emitted by the .NET side and committed. The parity test catches *divergence between the two emitters*, not a correlated bug where both emitters misread the spec identically.

## Alternatives considered

**Hand-written constants per language.** The v1 baseline. Rejected because it creates as many sources of truth as there are runtimes, with no structural enforcement of alignment — the v1 codebase demonstrated this with per-runtime hand-copies of header and auth constants and no cross-check. Retrieval cost: high and historically demonstrated.

**Protocol Buffers / `.proto`-only codegen.** Protobuf covers gRPC message schemas well and is already used for gRPC interfaces (`@bufbuild/buf` + `ts-proto` on TypeScript, `Grpc.Tools` + `Google.Protobuf` on .NET). It does not cover non-RPC catalogs: error codes, translation keys, auth scopes, OTel attribute names, binary-frame byte offsets, and JWT claim strings are typed string constants, not message fields. Forcing them into proto would be structurally awkward and would still leave the i18n and geo catalogs unaddressed.

**Runtime reflection / config-driven registration.** Defers the drift check to runtime (or to the first integration test that exercises the path), adds infra startup dependencies, and makes the failure mode a runtime exception rather than a compile or CI failure. Not competitive with compile-time enforcement for a framework designed to fail loud and early.

## References

- `docs/SRC_GEN.md` — canonical codegen how-to (why; .NET mechanics; TypeScript mechanics; multi-target dispatch; checklist for adding a new catalog).
- `docs/PATTERNS.md` (spec-driven codegen section) — philosophy summary; migration rule (delete the hand-written file, do not parallel-emit).
- `docs/dev/rules.md §26` — codegen discipline predicates walked at every audit round.
- `public/packages/dotnet/README.md` — source-generators registry + dependency graph (analyzer nodes shown as dashed arrows into destination assemblies).
- `public/packages/dotnet/source-gen-shared/core/` — `SpecFile.cs`, `LoadResult.cs`, `EmitDiagnostic.cs` (shared scaffolding wired into every `*-source-gen/` csproj).
- `public/packages/dotnet/result/envelope-source-gen/D2ResultEnvelopeGenerator.cs` — representative single-target generator.
- `public/packages/dotnet/source-gen-shared/error-codes-source-gen/ErrorCodesGenerator.cs` — second representative generator.
- `public/contracts/d2result-envelope/d2result-envelope.spec.json` — representative spec (`constName` + `value` + `doc` per entry).
- `public/tools/ts-codegen/src/d2result-envelope-emit.ts` — representative TypeScript emitter (mirrors the .NET Emitter shape).
- `public/packages/typescript/contract-tests/tests/d2result-envelope.parity.test.ts`, `error-codes.parity.test.ts` — representative parity tests.
- Concrete instances of this decision: [ADR-0003](0003-d2result-errors-as-values.md) (the D2Result envelope + error codes) and [ADR-0004](0004-i18n-tkmessage.md) (the TK translation-key catalog).
