<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.WireShapes.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn `IIncrementalGenerator` (netstandard2.0) that emits per-wire-shape JSON property-name catalog static classes by reading `contracts/<wire-shape>/<wire-shape>.spec.json` files via `<AdditionalFiles>`. **Multi-target dispatch** — one analyzer, one shared scaffolding, multiple consuming catalog assemblies. Mirrors the per-transport dispatch pattern proven by `D2.Shared.Headers.SourceGen`.

This source-gen enforces **§11.30** (every cross-language wire-format identifier under spec-driven contract + parity-tested) on the `TKMessage` (`{key, params?}`) and `InputError` (`{field, errors}`) wire shapes. Both ship across the .NET ↔ TS boundary inside the `D2Result` envelope; spec-driving the property names means the .NET serializer and the TS parser share one source of truth for the JSON keys, so cross-language drift on the property names is structurally impossible.

---

## Dispatch table

| Consuming assembly | Spec file | Emitted source | Emitted class |
|---|---|---|---|
| `D2.Shared.I18n.Abstractions` | `contracts/tk-message/tk-message.spec.json` | `TkMessageWireShape.g.cs` | `D2.Shared.I18n.TkMessageWireShape` |
| `D2.Shared.Result` | `contracts/input-error/input-error.spec.json` | `InputErrorWireShape.g.cs` | `D2.Shared.Result.InputErrorWireShape` |
| anything else | — | — (no-op) | — |

Adding a new wire-shape catalog: add a `DispatchEntry` to `sr_dispatch` in `WireShapesGenerator.cs`, author the spec + schema under `contracts/`, wire the consuming csproj's `<AdditionalFiles>` + `<ProjectReference OutputItemType="Analyzer">`, and ship.

---

## File layout

| Path | Contents |
|---|---|
| `D2.Shared.WireShapes.SourceGen.csproj` | netstandard2.0 IsRoslynComponent project; bundles the shared `source-gen-shared/` scaffolding via `<Compile Include>` link. |
| `WireShapeProperty.cs` | Parsed-shape record for one property entry. |
| `WireShapeSpec.cs` | Parsed-shape record for the spec root. |
| `WireShapeSpecLoader.cs` | Pure-logic JSON spec → `WireShapeSpec` parser. |
| `WireShapeEmitter.cs` | Pure-logic emitter — parameterized by namespace + class name so one emitter serves every catalog. |
| `WireShapesGenerator.cs` | The Roslyn `[Generator]` entry point. Dispatches per consuming assembly name. |
| `DiagnosticIds.cs` | String IDs for the analyzer's diagnostics — `D2WS001` through `D2WS005`. |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances for the IDs. |
| `EmitDiagnostics.cs` | Topic-specific factory helpers producing `EmitDiagnostic` records. |
| `EmitResult.cs` | Pure-logic emit result (generated source + diagnostics). |

---

## Diagnostic catalog

| ID | Severity | Meaning |
|---|---|---|
| `D2WS001` | Error | Spec file is malformed JSON or violates the schema. |
| `D2WS002` | Error | Two properties share the same `constName`. |
| `D2WS003` | Error | Two properties share the same wire `value`. |
| `D2WS004` | Error | Property `constName` does not match `^[A-Z][A-Z0-9_]*$`. |
| `D2WS005` | Error | No spec file passed to the analyzer for a target catalog assembly. |

---

## Cross-language parity

The TS-side mirror emitter lives at `tools/ts-codegen/src/tk-message-emit.ts` + `tools/ts-codegen/src/input-error-emit.ts`. Both sides read the SAME `contracts/tk-message/tk-message.spec.json` + `contracts/input-error/input-error.spec.json` files, so the .NET-emitted constants and the TS-emitted constants share byte-equal wire values for every property name. Parity is asserted by `server/shared/typescript/contract-tests/tests/tk-message.parity.test.ts` + `input-error.parity.test.ts` (fixture-driven per-VALUE pins).
