<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.WireShapes.SourceGen

> Parent: [`public/packages/dotnet/`](../../README.md)

**Input contracts:** [`contracts/tk-message/`](../../../../../contracts/tk-message/README.md) + [`contracts/input-error/`](../../../../../contracts/input-error/README.md)

Roslyn `IIncrementalGenerator` (netstandard2.0) that emits per-wire-shape JSON property-name catalog static classes by reading `contracts/<wire-shape>/<wire-shape>.spec.json` files via `<AdditionalFiles>`. **Multi-target dispatch** — one analyzer, one shared scaffolding, multiple consuming catalog assemblies. Mirrors the per-transport dispatch pattern proven by `DcsvIo.D2.Headers.SourceGen`.

This source-gen enforces the spec-driven wire-identifier contract — every cross-language wire-format identifier is spec-declared + parity-tested — on the `TKMessage` (`{key, params?}`) and `InputError` (`{field, errors}`) wire shapes. Both ship across the .NET ↔ TS boundary inside the `D2Result` envelope; spec-driving the property names means the .NET serializer and the TS parser share one source of truth for the JSON keys, so cross-language drift on the property names is structurally impossible.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Dispatch table

| Consuming assembly            | Spec file                                     | Emitted source             | Emitted class                          |
| ----------------------------- | --------------------------------------------- | -------------------------- | -------------------------------------- |
| `DcsvIo.D2.I18n.Abstractions` | `contracts/tk-message/tk-message.spec.json`   | `TkMessageWireShape.g.cs`  | `DcsvIo.D2.I18n.TkMessageWireShape`    |
| `DcsvIo.D2.Result`            | `contracts/input-error/input-error.spec.json` | `InputErrorWireShape.g.cs` | `DcsvIo.D2.Result.InputErrorWireShape` |
| anything else                 | —                                             | — (no-op)                  | —                                      |

Adding a new wire-shape catalog: add a `DispatchEntry` to `sr_dispatch` in `WireShapesGenerator.cs`, author the spec + schema under `contracts/`, wire the consuming csproj's `<AdditionalFiles>` + `<ProjectReference OutputItemType="Analyzer">`, and ship.

---

## Diagnostic catalog

| ID        | Severity | Meaning                                                            |
| --------- | -------- | ------------------------------------------------------------------ |
| `D2WS001` | Error    | Spec file is malformed JSON or violates the schema.                |
| `D2WS002` | Error    | Two properties share the same `constName`.                         |
| `D2WS003` | Error    | Two properties share the same wire `value`.                        |
| `D2WS004` | Error    | Property `constName` does not match `^[A-Z][A-Z0-9_]*$`.           |
| `D2WS005` | Error    | No spec file passed to the analyzer for a target catalog assembly. |

---

## Cross-language parity

The TS-side mirror lives at `tools/ts-codegen/src/wire-shape-emit.ts` — a single multi-target emitter exporting `runTkMessageEmit` and `runInputErrorEmit`. Both sides read the SAME `contracts/tk-message/tk-message.spec.json` + `contracts/input-error/input-error.spec.json` files, so the .NET-emitted constants and the TS-emitted constants share byte-equal wire values for every property name. Parity is asserted by `public/packages/typescript/contract-tests/tests/tk-message.parity.test.ts` + `input-error.parity.test.ts` (fixture-driven per-VALUE pins).
