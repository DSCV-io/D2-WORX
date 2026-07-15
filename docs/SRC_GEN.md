<!--
Copyright (c) DCSV. All rights reserved.
-->

# SRC_GEN.md — Spec-driven codegen across .NET + TypeScript

Canonical reference for D²-WORX's spec-driven codegen pattern — module authors
adding new shared catalogs, codegen-emitter authors, and consumers of generated
constants find here the single source for how JSON specs become typed code in
both .NET (Roslyn `IIncrementalGenerator`) and TypeScript (`public/tools/ts-codegen`).

---

## Table of contents

- [§0. Why spec-driven codegen](#0-why-spec-driven-codegen)
- [§1. .NET — Roslyn IIncrementalGenerator](#1-net--roslyn-iincrementalgenerator)
  - [§1.1. JSON specs as `<AdditionalFiles>`](#11-json-specs-as-additionalfiles)
  - [§1.2. Diagnostic ID convention](#12-diagnostic-id-convention)
  - [§1.3. Generator anatomy (incremental + filter + emit)](#13-generator-anatomy-incremental--filter--emit)
  - [§1.4. Example walkthrough — i18n](#14-example-walkthrough--i18n)
  - [§1.5. Dual-target dispatch — public twin + private Extensions](#15-dual-target-dispatch--public-twin--private-extensions)
- [§2. TypeScript — public/tools/ts-codegen](#2-typescript--publictoolstscodegen)
  - [§2.1. JSON specs as inputs](#21-json-specs-as-inputs)
  - [§2.2. Emitter pattern](#22-emitter-pattern)
  - [§2.3. Output shape](#23-output-shape)
  - [§2.4. Example walkthrough — auth-error-codes](#24-example-walkthrough--auth-error-codes)
- [§2.5. Geo source-gen — multi-target dispatch across two assemblies and two TS packages](#25-geo-source-gen--multi-target-dispatch-across-two-assemblies-and-two-ts-packages)
- [§2.6. Field-constraints source-gen — cross-language length caps + taxonomy enums](#26-field-constraints-source-gen--cross-language-length-caps--taxonomy-enums)
- [§2.7. TypeSpec emitter fleet — decorator vocabulary + wire-channel identity](#27-typespec-emitter-fleet--decorator-vocabulary--wire-channel-identity)
- [§3. Adding a new spec-driven catalog](#3-adding-a-new-spec-driven-catalog)
- [References](#references)

---

## §0. Why spec-driven codegen

D²-WORX uses spec-driven codegen for every cross-language constant catalog —
error codes, scopes, audiences, JWT claim names, wire-format headers, OTel
attribute names, encryption-frame byte offsets, RFC 7807 ProblemDetails keys,
the messaging registry, and more. The pattern's key properties:

- **Single source of truth.** A JSON spec under `public/contracts/<topic>/` (or the
  private dual-values half under `private/contracts/<topic>/` for product rows)
  is the canonical declaration; both .NET and TypeScript consumers derive their
  typed constants from the same file. Drift between the two language sides is
  structurally impossible.
- **Compile-time enforcement.** Renaming a constant breaks every consumer at
  compile time (.NET) or at build (TypeScript). The bug class "string literal
  drifted out of sync with the wire value" cannot ship.
- **Codegen output is committed to git.** The emitted `.g.cs` / `.g.ts` files
  are checked in (with `linguist-generated=true` so they show condensed in
  diffs) so PR review can inspect them, IDEs can navigate to them, and CI
  doesn't have to re-emit before compiling.
- **No template engines, no AST manipulation.** The emitters use plain string
  builders — `StringBuilder.AppendLine` on .NET, the `string-builder.ts` helper
  on TypeScript. Mirrored shape on both sides so a single engineer can fluently
  switch between the two.
- **Hand-mirrored constants are forbidden.** When a spec catalog exists for a
  concept, hand-writing a parallel constant in the consumer code is a process
  violation — the codegen is the only blessed surface for that catalog.

The two halves of the system mirror each other:

- **.NET side**: Roslyn `IIncrementalGenerator` per topic, csproj-named
  `*-source-gen/`, lives under `public/packages/dotnet/`. Emits per-target-assembly
  via assembly-name dispatch: public hosts emit public-only types; dual-target
  catalogs also emit product-union types into private
  **`DcsvIo.D2.Private.*.Extensions`** hosts (see §1.5).
- **TypeScript side**: per-topic `tsx` scripts under `public/tools/ts-codegen/src/`,
  invoked via `pnpm codegen`. Emits one `.g.ts` per consuming package.

Both halves consume the same JSON specs under `public/contracts/<topic>/` (public
framework rows) with private product dual-values under `private/contracts/`. The
emitters re-use the same diagnostic IDs (`D2I18N*`, `D2SCP*`, `D2AEC*`, etc.)
because a malformed spec is malformed regardless of which language is reading
it.

---

## §1. .NET — Roslyn IIncrementalGenerator

### §1.1. JSON specs as `<AdditionalFiles>`

Each consuming csproj wires the spec file in via the `<AdditionalFiles>`
mechanism:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>DcsvIo.D2.I18n.Abstractions</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\source-gen\DcsvIo.D2.I18n.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\messages\*.json" />
  </ItemGroup>
</Project>
```

Three required properties of this wiring:

- `OutputItemType="Analyzer"` + `ReferenceOutputAssembly="false"` — the
  source-gen dll runs at compile time but is NEVER added to the consumer's
  runtime closure. Zero runtime cost.
- `EmitCompilerGeneratedFiles=true` + `CompilerGeneratedFilesOutputPath` —
  the emitted `.g.cs` files land in `obj/$(Configuration)/$(TargetFramework)/Generated/`
  so PR review + IDE navigation + git tracking work seamlessly.
- `<AdditionalFiles>` paths can be glob patterns (`*.json`) when a spec topic
  has multiple per-locale or per-domain files (e.g. i18n) — the generator
  filters within itself to the files it cares about.

### §1.2. Diagnostic ID convention

Each source-gen owns a `D2<TOPIC>NNN` diagnostic ID prefix (3-5 char topic
abbreviation, 3-digit number). Examples currently in use:

| Topic                   | Prefix   | Owning source-gen                                                                                                                                                         |
| ----------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| i18n                    | `D2I18N` | [`i18n/source-gen`](../public/packages/dotnet/i18n/source-gen/README.md)                                                                                                   |
| Auth scopes             | `D2SCP`  | [`auth/scopes-source-gen`](../public/packages/dotnet/auth/scopes-source-gen/README.md)                                                                                     |
| Auth audiences          | `D2AUD`  | [`auth/audiences-source-gen`](../public/packages/dotnet/auth/audiences-source-gen/README.md)                                                                               |
| Auth protocol audiences | `D2PAUD` | [`auth/protocol-audiences-source-gen`](../public/packages/dotnet/auth/protocol-audiences-source-gen/) (no README)                                                          |
| Auth error codes        | `D2AEC`  | [`auth/error-codes-source-gen`](../public/packages/dotnet/auth/error-codes-source-gen/README.md) (shell)                                                                   |
| Generic error codes     | `D2EC`   | [`source-gen-shared/error-codes-source-gen`](../public/packages/dotnet/source-gen-shared/error-codes-source-gen/README.md) (shell)                                         |
| Error-codes engine      | `D2ERC`  | [`source-gen-shared/error-codes-emit`](../public/packages/dotnet/source-gen-shared/error-codes-emit/README.md) (shared engine — catalog-neutral; `D2ERC006`/`D2ERC007` owned by `error-codes/registry-source-gen`) |
| Error category          | `D2ECAT` | [`error-codes/category-source-gen`](../public/packages/dotnet/error-codes/category/README.md)                                                                              |
| D2Result envelope       | `D2RES`  | [`result/envelope-source-gen`](../public/packages/dotnet/result/envelope-source-gen/README.md)                                                                             |
| Headers                 | `D2HDR`  | [`headers/source-gen`](../public/packages/dotnet/headers/source-gen/README.md)                                                                                             |
| JWT claims              | `D2JWT`  | [`auth/jwt-claims-source-gen`](../public/packages/dotnet/auth/jwt-claims-source-gen/README.md)                                                                             |
| Messaging registry      | `D2MQ`   | [`messaging/source-gen`](../public/packages/dotnet/messaging/source-gen/README.md)                                                                                         |
| DLQ failure metadata    | `D2DLQ`  | [`messaging/dlq-failure-metadata-source-gen`](../public/packages/dotnet/messaging/dlq-failure-metadata-source-gen/README.md)                                               |
| OTel messaging tags     | `D2OTM`  | [`messaging/otel-messaging-tags-source-gen`](../public/packages/dotnet/messaging/otel-messaging-tags-source-gen/README.md)                                                 |
| Telemetry tags          | `D2TT`   | [`telemetry/tags-source-gen`](../public/packages/dotnet/telemetry/tags-source-gen/README.md)                                                                               |
| Encryption domains      | `D2ED`   | [`encryption/domains-source-gen`](../public/packages/dotnet/encryption/domains-source-gen/README.md) — emits `EncryptionDomains` constants + `EncryptionDomainMode` enum + `EncryptionDomainModes` mode/consumer lookups; `D2ED001-005` catalog shape, `D2ED006-009` per-domain `mode`/`consumerService` validation |
| Encryption frame        | `D2EF`   | [`encryption/frame-source-gen`](../public/packages/dotnet/encryption/frame-source-gen/README.md) — two arms: symmetric v1 (`D2EF001-005`, `EncryptionFrameLayout`) + sealed v2 (`D2EF006-012`, `SealedFrameLayout` from the sibling `public/contracts/encryption-frame-sealed/` spec; adds the `variable_binary_u16be` field kind — raw binary behind a 2-byte big-endian length prefix — with a build-time rule that such a field must sit immediately behind its `byte_fixed` length-prefix field) |
| ProblemDetails          | `D2PD`   | [`problem-details/source-gen`](../public/packages/dotnet/problem-details/source-gen/README.md)                                                                             |
| Wire shapes             | `D2WS`   | [`source-gen-shared/wire-shapes-source-gen`](../public/packages/dotnet/source-gen-shared/wire-shapes-source-gen/README.md)                                                 |
| Context                 | `D2CTX`  | [`context/source-gen`](../public/packages/dotnet/context/source-gen/README.md)                                                                                             |
| gRPC trailers           | `D2GT`   | [`result/grpc-trailers-source-gen`](../public/packages/dotnet/result/grpc-trailers-source-gen/README.md)                                                                   |
| In-process keys         | `D2IPK`  | [`encryption/in-process-keys-source-gen`](../public/packages/dotnet/encryption/in-process-keys-source-gen/README.md)                                                       |
| Geo catalogs            | `D2GEO`  | [`geo/source-gen`](../public/packages/dotnet/geo/source-gen/README.md)                                                                                                     |
| Field constraints       | `D2FC`   | [`validation/source-gen`](../public/packages/dotnet/validation/source-gen/README.md)                                                                                       |
| Advisory locks          | `D2LCK`  | [`entity-framework-core/locks-source-gen`](../public/packages/dotnet/entity-framework-core/locks-source-gen/README.md) — emits `AdvisoryLocks` into owning-module assembly (currently `DcsvIo.D2.Private.Edge.KeyCustodian.Infra`); shared Postgres = mechanism only |
| KC error codes          | `D2KEC`  | [`key-custodian/error-codes-source-gen`](../private/services/edge/key-custodian/error-codes-source-gen/README.md) (shell)                                                 |
| TypeSpec emitters       | `D2TSP`  | [`public/packages/typescript/typespec-emitters`](../public/packages/typescript/typespec-emitters/README.md) — see [§2.7](#27-typespec-emitter-fleet--decorator-vocabulary--wire-channel-identity) for diagnostics |

Diagnostic IDs are declared in two parallel files:

- `DiagnosticIds.cs` — pure string constants. Roslyn-decoupled so pure-logic
  emitter tests can reference them without taking a `Microsoft.CodeAnalysis`
  dep.
- `DiagnosticDescriptors.cs` — Roslyn `DiagnosticDescriptor` instances loaded
  only inside the host generator.

The split lets emitter tests verify "this spec produces diagnostic D2MQ004"
without spinning up a full Roslyn compilation; the generator's `Execute`
method passes the actual `DiagnosticDescriptor` to `context.ReportDiagnostic`.

### §1.3. Generator anatomy (incremental + filter + emit)

Every D²-WORX .NET source-gen follows the same shape:

```csharp
[Generator]
public sealed class TKGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Pipeline: AdditionalFiles → SpecFile records (value-equatable)
        var localeFiles = context.AdditionalTextsProvider
            .Where(at => at.Path.EndsWith(".json", StringComparison.Ordinal))
            .Where(at => at.Path.Contains("public/contracts/messages/", StringComparison.Ordinal))
            .Select((at, ct) => new LocaleFile(
                Path: at.Path,
                LocaleCode: Path.GetFileNameWithoutExtension(at.Path),
                Content: at.GetText(ct)?.ToString() ?? string.Empty));

        // 2. Aggregate the per-locale stream + the consuming assembly name
        var combined = localeFiles
            .Collect()
            .Combine(context.CompilationProvider.Select((c, _) => c.AssemblyName));

        // 3. Single-target dispatch — gate by consuming-assembly name
        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var (files, assemblyName) = tuple;
            if (assemblyName != "DcsvIo.D2.I18n.Abstractions") return;

            // 4. Load + emit
            var emitResult = TKEmitter.Emit(files);
            foreach (var diag in emitResult.Diagnostics) spc.ReportDiagnostic(diag.ToRoslyn());
            if (emitResult.GeneratedSource is { } source) spc.AddSource("TK.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }
}
```

Five key decisions:

1. **Incremental pipeline boundaries are value-equatable records.** Wrapping
   `AdditionalText` content in a `SpecFile(Path, Content)` record (with
   value-equality) is what enables Roslyn's incremental cache — unchanged spec
   files don't re-trigger the emit.
2. **Filter early.** The `.Where(at => at.Path.EndsWith(".json"))` filter runs
   at the IncrementalValuesProvider level — only files that pass the filter
   reach the heavier `.Select` / `.Combine` stages.
3. **Single-target dispatch by consuming-assembly name.** A source-gen may be
   referenced as Analyzer by multiple consumers, but its emission gates on the
   single assembly that owns the catalog. Other consumers get the analyzer dll
   but no emitted source. Multi-target dispatch (e.g. `messaging/dlq-failure-metadata-source-gen`
   emitting different classes into messaging/abstractions AND messaging/rabbitmq)
   uses one source-gen with two assembly-name branches.
4. **Loader / Emitter / Generator separation.** The Generator is the Roslyn
   host wrapper; the Loader (`<X>Loader.cs`) parses JSON → typed record; the
   Emitter (`<X>Emitter.cs`) takes the typed record → emit-result (string +
   diagnostics). The Loader + Emitter are pure-logic and live in
   `xUnit`-testable code without Roslyn dependencies.
5. **Roslyn-decoupled diagnostic records.** `EmitDiagnostic(Id, Severity,
Message, Location?)` carries everything the host needs but doesn't take a
   Roslyn type dep. The host wrapper `.ToRoslyn()` extension does the
   conversion at the edge.

### §1.4. Example walkthrough — i18n

`i18n/source-gen` is the canonical pattern this codebase reaches for. It emits
the `TK` static class — a hierarchical catalog of `TKMessage` constants — into
`DcsvIo.D2.I18n.Abstractions` by reading `public/contracts/messages/*.json`
translation catalogs.

**1. Author the spec.**

```json
// public/contracts/messages/en-US.json
{
  "common_errors_NOT_FOUND": "The requested resource was not found.",
  "common_errors_UNAUTHORIZED": "You are not authorized.",
  "auth_errors_INVALID_ROLE": "The role '{role}' is not valid for org '{org}'."
}
```

Key shape: `<domain>_<category>_<NAME>`. Values: free-form translation
strings; embedded `{name}` placeholders correspond to `TKMessage.With(name,
value)` parameter substitution at render time.

One JSON file per locale (`en-US.json`, `fr-FR.json`, etc.). en-US is the
source of truth for the catalog; other locales contribute coverage diagnostics.

**2. KeyDecomposer validates each key.**

The pure-logic `KeyDecomposer.Decompose(rawKey)` enforces:

- ≥ 2 underscore-separated segments.
- Each segment is a valid C# identifier (`^[A-Za-z_][A-Za-z0-9_]*$`).
- No leading / trailing / consecutive underscores in a segment.
- Decomposed identifiers don't collide with C# reserved words.

A failed decomposition produces `D2I18N001` and the key is dropped from the
emitted catalog.

**3. TKEmitter emits `TK.g.cs`.**

```csharp
public static partial class TK
{
    public static class Common
    {
        public static class Errors
        {
            public static readonly TKMessage NOT_FOUND = new("common_errors_NOT_FOUND");
            public static readonly TKMessage UNAUTHORIZED = new("common_errors_UNAUTHORIZED");
        }
    }

    public static class Auth
    {
        public static class Errors
        {
            public static readonly TKMessage INVALID_ROLE = new("auth_errors_INVALID_ROLE");
        }
    }
}
```

Each constant is a `static readonly TKMessage` carrying just the key.
Parameter substitution happens lazily at render time via `TKMessage.With(...)`.

**4. Consumer references the constant.**

```csharp
return D2Result.NotFound(TK.Common.Errors.NOT_FOUND);
return D2Result.ValidationFailed(TK.Auth.Errors.INVALID_ROLE.With("role", roleName).With("org", orgName));
```

The `TKMessage` type's `internal`-ctor design makes
`D2Result.Messages = ["untranslated literal"]` structurally unrepresentable —
every consumer is forced through `TK.*`.

**Per-source-gen details + diagnostic catalog**: see
[`public/packages/dotnet/i18n/source-gen/README.md`](../public/packages/dotnet/i18n/source-gen/README.md).

### §1.5. Dual-target dispatch — public twin + private Extensions

Some catalogs carry **product rows** that must never ship on the public export
surface. The generator engine still lives under `public/packages/dotnet/**`
(catalog-driven, free of product hardcodes), but emit is **dual-target**:

| Host assembly (PackageId) | AdditionalFiles | Emitted type FQN | Values |
| --- | --- | --- | --- |
| `DcsvIo.D2.Auth.Abstractions` | public scopes + audiences only | `DcsvIo.D2.Auth.Abstractions.Scopes` / `Audiences` | public |
| `DcsvIo.D2.Private.Auth.Abstractions.Extensions` | public∪private scopes + audiences | `DcsvIo.D2.Private.Auth.ProductScopes` / `ProductAudiences` | public∪private |
| `DcsvIo.D2.Encryption` | public encryption-domains only | `DcsvIo.D2.Encryption.EncryptionDomains*` | public |
| `DcsvIo.D2.Private.Encryption.Extensions` | public∪private encryption-domains | `DcsvIo.D2.Private.Encryption.ProductEncryptionDomains*` | public∪private |
| `DcsvIo.D2.I18n.Keys` | public messages only | `DcsvIo.D2.I18n.TK` | public |
| `DcsvIo.D2.Private.I18n.Keys.Extensions` | public∪private messages | `DcsvIo.D2.Private.I18n.ProductTK` | public∪private |

**Law (hard):**

1. **1:1 PackageId** — every private dual-target host is
   `DcsvIo.D2.Private.<PublicTail>.Extensions` under `private/packages/dotnet/**`.
   Never open `DcsvIo.D2.*.Extensions` without `.Private.`; never a multi-concern
   bag; never a non-Extensions twin brand.
2. **Public twin ProjectReference** — each Extensions csproj ProjectReferences
   its public twin (property-based `$(D2PublicPackagesDotnetRoot)…`).
3. **Distinct dual-type FQNs** — private hosts emit `Product*` under `DcsvIo.D2.Private.*`
   namespaces. Re-emitting public class names (`Scopes` / `TK` / …) into the
   Extensions assembly causes CS0433 when a consumer multi-refs public twin +
   Extensions.
4. **Merge** — Extensions hosts pass **public∪private** values via
   `<AdditionalFiles>`; public hosts pass **public only**. Generators gate on
   `compilation.AssemblyName`.
5. **Never export** — Extensions packages are `IsPackable=false`, live only on
   root `D2.slnx`, and never appear in `public/D2.Public.slnx`.
6. **Tracked Generated/** — every Extensions host sets
   `EmitCompilerGeneratedFiles=true`, `CompilerGeneratedFilesOutputPath=Generated`,
   and `Compile Remove` on that path (same scaffold as public hosts).

Private consumers take **concern-only** Extensions ProjectReferences measured
from `Product*` call-sites (no sibling over-deps). Generator private assembly
constants and `InternalsVisibleTo` (I18n `TKMessage` ctor) name the Extensions
PackageIds, not interim bag names.

---

## §2. TypeScript — public/tools/ts-codegen

### §2.1. JSON specs as inputs

The TypeScript side reads the same JSON specs under `public/contracts/<topic>/` (or private dual-values under `private/contracts/<topic>/`). Each
per-topic emitter is a `tsx` script under `public/tools/ts-codegen/src/`:

```
public/tools/ts-codegen/
├── README.md
├── package.json
├── src/
│   ├── orchestrator.ts             ← entry point; runs every emitter in dep order
│   ├── auth-context-emit.ts
│   ├── auth-scopes-emit.ts
│   ├── d2result-envelope-emit.ts
│   ├── dlq-failure-metadata-emit.ts
│   ├── encryption-domains-emit.ts   ← emits @dcsv-io/d2-encryption-abstractions (EncryptionDomains + EncryptionDomainModes/EncryptionDomainMode/ConsumerServiceByDomain mode twins)
│   ├── encryption-frame-emit.ts
│   ├── encryption-frame-sealed-emit.ts
│   ├── error-category-emit.ts       ← emits @dcsv-io/d2-error-category (ErrorCategory union + ErrorCategoryWire + ALL_ERROR_CATEGORIES)
│   ├── error-codes-emit.ts          ← unified error-code engine (generic + auth catalogs + AuthFailures + base factories)
│   ├── error-codes-registry-emit.ts ← emits @dcsv-io/d2-error-codes-registry (merged code → ErrorCodeInfo registry)
│   ├── grpc-trailers-emit.ts
│   ├── headers-emit.ts
│   ├── jwt-claims-emit.ts
│   ├── otel-messaging-tags-emit.ts
│   ├── problem-details-emit.ts
│   ├── protocol-audiences-emit.ts
│   ├── request-context-emit.ts
│   ├── wire-shape-emit.ts
│   └── lib/
│       ├── string-builder.ts       ← mirrors .NET StringBuilder.AppendLine
│       ├── diagnostics.ts          ← shared diagnostic shape + formatter
│       ├── spec-loader.ts          ← JSON parse + diagnostic on failure
│       ├── file-emit.ts            ← atomic write + byte-equal short-circuit
│       ├── tk-key-transform.ts     ← inverse KeyDecomposer (TK symbol → snake key + TS const path)
│       └── paths.ts                ← repo-root helpers
└── tests/
```

Each emitter exports a single `runXxxEmit(force?)` function returning the
diagnostics array (empty on success). The CLI entry at the bottom of each file
invokes `runXxxEmit` and exits non-zero on diagnostic count > 0.

### §2.2. Emitter pattern

Each emitter mirrors the .NET pattern shape:

```typescript
// public/tools/ts-codegen/src/error-codes-emit.ts
import { loadSpec } from "./lib/spec-loader.js";
import { StringBuilder } from "./lib/string-builder.js";
import { writeGeneratedFile, isOutputUpToDate } from "./lib/file-emit.js";
import { contractsPath, tsPackagePath } from "./lib/paths.js";

export function runErrorCodesEmit(force = false): Diagnostic[] {
  const specPath = contractsPath("error-codes/error-codes.spec.json");
  const target = tsPackagePath("@dcsv-io/d2-result", "src/generated/ErrorCodes.g.ts");

  if (!force && isOutputUpToDate(target, [specPath])) return [];

  const { spec, diagnostics } = loadSpec<ErrorCodesSpec>(specPath, "D2EC001");
  if (diagnostics.length > 0) return diagnostics;

  const sb = new StringBuilder();
  sb.appendLine("// <auto-generated />");
  sb.appendLine(
    "// Generated from public/contracts/error-codes/error-codes.spec.json.",
  );
  sb.appendLine("// Do not edit by hand.");
  sb.appendLine();
  sb.appendLine("export const ErrorCodes = {");
  for (const entry of spec.codes) {
    sb.appendLine(`  ${entry.constant}: "${entry.constant}" as const,`);
  }
  sb.appendLine("} as const;");

  writeGeneratedFile(target, sb.toString());
  return [];
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const diagnostics = runErrorCodesEmit(process.argv.includes("--force"));
  if (diagnostics.length > 0) {
    diagnostics.forEach((d) => console.error(formatDiagnostic(d)));
    process.exit(1);
  }
}
```

Five required properties:

1. **`loadSpec` for parse + diagnostic.** A malformed spec returns
   `D2<TOPIC>001` consistent with the .NET side; consumers grepping CI logs
   for `D2*` find both .NET build failures and TS codegen failures.
2. **`isOutputUpToDate` mtime check.** Unchanged builds cost a stat-call per
   source, not a full parse + emit. The `--force` flag bypasses for
   spec-edit verification.
3. **`writeGeneratedFile` byte-equal short-circuit.** Atomic write only if the
   content differs. A second consecutive run produces zero diff — this is the
   idempotency contract.
4. **String-builder mirrors .NET shape.** No template engine, no AST
   manipulation. `sb.appendLine(...)` is the only writing primitive.
5. **CLI entry at the bottom.** `if (import.meta.url === ...)` only runs when
   invoked directly. `pnpm codegen` (orchestrator.ts) imports `runXxxEmit`
   programmatically.

### §2.3. Output shape

Each emitter writes one `.g.ts` file per consuming package:

```typescript
// private/services/web/node_modules/@dcsv-io/d2-result/src/generated/ErrorCodes.g.ts
// (during local dev these resolve to backends/node/shared/result/src/generated/ErrorCodes.g.ts)

// <auto-generated />
// Generated from public/contracts/error-codes/error-codes.spec.json.
// Do not edit by hand.

export const ErrorCodes = {
  OK: "OK" as const,
  NOT_FOUND: "NOT_FOUND" as const,
  FORBIDDEN: "FORBIDDEN" as const,
  // ... 15 total entries
} as const;

export type ErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes];
```

Output paths land under each consuming package's `src/generated/` directory.
Files are committed to git (`linguist-generated=true` in `.gitattributes`).

### §2.4. Example walkthrough — auth-error-codes

The `auth-error-codes` catalog drives both `AuthErrorCodes` constants and the
`AuthFailures.*` semantic-helper factories on both .NET and TypeScript sides
from one spec.

**1. Author the spec.**

```json
// public/contracts/auth-error-codes/auth-error-codes.spec.json
{
  "errorCodes": [
    {
      "code": "AUTH_BEARER_MISSING",
      "httpStatus": 401,
      "category": "validation_failure",
      "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
      "factoryName": "BearerMissing",
      "factoryShape": "standard",
      "doc": "The Authorization header was missing on a protected endpoint."
    },
    {
      "code": "AUTH_JWKS_UNAVAILABLE",
      "httpStatus": 503,
      "category": "infrastructure_unavailable",
      "userMessageKey": "TK.Auth.Errors.TEMPORARILY_UNAVAILABLE",
      "factoryName": "JwksUnavailable",
      "factoryShape": "standard",
      "doc": "JWKS upstream is unavailable; no cached snapshot to fall back on."
    }
  ]
}
```

**2. Both halves emit from the same spec.**

The .NET `auth/error-codes-source-gen` shell drives the shared unified engine
(`source-gen-shared/error-codes-emit`) to emit `AuthErrorCodes.g.cs`
(constants + `AllCodes` set + `GetHttpStatus` switch + `KebabCase` helper) plus
`AuthFailures.g.cs` + `AuthFailures.Generic.g.cs` (the delegating semantic
`D2Result` / `D2Result<T>` factories, selected by `httpStatus` and shaped by
`factoryShape`) into `public/packages/dotnet/auth/core/Generated/`. The SAME engine
drives the generic catalog from its own shell — but in `FactoryHost.Base` mode:
the generic factories CONSTRUCT a `D2Result` directly and land ONTO the
`D2Result` / `D2Result<TData>` partials (plus the per-code booleans), rather
than a separate `<Domain>Failures` class. One engine, per-catalog config; the
`FactoryHost` axis selects construct-vs-delegate.

The TypeScript side mirrors this: the unified `error-codes-emit.ts` engine
(one shared `emitErrorCodesCatalog` / `emitFailuresCatalog` /
`emitBaseFactoriesCatalog` helper + per-catalog `CatalogConfig`) exports four
runners — `runErrorCodesEmit` (generic `error-codes.g.ts` constants →
`@dcsv-io/d2-result`), `runErrorCodesFactoriesEmit` (generic base/constructing
factories `factories.g.ts` → `@dcsv-io/d2-result`), `runAuthErrorCodesEmit`
(`auth-error-codes.g.ts` constants → `@dcsv-io/d2-auth-abstractions`), and
`runAuthFailuresEmit` (`auth-failures.g.ts` factories → `@dcsv-io/d2-auth-abstractions`,
base factory selected by `httpStatus`, branch by `factoryShape`). The TS base
factories are standalone module FUNCTIONS (not class members — `D2Result<T = void>`
is one class), each generic with a `void` default (`notFound<T = void>()`), so
one function spans the untyped + typed cases; this is the TS equivalent of the
.NET base factories on the `D2Result` / `D2Result<TData>` partials. The auth
delegating factories carry the same `<T = void>` shape, so `AuthFailures.x()`
yields `D2Result<void>` and `AuthFailures.x<User>()` yields `D2Result<User>` —
the single-method equivalent of the .NET `AuthFailures` + `AuthFailures<T>`
two-class split. The failure + base factories reference each `userMessageKey`
as a `TK.*` constant from `@dcsv-io/d2-i18n-keys` — each `TK.*` constant IS a `TKMessage`
instance, so it is used directly with no `tk()` wrapper at the call site —
never a key/path string literal, which would silently bypass the TK catalog and
ride the wire un-renderable. Both runtimes' engines emit the same wire key (the
snake en-US.json key) for any `userMessageKey`, guarded by the cross-runtime
TK-validity render test.

**3. Consumer references the constants identically across languages.**

```csharp
// .NET — domain-failures class (DcsvIo.D2.Auth.Errors namespace)
return AuthFailures.JwtSignatureInvalid();
```

```typescript
// TypeScript — same factory shape
return AuthFailures.jwtInvalidToken();
```

Cross-language wire format drift on the `d2_error_code` value is structurally
impossible — the literal string `"AUTH_TOKEN_INVALID"` comes from the same
spec field on both sides.

**Per-emitter details**: see
[`public/tools/ts-codegen/README.md`](../public/tools/ts-codegen/README.md) for the full
emitter catalog + `pnpm codegen --force` usage.

---

## §2.5. Geo source-gen — multi-target dispatch across two assemblies and two TS packages

The geo codegen pipeline is the largest and most structurally complex source-gen in D²-WORX. It
drives both type definitions (enums, wrapper structs, record shapes, JsonConverters, `GeoCatalog`
constants) and catalog data (per-entity static instances, lookup tables, nested hierarchies) from
the same seven spec files on both the .NET and TypeScript sides.

### Spec inputs

Seven spec files under `public/contracts/geo/`:

| Spec                              | Source           |
| --------------------------------- | ---------------- |
| `countries.spec.json`             | Pipeline-derived |
| `subdivisions.spec.json`          | Pipeline-derived |
| `currencies.spec.json`            | Pipeline-derived |
| `languages.spec.json`             | Pipeline-derived |
| `locales.spec.json`               | Pipeline-derived |
| `timezones.spec.json`             | Pipeline-derived |
| `geopolitical-entities.spec.json` | Hand-rolled      |

Each spec carries `{ catalogVersion, generatedAt, entries: [...] }`. The pipeline-derived specs are
produced by the `public/contracts/geo/` build pipeline from canonical ISO source data + overlays.

### .NET emitters (`DcsvIo.D2.Geo.SourceGen`)

`DcsvIo.D2.Geo.SourceGen` (`public/packages/dotnet/geo/source-gen/`) inspects
`compilation.AssemblyName` and dispatches per target:

| Target assembly              | What is emitted                                                                                                                                                                                                                        |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Geo.Abstractions` | **TYPES** — record shapes (`Country.g.cs` / `Subdivision.g.cs` / etc.) + `*Code` enums + wrapper structs + `JsonConverter`s + `GeoCatalog.g.cs` constants                                                                              |
| `DcsvIo.D2.Geo.Default`      | **DATA** — per-catalog `*Lookup` static instances + `FrozenDictionary` lookup tables + nested static-class hierarchies (`Subdivisions.US.NY` / `Locales.en.US` / `Timezones.America.New_York`) + `GeoDataInitializer.g.cs` coordinator |

Internal emitters (one per concern):

| Emitter                                                                                                                                                                         | Output                                                                                         |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `RecordShapeEmitter`                                                                                                                                                            | Seven record `.g.cs` files emitted into `geo/abstractions/Generated/`                          |
| `GeoEnumsEmitter`                                                                                                                                                               | `*Code` enums + fixed-vocabulary enums (e.g. `WritingDirection`)                               |
| `GeoWrapperStructEmitter`                                                                                                                                                       | `SubdivisionCode` / `LocaleCode` / `TimezoneCode` wrapper structs + lenient `TryParse`         |
| `GeoJsonConverterEmitter`                                                                                                                                                       | `System.Text.Json` `JsonConverter<T>` per catalog type                                         |
| `GeoCatalogEmitter`                                                                                                                                                             | `GeoCatalog` static class — `CatalogVersion` + `CatalogPublishedAt` (`DateTimeOffset`)         |
| `CountryDataEmitter` / `CurrencyDataEmitter` / `LanguageDataEmitter` / `SubdivisionDataEmitter` / `LocaleDataEmitter` / `TimezoneDataEmitter` / `GeopoliticalEntityDataEmitter` | Per-entity static data files emitted into `geo/default/Generated/`                             |
| `GeoDataInitializerEmitter`                                                                                                                                                     | `GeoDataInitializer.g.cs` — `[ModuleInitializer]`-driven second-pass FK-nav wiring coordinator |

### Diagnostic IDs (`D2GEO`)

| ID         | Trigger                                                                                            |
| ---------- | -------------------------------------------------------------------------------------------------- |
| `D2GEO001` | Malformed JSON / parse failure of a spec file                                                      |
| `D2GEO002` | FK code refers to entity not present in target catalog                                             |
| `D2GEO003` | FK detection ambiguity — field name unmatched by naming convention and no `fkTo` annotation        |
| `D2GEO004` | Geo code cannot form a valid C# identifier                                                         |
| `D2GEO005` | Vocabulary discipline violation — forbidden `region` / `state` / `province` at identifier position |
| `D2GEO006` | Missing or invalid `catalogVersion` / `generatedAt` in a spec                                      |
| `D2GEO007` | Required spec file missing from `AdditionalFiles`                                                  |
| `D2GEO009` | Structural-parity mismatch — spec field exists but no matching emitted record property             |

(`D2GEO008` is reserved — not currently in use.)

### TypeScript emitters (`public/tools/ts-codegen/src/geo-emitter/`)

The TS side mirrors the .NET emitter structure under `public/tools/ts-codegen/src/geo-emitter/`:

| Emitter                                                                                                                                                                                                | Output package         | Output artifact                                                                                 |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------- | ----------------------------------------------------------------------------------------------- |
| `record-shape-emit.ts`                                                                                                                                                                                 | `@dcsv-io/d2-geo-abstractions` | Seven record interface `.g.ts` files in `src/generated/`                                        |
| `enum-emit.ts`                                                                                                                                                                                         | `@dcsv-io/d2-geo-abstractions` | `*Code` `as const` objects + branded types + Zod schemas                                        |
| `wrapper-code-emit.ts`                                                                                                                                                                                 | `@dcsv-io/d2-geo-abstractions` | `SubdivisionCode` / `LocaleCode` / `TimezoneCode` branded types + Zod refinements + set helpers |
| `geo-catalog-emit.ts`                                                                                                                                                                                  | `@dcsv-io/d2-geo-abstractions` | `GeoCatalog` `as const` — `catalogVersion` + `catalogPublishedAt`                               |
| `default/country-data-emit.ts` / `currency-data-emit.ts` / `language-data-emit.ts` / `subdivision-data-emit.ts` / `locale-data-emit.ts` / `timezone-data-emit.ts` / `geopolitical-entity-data-emit.ts` | `@dcsv-io/d2-geo-default`      | Per-entity catalog data files                                                                   |
| `default/geo-data-initializer-emit.ts`                                                                                                                                                                 | `@dcsv-io/d2-geo-default`      | `geoDataInitializer.g.ts` — FK-nav wiring coordinator (module-init on first import)             |

### Cross-language parity

| Parity test                          | What it pins                                                                                                          |
| ------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| `geo-records.parity.test.ts`         | Fixture from `GeoRecordsFixtureEmitter` — field names + FK structure of all seven record types                        |
| `geo-enums.parity.test.ts`           | Fixture from `GeoEnumsFixtureEmitter` — per-VALUE wire form for every enum member                                     |
| `geo-wrapper-structs.parity.test.ts` | Fixture from `GeoWrapperStructsFixtureEmitter` — known codes for the three wrapper struct types                       |
| `geo-catalog.parity.test.ts`         | Fixture from `GeoCatalogFixtureEmitter` — `CatalogVersion` + `CatalogPublishedAt` byte shape                          |
| `geo-name-resolver.parity.test.ts`   | `public/contracts/geo/fixtures/confusables.fixture.json` — name-resolver four-pass output across a shared confusables corpus |

---

## §2.6. Field-constraints source-gen — cross-language length caps + taxonomy enums

The field-constraints catalog is the shared source of truth for all field-length and digit-count bounds plus three contact-domain taxonomy enumerations. It is the smallest cross-language codegen pipeline in the codebase — one spec, two target languages, one .NET target assembly, one TS package.

### Spec input

`public/contracts/validation/field-constraints.spec.json` — two arrays:

| Array         | Contains                                                                                                                                                                                    |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `constraints` | `{ name, value, doc }` entries for char-length caps (`FIRST_NAME_MAX`, `EMAIL_MAX`, `PHONE_E164_MAX`, `POSTAL_CODE_MAX`, …) and digit-count bounds (`PHONE_MIN_DIGITS`, `PHONE_MAX_DIGITS`) |
| `enums`       | `{ name, backing, doc, members[] }` entries for the three closed-list taxonomy enums (`NamePrefix`, `NameSuffix`, `BiologicalSex`); each member is `{ name, doc }`                          |

### .NET emitter (`DcsvIo.D2.Validation.SourceGen`)

`public/packages/dotnet/validation/source-gen/` — Roslyn `IIncrementalGenerator`, `netstandard2.0`, `D2FC` diagnostic prefix, single-target dispatch (`AssemblyName == "DcsvIo.D2.Validation.Abstractions"`).

Emits two files into `validation/abstractions/Generated/`:

| Output file             | Contents                                                                                                                                           |
| ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `FieldConstraints.g.cs` | `public static class FieldConstraints` — one `public const int` per `constraints` entry                                                            |
| `Taxonomy.g.cs`         | Three `byte`-backed `enum` types with `[JsonConverter(typeof(JsonStringEnumConverter))]` — one per `enums` entry; member names are the wire values |

### TypeScript emitter

`public/tools/ts-codegen/src/field-constraints-emit.ts` — mirrors the .NET pattern (mtime short-circuit, byte-equal atomic write, `--force` flag). Emits into `public/packages/typescript/validation/abstractions/src/generated/`:

| Output file              | Contents                                                                                           |
| ------------------------ | -------------------------------------------------------------------------------------------------- |
| `field-constraints.g.ts` | `export const FieldConstraints = { ... } as const` — one entry per `constraints` item              |
| `taxonomy.g.ts`          | Three `as const` enum objects + branded types + Zod `z.enum([...])` schemas — one per `enums` item |

### Diagnostic IDs (`D2FC`)

| ID        | Trigger                                                     |
| --------- | ----------------------------------------------------------- |
| `D2FC001` | Malformed JSON or schema violation                          |
| `D2FC002` | Duplicate constraint name                                   |
| `D2FC003` | Constraint name is empty / whitespace / not SCREAMING_SNAKE |
| `D2FC004` | Constraint value is not a positive integer                  |
| `D2FC005` | Duplicate enum name                                         |
| `D2FC006` | Enum name is empty or not a valid PascalCase C# identifier  |
| `D2FC007` | Enum declares an empty members list                         |
| `D2FC008` | Two members of the same enum share the same name            |
| `D2FC009` | Enum member name is empty or not a valid C# identifier      |

### Consumers

`DcsvIo.D2.Contacts` VO `Create` factories + `DcsvIo.D2.Location` VO `Create` factories consume `FieldConstraints.*` for length-cap enforcement. `@dcsv-io/d2-validation-abstractions` Zod schemas + frontend form validators consume the TS equivalents. Taxonomy enums are used by `DcsvIo.D2.Contacts.ValueObjects.NameAffixes` and `Demographics`.

---

## §2.7. TypeSpec emitter fleet — decorator vocabulary + wire-channel identity

The TypeSpec pipeline is a separate, independent codegen path from the `public/tools/ts-codegen` JSON emitters described in §2.1–§2.4. It consumes TypeSpec `.tsp` specs (under [`public/contracts/typespec/`](../public/contracts/typespec/README.md)) rather than JSON specs, and produces the full operation-contract artifact fleet from a single authored `.tsp` file.

### How it works

TypeSpec ops and models in `public/contracts/typespec/` are annotated with the `@d2*` decorator vocabulary defined in [`public/packages/typescript/typespec-decorators/`](../public/packages/typescript/typespec-decorators/README.md). The emitter fleet in [`public/packages/typescript/typespec-emitters/`](../public/packages/typescript/typespec-emitters/README.md) reads that decorator state at `tsp compile` time and emits the complete artifact fleet for each operation:

- C# and TypeScript DTOs
- `.proto` file + generated gRPC service stubs and clients
- REST route registrations (C# `Map*` calls) for **edge-module** process-kind (in-process façade / handler)
- Edge HTTP→gRPC **bridge** registrations (C# `Map*Bridge` → `I{Module}GrpcClient`) for **standalone** process-kind public HTTP
- Process-kind host routing from tspconfig (`process-kind-by-module`, `csharp-routes-namespace`, `csharp-bridge-namespace`) with fail-loud D2TSP014–019
- The module façade and handler interfaces
- OpenAPI extensions
- Wire-identity constants (`WireVersion.g.cs`) and manifest (`wire-identity.manifest.g.json`)

The regen script [`private/tools/scripts/regen-typespec-emitters.mjs`](../private/tools/scripts/regen-typespec-emitters.mjs) drives `tsp compile` and copies emitted artifacts to their target locations; it maintains a `COPY_MANIFEST` allowlist of namespace-agnostic artifacts.

**Hub READMEs for navigating the TypeSpec stack:**

- [`public/packages/typescript/README.md`](../public/packages/typescript/README.md) — TS shared-library hub
- [`public/packages/typescript/typespec-emitters/README.md`](../public/packages/typescript/typespec-emitters/README.md) — emitter fleet: per-emitter list, emit options, diagnostic IDs, testing
- [`public/packages/typescript/typespec-decorators/README.md`](../public/packages/typescript/typespec-decorators/README.md) — `@d2*` decorator vocabulary: what each decorator means, valid combinations, how the emitters read them
- [`public/contracts/typespec/README.md`](../public/contracts/typespec/README.md) — TypeSpec spec root: file layout, how to author a new op, how to run `tsp compile`

### D2TSP diagnostic IDs

IDs are allocated in `public/packages/typescript/typespec-emitters/src/lib.ts`. All are error severity.

| ID        | Name                        | Meaning                                                                                                                                                                                                                                       |
| --------- | --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2TSP001` | `unmapped-scalar`           | A scalar has no C#/proto/TypeScript mapping in the emitter's type table.                                                                                                                                                                      |
| `D2TSP002` | `unsupported-property-type` | An anonymous-model, model-variant, or otherwise-unrecognized property kind. Named enums and closed string-literal unions ARE supported — they map to a C# `public enum` + `[JsonConverter(typeof(JsonStringEnumConverter))]`, a TS `as const` object, and a proto `string` field carrying the member-name wire string. |
| `D2TSP003` | `missing-cqrs-category`     | An op carries neither `@d2Command` nor `@d2Query`. This is a defensive guard in namespace routing; the `category-required` invariant normally prevents this in valid programs.                                                                |
| `D2TSP004` | `route-missing-auth-intent` | A routed op carries neither `@d2RequireAnyScope`, `@d2RequireAllScopes`, nor `@d2Harmless`. Every routed op must declare an auth intent; the route emitter loud-fails rather than emitting a boot-failing endpoint with no auth gate.          |
| `D2TSP005` | `unsupported-http-verb`     | A verb other than GET/POST/PUT/DELETE/PATCH (e.g. HEAD, OPTIONS) has no `Map*` mapping in the route emitter.                                                                                                                                 |
| `D2TSP006` | `idempotent-requires-route` | `@d2Idempotent` is present on an op with no `@route`. Idempotency gating is REST-only and is meaningless without a public HTTP route.                                                                                                        |
| `D2TSP007` | `unsupported-union-shape`   | A union property whose variants are NOT a closed set of string literals (mixed-primitive, mixed-literal-kind, numeric-literal-only, discriminated, or model unions). There is no single cross-language enum representation; replace with a named enum or a closed string-literal union. |
| `D2TSP008` | `server-push-requires-payload` | A `@d2ServerPush` op whose output has no emittable payload (void return, or an output model with zero fields and zero nested models). The op's output model is the dispatched event payload, so a payload-less push is almost certainly a mistake. |
| `D2TSP009` | `unpinned-proto-field`      | A model property on a `@d2GrpcMethod`-bound model lacks a `@d2Field(N)` author-pinned field number. Positional assignment is permanently disabled — every field on every proto-bound model must carry an explicit `@d2Field(N)` pin. Fires only inside the proto emitter; DTO-only and in-process ops are unaffected. |
| `D2TSP010` | `channel-segment-mismatch`  | The wire-channel segment derived from `proto-package` disagrees with the trailing segment of `proto-csharp-namespace` OR with the `@versioned` enum value on the primary namespace. All three surfaces must carry the same generation (`V<N>(alpha|beta)?`) — they are cross-validation surfaces, not independent declarations. |
| `D2TSP011` | `duplicate-field-number`    | Two or more properties on the same proto-bound model carry the same `@d2Field(N)` pin. Duplicate field numbers produce invalid proto3; each property needs a unique number.                                                                  |
| `D2TSP012` | _retired_                   | Formerly `nested-redact-unsupported`. Nested-model `@d2Redact` is now fully supported (the model walker threads the reason into nested fields at any depth); the number stays retired so historical build-failure reports remain traceable.   |
| `D2TSP013` | `missing-concern`           | A client-exposed op (real-module mode — `csharp-clients-namespace` + `csharp-app-namespace-base` set, `@d2ServedBy` present, not `@d2Internal`) carries no `@d2Concern`. The concern names the folder + namespace segment the op's transport DTOs live in (`<clients-ns>.<Concern>`); without it the emitter cannot place them. Add `@d2Concern("<Segment>")` to the op. |
| `D2TSP014` | `missing-served-by-for-host-routing` | Real-module mode + `@route` op has no `@d2ServedBy`. Host routing (process-kind + routes/bridge namespace maps) is keyed by ServedBy; hard-derived `App….Routes` is forbidden for production Edge hosts. |
| `D2TSP015` | `missing-process-kind`      | Real-module mode + `@route` op has `@d2ServedBy` but `process-kind-by-module` has no entry (or the map is absent). Values must be `"edge-module"` \| `"standalone"`. |
| `D2TSP016` | `unknown-process-kind`      | `process-kind-by-module` value is outside the closed set `"edge-module"` \| `"standalone"`. |
| `D2TSP017` | `missing-routes-namespace`  | Edge-module op with `@route` needs a `csharp-routes-namespace` map entry for its ServedBy (production Edge.Api Map* namespace). |
| `D2TSP018` | `missing-bridge-namespace`  | Standalone op with `@route` + `@d2GrpcMethod` needs a `csharp-bridge-namespace` map entry for its ServedBy. |
| `D2TSP019` | `standalone-route-requires-grpc` | Standalone process-kind op has `@route` but no `@d2GrpcMethod` — public HTTP without a gRPC backend hop is invalid for the Edge bridge model. |

### Concern-based client namespace routing (`@d2Concern`)

`@d2Concern("<Segment>")` on a client-exposed op names its **co-location concern** — the folder + namespace segment that op's transport DTOs live in. It is a general, spec/config-driven mechanism exactly parallel to the app-handler arm (`csharp-app-namespace-base` + the `.<Category>.<PascalOp>` derivation): the C# DTO emitter routes an exposed op's DTOs to `<csharp-clients-namespace>.<Concern>` (folder = namespace), and the regen `COPY_MANIFEST` places them in `client/<Concern>/` alongside the hand-written runtime that serves them. The module façade splits into a `Facade/` folder — the interface lands in `<clients-ns>.Facade`, the impl + generated DI registration in `<app-ns>.Facade` — and the registration's file + method names derive from the clients-namespace **leaf** segment (leaf `Client` → `KeyCustodianClientGenerated.g.cs` / `AddD2KeyCustodianClient()`), never a hard-coded literal. There are ZERO hard-coded file/name special cases in emitter code: the maintenance surface is the `.tsp` (`@d2Concern` decorators) + `tspconfig.yaml` only. A client-exposed op that omits `@d2Concern` is a loud build failure (`D2TSP013`); `@dcsv-io/d2-typespec-decorators` additionally fires `invalid-concern` when the segment is not a legal C# identifier. The routing is C#-only — the TypeScript DTO mirror has no namespace, so it is unaffected.

### Wire-channel single source

`proto-package` (e.g. `d2.keycustodian.v2alpha`) is the single source of wire identity. From it the emitter derives:

- `channel` — the trailing dotted segment (`v2alpha`)
- `generation` — the numeric prefix of the channel (`2`)
- `stability` — `"alpha"` / `"beta"` / `"stable"` from the optional suffix

`proto-csharp-namespace` carries the same generation in PascalCase as its trailing segment (e.g. `D2.Services.Protos.KeyCustodian.V2Alpha`). `@versioned` on the primary namespace carries it as the last enum value (e.g. `v2alpha: "v2alpha"`). These two surfaces must agree with `proto-package`; they do not add new information — they are cross-validation surfaces only. `D2TSP010` fires when any surface disagrees. The validation logic lives in `src/lib/wire-channel.ts` (`validateChannelAgreement`) and uses a callback pattern so it is unit-testable without a live TypeSpec program.

Wire-channel grammar: `^d2\.[a-z][a-z0-9]*\.v\d+(alpha|beta)?$` (linear-bounded with bounded input — no `matchTimeout` needed).

### Emitted wire-identity artifacts

When a spec contains at least one `@d2GrpcMethod` op and the channel is validated, the emitter produces two additional artifacts alongside the proto and gRPC-client files:

| Artifact                          | Path                                                          | Governed by                                   |
| --------------------------------- | ------------------------------------------------------------- | --------------------------------------------- |
| `WireVersion.g.cs`                | `<proto-csharp-namespace-path>/Generated/WireVersion.g.cs`   | Byte-gate test (`proto-grpc-byte-parity.test.ts`) |
| `wire-identity.manifest.g.json`   | `<proto-csharp-namespace-path>/Generated/wire-identity.manifest.g.json` | Byte-gate test                   |

`WireVersion.g.cs` emits a `public static class WireVersion` with three `public const` fields (`CHANNEL`, `GENERATION`, `STABILITY`). The manifest records the identity facts (`protoPackage`, `protoCsharpNamespace`, `generation`, `stability`, `channel`) plus `x-d2-generated-by: "@dcsv-io/d2-typespec-emitters"`. Neither artifact carries package names (those are deployment details, not wire-identity facts).

Both artifacts are excluded from the `COPY_MANIFEST` allowlist in `private/tools/scripts/regen-typespec-emitters.mjs` because they are namespace-sensitive; they are governed by the byte-gate tests instead.

### `@versioned` adoption

Namespaces with gRPC ops adopt `@typespec/versioning @versioned(Namespace.Versions)` with a block `enum Versions { v2alpha: "v2alpha" }` inside the namespace block. The emitter reads the last entry of the version map via `getVersion(program, ns).getVersions()` and treats its `.value` as the `versionedChannel` cross-validation surface. Adopting `@versioned` is byte-neutral for existing emitter output on namespaces that have no `@service`/`@route` (the OpenAPI emitter early-returns; the proto emitter output is unchanged).

---

## §3. Adding a new spec-driven catalog

When introducing a brand-new spec-driven catalog, follow this checklist:

1. **Author the spec.** Create `public/contracts/<topic>/<topic>.spec.json` + a sibling
   `schema.json` describing the shape.
2. **Author the .NET source-gen** under the owning cluster folder, e.g.
   `public/packages/dotnet/<cluster>/<topic>-source-gen/` (or
   `public/packages/dotnet/<cluster>/source-gen/` when the cluster owns a single
   source-gen):
   - csproj is `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"`
     on Roslyn deps + bundled `System.Text.Json`.
   - References `source-gen-shared/core/` files via
     `<Compile Include="$(D2SourceGenSharedRoot)**\*.cs">` (the
     `D2SourceGenSharedRoot` property is defined in `Directory.Build.props`,
     so the include is nesting-agnostic) for the netstandard2.0 polyfills +
     cross-source-gen records (`SpecFile`, `LoadResult<TSpec>`, `EmitDiagnostic`).
   - **Error-codes catalogs** add a second glob:
     `<Compile Include="$(D2ErrorCodesEmitRoot)**\*.cs">` (also defined in
     `Directory.Build.props`). This pulls in the shared
     `source-gen-shared/error-codes-emit/` engine — entry model, loader,
     `ConstantsEmitter`, `FailuresEmitter`, engine diagnostics (`D2ERC*`), and
     `TkKeyTransform`. See the
     [error-codes-emit README](../public/packages/dotnet/source-gen-shared/error-codes-emit/README.md)
     for the full add-a-catalog recipe.
   - Allocates a `D2<TOPIC>NNN` diagnostic ID prefix (3-5 chars).
   - Loader (`<X>Loader.cs`) parses JSON → typed record; Emitter
     (`<X>Emitter.cs`) takes typed record → emit-result; Generator
     (`<X>Generator.cs`) is the Roslyn host wrapper with single-target dispatch
     by consuming-assembly name.
3. **Wire the consumer csproj** to reference the source-gen as Analyzer +
   declare the spec as `<AdditionalFiles>` per §1.1 above. Add the consumer to
   the sourcegen registry table in
   [`public/packages/dotnet/README.md`](../public/packages/dotnet/README.md).
4. **Author the TypeScript emitter** under `public/tools/ts-codegen/src/<topic>-emit.ts`:
   - Mirrors the .NET emitter shape — load spec, build string with
     `StringBuilder`, write via `writeGeneratedFile`.
   - Re-uses the same `D2<TOPIC>NNN` diagnostic IDs.
   - Register in `public/tools/ts-codegen/src/orchestrator.ts` if there's a dep
     ordering requirement.
5. **Author per-source-gen tests** under `tests/`:
   - `<X>EmitterTests` — feed the emitter typed specs, assert against the
     emitted source via per-VALUE substring pins (not snapshot frameworks).
   - `<X>SpecLoaderTests` — feed malformed JSON, assert the right diagnostic
     ID + message.
   - `<X>GeneratorTests` — Roslyn `CSharpGeneratorDriver` integration test
     that runs the full pipeline.
   - `<X>DiagnosticIdsTests` — assert every `D2<TOPIC>NNN` ID is declared in
     `DiagnosticIds.cs` AND `DiagnosticDescriptors.cs` (catches drift between
     the two files).
6. **Commit the emitted `.g.cs` + `.g.ts` files** to git. `.gitattributes`
   should mark them `linguist-generated=true` so they show condensed in PR
   diffs.

The cost of bringing up a new catalog is dominated by the spec design (what
fields, what catalog shape, what semantic factories). The actual codegen
plumbing is a ~200-line copy-paste from any existing sibling source-gen.

---

## References

- [`public/packages/dotnet/README.md`](../public/packages/dotnet/README.md) —
  sourcegen registry table covering all 19 `*-source-gen/` libs with one-line
  descriptions + per-source-gen README links.
- [`public/tools/ts-codegen/README.md`](../public/tools/ts-codegen/README.md) — TypeScript
  emitter catalog + library helpers + build integration + `--force` regen.
- [`public/packages/dotnet/source-gen-shared/`](../public/packages/dotnet/source-gen-shared/)
  — shared netstandard2.0 polyfills + cross-source-gen records (`SpecFile`,
  `LoadResult`, `EmitDiagnostic`) referenced by every `*-source-gen/` csproj.
- [`public/packages/dotnet/i18n/source-gen/README.md`](../public/packages/dotnet/i18n/source-gen/README.md)
  — the original SrcGen pattern this codebase mirrors elsewhere.
- [PATTERNS.md](PATTERNS.md) — high-level entry for the spec-driven codegen
  philosophy.
